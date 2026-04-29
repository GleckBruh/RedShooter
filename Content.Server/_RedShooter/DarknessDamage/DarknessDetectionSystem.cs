using Content.Goobstation.Common.CCVar;
using Content.Shared._RedShooter.DarknessDamage.Components;
using Content.Server.Disposal.Unit;
using Content.Server.Light.Components;
using Content.Shared.Light.Components;
using Content.Shared.Physics;
using Robust.Server.GameObjects;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._RedShooter.DarknessDamage;

/// <summary>
/// Detects the light level around an entity.
/// Works like LightDetectionSystem but also accounts for light sources
/// that are held in hands, pockets, or any other container on the entity.
/// Supports PointLightComponent and ExpendableLightComponent (flares, chemlights, torches).
/// </summary>
public sealed class DarknessDetectionSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;

    private float _lookupRange;
    private float _updateFrequency;
    private float _maximumLightLevel;

    private const float ExpendableLightFallbackEnergy = 3.0f;

    private TimeSpan _nextUpdate = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, GoobCVars.LightDetectionRange,  value => _lookupRange       = value, true);
        Subs.CVar(_cfg, GoobCVars.LightUpdateFrequency, value => _updateFrequency   = value, true);
        Subs.CVar(_cfg, GoobCVars.LightMaximumLevel,    value => _maximumLightLevel = value, true);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_nextUpdate > _timing.CurTime)
            return;

        _nextUpdate = _timing.CurTime + TimeSpan.FromSeconds(_updateFrequency);

        var query = EntityQueryEnumerator<DarknessDetectionComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            comp.CurrentLightLevel = ComputeLightLevel(uid, xform);
        }
    }

    private float ComputeLightLevel(EntityUid uid, TransformComponent xform)
    {
        if (HasComp<BeingDisposedComponent>(uid))
            return 0f;

        var worldPos = _transform.GetWorldPosition(xform);
        var totalLightLevel = 0f;

        // ── 1. World lights (raycast) ─────────────────────────────────────────
        var worldLights = _lookup.GetEntitiesInRange<PointLightComponent>(xform.Coordinates, _lookupRange);
        foreach (var ent in worldLights)
        {
            var (point, pointLight) = ent;

            var isActiveExpendable = TryComp<ExpendableLightComponent>(point, out var expLight)
                                     && expLight.CurrentState is ExpendableLightState.Lit or ExpendableLightState.Fading;

            if (!pointLight.Enabled && !isActiveExpendable)
                continue;

            if (isActiveExpendable && _containerSystem.IsEntityInContainer(point))
                continue;

            var lightPos = _transform.GetWorldPosition(Transform(point));
            var distance = (lightPos - worldPos).Length();

            if (distance <= 0.01f)
            {
                totalLightLevel += isActiveExpendable ? ExpendableLightFallbackEnergy : pointLight.Energy;
                continue;
            }

            if (totalLightLevel >= _maximumLightLevel)
                return _maximumLightLevel;

            if (distance > pointLight.Radius)
                continue;

            var direction  = (worldPos - lightPos).Normalized();
            var ray        = new CollisionRay(lightPos, direction, (int) CollisionGroup.Opaque);
            var rayResults = _physics.IntersectRay(xform.MapID, ray, distance, point);

            var blocked = false;
            foreach (var result in rayResults)
            {
                if (result.HitEntity != uid)
                {
                    blocked = true;
                    break;
                }
            }

            if (blocked)
                continue;

            var lightEnergy = isActiveExpendable ? ExpendableLightFallbackEnergy : pointLight.Energy;
            var t = distance / pointLight.Radius;
            totalLightLevel += lightEnergy * (1f - t * t);
        }

        // ── 2. Lights in containers (hands, pockets, inventory, bags) ─────────
        if (TryComp<ContainerManagerComponent>(uid, out var containerManager))
        {
            foreach (var container in containerManager.Containers.Values)
            {
                foreach (var contained in container.ContainedEntities)
                {
                    totalLightLevel += GetContainedLightEnergy(contained);

                    if (totalLightLevel >= _maximumLightLevel)
                        return _maximumLightLevel;
                }
            }
        }

        return Math.Min(totalLightLevel, _maximumLightLevel);
    }

    /// <summary>
    /// Returns the energy of an active light source on a contained entity.
    /// Recurses one level deeper to handle bags/toolboxes held in hand.
    /// </summary>
    private float GetContainedLightEnergy(EntityUid contained)
    {
        var energy = 0f;

        energy += GetEntityLightEnergy(contained);

        if (TryComp<ContainerManagerComponent>(contained, out var innerManager))
        {
            foreach (var inner in innerManager.Containers.Values)
                foreach (var innerEnt in inner.ContainedEntities)
                    energy += GetEntityLightEnergy(innerEnt);
        }

        return energy;
    }

    /// <summary>
    /// Returns light energy for a single entity, handling both
    /// ExpendableLight (netsync=false, Enabled=false during animation)
    /// and regular PointLight sources.
    /// </summary>
    private float GetEntityLightEnergy(EntityUid entity)
    {
        if (TryComp<ExpendableLightComponent>(entity, out var expendable)
            && expendable.CurrentState is ExpendableLightState.Lit or ExpendableLightState.Fading)
        {
            return ExpendableLightFallbackEnergy;
        }

        if (TryComp<PointLightComponent>(entity, out var light) && light.Enabled)
            return light.Energy;

        return 0f;
    }
}
