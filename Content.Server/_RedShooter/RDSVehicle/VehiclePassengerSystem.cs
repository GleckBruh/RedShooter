using Content.Server.Resist;
using Content.Shared._RedShooter.RDSVehicle;

namespace Content.Server._RedShooter.RDSVehicle;

public sealed class RdsVehiclePassengerSystem : SharedVehiclePassengerSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VehiclePassengerComponent, RdsVehicleEnteredEvent>(OnEntered);
        SubscribeLocalEvent<VehiclePassengerComponent, RdsVehicleExitedEvent>(OnExited);
    }

    private void OnEntered(EntityUid uid, VehiclePassengerComponent component, ref RdsVehicleEnteredEvent args)
    {
        RemComp<CanEscapeInventoryComponent>(args.User);
    }

    private void OnExited(EntityUid uid, VehiclePassengerComponent component, ref RdsVehicleExitedEvent args)
    {
        EnsureComp<CanEscapeInventoryComponent>(args.User);
    }
}
