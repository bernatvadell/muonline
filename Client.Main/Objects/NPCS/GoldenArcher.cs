using Client.Main.Content;
using Client.Main.Core.Utilities;
using Client.Main.Models;
using Client.Main.Objects.Monsters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(236, "Golden Archer")]
    public class GoldenArcher : MonsterObject //TODO:
    {
        private static readonly Dictionary<MonsterActionType, PlayerAction> _actionMap = new()
        {
            { MonsterActionType.Stop1,   PlayerAction.StopMale },
            { MonsterActionType.Stop2,   PlayerAction.StopMale },
            { MonsterActionType.Walk,    PlayerAction.WalkMale },
            { MonsterActionType.Attack1, PlayerAction.AttackFist },
            { MonsterActionType.Attack2, PlayerAction.AttackFist },
            { MonsterActionType.Shock,   PlayerAction.PlayerShock },
            { MonsterActionType.Die,     PlayerAction.PlayerDie1 },
            { MonsterActionType.Appear,  PlayerAction.AttackFist },
            { MonsterActionType.Attack3, PlayerAction.AttackFist },
            { MonsterActionType.Attack4, PlayerAction.AttackFist },
            { MonsterActionType.Run,     PlayerAction.Run }
        };
        
        public override async Task Load()
        {
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

            await base.Load();
            CurrentAction = (int)PlayerAction.StopMale;
        }
        // protected override void HandleClick() { }
    }
}
