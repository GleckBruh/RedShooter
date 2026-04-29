namespace Content.Shared._RedShooter.DarknessDamage.Components;

/// <summary>
/// Tracks the current light level around an entity, including light sources
/// held in hands, pockets or any container on the entity.
/// Used by DarknessDetectionSystem to feed DarknessDamageSystem.
/// </summary>
[RegisterComponent]
public sealed partial class DarknessDetectionComponent : Component
{
    /// <summary>
    /// The current computed light level. Updated every tick by DarknessDetectionSystem.
    /// </summary>
    [DataField]
    public float CurrentLightLevel = 0f;
}
