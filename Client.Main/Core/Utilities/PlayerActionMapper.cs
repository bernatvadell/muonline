using Client.Main.Models;                 // PlayerAction, ServerPlayerActionType, WeaponType
using Client.Main.Objects.Player;
using MUnique.OpenMU.Network.Packets;     // CharacterClassNumber
using System.Collections.Generic;

namespace Client.Main.Core.Utilities
{
    public static class PlayerActionMapper
    {
        #region ------------------------------------------------- Action → Name
        public static readonly Dictionary<PlayerAction, string> ActionNames = new()
        {
            // ––– system/internal –––
            { PlayerAction.Set, "Set (Internal?)" },

            // ––– idle ----------------------------------------------------
            { PlayerAction.StopMale, "Stop/Idle (Male, Unarmed)" },
            { PlayerAction.StopFemale, "Stop/Idle (Female, Unarmed)" },
            { PlayerAction.StopSummoner, "Stop/Idle (Summoner/Book?)" },
            { PlayerAction.StopSword, "Stop/Idle (1H Sword)" },
            { PlayerAction.StopTwoHandSword, "Stop/Idle (2H Sword)" },
            { PlayerAction.StopSpear, "Stop/Idle (Spear)" },
            { PlayerAction.StopScythe, "Stop/Idle (Scythe)" },
            { PlayerAction.StopBow, "Stop/Idle (Bow)" },
            { PlayerAction.StopCrossbow, "Stop/Idle (Crossbow)" },
            { PlayerAction.StopWand, "Stop/Idle (Wand/Staff)" },

            { PlayerAction.StopFlying, "Stop/Idle (Flying)" },
            { PlayerAction.StopFlyCrossbow, "Stop/Idle (Flying with Crossbow)" },
            { PlayerAction.StopRide, "Stop/Idle (Riding)" },
            { PlayerAction.StopRideWeapon, "Stop/Idle (Riding with Weapon)" },

            // ––– walk ----------------------------------------------------
            { PlayerAction.WalkMale, "Walk (Male, Unarmed)" },
            { PlayerAction.WalkFemale, "Walk (Female, Unarmed)" },
            { PlayerAction.WalkSword, "Walk (1H Sword)" },
            { PlayerAction.WalkTwoHandSword, "Walk (2H Sword)" },
            { PlayerAction.WalkSpear, "Walk (Spear)" },
            { PlayerAction.WalkScythe, "Walk (Scythe)" },
            { PlayerAction.WalkBow, "Walk (Bow)" },
            { PlayerAction.WalkCrossbow, "Walk (Crossbow)" },
            { PlayerAction.WalkWand, "Walk (Wand/Staff)" },
            { PlayerAction.WalkSwim, "Swim (Walk)" },

            // ––– run / fly / ride ----------------------------------------
            { PlayerAction.Run, "Run (Unarmed)" },
            { PlayerAction.RunSword, "Run (1H Sword)" },
            { PlayerAction.RunTwoSword, "Run (Dual Wield / 1H Sword Alt?)" },
            { PlayerAction.RunTwoHandSword, "Run (2H Sword)" },
            { PlayerAction.RunSpear, "Run (Spear)" },
            { PlayerAction.RunBow, "Run (Bow)" },
            { PlayerAction.RunCrossbow, "Run (Crossbow)" },
            { PlayerAction.RunWand, "Run (Wand/Staff)" },
            { PlayerAction.RunSwim, "Swim (Run/Dash)" },

            { PlayerAction.Fly, "Fly Forward" },
            { PlayerAction.FlyCrossbow, "Fly Forward (Crossbow)" },

            { PlayerAction.RunRide, "Ride Forward" },
            { PlayerAction.RunRideWeapon, "Ride Forward (Weapon)" },

            { PlayerAction.PlayerFlyRide, "Fly (Riding)" },
            { PlayerAction.PlayerFlyRideWeapon, "Fly (Riding with Weapon)" },

            // ––– generic combat ------------------------------------------
            { PlayerAction.AttackFist, "Attack (Fist)" },
            { PlayerAction.PlayerShock, "Get Hit / Shock" },
            { PlayerAction.PlayerDie1, "Die 1" },
            { PlayerAction.PlayerDie2, "Die 2" },

            // ––– melee sword ---------------------------------------------
            { PlayerAction.PlayerAttackSwordRight1, "Attack (1H Sword Right 1)" },
            { PlayerAction.PlayerAttackSwordRight2, "Attack (1H Sword Right 2 / Wheel?)" },
            { PlayerAction.PlayerAttackSwordLeft1,  "Attack (1H Sword Left 1 / Fury Strike?)" },
            { PlayerAction.PlayerAttackSwordLeft2,  "Attack (1H Sword Left 2 / Death Stab?)" },

            // ––– 2-hand sword --------------------------------------------
            { PlayerAction.PlayerAttackTwoHandSword1, "Attack (2H Sword 1 / Rush?)" },
            { PlayerAction.PlayerAttackTwoHandSword2, "Attack (2H Sword 2 / Javelin?)" },
            { PlayerAction.PlayerAttackTwoHandSword3, "Attack (2H Sword 3 / Deep Impact?)" },

            // ––– spear / scythe ------------------------------------------
            { PlayerAction.PlayerAttackSpear1,  "Attack (Spear 1)" },
            { PlayerAction.PlayerAttackScythe1, "Attack (Scythe 1)" },
            { PlayerAction.PlayerAttackScythe2, "Attack (Scythe 2)" },
            { PlayerAction.PlayerAttackScythe3, "Attack (Scythe 3)" },

            // ––– bow / crossbow ------------------------------------------
            { PlayerAction.PlayerAttackBow,         "Attack (Bow)" },
            { PlayerAction.PlayerAttackCrossbow,    "Attack (Crossbow)" },
            { PlayerAction.PlayerAttackFlyBow,      "Attack (Flying Bow)" },
            { PlayerAction.PlayerAttackFlyCrossbow, "Attack (Flying Crossbow)" },

            // ––– magic staff / etc. --------------------------------------
            { PlayerAction.PlayerSkillHand1, "Skill (Hand 1 - Magic?)" },
            { PlayerAction.PlayerSkillHand2, "Skill (Hand 2 - Magic?)" },

            // ––– strike / DL scepter -------------------------------------
            { PlayerAction.PlayerAttackStrike, "Attack (Strike - DL?)" },

            // ––– summon / book -------------------------------------------
            { PlayerAction.PlayerSkillSummon, "Skill (Summon)" },

            // ––– (set of hundreds of further skills / emotes) -------------
            { PlayerAction.PlayerAttackTeleport, "Attack (Teleport - DW?)" },
            { PlayerAction.PlayerAttackRideScythe, "Attack (Riding Scythe)" },
            { PlayerAction.PlayerAttackRideBow, "Attack (Riding Bow)" },
            { PlayerAction.PlayerAttackRideCrossbow, "Attack (Riding Crossbow)" },
            { PlayerAction.PlayerAttackRideSword, "Attack (Riding Sword)" },
            { PlayerAction.PlayerAttackRideTwoHandSword, "Attack (Riding 2H Sword)" },
            { PlayerAction.PlayerAttackRideSpear, "Attack (Riding Spear)" },
            { PlayerAction.PlayerAttackRideStrike, "Attack (Riding Strike - DL?)" },
            { PlayerAction.PlayerAttackRideTeleport, "Attack (Riding Teleport - DW?)" },
            { PlayerAction.PlayerAttackRideHorseSword, "Attack (Riding Horse with Sword)" },
            { PlayerAction.PlayerAttackRideAttackFlash, "Attack (Riding Flash - MG?)" },
            { PlayerAction.PlayerAttackRideAttackMagic, "Attack (Riding Magic - DW/Elf?)" },
            { PlayerAction.PlayerAttackDarkhorse, "Attack (Dark Horse Pet)" },
            { PlayerAction.PlayerIdle1Darkhorse, "Idle 1 (Dark Horse Pet)" },
            { PlayerAction.PlayerIdle2Darkhorse, "Idle 2 (Dark Horse Pet)" },
            { PlayerAction.PlayerFenrirAttack, "Attack (Fenrir - Basic?)" },
            { PlayerAction.PlayerFenrirAttackDarklordAqua, "Attack (Fenrir - DL Aqua Beam?)" },
            { PlayerAction.PlayerFenrirAttackDarklordSword, "Attack (Fenrir - DL Sword?)" },
            { PlayerAction.PlayerFenrirAttackDarklordTeleport, "Attack (Fenrir - DL Teleport?)" },
            { PlayerAction.PlayerFenrirAttackDarklordFlash, "Attack (Fenrir - DL Flash?)" },
            { PlayerAction.PlayerFenrirAttackTwoSword, "Attack (Fenrir - Dual Wield?)" },
            { PlayerAction.PlayerFenrirAttackMagic, "Attack (Fenrir - Magic?)" },
            { PlayerAction.PlayerFenrirAttackCrossbow, "Attack (Fenrir - Crossbow?)" },
            { PlayerAction.PlayerFenrirAttackSpear, "Attack (Fenrir - Spear?)" },
            { PlayerAction.PlayerFenrirAttackOneSword, "Attack (Fenrir - 1H Sword?)" },
            { PlayerAction.PlayerFenrirAttackBow, "Attack (Fenrir - Bow?)" },
            { PlayerAction.PlayerAttackOneFlash, "Attack (One Flash - MG?)" },
            { PlayerAction.PlayerAttackRush, "Attack (Rush - DK?)" },
            { PlayerAction.PlayerAttackDeathCannon, "Attack (Death Cannon - MG?)" },
            { PlayerAction.PlayerAttackRemoval, "Attack (Removal - Elf?)" },
            { PlayerAction.PlayerAttackStun, "Attack (Stun - DK?)" },
            { PlayerAction.PlayerAttackSkillSword1, "Skill Attack (Sword 1)" },
            { PlayerAction.PlayerAttackSkillSword2, "Skill Attack (Sword 2)" },
            { PlayerAction.PlayerAttackSkillSword3, "Skill Attack (Sword 3)" },
            { PlayerAction.PlayerAttackSkillSword4, "Skill Attack (Sword 4)" },
            { PlayerAction.PlayerAttackSkillSword5, "Skill Attack (Sword 5)" },
            { PlayerAction.PlayerAttackSkillWheel, "Skill Attack (Wheel)" },
            { PlayerAction.PlayerAttackSkillFuryStrike, "Skill Attack (Fury Strike)" },
            { PlayerAction.PlayerAttackSkillSpear, "Skill Attack (Spear)" },
            { PlayerAction.PlayerAttackDeathstab, "Attack (Death Stab)" },
            { PlayerAction.BlowSkill, "Skill (Power Strike?)" },
            { PlayerAction.TwistingSlashSkill, "Skill (Twisting Slash)" },
            { PlayerAction.RegularBlowSkill, "Skill (Melee Blow?)" },
            { PlayerAction.GreaterFortitudeSkill, "Skill (Greater Fortitude/Swell Life)" },
            { PlayerAction.PlayerSkillVitality, "Skill (Vitality / Inner Strength)" },

            // ––– jack, santa, emotes -------------------------------------
            { PlayerAction.PlayerGreeting1, "Emote (Greeting 1)" },
            { PlayerAction.PlayerGreetingFemale1, "Emote (Greeting Female 1)" },
            { PlayerAction.PlayerGoodbye1, "Emote (Goodbye 1)" },
            { PlayerAction.PlayerGoodbyeFemale1, "Emote (Goodbye Female 1)" },
            { PlayerAction.PlayerClap1, "Emote (Clap 1)" },
            { PlayerAction.PlayerClapFemale1, "Emote (Clap Female 1)" },
            { PlayerAction.PlayerCheer1, "Emote (Cheer 1)" },
            { PlayerAction.PlayerCheerFemale1, "Emote (Cheer Female 1)" },
            { PlayerAction.PlayerDirection1, "Emote (Direction 1)" },
            { PlayerAction.PlayerDirectionFemale1, "Emote (Direction Female 1)" },
            { PlayerAction.PlayerGesture1, "Emote (Gesture 1)" },
            { PlayerAction.PlayerGestureFemale1, "Emote (Gesture Female 1)" },
            { PlayerAction.PlayerUnknown1, "Emote (Shrug? 1)" },
            // { PlayerAction.PlayerUnknownFemale1, "Emote (Shrug? Female 1)" },
            // { PlayerAction.PlayerCry1, "Emote (Cry 1)" },
            // { PlayerAction.PlayerCryFemale1, "Emote (Cry Female 1)" },
            // { PlayerAction.PlayerAwkward1, "Emote (Awkward 1)" },
            // { PlayerAction.PlayerAwkwardFemale1, "Emote (Awkward Female 1)" },
            { PlayerAction.PlayerSee1, "Emote (Look Around? 1)" },
            { PlayerAction.PlayerSeeFemale1, "Emote (Look Around? Female 1)" },
            { PlayerAction.PlayerWin1, "Emote (Win 1)" },
            { PlayerAction.PlayerWinFemale1, "Emote (Win Female 1)" },
            { PlayerAction.PlayerSmile1, "Emote (Smile 1)" },
            { PlayerAction.PlayerSmileFemale1, "Emote (Smile Female 1)" },
            { PlayerAction.PlayerSleep1, "Emote (Sleep 1)" },
            { PlayerAction.PlayerSleepFemale1, "Emote (Sleep Female 1)" },
            { PlayerAction.PlayerCold1, "Emote (Cold 1)" },
            { PlayerAction.PlayerColdFemale1, "Emote (Cold Female 1)" },
            { PlayerAction.PlayerAgain1, "Emote (Come on? 1)" },
            { PlayerAction.PlayerAgainFemale1, "Emote (Come on? Female 1)" },
            { PlayerAction.PlayerRespect1, "Emote (Respect 1)" },
            { PlayerAction.PlayerSalute1, "Emote (Salute 1)" },
            { PlayerAction.PlayerScissors, "Emote (Scissors)" },
            { PlayerAction.PlayerRock, "Emote (Rock)" },
            { PlayerAction.PlayerPaper, "Emote (Paper)" },
            { PlayerAction.PlayerHustle, "Emote (Hustle Dance?)" },
            { PlayerAction.PlayerProvocation, "Emote (Provocation)" },
            { PlayerAction.PlayerLookAround, "Emote (Look Around)" },
            { PlayerAction.PlayerCheers, "Emote (Cheers)" },
            { PlayerAction.PlayerKoreaHandclap, "Emote (Korea Handclap Dance?)" },
            { PlayerAction.PlayerPointDance, "Emote (Point Dance?)" },
            { PlayerAction.PlayerRush1, "Emote (Rush / Charge?)" },

            { PlayerAction.PlayerJack1, "Jack O'Lantern Emote 1" },
            { PlayerAction.PlayerJack2, "Jack O'Lantern Emote 2" },
            { PlayerAction.PlayerSanta1, "Santa Emote 1" },
            { PlayerAction.PlayerSanta2, "Santa Emote 2" },

            { PlayerAction.PlayerStandingRest, "Rest (Standing/Leaning)" },
            { PlayerAction.PlayerFlyingRest,   "Rest (Flying)" },
            { PlayerAction.PlayerSit1, "Sit Pose 1" },
            { PlayerAction.PlayerSitFemale1, "Sit Female Pose 1" },
            { PlayerAction.PlayerSit2, "Sit Pose 2" },
            { PlayerAction.PlayerSit3, "Sit Pose 3" },
            { PlayerAction.PlayerSit4, "Sit Pose 4" },
            { PlayerAction.PlayerSit5, "Sit Pose 5" },
            { PlayerAction.PlayerSit6, "Sit Pose 6" },

            { PlayerAction.PlayerHealing1, "Healing 1" },
            { PlayerAction.PlayerHealingFemale1, "Healing Female 1" },

            { PlayerAction.PlayerPose1, "Pose 1" },
            { PlayerAction.PlayerPoseFemale1, "Pose Female 1" },

            { PlayerAction.PlayerComeUp, "Get Up" },

            { PlayerAction.PlayerChangeUp, "3rd Class Change Up Effect?" },
            { PlayerAction.PlayerRecoverSkill, "Skill (Recovery?)" },

            // ––– rage-fenrir / uni (przykładowe) --------------------------
            { PlayerAction.PlayerRageUniAttack, "Attack (Unicorn - Rage Fighter?)" },
            { PlayerAction.PlayerRageUniAttackOneRight, "Attack (Unicorn - Rage Fighter 1H Right?)" },
            { PlayerAction.PlayerRageUniRun, "Run (Unicorn - Rage Fighter?)" },
            { PlayerAction.PlayerRageUniRunOneRight, "Run (Unicorn - Rage Fighter 1H Right?)" },
            { PlayerAction.PlayerRageUniStopOneRight, "Stop/Idle (Unicorn - Rage Fighter 1H Right?)" },

            { PlayerAction.PlayerRageFenrir, "Stop/Idle (Fenrir - Rage Fighter?)" },
            { PlayerAction.PlayerRageFenrirTwoSword, "Stop/Idle (Fenrir - Rage Fighter Dual Wield?)" },
            { PlayerAction.PlayerRageFenrirOneRight, "Stop/Idle (Fenrir - Rage Fighter 1H Right?)" },
            { PlayerAction.PlayerRageFenrirOneLeft, "Stop/Idle (Fenrir - Rage Fighter 1H Left?)" },
            { PlayerAction.PlayerRageFenrirWalk, "Walk (Fenrir - Rage Fighter?)" },
            { PlayerAction.PlayerRageFenrirWalkOneRight, "Walk (Fenrir - Rage Fighter 1H Right?)" },
            { PlayerAction.PlayerRageFenrirWalkOneLeft, "Walk (Fenrir - Rage Fighter 1H Left?)" },
            { PlayerAction.PlayerRageFenrirWalkTwoSword, "Walk (Fenrir - Rage Fighter Dual Wield?)" },
            { PlayerAction.PlayerRageFenrirRun, "Run (Fenrir - Rage Fighter?)" },
            { PlayerAction.PlayerRageFenrirRunTwoSword, "Run (Fenrir - Rage Fighter Dual Wield?)" },
            { PlayerAction.PlayerRageFenrirRunOneRight, "Run (Fenrir - Rage Fighter 1H Right?)" },
            { PlayerAction.PlayerRageFenrirRunOneLeft, "Run (Fenrir - Rage Fighter 1H Left?)" },
            { PlayerAction.PlayerRageFenrirStand, "Stand (Fenrir - Rage Fighter?)" },
            { PlayerAction.PlayerRageFenrirStandTwoSword, "Stand (Fenrir - Rage Fighter Dual Wield?)" },
            { PlayerAction.PlayerRageFenrirStandOneRight, "Stand (Fenrir - Rage Fighter 1H Right?)" },
            { PlayerAction.PlayerRageFenrirStandOneLeft, "Stand (Fenrir - Rage Fighter 1H Left?)" },
            { PlayerAction.PlayerRageFenrirDamage, "Get Hit (Fenrir - Rage Fighter?)" },
            { PlayerAction.PlayerRageFenrirDamageTwoSword, "Get Hit (Fenrir - Rage Fighter Dual Wield?)" },
            { PlayerAction.PlayerRageFenrirDamageOneRight, "Get Hit (Fenrir - Rage Fighter 1H Right?)" },
            { PlayerAction.PlayerRageFenrirDamageOneLeft, "Get Hit (Fenrir - Rage Fighter 1H Left?)" },
            { PlayerAction.PlayerRageFenrirAttackRight, "Attack (Fenrir - Rage Fighter Right?)" },

            { PlayerAction.PlayerStopRagefighter, "Stop/Idle (Rage Fighter)" }
        };

