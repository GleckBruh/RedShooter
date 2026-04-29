using Content.Shared.Damage;
using Robust.Shared.Audio;

namespace Content.Shared._RedShooter.DarknessDamage.Components;

/// <summary>
/// Causes an entity to take damage while standing in darkness.
/// </summary>
[RegisterComponent]
public sealed partial class DarknessDamageComponent : Component
{
    /// <summary>
    /// How often damage is applied.
    /// </summary>
    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(1f);

    /// <summary>
    /// Next scheduled damage tick.
    /// </summary>
    [DataField]
    public TimeSpan NextUpdate = TimeSpan.Zero;

    /// <summary>
    /// Damage dealt per tick while in darkness.
    /// </summary>
    [DataField(required: true)]
    public DamageSpecifier DamageToDeal = new();

    /// <summary>
    /// Sound played when damage is applied.
    /// </summary>
    [DataField]
    public SoundSpecifier? SoundOnDamage;

    /// <summary>
    /// Light level below which the entity is considered to be in darkness.
    /// </summary>
    [DataField]
    public float DarknessThreshold = 0.1f;
}
