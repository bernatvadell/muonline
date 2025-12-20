
namespace Client.Main.Objects.Vehicle;

public class VehicleDefinition
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string TexturePath { get; set; }

    /// <summary>
    /// Base animation speed for this vehicle (multiplies action PlaySpeed).
    /// Default is 25f to match player/vehicle baseline.
    /// </summary>
    public float AnimationSpeed { get; set; } = 25f;

    /// <summary>
    /// Action indices for this vehicle model (as in SourceMain5.2 mount SetAction indices).
    /// </summary>
    public int IdleActionIndex { get; set; } = 0;
    public int RunActionIndex { get; set; } = 2;
    public int SkillActionIndex { get; set; } = 4;

    /// <summary>
    /// Vertical offset (Z-axis) applied to the rider when mounted on this vehicle.
    /// Positive values raise the rider, negative values lower them.
    /// Default is 0 (no offset).
    /// </summary>
    public float RiderHeightOffset { get; set; } = 0f;

    /// <summary>
    /// Multiplier for the vehicle's animation speed.
    /// Values greater than 1.0 speed up the animation, less than 1.0 slow it down.
    /// Default is 1.0 (normal speed).
    /// </summary>
    public float AnimationSpeedMultiplier { get; set; } = 1.0f;

    /// <summary>
    /// Per-action animation speed multipliers for idle/run/skill actions.
    /// These multiply AnimationSpeedMultiplier for the specific action index.
    /// </summary>
    public float IdleAnimationSpeedMultiplier { get; set; } = 1.0f;
    public float RunAnimationSpeedMultiplier { get; set; } = 1.0f;
    public float SkillAnimationSpeedMultiplier { get; set; } = 1.0f;

    /// <summary>
    /// Absolute PlaySpeed overrides per action index (mirrors SourceMain5.2 mount "Velocity" values).
    /// When provided, overrides take precedence over multipliers.
    /// </summary>
    public Dictionary<int, float> ActionPlaySpeedOverrides { get; set; }
}
