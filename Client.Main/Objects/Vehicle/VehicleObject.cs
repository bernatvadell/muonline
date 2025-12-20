using Client.Main.Content;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Vehicle;

public class VehicleObject : ModelObject
{
    // Default vehicle animation indices (some vehicles override via VehicleDefinition)
    public const int DefaultAnimationIdle = 0;
    public const int DefaultAnimationRun = 2;
    public const int DefaultAnimationSkill = 4;

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
    private float idleAnimationSpeedMultiplier = 1.0f;
    private float runAnimationSpeedMultiplier = 1.0f;
    private float skillAnimationSpeedMultiplier = 1.0f;
    private int idleActionIndex = DefaultAnimationIdle;
    private int runActionIndex = DefaultAnimationRun;
    private int skillActionIndex = DefaultAnimationSkill;
    private Dictionary<int, float> actionPlaySpeedOverrides;

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
        AnimationSpeed = 25f;
    }

    private async Task OnChangeIndex()
    {
        if (ItemIndex < 0)
        {
            Model = null;
            RiderHeightOffset = 0f;
            AnimationSpeedMultiplier = 1.0f;
            idleAnimationSpeedMultiplier = 1.0f;
            runAnimationSpeedMultiplier = 1.0f;
            skillAnimationSpeedMultiplier = 1.0f;
            idleActionIndex = DefaultAnimationIdle;
            runActionIndex = DefaultAnimationRun;
            skillActionIndex = DefaultAnimationSkill;
            actionPlaySpeedOverrides = null;
            return;
        }
        VehicleDefinition riderDefinition = VehicleDatabase.GetVehicleDefinition(itemIndex);
        if (riderDefinition == null) return;

        // Store configuration from definition
        RiderHeightOffset = riderDefinition.RiderHeightOffset;
        AnimationSpeedMultiplier = riderDefinition.AnimationSpeedMultiplier;
        AnimationSpeed = riderDefinition.AnimationSpeed;
        idleAnimationSpeedMultiplier = riderDefinition.IdleAnimationSpeedMultiplier;
        runAnimationSpeedMultiplier = riderDefinition.RunAnimationSpeedMultiplier;
        skillAnimationSpeedMultiplier = riderDefinition.SkillAnimationSpeedMultiplier;
        idleActionIndex = riderDefinition.IdleActionIndex;
        runActionIndex = riderDefinition.RunActionIndex;
        skillActionIndex = riderDefinition.SkillActionIndex;
        actionPlaySpeedOverrides = riderDefinition.ActionPlaySpeedOverrides;

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
        if (Model?.Actions == null)
            return;

        if (actionPlaySpeedOverrides != null && actionPlaySpeedOverrides.Count > 0)
        {
            foreach (var kvp in actionPlaySpeedOverrides)
            {
                int index = kvp.Key;
                if (index < 0 || index >= Model.Actions.Length)
                {
                    continue;
                }

                var action = Model.Actions[index];
                if (action == null)
                {
                    continue;
                }

                action.PlaySpeed = kvp.Value;
            }
            return;
        }

        bool hasCustomMultiplier = AnimationSpeedMultiplier != 1.0f
            || idleAnimationSpeedMultiplier != 1.0f
            || runAnimationSpeedMultiplier != 1.0f
            || skillAnimationSpeedMultiplier != 1.0f;
        if (!hasCustomMultiplier)
            return;

        for (int i = 0; i < Model.Actions.Length; i++)
        {
            var action = Model.Actions[i];
            if (action == null)
            {
                continue;
            }

            float multiplier = AnimationSpeedMultiplier;
            if (i == idleActionIndex)
            {
                multiplier *= idleAnimationSpeedMultiplier;
            }
            else if (i == runActionIndex)
            {
                multiplier *= runAnimationSpeedMultiplier;
            }
            else if (i == skillActionIndex)
            {
                multiplier *= skillAnimationSpeedMultiplier;
            }

            if (multiplier != 1.0f)
            {
                action.PlaySpeed *= multiplier;
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
            targetAnim = skillActionIndex;
            // Skill animation should play once and hold on last frame
            HoldOnLastFrame = true;
        }
        else
        {
            if (isMoving)
                targetAnim = runActionIndex;
            else
                targetAnim = idleActionIndex;

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
