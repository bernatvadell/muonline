
namespace Client.Main.Objects.Vehicle;

public class VehicleDefinition
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string TexturePath { get; set; }

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
}
