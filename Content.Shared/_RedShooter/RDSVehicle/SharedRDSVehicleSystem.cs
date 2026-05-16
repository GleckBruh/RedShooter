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

using System;
using System.Linq;
using System.Numerics;
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
using Content.Shared.Physics;
using Robust.Shared.GameObjects;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._RedShooter.RDSVehicle;

public abstract partial class RdsSharedVehicleSystem : EntitySystem
{
    [Dependency] private readonly SharedAmbientSoundSystem _ambientSound = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

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
        SubscribeLocalEvent<VehiclePassengerComponent, ContainerIsRemovingAttemptEvent>(OnRemovingAttempt);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted)
            return;

        var query = EntityQueryEnumerator<RdsVehicleComponent, VehiclePassengerComponent>();
        while (query.MoveNext(out var uid, out var vehicle, out var passenger))
        {
            if (!vehicle.EngineRunning || passenger.Driver == null)
                continue;

            UpdateVehicle(uid, vehicle, passenger.Driver.Value, frameTime);
        }
    }

    private void OnInit(EntityUid uid, RdsVehicleComponent component, ComponentInit args)
    {
        _appearance.SetData(uid, VehicleState.Animated, component.EngineRunning);
        _appearance.SetData(uid, VehicleState.DrawOver, false);
        component.CurrentAngle = Transform(uid).LocalRotation;
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
    }

    private void OnRemovingAttempt(EntityUid uid, VehiclePassengerComponent component, ContainerIsRemovingAttemptEvent args)
    {
        if (args.Container.ID != component.DriverContainerName
            && args.Container.ID != component.PassengerContainerName)
            return;

        if (HasComp<VehicleDriverComponent>(args.EntityUid))
            args.Cancel();
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

        component.SirenStream = component.SirenEnabled
            ? _audio.Stop(component.SirenStream)
            : _audio.PlayPvs(component.SirenSound, uid)?.Entity;
        component.SirenEnabled = !component.SirenEnabled;
        args.Handled = true;
    }

    private void Mount(EntityUid driver, EntityUid vehicle)
    {
    }

    private void OnItemSlotEject(EntityUid uid, RdsVehicleComponent comp, ref ItemSlotEjectAttemptEvent args)
    {
        if (!comp.PreventEjectOfKey || !TryComp<VehiclePassengerComponent>(uid, out var passengerComp) ||
            passengerComp.Driver == null || args.Slot.ID != comp.KeySlot || args.User == passengerComp.Driver)
            return;

        args.Cancelled = true;
    }

    private void OnBreak(EntityUid uid, RdsVehicleComponent component, BreakageEventArgs args)
    {
        component.IsBroken = true;

        //remove drivers ability to drive if there is a driver
        if (TryComp<VehiclePassengerComponent>(uid, out var passengerComp)
            && passengerComp.Driver != null)

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

    private void UpdateVehicle(EntityUid uid, RdsVehicleComponent vehicle, EntityUid driver, float frameTime)
    {
        if (!TryComp<InputMoverComponent>(driver, out var mover))
            return;

        var moveButtons = mover.HeldMoveButtons;
        var inputForward = (moveButtons & MoveButtons.Up) != 0;
        var inputBackward = (moveButtons & MoveButtons.Down) != 0;
        var inputLeft = (moveButtons & MoveButtons.Left) != 0;
        var inputRight = (moveButtons & MoveButtons.Right) != 0;

        if (inputForward)
            vehicle.CurrentSpeed += vehicle.Acceleration * frameTime;
        else if (inputBackward)
            vehicle.CurrentSpeed -= vehicle.Acceleration * frameTime;
        else
            vehicle.CurrentSpeed = vehicle.CurrentSpeed > 0
                ? Math.Max(0, vehicle.CurrentSpeed - vehicle.Friction * frameTime)
                : Math.Min(0, vehicle.CurrentSpeed + vehicle.Friction * frameTime);

        vehicle.CurrentSpeed = Math.Clamp(vehicle.CurrentSpeed, -vehicle.MaxSpeed * 0.5f, vehicle.MaxSpeed);

        if (Math.Abs(vehicle.CurrentSpeed) > 0.1f)
        {
            var turnDirection = 0f;
            if (inputLeft) turnDirection = 1f;
            if (inputRight) turnDirection = -1f;

            // Инвертируем поворот при заднем ходу
            if (vehicle.CurrentSpeed < 0f)
                turnDirection *= -1f;

            // Замедление при повороте
            if (turnDirection != 0f)
                vehicle.CurrentSpeed *= 1f - 0.015f * Math.Abs(vehicle.CurrentSpeed) / vehicle.MaxSpeed;

            // Поворот сильнее на низкой скорости, плавнее на высокой
            var speedFactor = Math.Abs(vehicle.CurrentSpeed) / vehicle.MaxSpeed;
            var actualTurnSpeed = vehicle.TurnSpeed * (0.8f + speedFactor * 0.6f);
            vehicle.CurrentAngle += turnDirection * actualTurnSpeed * frameTime;

            var driftFactor = 0.1f + speedFactor * 0.4f;
            vehicle.VelocityAngle = Angle.Lerp(vehicle.VelocityAngle, vehicle.CurrentAngle, driftFactor);
        }
        else
        {
            vehicle.VelocityAngle = vehicle.CurrentAngle;
        }

        var delta = vehicle.VelocityAngle.ToVec() * vehicle.CurrentSpeed * frameTime;

        var xform = Transform(uid);
        var oldPos = xform.LocalPosition;
        _transform.SetLocalPosition(uid, oldPos + delta);

        var intersecting = _physics.GetEntitiesIntersectingBody(uid, (int) CollisionGroup.Impassable);
        intersecting.Remove(uid);

        if (intersecting.Count > 0)
        {
            _transform.SetLocalPosition(uid, oldPos);
            vehicle.CurrentSpeed *= -0.1f;
        }

        _transform.SetLocalRotation(uid, vehicle.CurrentAngle);
    }
}
