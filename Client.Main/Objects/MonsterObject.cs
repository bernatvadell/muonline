using Client.Data.BMD;
using Client.Main.Controllers; // Added for GraphicsManager
using Client.Main.Controls; // Added for Camera (if in controls) or Scene? Camera is typically in Scenes or Controls.
using Client.Main.Graphics; // Added for Camera
using Client.Main.Models;
using Client.Main.Objects.Player;
using Client.Main.Scenes; // Camera is likely here or in Controllers
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics; // Added for SpriteSortMode, etc.
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

        private float _healthFraction = 1f;
        
        // Estimated health tracking because server doesn't always send HP%
        private int _currentHealth;
        private int _approximateMaxHealth = -1; // -1 indicates not yet initialized

        /// <summary>
        /// Minimal health/shield update shim to accept server-provided fractions.
        /// Triggers damage reactions and death fade when health reaches zero.
        /// </summary>
        public void UpdateHealthFractions(float? healthFraction, float? shieldFraction, uint? healthDamage = null, uint? shieldDamage = null)
        {
            // Initialize approximate health on first hit if needed
            // If we don't know the max health, we guess it based on the first damage received.
            // A common assumption for "feeling good" is that a mob takes ~5-10 hits to kill.
            // Let's pick 6 hits as a baseline for visual feedback.
            if (_approximateMaxHealth < 0)
            {
                 if (healthDamage.HasValue && healthDamage.Value > 0)
                 {
                     _approximateMaxHealth = (int)healthDamage.Value * 6;
                     _currentHealth = _approximateMaxHealth;
                 }
                 else
                 {
                     _approximateMaxHealth = 50000; // Fallback if first hit has no damage info
                     _currentHealth = _approximateMaxHealth;
                 }
            }

            if (healthFraction.HasValue)
            {
                _healthFraction = MathHelper.Clamp(healthFraction.Value, 0f, 1f);
                // Sync our estimate to the server's truth if available
                _currentHealth = (int)(_approximateMaxHealth * _healthFraction);
            }
            else if (healthDamage.HasValue && healthDamage.Value > 0)
            {
                // Fallback: Apply damage to our local estimate
                _currentHealth -= (int)healthDamage.Value;
                if (_currentHealth < 0) _currentHealth = 0;
                
                // Update the visual fraction based on our estimate
                _healthFraction = (float)_currentHealth / _approximateMaxHealth;
            }

            float hf = _healthFraction;
            if (hf <= 0f)
            {
                StartDeathFade();
            }
            else
            {
                OnReceiveDamage();
            }

            if (shieldFraction.HasValue)
            {
                OnReceiveDamage();
            }
            // Optional: Handle shield damage similarly if needed, but monsters usually rely on HP
        }

        public override void DrawHoverName() { }

        public override void DrawAfter(GameTime gameTime)
        {
            base.DrawAfter(gameTime);

            if (!Visible || IsDead || OutOfView) return;

            // Only draw overlay if close enough
            float distSq = Vector3.DistanceSquared(Camera.Instance.Position, WorldPosition.Translation);
            if (distSq > 3000 * 3000) return; // Hide if too far

            DrawMonsterOverlay();
        }

        private void DrawMonsterOverlay()
        {
            var font = GraphicsManager.Instance.Font;
            if (font == null) return;

            string name = DisplayName;
            if (string.IsNullOrEmpty(name)) return;

            // Calculate position above head
            // Use BoundingBoxWorld max Z + a little offset
            float headHeight = BoundingBoxWorld.Max.Z - BoundingBoxWorld.Min.Z;
            Vector3 anchor = new Vector3(
                (BoundingBoxWorld.Min.X + BoundingBoxWorld.Max.X) * 0.5f,
                (BoundingBoxWorld.Min.Y + BoundingBoxWorld.Max.Y) * 0.5f,
                BoundingBoxWorld.Max.Z + 20f
            );

            Vector3 screenPos = GraphicsDevice.Viewport.Project(
                anchor,
                Camera.Instance.Projection,
                Camera.Instance.View,
                Matrix.Identity);

            // Check if behind camera
            if (screenPos.Z < 0f || screenPos.Z > 1f) return;

            // Setup scaling
            const float baseScale = 0.4f; // Adjust to match standard MU look
            float scale = baseScale * Constants.RENDER_SCALE;
            
            // Measure text
            Vector2 nameSize = font.MeasureString(name) * scale;
            
            // Bar dimensions
            float barWidth = 60f * Constants.RENDER_SCALE;
            float barHeight = 6f * Constants.RENDER_SCALE;
            float barPadding = 2f * Constants.RENDER_SCALE;

            // Centering
            float centerX = screenPos.X;
            float topY = screenPos.Y;

            // Positions
            Vector2 namePos = new Vector2(centerX - nameSize.X * 0.5f, topY - nameSize.Y - barHeight - barPadding);
            Rectangle barBgRect = new Rectangle(
                (int)(centerX - barWidth * 0.5f),
                (int)(topY - barHeight),
                (int)barWidth,
                (int)barHeight
            );

            // HP Bar Fill
            int fillWidth = (int)(barWidth * _healthFraction);
            Rectangle barFillRect = new Rectangle(
                barBgRect.X,
                barBgRect.Y,
                fillWidth,
                (int)barHeight
            );

            // Draw
            var sb = GraphicsManager.Instance.Sprite;
            // Use existing batch scope or create new one safely? 
            // WorldControl calls DrawAfter inside a loop but setup is done per-list in DrawAfterPass...
            // Actually WorldControl.DrawAfterPass iterates list and calls obj.DrawAfter.
            // It calls SetDepthState beforehand.
            
            // We need to start a SpriteBatch. 
            // Using logic similar to WorldObject.DrawHoverName
            // Fix: explicit cast for int arguments in Rectangle and Draw
             using (new Client.Main.Helpers.SpriteBatchScope(sb, SpriteSortMode.Deferred, Microsoft.Xna.Framework.Graphics.BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone))
            {
                 // Name Background (optional, maybe just shadow/outline?)
                 // Let's draw shadow for text
                 
                 // Text Shadow
                 sb.DrawString(font, name, namePos + Vector2.One, Color.Black, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                 // Text Main
                 sb.DrawString(font, name, namePos, Color.LightYellow, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                 // HP Bar Background (Black/Dark)
                 sb.Draw(GraphicsManager.Instance.Pixel, barBgRect, new Color(0, 0, 0, 180));

                 // HP Bar Fill (Red)
                 sb.Draw(GraphicsManager.Instance.Pixel, barFillRect, Color.Red);
                 
                 // HP Bar Border (Optional, maybe specific style?)
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
                {
                    var srcAction = srcModel.Actions[src];
                    // Clone the action to avoid sharing PlaySpeed with player
                    // Player's dynamic attack speed modifiers should not affect monsters
                    // Use base PlaySpeed values from PlayerObject.InitializeActionSpeeds()
                    float basePlaySpeed = GetPlayerActionBaseSpeed(src);
                    actions[dst] = new BMDTextureAction
                    {
                        NumAnimationKeys = srcAction.NumAnimationKeys,
                        LockPositions = srcAction.LockPositions,
                        Positions = srcAction.Positions, // Array reference is fine, positions don't change
                        PlaySpeed = basePlaySpeed
                    };
                }
            }
            return actions;
        }

        /// <summary>
        /// Returns base PlaySpeed for player actions used by monsters.
        /// Values mirror PlayerObject.InitializeActionSpeeds() and base attack speeds.
        /// </summary>
        private static float GetPlayerActionBaseSpeed(int playerActionIndex)
        {
            var action = (PlayerAction)playerActionIndex;

            return action switch
            {
                // Stop animations
                PlayerAction.PlayerStopMale or PlayerAction.PlayerStopFemale => 0.28f,
                PlayerAction.PlayerStopSword => 0.26f,
                PlayerAction.PlayerStopTwoHandSword => 0.24f,
                PlayerAction.PlayerStopSpear => 0.24f,
                PlayerAction.PlayerStopBow => 0.22f,
                PlayerAction.PlayerStopCrossbow => 0.22f,

                // Walk animations
                PlayerAction.PlayerWalkMale or PlayerAction.PlayerWalkFemale or
                PlayerAction.PlayerWalkSword or PlayerAction.PlayerWalkTwoHandSword or
                PlayerAction.PlayerWalkSpear or PlayerAction.PlayerWalkBow or
                PlayerAction.PlayerWalkCrossbow => 0.38f,

                // Run animations
                PlayerAction.PlayerRun => 0.34f,

                // Attack animations: base 0.25f (without player's attack speed bonus)
                PlayerAction.PlayerAttackFist => 0.25f,
                PlayerAction.PlayerAttackSwordRight1 or PlayerAction.PlayerAttackSwordRight2 or
                PlayerAction.PlayerAttackSwordLeft1 or PlayerAction.PlayerAttackSwordLeft2 or
                PlayerAction.PlayerAttackTwoHandSword1 or PlayerAction.PlayerAttackTwoHandSword2 or
                PlayerAction.PlayerAttackTwoHandSword3 or PlayerAction.PlayerAttackSpear1 or
                PlayerAction.PlayerAttackScythe1 or PlayerAction.PlayerAttackScythe2 or
                PlayerAction.PlayerAttackScythe3 => 0.25f,
                PlayerAction.PlayerAttackBow or PlayerAction.PlayerAttackCrossbow => 0.30f,

                // Shock
                PlayerAction.PlayerShock => 0.40f,

                // Defense
                PlayerAction.PlayerDefense1 => 0.32f,

                // Die
                PlayerAction.PlayerDie1 or PlayerAction.PlayerDie2 => 0.45f,

                // Appear/ComeUp
                PlayerAction.PlayerComeUp => 0.40f,

                // Default fallback for any other action
                _ => 0.30f
            };
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
