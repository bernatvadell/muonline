using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Client.Main.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(16, "Elite Skeleton")]
    public class EliteSkeleton : MonsterObject
    {
        private static readonly Dictionary<MonsterActionType, PlayerAction> _actionMap = new()
        {
            { MonsterActionType.Stop1,   PlayerAction.PlayerStopMale },
            { MonsterActionType.Stop2,   PlayerAction.PlayerStopMale },
            { MonsterActionType.Walk,    PlayerAction.PlayerWalkBow },
            { MonsterActionType.Attack1, PlayerAction.PlayerAttackSwordRight1 },
            { MonsterActionType.Attack2, PlayerAction.PlayerAttackSwordRight2 },
            { MonsterActionType.Shock,   PlayerAction.PlayerShock },
            { MonsterActionType.Die,     PlayerAction.PlayerDie1 },
            { MonsterActionType.Appear,  PlayerAction.PlayerComeUp },
            { MonsterActionType.Attack3, PlayerAction.PlayerAttackSwordRight1 },
            { MonsterActionType.Attack4, PlayerAction.PlayerAttackSwordRight2 },
            { MonsterActionType.Run,     PlayerAction.PlayerRun }
        };
        private WeaponObject _rightHandWeapon;
        private WeaponObject _leftHandWeapon;
        public EliteSkeleton()
        {
            Scale = 1.2f; // Set according to C++ Setting_Monster
            RenderShadow = true;
            _rightHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 33
            };
            _leftHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 42 // Assuming 43 is left hand
            };
            Children.Add(_rightHandWeapon);
            Children.Add(_leftHandWeapon);
        }

        public override async Task Load()
        {
            // Uses player model with a specific appearance subtype
            var skeletonModel = await BMDLoader.Instance.Prepare($"Skill/Skeleton03.bmd");
            var playerModel = await BMDLoader.Instance.Prepare("Player/Player.bmd");

            if (skeletonModel != null && playerModel != null)
            {
                int count = Enum.GetValues(typeof(MonsterActionType)).Length;
                var map = _actionMap.ToDictionary(
                    p => (int)p.Key,
                    p => (int)p.Value);

                skeletonModel.Actions = BuildActionArray(playerModel, count, map);
                skeletonModel.Bones = BuildBoneArray(playerModel, count, map);
            }

            Model = skeletonModel;

            var item = ItemDatabase.GetItemDefinition(1, 3); // Tomahawk
            if (item != null)
                _rightHandWeapon.Model = await BMDLoader.Instance.Prepare(item.TexturePath);
            var shield = ItemDatabase.GetItemDefinition(6, 6); // Skull Shield
            if (shield != null)
                _leftHandWeapon.Model = await BMDLoader.Instance.Prepare(shield.TexturePath);

            await base.Load();
            // No specific sounds assigned in C++ for this SubType
        }

        protected override void OnStartWalk()
        {
            base.OnStartWalk();
            var listenerPosition = ((Controls.WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mBone1.wav", Position, listenerPosition);
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mBone2.wav", Position, listenerPosition);
        }

        public override void OnReceiveDamage()
        {
            base.OnReceiveDamage();
            var listenerPosition = ((Controls.WalkableWorldControl)World).Walker.Position;
            SoundController.Instance.PlayBufferWithAttenuation("Sound/mBone2.wav", Position, listenerPosition);
        }
    }
}