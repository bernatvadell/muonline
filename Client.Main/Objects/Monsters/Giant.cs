using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Client.Main.Core.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(7, "Giant")]
    public class Giant : MonsterObject
    {
        private WeaponObject _rightHandWeapon;
        private WeaponObject _leftHandWeapon;
        private new ILogger _logger = ModelObject.AppLoggerFactory?.CreateLogger<MonsterObject>();

        public Giant()
        {
            RenderShadow = true;
            Scale = 1.6f;
            _rightHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 41
            };
            _leftHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 32
            };
            Children.Add(_rightHandWeapon);
            Children.Add(_leftHandWeapon);
        }

        public override async Task Load()
        {
            // Model Loading Type: 5 -> File Number: 5 + 1 = 6
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster06.bmd");
            var item = ItemDatabase.GetItemDefinition(1, 2); // Double Axe
            if (item != null)
            {
                _rightHandWeapon.Model = await BMDLoader.Instance.Prepare(item.TexturePath);
                _leftHandWeapon.Model = await BMDLoader.Instance.Prepare(item.TexturePath);
            }
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
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            // Play one of the idle sounds (index 0 or 1)
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mGiant1.wav", Position, listenerPosition); // Index 0 -> Sound 25
            // SoundController.Instance.PlayBufferWithAttenuation("Sound/mGiant2.wav", Position, listenerPosition); // Index 1 -> Sound 26
        }

        public override void OnPerformAttack(int attackType = 1)
        {
            base.OnPerformAttack(attackType);
            Vector3 listenerPosition = ((WalkableWorldControl)World).Walker.Position;
            // Play one of the attack sounds (index 2 or 3)
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mGiantAttack1.wav", Position, listenerPosition); // Index 2 -> Sound 27
            // SoundController.Instance.PlayBufferWithAttenuation("Sound/mGiantAttack2.wav", Position, listenerPosition); // Index 3 -> Sound 28
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
        private new bool IsValidAction(int actionIndex)
        {
            return Model != null
                && Model.Actions != null
                && actionIndex >= 0
                && actionIndex < Model.Actions.Length
                && Model.Actions[actionIndex] != null;
        }

        private new void SetActionSpeed(MonsterActionType actionType, float speed)
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