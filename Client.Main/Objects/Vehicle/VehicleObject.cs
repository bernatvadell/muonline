using Client.Main.Content;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Vehicle;

public class VehicleObject : ModelObject
{
    // Vehicle animation indices
    public const int AnimationIdle = 0;
    public const int AnimationRun = 2;
    public const int AnimationSkill = 4; // Plasma Storm etc.

    // Vehicle IDs that have root motion in their run animation and need position locking
    private static readonly HashSet<int> VehiclesWithRootMotion = new()
    {
        7,  // Rider 01 (Uniria/Dinorant)
        8,  // Rider 02
        27, // Pon Up Ride
        28, // Pon Ride
        22, // Griffs Up Ride
        23, // Griffs Ride
        30, // Rippen Up Ride
        31, // Rippen Ride
    };

    private short itemIndex = -1;
    public short ItemIndex
    {
        get => itemIndex;
        set
        {
            if (itemIndex == value) return;
            itemIndex = value;
            _ = OnChangeIndex();
        }
    }

    /// <summary>
    /// The vertical offset to apply to the rider when mounted on this vehicle.
    /// Retrieved from VehicleDefinition when the vehicle is loaded.
    /// </summary>
    public float RiderHeightOffset { get; private set; } = 0f;

    /// <summary>
    /// The animation speed multiplier for this vehicle.
    /// Retrieved from VehicleDefinition when the vehicle is loaded.
    /// </summary>
    public float AnimationSpeedMultiplier { get; private set; } = 1.0f;

    public VehicleObject()
    {
        RenderShadow = true;
        IsTransparent = true;
        AffectedByTransparency = true;
        BlendState = BlendState.AlphaBlend;
        BlendMesh = -1;
        BlendMeshState = BlendState.Additive;
        Alpha = 1f;
        LinkParentAnimation = false;
    }

    private async Task OnChangeIndex()
    {
        if (ItemIndex < 0)
        {
            Model = null;
            RiderHeightOffset = 0f;
            AnimationSpeedMultiplier = 1.0f;
            return;
        }
        VehicleDefinition riderDefinition = VehicleDatabase.GetVehicleDefinition(itemIndex);
        if (riderDefinition == null) return;

        // Store configuration from definition
        RiderHeightOffset = riderDefinition.RiderHeightOffset;
        AnimationSpeedMultiplier = riderDefinition.AnimationSpeedMultiplier;

        string modelPath = riderDefinition.TexturePath;

        Model = await BMDLoader.Instance.Prepare(Path.Combine("Skill", modelPath));

        if (Model == null)
        {
            Status = GameControlStatus.Error;
        }
        else
        {
            if (Status == GameControlStatus.Error)
            {
                Status = GameControlStatus.Ready;
            }

            // Apply animation speed multiplier to all actions
            ApplyAnimationSpeedMultiplier();

            // For vehicles with root motion in their animations, lock positions to prevent drifting
            if (VehiclesWithRootMotion.Contains(itemIndex))
            {
                ApplyPositionLockToAnimations();
            }
        }
    }

    /// <summary>
    /// Applies the AnimationSpeedMultiplier to all animations for this vehicle.
    /// </summary>
    private void ApplyAnimationSpeedMultiplier()
    {
        if (Model?.Actions == null || AnimationSpeedMultiplier == 1.0f)
            return;

        foreach (var action in Model.Actions)
        {
            if (action != null)
            {
                action.PlaySpeed *= AnimationSpeedMultiplier;
            }
        }
    }

    /// <summary>
    /// Forces LockPositions on all animations for vehicles that have root motion baked in.
    /// This prevents the vehicle from drifting ahead during run animations.
    /// </summary>
    private void ApplyPositionLockToAnimations()
    {
        if (Model?.Actions == null)
            return;

        foreach (var action in Model.Actions)
        {
            if (action != null && !action.LockPositions)
            {
                action.LockPositions = true;
            }
        }
    }

    public override async Task Load()
    {
        await base.Load();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
    }

    /// <summary>
    /// Sets the vehicle animation based on rider state.
    /// </summary>
    public void SetRiderAnimation(bool isMoving, bool isUsingSkill = false)
    {
        if (Model == null || Hidden)
            return;

        int targetAnim;
        if (isUsingSkill)
        {
            targetAnim = AnimationSkill;
            // Skill animation should play once and hold on last frame
            HoldOnLastFrame = true;
        }
        else
        {
            if (isMoving)
                targetAnim = AnimationRun;
            else
                targetAnim = AnimationIdle;

            // Normal animations should loop
            HoldOnLastFrame = false;
        }

        if (CurrentAction != targetAnim)
        {
            CurrentAction = targetAnim;
            // Reset animation time when changing actions
            _animTime = 0.0;
        }
    }

    public override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);
        foreach (var child in Children)
        {
            child.Draw(gameTime);
        }
    }

    public override void DrawAfter(GameTime gameTime)
    {
        base.DrawAfter(gameTime);
        foreach (var child in Children)
        {
            child.DrawAfter(gameTime);
        }
    }
}