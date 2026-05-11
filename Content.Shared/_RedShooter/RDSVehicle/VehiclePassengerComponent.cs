using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._RedShooter.RDSVehicle;

[RegisterComponent, NetworkedComponent]
public sealed partial class VehiclePassengerComponent : Component
{
    [DataField]
    public string PassengerContainerName = "vehicle_passengers";

    [DataField]
    public int MaxPassengers = 4;

    [DataField]
    public EntProtoId ExitAction = "RDSActionVehicleExit";

    [DataField]
    public EntProtoId HornAction = "ActionHorn";

    [DataField]
    public EntProtoId SirenAction = "ActionSiren";

    [ViewVariables]
    public List<EntityUid> Passengers = new();

    [DataField]
    public string DriverContainerName = "vehicle_driver";

    [DataField]
    public SoundSpecifier? EnterSound;

    [DataField]
    public SoundSpecifier? ExitSound;

    [ViewVariables]
    public EntityUid? Driver;
}
