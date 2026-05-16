using Content.Shared.Actions;

namespace Content.Shared._RedShooter.RDSVehicle;

public sealed partial class ExitVehicleActionEvent : InstantActionEvent { }

[ByRefEvent]
public record struct RdsVehicleEnteredEvent(EntityUid User);

[ByRefEvent]
public record struct RdsVehicleExitedEvent(EntityUid User);