        public static string GetActionName(PlayerAction action)
            => ActionNames.TryGetValue(action, out var n) ? n : $"Unknown Action ({(int)action})";

        public static string GetActionName(int actionIndex)
            => GetActionName((PlayerAction)actionIndex);
        #endregion

        #region ------------------------------------------ Server → Client map
        private static readonly Dictionary<ServerPlayerActionType, PlayerAction> ServerToClientMap = new()
        {
            { ServerPlayerActionType.Attack1, PlayerAction.AttackFist },
            { ServerPlayerActionType.Attack2, PlayerAction.AttackFist },

            { ServerPlayerActionType.Stand1,  PlayerAction.StopMale },
            { ServerPlayerActionType.Stand2,  PlayerAction.StopMale },
            { ServerPlayerActionType.Move1,   PlayerAction.WalkMale },
            { ServerPlayerActionType.Move2,   PlayerAction.WalkMale },
            { ServerPlayerActionType.Damage1, PlayerAction.PlayerShock },
            { ServerPlayerActionType.Die1,    PlayerAction.PlayerDie1 },

            { ServerPlayerActionType.Sit,      PlayerAction.PlayerSit1 },
            { ServerPlayerActionType.Healing,  PlayerAction.PlayerHealing1 },
            { ServerPlayerActionType.Pose,     PlayerAction.PlayerStandingRest },

            { ServerPlayerActionType.Greeting,    PlayerAction.PlayerGreeting1 },
            { ServerPlayerActionType.Goodbye,     PlayerAction.PlayerGoodbye1 },
            { ServerPlayerActionType.Clap,        PlayerAction.PlayerClap1 },
            { ServerPlayerActionType.Gesture,     PlayerAction.PlayerGesture1 },
            { ServerPlayerActionType.Direction,   PlayerAction.PlayerDirection1 },
            { ServerPlayerActionType.Unknown,     PlayerAction.PlayerUnknown1 },
            // { ServerPlayerActionType.Cry,         PlayerAction.PlayerCry1 },
            { ServerPlayerActionType.Cheer,       PlayerAction.PlayerCheer1 },
            // { ServerPlayerActionType.Awkward,     PlayerAction.PlayerAwkward1 },
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
        /// <summary> Odwrócone mapowanie + ręczne wpisy dla wszystkich animacji ataku. </summary>
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
                PlayerAction.AttackFist,
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
                PlayerAction.PlayerAttackStrike,
                PlayerAction.PlayerSkillHand1,
                PlayerAction.PlayerSkillSummon
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
                return PlayerAction.StopMale;

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

                    if (baseClientAction == PlayerAction.PlayerStandingRest)
                        return PlayerAction.PlayerStandingRest;

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
                            (baseClientAction == PlayerAction.StopMale && femaleCandidate == PlayerAction.StopFemale) ||
                            (baseClientAction == PlayerAction.WalkMale && femaleCandidate == PlayerAction.WalkFemale))
                        {
                            return femaleCandidate;
                        }
                    }
                }

                return baseClientAction;
            }

            return PlayerAction.StopMale;
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
                            (potentialMale == PlayerAction.StopMale && clientAction == PlayerAction.StopFemale) ||
                            (potentialMale == PlayerAction.WalkMale && clientAction == PlayerAction.WalkFemale))
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
                WeaponType.Sword => PlayerAction.AttackFist,
                WeaponType.TwoHandSword => PlayerAction.PlayerAttackTwoHandSword1,
                WeaponType.Spear => PlayerAction.PlayerAttackSpear1,
                WeaponType.Bow => PlayerAction.PlayerAttackBow,
                WeaponType.Crossbow => PlayerAction.PlayerAttackCrossbow,
                WeaponType.Staff => PlayerAction.PlayerSkillHand1,
                WeaponType.Scepter => PlayerAction.PlayerAttackStrike,
                WeaponType.Scythe => PlayerAction.PlayerAttackScythe1,
                WeaponType.Book => PlayerAction.PlayerSkillSummon,
                WeaponType.Fist or _ => PlayerAction.AttackFist
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
