using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(3, "Spider")]
    public class Spider : MonsterObject
    {
        public Spider()
        {
            Scale = 0.4f;
            RenderShadow = true;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster10.bmd");
            await base.Load();

            if (Model != null && Model.Actions != null)
            {
                const int MONSTER_ACTION_WALK = (int)MonsterActionType.Walk; // 2
                if (MONSTER_ACTION_WALK < Model.Actions.Length && Model.Actions[MONSTER_ACTION_WALK] != null)
                {
                    Model.Actions[MONSTER_ACTION_WALK].PlaySpeed = 1.2f;
                }
            }
        }

        private void PlaySpiderSound()
        {
            Vector3 listenerPosition = ((Controls.WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mSpider1.wav", Position, listenerPosition);
        }

        protected override void OnIdle()
        {
            base.OnIdle();
            PlaySpiderSound();
        }

        protected override void OnStartWalk()
        {
            base.OnStartWalk();
            // PlaySpiderSound();
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            PlaySpiderSound();
        }

        public override void OnReceiveDamage()
        {
            base.OnReceiveDamage();
            PlaySpiderSound();
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            PlaySpiderSound();
        }
    }
}