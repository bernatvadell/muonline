using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(150, "Bali")]
    public class Bali : MonsterObject
    {
        public Bali()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster33.bmd");
            await base.Load();

            // Specific PlaySpeed adjustments from C++ OpenMonsterModel
            if (Model?.Actions != null)
            {
                const int ATTACK3_INDEX = (int)MonsterActionType.Attack3;
                const int ATTACK4_INDEX = (int)MonsterActionType.Attack4;
                const int APPEAR_INDEX = (int)MonsterActionType.Appear;
                const int RUN_INDEX = (int)MonsterActionType.Run;

                if (ATTACK3_INDEX < Model.Actions.Length && Model.Actions[ATTACK3_INDEX] != null)
                    Model.Actions[ATTACK3_INDEX].PlaySpeed = 0.4f;
                if (ATTACK4_INDEX < Model.Actions.Length && Model.Actions[ATTACK4_INDEX] != null)
                    Model.Actions[ATTACK4_INDEX].PlaySpeed = 0.4f;
                if (APPEAR_INDEX < Model.Actions.Length && Model.Actions[APPEAR_INDEX] != null)
                    Model.Actions[APPEAR_INDEX].PlaySpeed = 0.4f;
                if (RUN_INDEX < Model.Actions.Length && Model.Actions[RUN_INDEX] != null)
                    Model.Actions[RUN_INDEX].PlaySpeed = 0.4f;
            }
            // C++: b->BoneHead = 6;
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 126, 127, 128, 129, 127);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mBali1.wav", Position, listenerPosition); // Sound 126
            // Consider adding logic for Sound 127 (mBali2.wav) if desired
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            if (attackType == 1)
                SoundController.Instance.PlayBufferWithAttenuation("Sound/mBaliAttack1.wav", Position, listenerPosition); // Sound 128
            else
                SoundController.Instance.PlayBufferWithAttenuation("Sound/mBaliAttack2.wav", Position, listenerPosition); // Sound 129
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mBali2.wav", Position, listenerPosition); // Sound 127
        }
    }
}
