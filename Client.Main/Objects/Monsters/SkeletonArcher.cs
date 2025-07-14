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
    [NpcInfo(15, "Skeleton Archer")]
    public class SkeletonArcher : MonsterObject
    {
        private static readonly Dictionary<MonsterActionType, PlayerAction> _actionMap = new()
        {
            { MonsterActionType.Stop1,   PlayerAction.PlayerStopBow },
            { MonsterActionType.Stop2,   PlayerAction.PlayerStopBow },
            { MonsterActionType.Walk,    PlayerAction.PlayerWalkBow },
            { MonsterActionType.Attack1, PlayerAction.PlayerAttackBow },
            { MonsterActionType.Attack2, PlayerAction.PlayerAttackBow },
            { MonsterActionType.Shock,   PlayerAction.PlayerShock },
            { MonsterActionType.Die,     PlayerAction.PlayerDie1 },
            { MonsterActionType.Appear,  PlayerAction.PlayerComeUp },
            { MonsterActionType.Attack3, PlayerAction.PlayerAttackBow },
            { MonsterActionType.Attack4, PlayerAction.PlayerAttackBow },
            { MonsterActionType.Run,     PlayerAction.PlayerRun }
        };
        private WeaponObject _rightHandWeapon;
        public SkeletonArcher()
        {
            Scale = 1.1f; // Set according to C++ Setting_Monster
            RenderShadow = true;
            _rightHandWeapon = new WeaponObject
            {
                LinkParentAnimation = false,
                ParentBoneLink = 33
            };
            Children.Add(_rightHandWeapon);
        }

        public override async Task Load()
        {
            // Uses player model with a specific appearance subtype
            var skeletonModel = await BMDLoader.Instance.Prepare($"Skill/Skeleton02.bmd");
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

            var item = ItemDatabase.GetItemDefinition(4, 2); // Elven Bow
            if (item != null)
                _rightHandWeapon.Model = await BMDLoader.Instance.Prepare(item.TexturePath);

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