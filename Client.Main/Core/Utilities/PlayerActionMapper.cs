using Client.Main.Models;                 // PlayerAction, ServerPlayerActionType, WeaponType
using Client.Main.Objects.Player;
using MUnique.OpenMU.Network.Packets;     // CharacterClassNumber
using System.Collections.Generic;

namespace Client.Main.Core.Utilities
{
    public static class PlayerActionMapper
    {

        public static string GetActionName(PlayerAction action)
            => System.Enum.IsDefined(typeof(PlayerAction), action) ? action.ToString() : $"Unknown Action ({(int)action})";

        public static string GetActionName(int actionIndex)
            => GetActionName((PlayerAction)actionIndex);


        #region ------------------------------------------ Server → Client map
        private static readonly Dictionary<ServerPlayerActionType, PlayerAction> ServerToClientMap = new()
        {
            { ServerPlayerActionType.Attack1, PlayerAction.PlayerAttackFist },
            { ServerPlayerActionType.Attack2, PlayerAction.PlayerAttackFist },

            { ServerPlayerActionType.Stand1,  PlayerAction.PlayerStopMale },
            { ServerPlayerActionType.Stand2,  PlayerAction.PlayerStopMale },
            { ServerPlayerActionType.Move1,   PlayerAction.PlayerWalkMale },
            { ServerPlayerActionType.Move2,   PlayerAction.PlayerWalkMale },
            { ServerPlayerActionType.Damage1, PlayerAction.PlayerShock },
            { ServerPlayerActionType.Die1,    PlayerAction.PlayerDie1 },

            { ServerPlayerActionType.Sit,      PlayerAction.PlayerSit1 },
            { ServerPlayerActionType.Healing,  PlayerAction.PlayerHealing1 },
            { ServerPlayerActionType.Pose,     PlayerAction.PlayerPoseMale1 },

            { ServerPlayerActionType.Greeting,    PlayerAction.PlayerGreeting1 },
            { ServerPlayerActionType.Goodbye,     PlayerAction.PlayerGoodbye1 },
            { ServerPlayerActionType.Clap,        PlayerAction.PlayerClap1 },
            { ServerPlayerActionType.Gesture,     PlayerAction.PlayerGesture1 },
            { ServerPlayerActionType.Direction,   PlayerAction.PlayerDirection1 },
            { ServerPlayerActionType.Unknown,     PlayerAction.PlayerUnknown1 },
            { ServerPlayerActionType.Cry,         PlayerAction.PlayerCry1 },
            { ServerPlayerActionType.Cheer,       PlayerAction.PlayerCheer1 },
            { ServerPlayerActionType.Awkward,     PlayerAction.PlayerAwkward1 },
            { ServerPlayerActionType.See,         PlayerAction.PlayerSee1 },
            { ServerPlayerActionType.Win,         PlayerAction.PlayerWin1 },
            { ServerPlayerActionType.Smile,       PlayerAction.PlayerSmile1 },
            { ServerPlayerActionType.Sleep,       PlayerAction.PlayerSleep1 },
            { ServerPlayerActionType.Cold,        PlayerAction.PlayerCold1 },
            { ServerPlayerActionType.Again,       PlayerAction.PlayerAgain1 },
            { ServerPlayerActionType.Respect,     PlayerAction.PlayerRespect1 },
            { ServerPlayerActionType.Salute,      PlayerAction.PlayerSalute1 },
            { ServerPlayerActionType.Rush,        PlayerAction.PlayerRush1 },
            { ServerPlayerActionType.Scissors,    PlayerAction.PlayerScissors },
            { ServerPlayerActionType.Rock,        PlayerAction.PlayerRock },
            { ServerPlayerActionType.Paper,       PlayerAction.PlayerPaper },
            { ServerPlayerActionType.Hustle,      PlayerAction.PlayerHustle },
            { ServerPlayerActionType.Provocation, PlayerAction.PlayerProvocation },
            { ServerPlayerActionType.LookAround,  PlayerAction.PlayerLookAround },
            { ServerPlayerActionType.Cheers,      PlayerAction.PlayerCheers },

            // —— transform rings ————————————————————————
            { ServerPlayerActionType.Jack1,    PlayerAction.PlayerJack1 },
            { ServerPlayerActionType.Jack2,    PlayerAction.PlayerJack2 },

            { ServerPlayerActionType.Santa1_1, PlayerAction.PlayerSanta1 },
            { ServerPlayerActionType.Santa1_2, PlayerAction.PlayerSanta1 },
            { ServerPlayerActionType.Santa1_3, PlayerAction.PlayerSanta1 },
            { ServerPlayerActionType.Santa2_1, PlayerAction.PlayerSanta2 },
            { ServerPlayerActionType.Santa2_2, PlayerAction.PlayerSanta2 },
            { ServerPlayerActionType.Santa2_3, PlayerAction.PlayerSanta2 }
        };
        #endregion

