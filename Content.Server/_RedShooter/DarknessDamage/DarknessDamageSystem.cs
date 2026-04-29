using Content.Shared._RedShooter.DarknessDamage.Components;
using Content.Shared.Damage;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._RedShooter.DarknessDamage;

/// <summary>
/// Deals damage to an entity that is standing in darkness.
/// Requires <see cref="DarknessDamageComponent"/> and <see cref="DarknessDetectionComponent"/>.
/// Light from held items and inventory is accounted for by <see cref="DarknessDetectionSystem"/>.
/// </summary>
public sealed class DarknessDamageSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DarknessDamageComponent, DarknessDetectionComponent>();
        while (query.MoveNext(out var uid, out var comp, out var detection))
        {
            if (comp.NextUpdate > _timing.CurTime)
                continue;

            comp.NextUpdate = _timing.CurTime + comp.UpdateInterval;

            if (detection.CurrentLightLevel <= comp.DarknessThreshold && !_mobState.IsDead(uid))
            {
                _damageable.TryChangeDamage(uid, comp.DamageToDeal, ignoreResistances: false);

                if (comp.SoundOnDamage != null)
                    _audio.PlayPvs(comp.SoundOnDamage, uid, AudioParams.Default.WithVolume(-2f));
            }
        }
    }
}
