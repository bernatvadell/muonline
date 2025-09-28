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
    [NpcInfo(69, "Alquamos")]
    public class Alquamos : MonsterObject
    {
        public Alquamos()
        {
            Scale = 1.0f;
            BlendMesh = -2; // Use full blending like other semi-transparent monsters
            BlendMeshLight = 0.7f; // Reduced light for more subtle blending
            Alpha = 0.85f; // Slightly transparent for better blending with environment
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster51.bmd");
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

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 175, 175, 175, 175, 176);
        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            // Note: C++ had commented out idle sound, but we'll use attack sound for idle as per mapping
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mAlquamosAttack1.wav", Position, listenerPosition); // Sound 175
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mAlquamosAttack1.wav", Position, listenerPosition); // Sound 175
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mAlquamosDie.wav", Position, listenerPosition); // Sound 176
        }
    }
}