        #region ------------------------------------------ Client → Server map
        private static readonly Dictionary<PlayerAction, ServerPlayerActionType> ClientToServerMap;

        static PlayerActionMapper()
        {
            ClientToServerMap = new Dictionary<PlayerAction, ServerPlayerActionType>();

            foreach (var kvp in ServerToClientMap)
            {
                if (!ClientToServerMap.ContainsKey(kvp.Value))
                    ClientToServerMap.Add(kvp.Value, kvp.Key);
            }

            var attackAnims = new[]
            {
                PlayerAction.PlayerAttackFist,
                PlayerAction.PlayerAttackSwordRight1,
                PlayerAction.PlayerAttackSwordRight2,
                PlayerAction.PlayerAttackSwordLeft1,
                PlayerAction.PlayerAttackSwordLeft2,
                PlayerAction.PlayerAttackTwoHandSword1,
                PlayerAction.PlayerAttackTwoHandSword2,
                PlayerAction.PlayerAttackTwoHandSword3,
                PlayerAction.PlayerAttackSpear1,
                PlayerAction.PlayerAttackScythe1,
                PlayerAction.PlayerAttackScythe2,
                PlayerAction.PlayerAttackScythe3,
                PlayerAction.PlayerAttackBow,
                PlayerAction.PlayerAttackCrossbow,
                PlayerAction.PlayerAttackFlyBow,
                PlayerAction.PlayerAttackFlyCrossbow,
                // PlayerAction.PlayerAttackStrike,
                PlayerAction.PlayerSkillHand1,
                // PlayerAction.PlayerSkillSummon
            };

            foreach (var a in attackAnims)
                ClientToServerMap[a] = ServerPlayerActionType.Attack1;
        }
        #endregion

        #region --------------------------------------------------- helpers
        public static bool IsCharacterFemale(CharacterClassNumber cls) => cls switch
        {
            CharacterClassNumber.FairyElf or
            CharacterClassNumber.MuseElf or
            CharacterClassNumber.HighElf or
            CharacterClassNumber.Summoner or
            CharacterClassNumber.BloodySummoner or
            CharacterClassNumber.DimensionMaster => true,
            _ => false
        };
        #endregion

        #region --------------------------------------------- main → client
        public static PlayerAction GetClientAction(byte serverActionId,
                                                   CharacterClassNumber characterClass)
        {
            if (!System.Enum.IsDefined(typeof(ServerPlayerActionType), serverActionId))
                return PlayerAction.PlayerStopMale;

            var serverAction = (ServerPlayerActionType)serverActionId;

            if (serverAction == ServerPlayerActionType.Attack1 ||
                serverAction == ServerPlayerActionType.Attack2)
            {
                return GetAttackAnimationForClass(characterClass);
            }

            // —— emotes / idle / damage / … ——
            if (ServerToClientMap.TryGetValue(serverAction, out var baseClientAction))
            {
                if (IsCharacterFemale(characterClass))
                {
                    if (baseClientAction == PlayerAction.PlayerSit1)
                        return PlayerAction.PlayerSitFemale1;

                    if (baseClientAction == PlayerAction.PlayerHealing1)
                        return PlayerAction.PlayerHealingFemale1;

                    if (baseClientAction == PlayerAction.PlayerPoseMale1)
                        return PlayerAction.PlayerPoseMale1;

                    bool noFemaleRange = baseClientAction >= PlayerAction.PlayerRespect1 &&
                                         baseClientAction <= PlayerAction.PlayerRush1;

                    if (noFemaleRange &&
                        !(baseClientAction >= PlayerAction.PlayerScissors
                                           && baseClientAction <= PlayerAction.PlayerPaper) &&
                        baseClientAction != PlayerAction.PlayerHustle &&
                        baseClientAction != PlayerAction.PlayerProvocation &&
                        baseClientAction != PlayerAction.PlayerLookAround &&
                        baseClientAction != PlayerAction.PlayerCheers)
                    {
                        return baseClientAction;
                    }

                    PlayerAction femaleCandidate = baseClientAction + 1;
                    if (System.Enum.IsDefined(typeof(PlayerAction), femaleCandidate))
                    {
                        string maleName = System.Enum.GetName(typeof(PlayerAction), baseClientAction);
                        string femaleName = System.Enum.GetName(typeof(PlayerAction), femaleCandidate);

                        if (femaleName == maleName + "Female1" ||
                            (baseClientAction == PlayerAction.PlayerStopMale && femaleCandidate == PlayerAction.PlayerStopFemale) ||
                            (baseClientAction == PlayerAction.PlayerWalkMale && femaleCandidate == PlayerAction.PlayerWalkFemale))
                        {
                            return femaleCandidate;
                        }
                    }
                }

                return baseClientAction;
            }

            return PlayerAction.PlayerStopMale;
        }
        #endregion

