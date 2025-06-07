using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Linq;

namespace Client.Main.Objects.Monsters
{
    public abstract class MonsterObject : WalkerObject
    {
        // --- Fields ---
        private int _lastActionForIdleSound = -1;
        private bool _isFading = false;
        private float _fadeTimer = 0f;
        private float _fadeDuration = 2f;
        private float _startZ;
        private const float SinkDistance = 20f;

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
        public MonsterObject()
        {
            Interactive = true;
            AnimationSpeed = 4f;
        }

        public void StartDeathFade(float duration = 2f)
        {
            if (_isFading) return;
            _isFading = true;
            _fadeDuration = duration;
            _fadeTimer = 0f;
            _startZ = Position.Z;
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

            base.Update(gameTime);

            if (_isFading)
            {
                RenderShadow = false;
                _fadeTimer += (float)gameTime.ElapsedGameTime.TotalSeconds;
                float progress = MathHelper.Clamp(_fadeTimer / _fadeDuration, 0f, 1f);
                Alpha = MathHelper.Lerp(1f, 0f, progress);
                Position = new Vector3(Position.X, Position.Y, MathHelper.Lerp(_startZ, _startZ - SinkDistance, progress));

                if (progress >= 1f)
                {
                    _isFading = false;
                    World?.RemoveObject(this);
                    Dispose();
                    return;
                }
            }


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
        /// Called when the monster’s death animation starts.
        /// </summary>
        public virtual void OnDeathAnimationStart()
        {
            _lastActionForIdleSound = -1;
            // Override to play death sound.
        }
    }
}
