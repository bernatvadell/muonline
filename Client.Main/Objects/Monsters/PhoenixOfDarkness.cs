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
    [NpcInfo(77, "Phoenix Of Darkness")]
    public class PhoenixOfDarkness : MonsterObject
    {
        public PhoenixOfDarkness()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster56.bmd");
            await base.Load();

            // Specific PlaySpeed adjustment from C++ OpenMonsterModel
            if (Model?.Actions != null)
            {
                const int MONSTER_ACTION_DIE = (int)MonsterActionType.Die;
                if (MONSTER_ACTION_DIE < Model.Actions.Length && Model.Actions[MONSTER_ACTION_DIE] != null)
                {
                    Model.Actions[MONSTER_ACTION_DIE].PlaySpeed = 0.22f;
                }
            }
        }

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 183, 184, 185, 185, -1);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mPhoenix1.wav", Position, listenerPosition); // Sound 183
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mPhoenixAttack1.wav", Position, listenerPosition); // Sound 185
        }

        // Note: No death sound according to C++ mapping (death sound index was -1)
    }
}
