// Dead Space 14, licensed under custom terms with restrictions on public hosting and commercial use.
// See LICENSE.TXT in the repository root for details.

using Robust.Shared.GameStates;

namespace Content.Shared.DeadSpace.Carrying;

public enum CarrySize : byte
{
    Small,
    Large,
}

public enum CarryStrength : byte
{
    SmallOnly,
    Any,
}

[RegisterComponent]
public sealed partial class CarrySizeComponent : Component
{
    [DataField]
    public CarrySize Size = CarrySize.Large;
}

[RegisterComponent]
public sealed partial class CarryStrengthComponent : Component
{
    [DataField]
    public CarryStrength Strength = CarryStrength.Any;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class CarryingComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Carried;

    public readonly List<EntityUid> VirtualItems = new();

    [ViewVariables]
    public bool Stopping;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class CarriedComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Carrier;

    [ViewVariables]
    public bool Stopping;

    [ViewVariables]
    public bool AddedBlockMovement;

    [ViewVariables]
    public bool? PreviousCanCollide;

    [ViewVariables]
    public bool ForcedDown;

    [ViewVariables]
    public bool WasStanding;
}
