using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(7, "Giant")]
    public class Giant : MonsterObject
    {
        private ILogger _logger = ModelObject.AppLoggerFactory?.CreateLogger<MonsterObject>();

        public Giant()
        {
            RenderShadow = true;
            Scale = 1.6f;
        }

        public override async Task Load()
        {
            // Model Loading Type: 5 -> File Number: 5 + 1 = 6
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster06.bmd");
            await base.Load(); // Important that base.Load() is called before accessing Model.Actions

            if (Model != null && Model.Actions != null)
            {
                _logger?.LogDebug($"Setting speeds for Giant (Model: {Model.Name})");

                SetActionSpeed(MonsterActionType.Stop1, 0.25f);
                SetActionSpeed(MonsterActionType.Stop2, 0.20f);
                SetActionSpeed(MonsterActionType.Walk, 0.34f);
                SetActionSpeed(MonsterActionType.Attack1, 0.33f);
                SetActionSpeed(MonsterActionType.Attack2, 0.33f);
                SetActionSpeed(MonsterActionType.Shock, 0.5f);
                SetActionSpeed(MonsterActionType.Die, 0.55f);
                // If a Run action exists, set its default speed
                // SetActionSpeed(MonsterActionType.Run, DEFAULT_RUN_SPEED);

                _logger?.LogDebug(" - Giant uses default animation speeds.");

                // --- Step 2: (Optional) Set Loop for Die ---
                int dieActionIndex = (int)MonsterActionType.Die;
                if (IsValidAction(dieActionIndex))
                {
                    // Assuming BMDTextureAction has a bool Loop field
                    // Model.Actions[dieActionIndex].Loop = true;
                    _logger?.LogDebug($" - Action Die ({dieActionIndex}) should be looped (C++ logic).");
                    // Note: Looping the death animation may be undesirable in game logic.
                }
            }
            else
            {
                _logger?.LogDebug($"Error: Model or Actions is null for Giant after Load.");
            }
        }

        // --- Sound handling methods (mapping from C++) ---

        // Sound mapping based on C++ SetMonsterSound(MODEL_MONSTER01 + Type, 25, 26, 27, 28, 29);
        // Sound index 0 -> Sound ID 25 (mGiant1.wav)
        // Sound index 1 -> Sound ID 26 (mGiant2.wav)
        // Sound index 2 -> Sound ID 27 (mGiantAttack1.wav)
        // Sound index 3 -> Sound ID 28 (mGiantAttack2.wav)
        // Sound index 4 -> Sound ID 29 (mGiantDie.wav)

        protected override void OnIdle()
        {
            base.OnIdle();
            // In C++ the monster could play one of two idle sounds (25 or 26)
            // Here we play only the first (mGiant1.wav - ID 25)
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mGiant1.wav", Position, listenerPosition);
            // You can add randomness or a condition to play mGiant2.wav (ID 26)
        }

        // No specific OnStartWalk method in C#, but sounds 25/26 could have been used for walking too

        public override void OnPerformAttack(int attackType = 1) // attackType is not used to select the sound here
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            // In C++ there were two attack sounds (27 and 28)
            // Here we play only the first (mGiantAttack1.wav - ID 27)
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mGiantAttack1.wav", Position, listenerPosition);
            // You could add logic based on attackType to play mGiantAttack2.wav (ID 28)
            // if (attackType == 2) SoundController.Instance.PlayBufferWithAttenuation("Sound/mGiantAttack2.wav", ...);
        }

        public override void OnReceiveDamage()
        {
            base.OnReceiveDamage();
            // C++ does not map a sound for OnReceiveDamage (Shock) specifically for Giant in SetMonsterSound.
            // The hit sound will likely be handled globally or by default behavior.
            // We leave unchanged or add a generic hit sound if needed.
        }

        public override void OnDeathAnimationStart()
        {
            base.OnDeathAnimationStart();
            // Corresponds to Sound index 4 -> Sound ID 29 (mGiantDie.wav)
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mGiantDie.wav", Position, listenerPosition);
        }

        // --- Helper method for setting speed (same as in BudgeDragon) ---
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
                var action = Model.Actions[actionIndex];
                action.PlaySpeed = speed * 2;
                _logger?.LogDebug($" - Set PlaySpeed for action {(MonsterActionType)actionIndex} ({actionIndex}) to {speed}");
            }
            else
            {
                _logger?.LogDebug($" - Warning: Cannot set PlaySpeed for action {(MonsterActionType)actionType} ({actionIndex}). Action does not exist or is null.");
            }
        }
    }
}