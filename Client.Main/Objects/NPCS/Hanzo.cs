using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Controls.UI.Game;
using Client.Main.Models;
using Client.Main.Networking;
using Client.Main.Objects.Effects;
using Microsoft.Xna.Framework;
using System;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(251, "Hanzo The Blacksmith")]
    public class Hanzo : NPCObject
    {
        public override bool CanRepair => true;

        private static readonly ushort[] Sequence = { 0, 1, 2 };

        private int _loopsTarget;
        private float _idleSecondsRemaining;
        private int _lastFrame = -1;

        private readonly Random _rng = Random.Shared;

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("NPC/Smith01.bmd");
            await base.Load();

            ResetSequence();
        }

        protected override void HandleClick()
        {
            var svc = MuGame.Network?.GetCharacterService();
            if (svc != null)
                _ = svc.SendTalkToNpcRequestAsync(NetworkId);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!Visible)
                return;

            if (CurrentAction == 0)
            {
                // On the first frame after switching back to idle, CurrentFrame can still
                // reflect the previous action (one-shot). Prime _lastFrame and skip SFX
                // trigger evaluation to avoid double-playing the sound.
                if (_lastFrame < 0)
                {
                    _lastFrame = CurrentFrame;
                    return;
                }

                bool wrapped = (_lastFrame >= 0) && (CurrentFrame < _lastFrame);
                if (!wrapped && _lastFrame < 4 && CurrentFrame >= 4)
                {
                    Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
                    SoundController.Instance.PlayBufferWithAttenuation(
                        "Sound/nBlackSmith.wav",
                        Position,
                        listenerPosition,
                        maxDistance: 2000f,
                        loop: false
                    );
                }

                if (!wrapped && _lastFrame < 4 && CurrentFrame >= 4)
                {
                    int quantity = _rng.Next(10, 15);
                    for (int i = 0; i < quantity; i++)
                    {
                        var sparkle = ElfBuffSparkle.Rent(Vector3.Zero, 1f, 1.5f, new Vector3(1, 0.5f, 0)); // yellow-red
                        Children.Add(sparkle);
                        sparkle.Position = new Vector3(0, -70, 90);
                        sparkle.Scale *= 3f;
                        sparkle.Alpha *= 2f;
                        if (sparkle.Status == GameControlStatus.NonInitialized) _ = sparkle.Load();
                    }
                }

                _lastFrame = CurrentFrame;
            }
            else
            {
                _lastFrame = -1;
            }

            if (CurrentAction == 0 && !IsOneShotPlaying)
            {
                _idleSecondsRemaining -= (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (_idleSecondsRemaining <= 0f)
                {
                    PlayStepOne();
                }
            }
        }

        private void ResetSequence()
        {
            _loopsTarget = _rng.Next(4, 7); // 4..6

            if (!_animationController.TryGetActionDurationSeconds(Sequence[0], out var secondsPerLoop))
            {
                secondsPerLoop = 1.0f;
            }

            _idleSecondsRemaining = secondsPerLoop * _loopsTarget;
            PlayAction(Sequence[0]);
        }

        private void PlayStepOne()
        {
            _animationController.PlayOneShot(
                Sequence[1],
                returnActionIndex: Sequence[0],
                onCompleted: PlayStepTwo
            );
        }

        private void PlayStepTwo()
        {
            _animationController.PlayOneShot(
                Sequence[2],
                returnActionIndex: Sequence[0],
                onCompleted: ResetSequence
            );
        }
    }
}