        #region --------------------------------------------- main → server
        public static ServerPlayerActionType GetServerActionType(PlayerAction clientAction,
                                                                 CharacterClassNumber characterClass)
        {
            PlayerAction baseMaleAction = clientAction;

            if (IsCharacterFemale(characterClass))
            {
                if (clientAction == PlayerAction.PlayerSitFemale1)
                    baseMaleAction = PlayerAction.PlayerSit1;
                else if (clientAction == PlayerAction.PlayerHealingFemale1)
                    baseMaleAction = PlayerAction.PlayerHealing1;
                else
                {
                    PlayerAction potentialMale = clientAction - 1;
                    if (System.Enum.IsDefined(typeof(PlayerAction), potentialMale))
                    {
                        string maleName = System.Enum.GetName(typeof(PlayerAction), potentialMale);
                        string femaleName = System.Enum.GetName(typeof(PlayerAction), clientAction);

                        if (femaleName == maleName + "Female1" ||
                            (potentialMale == PlayerAction.PlayerStopMale && clientAction == PlayerAction.PlayerStopFemale) ||
                            (potentialMale == PlayerAction.PlayerWalkMale && clientAction == PlayerAction.PlayerWalkFemale))
                        {
                            baseMaleAction = potentialMale;
                        }
                    }
                }
            }

            if (ClientToServerMap.TryGetValue(baseMaleAction, out var serverAction))
                return serverAction;

            return default;
        }
        #endregion

        #region ------------------------------------ attack-animation logic
        private static PlayerAction GetAttackAnimationForClass(CharacterClassNumber cls)
        {
            return GetWeaponTypeForClass(cls) switch
            {
                WeaponType.Sword => PlayerAction.PlayerAttackSwordRight1,
                WeaponType.TwoHandSword => PlayerAction.PlayerAttackTwoHandSword1,
                WeaponType.Spear => PlayerAction.PlayerAttackSpear1,
                WeaponType.Bow => PlayerAction.PlayerAttackBow,
                WeaponType.Crossbow => PlayerAction.PlayerAttackCrossbow,
                WeaponType.Staff => PlayerAction.PlayerSkillHand1,
                // WeaponType.Scepter => PlayerAction.PlayerAttackStrike, //TODO:
                WeaponType.Scythe => PlayerAction.PlayerAttackScythe1,
                // WeaponType.Book => PlayerAction.PlayerSkillSummon, //TODO:
                WeaponType.Fist or _ => PlayerAction.PlayerAttackFist
            };
        }

        private static WeaponType GetWeaponTypeForClass(CharacterClassNumber cls) => cls switch
        {
            CharacterClassNumber.DarkWizard or
            CharacterClassNumber.SoulMaster or
            CharacterClassNumber.GrandMaster => WeaponType.Staff,

            CharacterClassNumber.FairyElf or
            CharacterClassNumber.MuseElf or
            CharacterClassNumber.HighElf => WeaponType.Bow,

            CharacterClassNumber.DarkKnight or
            CharacterClassNumber.BladeKnight or
            CharacterClassNumber.BladeMaster => WeaponType.Sword,

            CharacterClassNumber.MagicGladiator or
            CharacterClassNumber.DuelMaster => WeaponType.Sword,

            CharacterClassNumber.DarkLord or
            CharacterClassNumber.LordEmperor => WeaponType.Scepter,

            CharacterClassNumber.Summoner or
            CharacterClassNumber.BloodySummoner or
            CharacterClassNumber.DimensionMaster => WeaponType.Book,

            CharacterClassNumber.RageFighter or
            CharacterClassNumber.FistMaster => WeaponType.Fist,

            _ => WeaponType.None
        };
        #endregion
    }
}
