using System.Linq;
using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Content.Shared.Resist;

namespace Content.Shared._RedShooter.RDSVehicle;

public abstract partial class SharedVehiclePassengerSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VehiclePassengerComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<VehiclePassengerComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<VehiclePassengerComponent, EntInsertedIntoContainerMessage>(OnEntInserted);
        SubscribeLocalEvent<ExitVehicleActionEvent>(OnExitAction);
        SubscribeLocalEvent<VehiclePassengerComponent, EntityTerminatingEvent>(OnRemove);
    }

    private void OnInit(EntityUid uid, VehiclePassengerComponent component, ComponentInit args)
    {
        _container.EnsureContainer<Container>(uid, component.PassengerContainerName);
        _container.EnsureContainer<Container>(uid, component.DriverContainerName);
    }

    private void OnGetVerbs(EntityUid uid, VehiclePassengerComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!HasComp<MobMoverComponent>(args.User))
            return;

        if (!HasComp<RdsVehicleComponent>(uid))
            return;

        if (component.Driver == null)
        {
            var driververb = new AlternativeVerb
            {
                Text = "Сесть за руль",
                Act = () => TryEnterAsDriver(args.User, uid, component)
            };
            args.Verbs.Add(driververb);
        }


        if (component.Passengers.Count < component.MaxPassengers)
        {
            var verb = new AlternativeVerb
            {
                Text = "Сесть как пассажир",
                Act = () => TryEnterVehicle(args.User, uid, component)
            };
            args.Verbs.Add(verb);
        }
    }

    private void TryEnterVehicle(EntityUid user, EntityUid vehicle, VehiclePassengerComponent component)
    {
        var container = _container.GetContainer(vehicle, component.PassengerContainerName);

        if (!_container.Insert(user, container))
            return;

        var ev = new RdsVehicleEnteredEvent(user);
        RaiseLocalEvent(vehicle, ref ev);

        _audio.PlayPvs(component.EnterSound, vehicle);
        component.Passengers.Add(user);
    }

    private void TryEnterAsDriver(EntityUid user, EntityUid vehicle, VehiclePassengerComponent component)
    {
        var container = _container.GetContainer(vehicle, component.DriverContainerName);

        if (!_container.Insert(user, container))
            return;

        var ev = new RdsVehicleEnteredEvent(user);
        RaiseLocalEvent(vehicle, ref ev);

        _audio.PlayPvs(component.EnterSound, vehicle);
        component.Driver = user;
        EnsureComp<VehicleDriverComponent>(user);

        if (TryComp<RdsVehicleComponent>(vehicle, out var vehicleComp))
        {
            if (vehicleComp.HornSound != null)
                _actionsSystem.AddAction(user, ref vehicleComp.HornAction, component.HornAction, vehicle);
            if (vehicleComp.SirenSound != null)
                _actionsSystem.AddAction(user, ref vehicleComp.SirenAction, component.SirenAction, vehicle);

            if (vehicleComp.EngineRunning)
                return;
        }
    }

    private void OnEntInserted(EntityUid uid, VehiclePassengerComponent component, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != component.PassengerContainerName && args.Container.ID != component.DriverContainerName)
            return;

        _actionsSystem.AddAction(args.Entity, component.ExitAction, uid);
    }

    private void OnExitAction(ExitVehicleActionEvent args)
    {
        var passenger = args.Performer;

        var vehicle = Transform(passenger).ParentUid;

        var vehiclePos = Transform(vehicle).Coordinates;

        if (!TryComp<VehiclePassengerComponent>(vehicle, out var component))
            return;

        _container.TryRemoveFromContainer(passenger);
        _audio.PlayPvs(component.ExitSound, vehicle);
        _actionsSystem.RemoveProvidedActions(passenger, vehicle);
        _transform.SetCoordinates(passenger, vehiclePos.Offset(new Vector2(0f, 0f)));

        var ev = new RdsVehicleExitedEvent(passenger);
        RaiseLocalEvent(vehicle, ref ev);

        if (component.Driver == passenger)
        {
            component.Driver = null;
            RemComp<VehicleDriverComponent>(passenger);
        }
        else
        {
            component.Passengers.Remove(passenger);
        }
    }

    private void OnRemove(EntityUid uid, VehiclePassengerComponent component, ref EntityTerminatingEvent args)
    {
        foreach (var passenger in component.Passengers.ToList())
        {
            _container.TryRemoveFromContainer(passenger);
            _actionsSystem.RemoveProvidedActions(passenger, uid);
        }
        if (component.Driver != null)
        {
            _container.TryRemoveFromContainer(component.Driver.Value);
            _actionsSystem.RemoveProvidedActions(component.Driver.Value, uid);
            RemComp<RelayInputMoverComponent>(component.Driver.Value);
        }
    }

}
