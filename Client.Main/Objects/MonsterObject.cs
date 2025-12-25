using Client.Data.BMD;
using Client.Main.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Client.Main.Objects
{
    public abstract class MonsterObject : WalkerObject
    {
        // --- Fields ---
        private int _lastActionForIdleSound = -1;
        private bool _isFading = false;
        private float _fadeTimer = 0f;
        private float _fadeDuration = 3.5f; // longer fade for smoother disappearance
        private float _startZ;
        private const float SinkBelowGround = 30f; // how deep to sink below terrain surface

        /// <summary>
        /// Determines whether a blood stain should be spawned when the monster dies.
        /// </summary>
        public bool Blood { get; set; } = true;

        /// <summary>
        /// Last target id received from server animation packets (used for attack effects).
        /// </summary>
        public ushort LastAttackTargetId { get; internal set; }

        /// <summary>
        /// Set of mesh indices that should NOT use blending (equivalent to NoneBlendMesh = true in original code).
        /// </summary>
        public HashSet<int> NoneBlendMeshes { get; set; } = new HashSet<int>();

        /// <summary>
        /// Gets the monster's display name defined by <see cref="NpcInfoAttribute"/>.
        /// </summary>
        public override string DisplayName
        {
            get
            {
                var attr = (NpcInfoAttribute)GetType()
                    .GetCustomAttributes(typeof(NpcInfoAttribute), inherit: false)
                    .FirstOrDefault();
                return attr?.DisplayName ?? base.DisplayName;
            }
        }

        // --- Constructors ---
        public MonsterObject() : base()
        {
            Interactive = true;
            AnimationSpeed = 6f;
        }

        public void StartDeathFade(float duration = 3.5f)
        {
            if (_isFading) return;

            // Ensure the monster stops moving while the death animation plays
            StopMovement();
            Interactive = false; // prevent dead monsters from blocking selection
            _isFading = true;
            _fadeDuration = Math.Max(1.5f, duration);
            _fadeTimer = 0f;
            _startZ = Position.Z;

            if (Blood && World != null)
            {
                var stain = new Effects.BloodStainEffect
                {
                    Position = new Vector3(Position.X, Position.Y,
                        World.Terrain.RequestTerrainHeight(Position.X, Position.Y) + Effects.BloodStainEffect.GroundOffset)
                };
                World.Objects.Add(stain);
                _ = stain.Load();
            }
        }

        /// <summary>
        /// Indicates whether the monster is in its death fade stage.
        /// While true the monster should no longer be considered alive.
        /// </summary>
        public bool IsDead => _isFading;

        // --- Public Methods ---
        public override void Update(GameTime gameTime)
        {
            bool wasMoving = IsMoving;

            // Apply fading BEFORE base.Update so buffers/shaders see updated alpha/color this frame
            if (_isFading)
            {
                RenderShadow = false;
                // Also disable shadows for all equipment (weapons, shields, etc.)
                foreach (var child in Children)
                {
                    if (child is ModelObject modelChild)
                    {
                        modelChild.RenderShadow = false;
                    }
                }
                _fadeTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                float p = MathHelper.Clamp(_fadeTimer / _fadeDuration, 0f, 1f);

                // Smooth fade (ease-out)
                float alpha = 1f - p * p; // quadratic ease-out
                Alpha = MathHelper.Clamp(alpha, 0f, 1f);

                // Also darken body color for shader paths that ignore alpha
                byte shade = (byte)(255 * Alpha);
                Color = new Color(shade, shade, shade, (byte)255);
                InvalidateBuffers();

                // Compute terrain height to sink under ground reliably
                float groundZ = World?.Terrain?.RequestTerrainHeight(Position.X, Position.Y) ?? _startZ;
                float targetZ = groundZ - SinkBelowGround;

                // Start sinking after a short delay for better readability
                const float sinkStart = 0.3f; // start sinking after 30% of fade time
                float sinkP = p <= sinkStart ? 0f : (p - sinkStart) / (1f - sinkStart);
                // Ease-in sink for natural fall
                float sinkEase = sinkP * sinkP;
                float newZ = MathHelper.Lerp(_startZ, targetZ, sinkEase);
                Position = new Vector3(Position.X, Position.Y, newZ);

                if (p >= 1f)
                {
                    _isFading = false;
                    World?.RemoveObject(this);
                    Dispose();
                    return;
                }
            }

            base.Update(gameTime);


            // If the monster just stopped moving
            if (wasMoving && !IsMoving && !IsOneShotPlaying)
            {
                // Transition to idle animation if appropriate
                if (CurrentAction == (int)MonsterActionType.Walk ||
                    CurrentAction == (int)MonsterActionType.Run)
                {
                    PlayAction((byte)MonsterActionType.Stop1);
                }

                // Play idle sound once
                if (CurrentAction == (int)MonsterActionType.Stop1 &&
                    _lastActionForIdleSound != CurrentAction)
                {
                    OnIdle();
                    _lastActionForIdleSound = CurrentAction;
                }
            }
            // If the monster just started moving
            else if (!wasMoving && IsMoving)
            {
                OnStartWalk();
                _lastActionForIdleSound = -1;
            }
            // If still moving, reset idle sound flag
            else if (IsMoving)
            {
                _lastActionForIdleSound = -1;
            }
            // If idle and sound not yet played
            else
            {
                if (!IsOneShotPlaying &&
                    CurrentAction == (int)MonsterActionType.Stop1 &&
                    _lastActionForIdleSound != CurrentAction)
                {
                    OnIdle();
                    _lastActionForIdleSound = CurrentAction;
                }
            }
        }

        // --- Protected Virtual Methods for Overriding ---

        /// <summary>
        /// Called when the monster enters idle state.
        /// </summary>
        protected virtual void OnIdle()
        {
            // Base does nothing; override to play idle sound.
        }

        /// <summary>
        /// Called when the monster starts walking.
        /// </summary>
        protected virtual void OnStartWalk()
        {
            // Base does nothing; override to play walk sound.
        }

        /// <summary>
        /// Called when the monster performs an attack.
        /// </summary>
        /// <param name="attackType">Attack variation index.</param>
        public virtual void OnPerformAttack(int attackType = 1)
        {
            _lastActionForIdleSound = -1;
            // Override to play attack sound.
        }

        /// <summary>
        /// Called when the monster receives damage.
        /// </summary>
        public virtual void OnReceiveDamage()
        {
            _lastActionForIdleSound = -1;
            // Override to play hit sound.
        }

        /// <summary>
        /// Minimal health/shield update shim to accept server-provided fractions.
        /// Triggers damage reactions and death fade when health reaches zero.
        /// </summary>
        public void UpdateHealthFractions(float? healthFraction, float? shieldFraction, uint? healthDamage = null, uint? shieldDamage = null)
        {
            if (healthFraction.HasValue)
            {
                float hf = MathHelper.Clamp(healthFraction.Value, 0f, 1f);
                if (hf <= 0f)
                {
                    StartDeathFade();
                }
                else
                {
                    OnReceiveDamage();
                }
            }

            if (shieldFraction.HasValue)
            {
                OnReceiveDamage();
            }
        }

        /// <summary>
        /// Called when the monster’s death animation starts.
        /// </summary>
        public virtual void OnDeathAnimationStart()
        {
            _lastActionForIdleSound = -1;
            // Override to play death sound.
        }

        // --- Helper method for setting speed ---
        protected bool IsValidAction(int actionIndex)
        {
            return Model != null
                && Model.Actions != null
                && actionIndex >= 0
                && actionIndex < Model.Actions.Length
                && Model.Actions[actionIndex] != null;
        }

        protected void SetActionSpeed(MonsterActionType actionType, float speed)
        {
            int actionIndex = (int)actionType;
            if (IsValidAction(actionIndex))
            {
                var action = Model.Actions[actionIndex];
                action.PlaySpeed = speed * 2;
            }
            else
            {
                _logger?.LogDebug($"Warning: Cannot set PlaySpeed for action {(MonsterActionType)actionType} ({actionIndex}). Action does not exist or is null.");
            }
        }

        protected static BMDTextureAction[] BuildActionArray(
            BMD srcModel,
            int dstCount,
            IReadOnlyDictionary<int, int> map)
        {
            var actions = new BMDTextureAction[dstCount];
            foreach (var kv in map)
            {
                int dst = kv.Key;
                int src = kv.Value;
                if (src >= 0 && src < srcModel.Actions.Length)
                    actions[dst] = srcModel.Actions[src];
            }
            return actions;
        }

        protected static BMDTextureBone[] BuildBoneArray(
            BMD srcModel,
            int actionCount,
            IReadOnlyDictionary<int, int> map)
        {
            var bones = new BMDTextureBone[srcModel.Bones.Length];

            for (int i = 0; i < bones.Length; i++)
            {
                var src = srcModel.Bones[i];
                if (ReferenceEquals(src, BMDTextureBone.Dummy))
                {
                    bones[i] = BMDTextureBone.Dummy;
                    continue;
                }

                var matrices = new BMDBoneMatrix[actionCount];
                foreach (var kv in map)
                {
                    int dst = kv.Key;
                    int srcIdx = kv.Value;
                    if (srcIdx >= 0 && srcIdx < src.Matrixes.Length)
                        matrices[dst] = src.Matrixes[srcIdx];
                }

                bones[i] = new BMDTextureBone
                {
                    Name = src.Name,
                    Parent = src.Parent,
                    Matrixes = matrices
                };
            }

            return bones;
        }

        protected new ILogger _logger = ModelObject.AppLoggerFactory?.CreateLogger<MonsterObject>();

        /// <summary>
        /// Override to support NoneBlendMeshes functionality from original code.
        /// </summary>
        protected override bool IsBlendMesh(int mesh)
        {
            // If mesh is in NoneBlendMeshes set, it should NOT use blending
            if (NoneBlendMeshes.Contains(mesh))
                return false;

            // Otherwise use the base implementation
            return base.IsBlendMesh(mesh);
        }
    }
}
