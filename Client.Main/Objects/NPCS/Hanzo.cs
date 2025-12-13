using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Controls.UI.Game;
using Client.Main.Networking;
using Microsoft.Xna.Framework;
using System;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(251, "Hanzo The Blacksmith")]
    public class Hanzo : NPCObject
    {
        private static readonly ushort[] Sequence = { 0, 1, 2 };

        private int _step;
        private int _loopsDone;
        private int _loopsTarget;
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

            ushort desiredAction = Sequence[_step];
            if (CurrentAction != desiredAction)
                PlayAction(desiredAction);

            bool wrapped = (_lastFrame >= 0) && (CurrentFrame < _lastFrame);

            if (CurrentAction == 0 && !wrapped && _lastFrame < 4 && CurrentFrame >= 4)
            {
                Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
                SoundController.Instance.PlayBufferWithAttenuation(
                    "Sound/nBlackSmith.wav",
                    Position,
                    listenerPosition,
                    maxDistance: 1000f,
                    loop: false
                );
            }

            if (wrapped)
            {
                _loopsDone++;
                if (_loopsDone >= _loopsTarget)
                    AdvanceStep();
            }

            _lastFrame = CurrentFrame;
        }

        private void ResetSequence()
        {
            _step = 0;
            _loopsDone = 0;
            _loopsTarget = _rng.Next(4, 7); // 4..6
            PlayAction(Sequence[_step]);
        }

        private void AdvanceStep()
        {
            _step = (_step + 1) % Sequence.Length;

            _loopsDone = 0;
            _loopsTarget = (_step == 0) ? _rng.Next(4, 7) : 1;

            PlayAction(Sequence[_step]);
        }
    }
}
