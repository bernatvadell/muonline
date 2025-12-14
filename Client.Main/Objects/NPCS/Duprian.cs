using Client.Main.Content;
using Microsoft.Xna.Framework;
using System;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(543, "Gens Duprian Steward")]
    public class Duprian : NPCObject
    {
        private const float ANIMATION_SPEED_MULTIPLIER = 2.0f;
        private readonly Random _rng = Random.Shared;
        private int _loopsTarget;
        private float _idleSecondsRemaining;

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/duprian.bmd");
            await base.Load();

            // Increase animation speed for more dynamic NPC behavior
            if (AnimationSpeed > 0)
            {
                AnimationSpeed *= ANIMATION_SPEED_MULTIPLIER;
            }

            ResetSequence();
        }

        protected override void HandleClick()
        {
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!Visible)
                return;

            if (CurrentAction == 0 && !IsOneShotPlaying)
            {
                _idleSecondsRemaining -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_idleSecondsRemaining <= 0f)
                {
                    PlayRandomGesture();
                }
            }
        }

        private void ResetSequence()
        {
            _loopsTarget = _rng.Next(4, 7); // 4..6
            PlayAction(0);

            if (!_animationController.TryGetActionDurationSeconds(0, out var secondsPerLoop))
            {
                secondsPerLoop = 1.0f;
            }

            _idleSecondsRemaining = secondsPerLoop * _loopsTarget;
        }

        private void PlayRandomGesture()
        {
            ushort action = (ushort)(_rng.Next(2) + 1);
            _animationController.PlayOneShot(
                action,
                returnActionIndex: 0,
                onCompleted: ResetSequence
            );
        }
    }
}
