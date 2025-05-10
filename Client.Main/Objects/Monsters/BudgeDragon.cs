using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(2, "Budge Dragon")]
    public class BudgeDragon : MonsterObject
    {
        public BudgeDragon()
        {
            RenderShadow = true;
            Scale = 0.5f;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster03.bmd");
            Position = new Vector3(Position.X, Position.Y, Position.Z - 40f);
            await base.Load();

            if (Model != null && Model.Actions != null)
            {
                SetActionSpeed(MonsterActionType.Stop1, 0.25f);
                SetActionSpeed(MonsterActionType.Stop2, 0.20f);
                SetActionSpeed(MonsterActionType.Walk, 0.34f);
                SetActionSpeed(MonsterActionType.Attack1, 0.33f);
                SetActionSpeed(MonsterActionType.Attack2, 0.33f);
                SetActionSpeed(MonsterActionType.Shock, 0.5f);
                SetActionSpeed(MonsterActionType.Die, 0.55f);

                System.Diagnostics.Debug.WriteLine("Walk speed for BudgeDragon...");
                SetActionSpeed(MonsterActionType.Walk, 0.7f);

                int dieActionIndex = (int)MonsterActionType.Die;
                if (IsValidAction(dieActionIndex))
                {
                    System.Diagnostics.Debug.WriteLine($"Action Die ({dieActionIndex}) should be looped (C++ logic).");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"Error: Model or Actions is null for BudgeDragon after Load.");
            }
        }

        protected override void OnIdle()
        {
            base.OnIdle();
            Vector3 listenerPosition = ((Controls.WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mBudge1.wav", Position, listenerPosition);
        }

        protected override void OnStartWalk()
        {
            base.OnStartWalk();
            Vector3 listenerPosition = ((Controls.WalkableWorldControl)World).Walker.Position;
            // SoundController.Instance.PlayBufferWithAttenuation("Sound/mBudge1.wav", Position, listenerPosition);
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((Controls.WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mBudgeAttack1.wav", Position, listenerPosition);
        }

        public override void OnReceiveDamage()
        {
            base.OnReceiveDamage();
            Vector3 listenerPosition = ((Controls.WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mBudgeAttack1.wav", Position, listenerPosition); // In C++ it was the same sound as the attack
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            Vector3 listenerPosition = ((Controls.WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mBudgeDie.wav", Position, listenerPosition);
        }

        // --- Helper method for setting speed ---
        private bool IsValidAction(int actionIndex)
        {
            return Model != null
                && Model.Actions != null
                && actionIndex >= 0
                && actionIndex < Model.Actions.Length
                && Model.Actions[actionIndex] != null;
        }

        private void SetActionSpeed(MonsterActionType actionType, float speed)
        {
            int actionIndex = (int)actionType;
            if (IsValidAction(actionIndex))
            {
                // Check whether PlaySpeed actually exists in BMDTextureAction
                var action = Model.Actions[actionIndex];
                // Assuming that BMDTextureAction in C# is a class/struct with a PlaySpeed field
                // (as implied by the provided BMDTextureAction.cs snippet)
                action.PlaySpeed = speed * 2;
                System.Diagnostics.Debug.WriteLine($" - Set PlaySpeed for action {(MonsterActionType)actionIndex} ({actionIndex}) to {speed}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($" - Warning: Cannot set PlaySpeed for action {(MonsterActionType)actionType} ({actionIndex}). Action does not exist or is null.");
            }
        }
    }
}
