// SPDX-FileCopyrightText: 2024 Piras314 <p1r4s@proton.me>
// SPDX-FileCopyrightText: 2024 Scruq445 <storchdamien@gmail.com>
// SPDX-FileCopyrightText: 2025 Aiden <28298836+Aidenkrz@users.noreply.github.com>
// SPDX-FileCopyrightText: 2025 Fishbait <Fishbait@git.ml>
// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Misandry <mary@thughunt.ing>
// SPDX-FileCopyrightText: 2025 fishbait <gnesse@gmail.com>
// SPDX-FileCopyrightText: 2025 gluesniffler <linebarrelerenthusiast@gmail.com>
// SPDX-FileCopyrightText: 2025 gus <august.eymann@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._vg.TileMovement;
using Content.Shared.Access.Components;
using Content.Shared.Actions;
using Content.Shared.Audio;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Destructible;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Damage;
using Content.Shared.Actions.Components;

namespace Content.Shared._RedShooter.RDSVehicle;

public abstract partial class RdsSharedVehicleSystem : EntitySystem
{
    [Dependency] private readonly SharedAmbientSoundSystem _ambientSound = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;

    private static readonly EntProtoId HornActionId = "ActionHorn";
    private static readonly EntProtoId SirenActionId = "ActionSiren";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RdsVehicleComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<RdsVehicleComponent, ComponentRemove>(OnRemove);

        SubscribeLocalEvent<RdsVehicleComponent, EntInsertedIntoContainerMessage>(OnInsert);
        SubscribeLocalEvent<RdsVehicleComponent, EntRemovedFromContainerMessage>(OnEject);

        SubscribeLocalEvent<RdsVehicleComponent, HornActionEvent>(OnHorn);
        SubscribeLocalEvent<RdsVehicleComponent, SirenActionEvent>(OnSiren);
        SubscribeLocalEvent<RdsVehicleComponent, ItemSlotEjectAttemptEvent>(OnItemSlotEject);
        SubscribeLocalEvent<RdsVehicleComponent, BreakageEventArgs>(OnBreak);
        SubscribeLocalEvent<RdsVehicleComponent, DamageChangedEvent>(OnRepair);
        SubscribeLocalEvent<RdsVehicleComponent, GetAdditionalAccessEvent>(OnGetAdditionalAccess);
    }

    private void OnInit(EntityUid uid, RdsVehicleComponent component, ComponentInit args)
    {
        _appearance.SetData(uid, VehicleState.Animated, component.EngineRunning);
        _appearance.SetData(uid, VehicleState.DrawOver, false);
    }

    private void OnRemove(EntityUid uid, RdsVehicleComponent component, ComponentRemove args)
    {
        _appearance.SetData(uid, VehicleState.DrawOver, false);
    }

    private void OnInsert(EntityUid uid, RdsVehicleComponent component, ref EntInsertedIntoContainerMessage args)
    {
        if (HasComp<InstantActionComponent>(args.Entity)
            || args.Container.ID != component.KeySlot
            || component.IsBroken)
            return;

        component.EngineRunning = true;
        _appearance.SetData(uid, VehicleState.Animated, true);

        _ambientSound.SetAmbience(uid, true);

        if (!TryComp<VehiclePassengerComponent>(uid, out var passengerComp)
            || passengerComp.Driver == null)
            return;

        Mount(passengerComp.Driver.Value, uid);
    }

    private void OnEject(EntityUid uid, RdsVehicleComponent component, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != component.KeySlot)
            return;
        component.EngineRunning = false;
        _appearance.SetData(uid, VehicleState.Animated, false);
        _ambientSound.SetAmbience(uid, false);

        if (!TryComp<VehiclePassengerComponent>(uid, out var passengerComp)
            || passengerComp.Driver == null)
            return;

        RemComp<RelayInputMoverComponent>(passengerComp.Driver.Value);
    }

    private void OnHorn(EntityUid uid, RdsVehicleComponent component, InstantActionEvent args)
    {
        if (args.Handled
            || !TryComp<VehiclePassengerComponent>(uid, out var passengerComp)
            || passengerComp.Driver != args.Performer
            || component.HornSound == null)
            return;

        _audio.PlayPvs(component.HornSound, uid);
        args.Handled = true;
    }

    private void OnSiren(EntityUid uid, RdsVehicleComponent component, InstantActionEvent args)
    {
        if (args.Handled
            || !TryComp<VehiclePassengerComponent>(uid, out var passengerComp)
            || passengerComp.Driver != args.Performer
            || component.SirenSound == null)
            return;

        component.SirenStream = component.SirenEnabled ? _audio.Stop(component.SirenStream) : _audio.PlayPvs(component.SirenSound, uid)?.Entity;
        component.SirenEnabled = !component.SirenEnabled;
        args.Handled = true;
    }

    private void Mount(EntityUid driver, EntityUid vehicle)
    {
        _mover.SetRelay(driver, vehicle);

        if (HasComp<TileMovementComponent>(driver))
            EnsureComp<TileMovementComponent>(vehicle);
    }

    private void OnItemSlotEject(EntityUid uid, RdsVehicleComponent comp, ref ItemSlotEjectAttemptEvent args)
    {
        if (!comp.PreventEjectOfKey ||!TryComp<VehiclePassengerComponent>(uid, out var passengerComp) || passengerComp.Driver == null || args.Slot.ID != comp.KeySlot || args.User == passengerComp.Driver)
            return;

        args.Cancelled = true;
    }

    private void OnBreak(EntityUid uid, RdsVehicleComponent component, BreakageEventArgs args)
    {
        component.IsBroken = true;

        //remove drivers ability to drive if there is a driver
        if (TryComp<VehiclePassengerComponent>(uid, out var passengerComp)
            && passengerComp.Driver != null)
            RemComp<RelayInputMoverComponent>(passengerComp.Driver.Value);

        //stop animation
        component.EngineRunning = false;
        _appearance.SetData(uid, VehicleState.Animated, false);
        _ambientSound.SetAmbience(uid, false);
    }

    private void OnRepair(EntityUid uid, RdsVehicleComponent component, DamageChangedEvent args)
    {
        if (component.IsBroken && args.Damageable.TotalDamage == FixedPoint2.Zero)
            component.IsBroken = false;
    }

    private void OnGetAdditionalAccess(EntityUid uid, RdsVehicleComponent component, ref GetAdditionalAccessEvent args)
    {
        if (!TryComp<VehiclePassengerComponent>(uid, out var passengerComp)
            || passengerComp.Driver == null)
            return;

        args.Entities.Add(passengerComp.Driver.Value);
    }
}
