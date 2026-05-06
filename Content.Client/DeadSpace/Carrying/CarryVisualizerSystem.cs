// Dead Space 14, licensed under custom terms with restrictions on public hosting and commercial use.
// See LICENSE.TXT in the repository root for details.

using System.Linq;
using System.Numerics;
using Content.Shared.DeadSpace.Carrying;
using Content.Shared.Humanoid;
using Content.Shared.Rotation;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;

namespace Content.Client.DeadSpace.Carrying;

public sealed class CarryVisualizerSystem : EntitySystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private readonly Dictionary<EntityUid, CarryVisualState> _states = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CarriedComponent, AfterAutoHandleStateEvent>(OnCarriedState);
        SubscribeLocalEvent<CarriedComponent, ComponentShutdown>(OnCarriedShutdown);
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<CarriedComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var carried, out var sprite))
        {
            UpdateVisual((uid, carried, sprite));
        }
    }

    private void OnCarriedState(Entity<CarriedComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        UpdateVisual((ent.Owner, ent.Comp, sprite));
    }

    private void OnCarriedShutdown(Entity<CarriedComponent> ent, ref ComponentShutdown args)
    {
        ResetVisual(ent.Owner);
    }

    private void UpdateVisual(Entity<CarriedComponent, SpriteComponent> ent)
    {
        if (ent.Comp1.Carrier is not { } carrier ||
            !TryComp<SpriteComponent>(carrier, out var carrierSprite))
        {
            ResetVisual(ent.Owner);
            return;
        }

        if (!_states.ContainsKey(ent.Owner))
        {
            _states[ent.Owner] = new CarryVisualState(
                ent.Comp2.Offset,
                ent.Comp2.DrawDepth,
                ent.Comp2.Rotation,
                ent.Comp2.EnableDirectionOverride,
                ent.Comp2.DirectionOverride);
        }

        var state = _states[ent.Owner];
        var angle = _transform.GetWorldRotation(carrier) + _eye.CurrentEye.Rotation;
        var direction = angle.GetCardinalDir();

        var offset = direction switch
        {
            Direction.North => new Vector2(-0.02f, 0.02f),
            Direction.South => new Vector2(-0.10f, -0.08f),
            Direction.East => new Vector2(0.04f, -0.10f),
            Direction.West => new Vector2(-0.04f, -0.10f),
            _ => new Vector2(-0.08f, -0.08f),
        };

        var isHumanoid = HasComp<HumanoidAppearanceComponent>(ent.Owner);
        var behindCarrier = direction is Direction.North or Direction.East ||
                            direction == Direction.South && isHumanoid;
        var drawDepth = behindCarrier
            ? carrierSprite.DrawDepth - 1
            : carrierSprite.DrawDepth + 1;

        if (isHumanoid)
        {
            var rotation = direction switch
            {
                Direction.East => Angle.FromDegrees(90),
                Direction.West => Angle.FromDegrees(-90),
                Direction.South => Angle.Zero,
                Direction.North => Angle.Zero,
                _ => Angle.Zero,
            };

            var directionOverride = direction switch
            {
                Direction.North => Direction.North,
                Direction.South => Direction.South,
                _ => Direction.South,
            };

            ent.Comp2.EnableDirectionOverride = true;
            ent.Comp2.DirectionOverride = directionOverride;
            _sprite.SetRotation((ent.Owner, ent.Comp2), rotation);
        }

        SetHeadOnly(ent.Owner, ent.Comp2, state, isHumanoid && direction == Direction.South);

        _sprite.SetOffset((ent.Owner, ent.Comp2), state.Offset + offset);
        _sprite.SetDrawDepth((ent.Owner, ent.Comp2), drawDepth);
    }

    private void ResetVisual(EntityUid uid)
    {
        if (!_states.Remove(uid, out var state))
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        RestoreLayerVisibility(uid, sprite, state);
        _sprite.SetOffset((uid, sprite), state.Offset);
        _sprite.SetDrawDepth((uid, sprite), state.DrawDepth);
        _sprite.SetRotation((uid, sprite), GetCurrentRotation(uid, state.Rotation));
        sprite.EnableDirectionOverride = state.EnableDirectionOverride;
        sprite.DirectionOverride = state.DirectionOverride;
    }

    private void SetHeadOnly(EntityUid uid, SpriteComponent sprite, CarryVisualState state, bool enabled)
    {
        if (!enabled)
        {
            RestoreLayerVisibility(uid, sprite, state);
            return;
        }

        state.LayerVisibility ??= CaptureLayerVisibility(sprite);

        var visibleHeadLayers = new HashSet<int>();
        foreach (var layer in HumanoidVisualLayersExtension.Sublayers(HumanoidVisualLayers.Head))
        {
            if (_sprite.LayerMapTryGet((uid, sprite), layer, out var index, false))
                visibleHeadLayers.Add(index);
        }

        if (visibleHeadLayers.Count == 0)
        {
            RestoreLayerVisibility(uid, sprite, state);
            return;
        }

        for (var i = 0; i < sprite.AllLayers.Count(); i++)
        {
            _sprite.LayerSetVisible((uid, sprite), i, visibleHeadLayers.Contains(i) && state.LayerVisibility.GetValueOrDefault(i));
        }
    }

    private void RestoreLayerVisibility(EntityUid uid, SpriteComponent sprite, CarryVisualState state)
    {
        if (state.LayerVisibility == null)
            return;

        foreach (var (index, visible) in state.LayerVisibility)
        {
            if (index >= sprite.AllLayers.Count())
                continue;

            _sprite.LayerSetVisible((uid, sprite), index, visible);
        }

        state.LayerVisibility = null;
    }

    private static Dictionary<int, bool> CaptureLayerVisibility(SpriteComponent sprite)
    {
        var visibility = new Dictionary<int, bool>();
        for (var i = 0; i < sprite.AllLayers.Count(); i++)
        {
            visibility[i] = sprite[i].Visible;
        }

        return visibility;
    }

    private Angle GetCurrentRotation(EntityUid uid, Angle fallback)
    {
        if (!TryComp<RotationVisualsComponent>(uid, out var rotationVisuals))
            return fallback;

        if (!TryComp<AppearanceComponent>(uid, out var appearance) ||
            !_appearance.TryGetData<RotationState>(uid, RotationVisuals.RotationState, out var state, appearance))
        {
            return fallback;
        }

        return state == RotationState.Horizontal
            ? rotationVisuals.HorizontalRotation
            : rotationVisuals.VerticalRotation;
    }

    private sealed class CarryVisualState(
        Vector2 offset,
        int drawDepth,
        Angle rotation,
        bool enableDirectionOverride,
        Direction directionOverride)
    {
        public readonly Vector2 Offset = offset;
        public readonly int DrawDepth = drawDepth;
        public readonly Angle Rotation = rotation;
        public readonly bool EnableDirectionOverride = enableDirectionOverride;
        public readonly Direction DirectionOverride = directionOverride;
        public Dictionary<int, bool>? LayerVisibility;
    }
}
