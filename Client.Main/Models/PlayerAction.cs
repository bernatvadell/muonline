using System.Collections.Generic;

namespace Client.Main.Models
{
    public enum PlayerAction
    {
        Set, // 0

        StopMale = 1,
        StopFemale = 2,
        StopSummoner = 3,
        StopSword = 4,
        StopTwoHandSword = 5,
        StopSpear = 6,
        StopScythe = 7,
        StopBow = 8,
        StopCrossbow = 9,
        StopWand = 10,
        StopArm1 = 11, // Scepter?
        StopArm2 = 12, // Shield?
        StopArm3 = 13, // Armed Alt?
        // 14-16 missing
        StopFlying = 17,
        StopFlyCrossbow = 18,
        StopRide = 19, // Fenrir/Horse/Uniria
        StopRideWeapon = 20,
        PlayerDefense1 = 21, // Defense / Block

        // 22-37 missing (likely other skills/emotes)
        AttackFist = 38,

        // 39 missing
        PlayerAttackSwordRight1 = 40, // Attack (1H Sword Right 1)
        PlayerAttackSwordRight2 = 41, // Attack (1H Sword Right 2) - Possibly Wheel skill?
        PlayerAttackSwordLeft1 = 42,  // Attack (1H Sword Left 1) - Possibly Fury Strike?
        PlayerAttackSwordLeft2 = 43,  // Attack (1H Sword Left 2) - Possibly Death Stab?
        PlayerAttackTwoHandSword1 = 44, // Attack (2H Sword 1) - Possibly Rush?
        PlayerAttackTwoHandSword2 = 45, // Attack (2H Sword 2) - Possibly Javelin?
        PlayerAttackTwoHandSword3 = 46, // Attack (2H Sword 3) - Possibly Deep Impact?

        WalkMale = 47,
        WalkFemale = 48,
        WalkSword = 49,
        WalkTwoHandSword = 50,
        WalkSpear = 51,
        WalkScythe = 52,
        WalkBow = 53,
        WalkCrossbow = 54,
        WalkWand = 55,
        // 56 missing
        PlayerSkillVitality = 57, // Skill (Vitality / Inner Strength?)
        WalkSwim = 58,

        Run = 59,
        RunSword = 60,
        RunTwoSword = 61,
        RunTwoHandSword = 62,
        RunSpear = 63,
        RunBow = 64,
        RunCrossbow = 65,
        RunWand = 66,
        // 67-83 missing

        RunSwim = 84,

        Fly = 85,
        FlyCrossbow = 86,

        RunRide = 87,
        RunRideWeapon = 88,

        PlayerFlyRide = 89, // Fly (Riding)
        PlayerFlyRideWeapon = 90, // Fly (Riding with Weapon)

        PlayerDarklordStand = 91, // Stop/Idle (Dark Lord)
        PlayerDarklordWalk = 92,  // Walk (Dark Lord)
        PlayerStopRideHorse = 93, // Stop/Idle (Riding Horse) - Może być to samo co StopRide
        PlayerRunRideHorse = 94,  // Run (Riding Horse) - Może być to samo co RunRide

        PlayerAttackStrike = 95, // Attack (Strike - DL?)
        PlayerAttackTeleport = 96, // Attack (Teleport - DW?)
        PlayerAttackRideStrike = 97, // Attack (Riding Strike - DL?)
        PlayerAttackRideTeleport = 98, // Attack (Riding Teleport - DW?)
        PlayerAttackRideHorseSword = 99, // Attack (Riding Horse with Sword)
        PlayerAttackRideAttackFlash = 100, // Attack (Riding Flash - MG?)
        PlayerAttackRideAttackMagic = 101, // Attack (Riding Magic - DW/Elf?)
        PlayerAttackDarkhorse = 102, // Attack (Dark Horse Pet)
        PlayerIdle1Darkhorse = 103, // Idle 1 (Dark Horse Pet)
        PlayerIdle2Darkhorse = 104, // Idle 2 (Dark Horse Pet)

        PlayerFenrirAttack = 105, // Attack (Fenrir - Basic?)
        PlayerFenrirAttackDarklordAqua = 106, // Attack (Fenrir - DL Aqua Beam?)
        PlayerFenrirAttackDarklordStrike = 107, // Attack (Fenrir - DL Strike?)
        PlayerFenrirAttackDarklordSword = 108, // Attack (Fenrir - DL Sword?)
        PlayerFenrirAttackDarklordTeleport = 109, // Attack (Fenrir - DL Teleport?)
        PlayerFenrirAttackDarklordFlash = 110, // Attack (Fenrir - DL Flash?)
        PlayerFenrirAttackTwoSword = 111, // Attack (Fenrir - Dual Wield?)
        PlayerFenrirAttackMagic = 112, // Attack (Fenrir - Magic?)
        PlayerFenrirAttackCrossbow = 113, // Attack (Fenrir - Crossbow?)
        PlayerFenrirAttackSpear = 114, // Attack (Fenrir - Spear?)
        PlayerFenrirAttackOneSword = 115, // Attack (Fenrir - 1H Sword?)
        PlayerFenrirAttackBow = 116, // Attack (Fenrir - Bow?)
        PlayerFenrirSkill = 117, // Skill (Fenrir - Generic?)
        PlayerFenrirSkillTwoSword = 118, // Skill (Fenrir - Dual Wield?)
        PlayerFenrirSkillOneRight = 119, // Skill (Fenrir - 1H Right?)
        PlayerFenrirSkillOneLeft = 120, // Skill (Fenrir - 1H Left?)
        PlayerFenrirDamage = 121, // Get Hit (Fenrir)
        PlayerFenrirDamageTwoSword = 122, // Get Hit (Fenrir - Dual Wield?)
        PlayerFenrirDamageOneRight = 123, // Get Hit (Fenrir - 1H Right?)
        PlayerFenrirDamageOneLeft = 124, // Get Hit (Fenrir - 1H Left?)

        // 125 missing

        PlayerAttackSpear1 = 126, // Attack (Spear 1)
        PlayerAttackScythe1 = 127, // Attack (Scythe 1)
        PlayerAttackScythe2 = 128, // Attack (Scythe 2)
        PlayerAttackScythe3 = 129, // Attack (Scythe 3)
        PlayerAttackBow = 130, // Attack (Bow)
        PlayerAttackCrossbow = 131, // Attack (Crossbow)
        PlayerAttackFlyBow = 132, // Attack (Flying Bow)
        PlayerAttackFlyCrossbow = 133, // Attack (Flying Crossbow)

        BlowSkill = 134, // Skill (Power Strike?)
        PlayerAttackRideSword = 135, // Attack (Riding Sword)
        PlayerAttackRideTwoHandSword = 136, // Attack (Riding 2H Sword)
        PlayerAttackRideSpear = 137, // Attack (Riding Spear)

        TwistingSlashSkill = 138, // Skill (Twisting Slash)
        RegularBlowSkill = 139, // Skill (Melee Blow?) - Uppercut/Cyclone? (154?)

        PlayerAttackRideScythe = 140, // Attack (Riding Scythe)
        PlayerAttackRideBow = 141, // Attack (Riding Bow)
        PlayerAttackRideCrossbow = 142, // Attack (Riding Crossbow)

        PlayerAttackSkillSword1 = 143, // Skill Attack (Sword 1)
        PlayerAttackSkillSword2 = 144, // Skill Attack (Sword 2)
        PlayerAttackSkillSword3 = 145, // Skill Attack (Sword 3)
        PlayerAttackSkillSword4 = 146, // Skill Attack (Sword 4)
        PlayerAttackSkillSword5 = 147, // Skill Attack (Sword 5)

        PlayerAttackSkillWheel = 148, // Skill Attack (Wheel)
        PlayerAttackSkillFuryStrike = 149, // Skill Attack (Fury Strike)
        GreaterFortitudeSkill = 150, // Skill (Greater Fortitude/Swell Life) (152?)

        PlayerSkillRide = 151, // Skill (Riding) - ogólne?
        PlayerSkillRideFly = 152, // Skill (Flying Riding)

        PlayerAttackSkillSpear = 153, // Skill Attack (Spear)
        PlayerAttackDeathstab = 154, // Attack (Death Stab)
        PlayerSkillHellBegin = 155, // Skill (Hellfire Begin?)
        PlayerSkillHellStart = 156, // Skill (Hellfire Start?)

        EvilSpiritSkill = 157, // Skill (Evil Spirits)
        FlameSkill = 158, // Skill (Flame)

        PlayerAttackOneFlash = 159, // Attack (One Flash - MG?)
        PlayerAttackRush = 160, // Attack (Rush - DK?)
        PlayerAttackDeathCannon = 161, // Attack (Death Cannon - MG?)
        PlayerAttackRemoval = 162, // Attack (Removal - Elf?)
        PlayerAttackStun = 163, // Attack (Stun - DK?)

        // 164-179 missing (likely many skills)

        PlayerSkillHand1 = 180, // Skill (Hand 1 - Magic?)
        PlayerSkillHand2 = 181, // Skill (Hand 2 - Magic?)
        PlayerSkillWeapon1 = 182, // Skill (Weapon 1)
        PlayerSkillWeapon2 = 183, // Skill (Weapon 2)
        PlayerSkillElf1 = 184, // Skill (Elf 1)
        PlayerSkillTeleport = 185, // Skill (Teleport)
        PlayerSkillFlash = 186, // Skill (Flash / Mass Teleport?)
        PlayerSkillInferno = 187, // Skill (Inferno)
        PlayerSkillHell = 188, // Skill (Hellfire Cast?)

        // 189-199 missing

        PlayerSkillSleep = 200,
        PlayerSkillSleepUni = 201,
        PlayerSkillSleepDino = 202,
        PlayerSkillSleepFenrir = 203,
        PlayerSkillChainLightning = 204,
        PlayerSkillChainLightningUni = 205,
        PlayerSkillChainLightningDino = 206,
        PlayerSkillChainLightningFenrir = 207,
        PlayerSkillLightningOrb = 208, // Lightning Shock?
        PlayerSkillLightningOrbUni = 209,
        PlayerSkillLightningOrbDino = 210,
        PlayerSkillLightningOrbFenrir = 211,
        PlayerSkillDrainLife = 212,
        PlayerSkillDrainLifeUni = 213,
        PlayerSkillDrainLifeDino = 214,
        PlayerSkillDrainLifeFenrir = 215,
        PlayerSkillSummon = 216,
        PlayerSkillSummonUni = 217,
        PlayerSkillSummonDino = 218,
        PlayerSkillSummonFenrir = 219,

        // 220-231 missing
        PlayerSkillBlowOfDestruction = 232,
        PlayerSkillSwellOfMagicPower = 233, // Swell Life / Greater Fortitude?
        PlayerSkillMultishotBowStand = 234, // Multi-Shot (Bow, Standing)
        PlayerSkillMultishotBowFlying = 235, // Multi-Shot (Bow, Flying)
        PlayerSkillMultishotCrossbowStand = 236, // Multi-Shot (Crossbow, Standing)
        PlayerSkillMultishotCrossbowFlying = 237, // Multi-Shot (Crossbow, Flying)
        PlayerSkillRecovery = 238, // Recovery / Heal?
        PlayerSkillGiganticstorm = 239, // Gigantic Storm
        PlayerSkillFlamestrike = 240, // Flame Strike
        PlayerSkillLightningShock = 241, // Lightning Shock

        // 242-259 missing
        PlayerSkillThrust = 260, // Thrust (Rage Fighter)
        PlayerSkillStamp = 261, // Stomp / Earthshake (Rage Fighter)
        PlayerSkillGiantswing = 262, // Giant Swing (Rage Fighter)
        PlayerSkillDarksideReady = 263, // Dark Side Ready (Rage Fighter)
        PlayerSkillDarksideAttack = 264, // Dark Side Attack (Rage Fighter)
        PlayerSkillDragonLore = 265, // Dragon Lore (Rage Fighter)
        PlayerSkillDragonKick = 266, // Dragon Kick (Rage Fighter)
        PlayerSkillAttUpOurforces = 267, // Increase Attack Power (Rage Fighter Buff)
        PlayerSkillHpUpOurforces = 268, // Increase Health (Rage Fighter Buff)
        PlayerSkillDefUpOurforces = 269, // Increase Defense (Rage Fighter Buff)
        PlayerSkillOccupy = 270, // Occupy? (Rage Fighter) - Może Phoenix Shot?
        PlayerSkillPhoenixShot = 271, // Phoenix Shot (Rage Fighter)

        // 272-300 missing (Emotes?)
        PlayerGreeting1 = 301,
        PlayerGreetingFemale1 = 302,
        PlayerGoodbye1 = 303,
        PlayerGoodbyeFemale1 = 304,
        PlayerClap1 = 305,
        PlayerClapFemale1 = 306,
        PlayerCheer1 = 307,
        PlayerCheerFemale1 = 308,
        PlayerDirection1 = 309,
        PlayerDirectionFemale1 = 310,
        PlayerGesture1 = 311,
        PlayerGestureFemale1 = 312,
        PlayerUnknown1 = 313, // Shrug?
        PlayerUnknownFemale1 = 314, // Shrug?
        PlayerCry1 = 315,
        PlayerCryFemale1 = 316,
        PlayerAwkward1 = 317,
        PlayerAwkwardFemale1 = 318,
        PlayerSee1 = 319, // Look around?
        PlayerSeeFemale1 = 320, // Look around?
        PlayerWin1 = 321,
        PlayerWinFemale1 = 322,
        PlayerSmile1 = 323,
        PlayerSmileFemale1 = 324,
        PlayerSleep1 = 325,
        PlayerSleepFemale1 = 326,
        PlayerCold1 = 327,
        PlayerColdFemale1 = 328,
        PlayerAgain1 = 329, // Come on?
        PlayerAgainFemale1 = 330, // Come on?
        PlayerRespect1 = 331,
        PlayerSalute1 = 332,
        PlayerScissors = 333,
        PlayerRock = 334,
        PlayerPaper = 335,
        PlayerHustle = 336, // Dance?
        PlayerProvocation = 337,
        PlayerLookAround = 338,
        PlayerCheers = 339,
        PlayerKoreaHandclap = 340, // Dance?
        PlayerPointDance = 341, // Dance?
        PlayerRush1 = 342, // Charge?

        // 343-346 missing
        PlayerComeUp = 347, // Get up from ground?

        PlayerShock = 348, // Get Hit / Shock 
        PlayerDie1 = 349, // Die 1
        PlayerDie2 = 350, // Die 2

        PlayerSitFemale1 = 351, // Sit Female 1
        PlayerSitFemale2 = 352, // Sit Female 2

        PlayerHealing1 = 353, // Healing 1
        PlayerHealingFemale1 = 354, // Healing Female 1
        PlayerPose1 = 355, // Pose 1
        PlayerPoseFemale1 = 356, // Pose Female 1

        // 357-359 missing
        PlayerSit1 = 360,
        PlayerSit2 = 361,
        PlayerSit3 = 362,
        PlayerSit4 = 363,
        PlayerSit5 = 364,
        PlayerSit6 = 365,
        // 366 missing
        PlayerFlyingRest = 367,
        // 368 missing
        PlayerStandingRest = 369,

        PlayerJack1 = 370, // Jack O'Lantern Emote 1
        PlayerJack2 = 371, // Jack O'Lantern Emote 2
        PlayerSanta1 = 372, // Santa Emote 1
        PlayerSanta2 = 373, // Santa Emote 2
        PlayerChangeUp = 374, // 3rd Class Change Up Effect?
        PlayerRecoverSkill = 375, // Skill Recovery?

        // 376-380 missing
        PlayerSkillRide2 = 381, // Skill (Riding)
        PlayerSkillRideFly2 = 382, // Skill (Flying Riding)

        // 383-412 missing
        PlayerStopArm1 = 413, // Stop/Idle (Armed Alt 1?)
        PlayerStopArm2 = 414, // Stop/Idle (Armed Alt 2?)
        PlayerStopArm3 = 415, // Stop/Idle (Armed Alt 3?)

        // 416-421 missing
        PlayerFenrirAttackMagic2 = 422, // Attack (Fenrir Magic?)
        PlayerFenrirAttackCrossbow2 = 423, // Attack (Fenrir Crossbow?) 
        PlayerFenrirAttackSpear2 = 424, // Attack (Fenrir Spear?)
        PlayerFenrirAttackOneSword2 = 425, // Attack (Fenrir 1H Sword?)
        PlayerFenrirAttackBow2 = 426, // Attack (Fenrir Bow?)

        // 427-431 missing
        PlayerFenrirRunMagom = 432, // Run (Fenrir - Magic User?)
        PlayerFenrirRunTwoSwordMagom = 433, // Run (Fenrir - Magic User Dual Wield?)
        PlayerFenrirRunOneRightMagom = 434, // Run (Fenrir - Magic User 1H Right?)
        PlayerFenrirRunOneLeftMagom = 435, // Run (Fenrir - Magic User 1H Left?)
        PlayerFenrirRunElf = 436, // Run (Fenrir - Elf?)
        PlayerFenrirRunTwoSwordElf = 437, // Run (Fenrir - Elf Dual Wield?)
        PlayerFenrirRunOneRightElf = 438, // Run (Fenrir - Elf 1H Right?)
        PlayerFenrirRunOneLeftElf = 439, // Run (Fenrir - Elf 1H Left?)

        // 440-458 missing
        PlayerRageUniAttack = 459, // Attack (Unicorn - Rage Fighter?)
        PlayerRageUniAttackOneRight = 460, // Attack (Unicorn - Rage Fighter 1H Right?)
        PlayerRageUniRun = 461, // Run (Unicorn - Rage Fighter?)
        PlayerRageUniRunOneRight = 462, // Run (Unicorn - Rage Fighter 1H Right?)
        PlayerRageUniStopOneRight = 463, // Stop/Idle (Unicorn - Rage Fighter 1H Right?)
        PlayerRageFenrir = 464, // Stop/Idle (Fenrir - Rage Fighter?)
        PlayerRageFenrirTwoSword = 465, // Stop/Idle (Fenrir - Rage Fighter Dual Wield?)
        PlayerRageFenrirOneRight = 466, // Stop/Idle (Fenrir - Rage Fighter 1H Right?)
        PlayerRageFenrirOneLeft = 467, // Stop/Idle (Fenrir - Rage Fighter 1H Left?)
        PlayerRageFenrirWalk = 468, // Walk (Fenrir - Rage Fighter?)
        PlayerRageFenrirWalkOneRight = 469, // Walk (Fenrir - Rage Fighter 1H Right?)
        PlayerRageFenrirWalkOneLeft = 470, // Walk (Fenrir - Rage Fighter 1H Left?)
        PlayerRageFenrirWalkTwoSword = 471, // Walk (Fenrir - Rage Fighter Dual Wield?)
        PlayerRageFenrirRun = 472, // Run (Fenrir - Rage Fighter?)
        PlayerRageFenrirRunTwoSword = 473, // Run (Fenrir - Rage Fighter Dual Wield?)
        PlayerRageFenrirRunOneRight = 474, // Run (Fenrir - Rage Fighter 1H Right?)
        PlayerRageFenrirRunOneLeft = 475, // Run (Fenrir - Rage Fighter 1H Left?)
        PlayerRageFenrirStand = 476, // Stand (Fenrir - Rage Fighter?) - To samo co Stop?
        PlayerRageFenrirStandTwoSword = 477, // Stand (Fenrir - Rage Fighter Dual Wield?)
        PlayerRageFenrirStandOneRight = 478, // Stand (Fenrir - Rage Fighter 1H Right?)
        PlayerRageFenrirStandOneLeft = 479, // Stand (Fenrir - Rage Fighter 1H Left?)
        PlayerRageFenrirDamage = 480, // Get Hit (Fenrir - Rage Fighter?)
        PlayerRageFenrirDamageTwoSword = 481, // Get Hit (Fenrir - Rage Fighter Dual Wield?)
        PlayerRageFenrirDamageOneRight = 482, // Get Hit (Fenrir - Rage Fighter 1H Right?)
        PlayerRageFenrirDamageOneLeft = 483, // Get Hit (Fenrir - Rage Fighter 1H Left?)
        PlayerRageFenrirAttackRight = 484, // Attack (Fenrir - Rage Fighter Right?)
        PlayerStopRagefighter = 485, // Stop/Idle (Rage Fighter)
    }

    public static class PlayerActionMapper
    {
        public static readonly Dictionary<PlayerAction, string> ActionNames = new Dictionary<PlayerAction, string>
        {
            { PlayerAction.Set, "Set (Internal?)" },
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
            { PlayerAction.StopArm1, "Stop/Idle (Scepter?)" },
            { PlayerAction.StopArm2, "Stop/Idle (Shield?)" },
            { PlayerAction.StopArm3, "Stop/Idle (Armed Alt?)" },
            { PlayerAction.StopFlying, "Stop/Idle (Flying)" },
            { PlayerAction.StopFlyCrossbow, "Stop/Idle (Flying with Crossbow)" },
            { PlayerAction.StopRide, "Stop/Idle (Riding)" },
            { PlayerAction.StopRideWeapon, "Stop/Idle (Riding with Weapon)" },
            { PlayerAction.PlayerDefense1, "Defense / Block" },
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
            { PlayerAction.AttackFist, "Attack (Fist)" },
            { PlayerAction.PlayerAttackSwordRight1, "Attack (1H Sword Right 1)" },
            { PlayerAction.PlayerAttackSwordRight2, "Attack (1H Sword Right 2 / Wheel?)" },
            { PlayerAction.PlayerAttackSwordLeft1, "Attack (1H Sword Left 1 / Fury Strike?)" },
            { PlayerAction.PlayerAttackSwordLeft2, "Attack (1H Sword Left 2 / Death Stab?)" },
            { PlayerAction.PlayerAttackTwoHandSword1, "Attack (2H Sword 1 / Rush?)" },
            { PlayerAction.PlayerAttackTwoHandSword2, "Attack (2H Sword 2 / Javelin?)" },
            { PlayerAction.PlayerAttackTwoHandSword3, "Attack (2H Sword 3 / Deep Impact?)" },
            { PlayerAction.PlayerAttackSpear1, "Attack (Spear 1)" },
            { PlayerAction.PlayerAttackScythe1, "Attack (Scythe 1)" },
            { PlayerAction.PlayerAttackScythe2, "Attack (Scythe 2)" },
            { PlayerAction.PlayerAttackScythe3, "Attack (Scythe 3)" },
            { PlayerAction.PlayerAttackBow, "Attack (Bow)" },
            { PlayerAction.PlayerAttackCrossbow, "Attack (Crossbow)" },
            { PlayerAction.PlayerAttackFlyBow, "Attack (Flying Bow)" },
            { PlayerAction.PlayerAttackFlyCrossbow, "Attack (Flying Crossbow)" },
            { PlayerAction.PlayerAttackRideSword, "Attack (Riding Sword)" },
            { PlayerAction.PlayerAttackRideTwoHandSword, "Attack (Riding 2H Sword)" },
            { PlayerAction.PlayerAttackRideSpear, "Attack (Riding Spear)" },
            { PlayerAction.PlayerAttackRideScythe, "Attack (Riding Scythe)" },
            { PlayerAction.PlayerAttackRideBow, "Attack (Riding Bow)" },
            { PlayerAction.PlayerAttackRideCrossbow, "Attack (Riding Crossbow)" },
            { PlayerAction.PlayerAttackSkillSword1, "Skill Attack (Sword 1)" },
            { PlayerAction.PlayerAttackSkillSword2, "Skill Attack (Sword 2)" },
            { PlayerAction.PlayerAttackSkillSword3, "Skill Attack (Sword 3)" },
            { PlayerAction.PlayerAttackSkillSword4, "Skill Attack (Sword 4)" },
            { PlayerAction.PlayerAttackSkillSword5, "Skill Attack (Sword 5)" },
            { PlayerAction.PlayerAttackSkillWheel, "Skill Attack (Wheel)" },
            { PlayerAction.PlayerAttackSkillFuryStrike, "Skill Attack (Fury Strike)" },
            { PlayerAction.PlayerSkillVitality, "Skill (Vitality / Inner Strength)" },
            { PlayerAction.PlayerSkillRide, "Skill (Riding)" },
            { PlayerAction.PlayerSkillRideFly, "Skill (Flying Riding)" },
            { PlayerAction.PlayerAttackSkillSpear, "Skill Attack (Spear)" },
            { PlayerAction.PlayerAttackDeathstab, "Attack (Death Stab)" },
            { PlayerAction.PlayerSkillHellBegin, "Skill (Hellfire Begin?)" },
            { PlayerAction.PlayerSkillHellStart, "Skill (Hellfire Start?)" },
            { PlayerAction.BlowSkill, "Skill (Power Strike?)" },
            { PlayerAction.TwistingSlashSkill, "Skill (Twisting Slash)" },
            { PlayerAction.RegularBlowSkill, "Skill (Melee Blow?)" },
            { PlayerAction.GreaterFortitudeSkill, "Skill (Greater Fortitude/Swell Life)" },
            { PlayerAction.EvilSpiritSkill, "Skill (Evil Spirits)" },
            { PlayerAction.FlameSkill, "Skill (Flame)" },
            { PlayerAction.PlayerSit1, "Sit Pose 1" },
            { PlayerAction.PlayerSit2, "Sit Pose 2" },
            { PlayerAction.PlayerSit3, "Sit Pose 3" },
            { PlayerAction.PlayerSit4, "Sit Pose 4" },
            { PlayerAction.PlayerSit5, "Sit Pose 5" },
            { PlayerAction.PlayerSit6, "Sit Pose 6" },
            { PlayerAction.PlayerFlyingRest, "Rest (Flying)" },
            { PlayerAction.PlayerStandingRest, "Rest (Standing/Leaning)" },
            { PlayerAction.PlayerFlyRide, "Fly (Riding)" },
            { PlayerAction.PlayerFlyRideWeapon, "Fly (Riding with Weapon)" },
            { PlayerAction.PlayerDarklordStand, "Stop/Idle (Dark Lord)" },
            { PlayerAction.PlayerDarklordWalk, "Walk (Dark Lord)" },
            { PlayerAction.PlayerStopRideHorse, "Stop/Idle (Riding Horse)" },
            { PlayerAction.PlayerRunRideHorse, "Run (Riding Horse)" },
            { PlayerAction.PlayerAttackStrike, "Attack (Strike - DL?)" },
            { PlayerAction.PlayerAttackTeleport, "Attack (Teleport - DW?)" },
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
            { PlayerAction.PlayerFenrirAttackDarklordStrike, "Attack (Fenrir - DL Strike?)" },
            { PlayerAction.PlayerFenrirAttackDarklordSword, "Attack (Fenrir - DL Sword?)" },
            { PlayerAction.PlayerFenrirAttackDarklordTeleport, "Attack (Fenrir - DL Teleport?)" },
            { PlayerAction.PlayerFenrirAttackDarklordFlash, "Attack (Fenrir - DL Flash?)" },
            { PlayerAction.PlayerFenrirAttackTwoSword, "Attack (Fenrir - Dual Wield?)" },
            { PlayerAction.PlayerFenrirAttackMagic, "Attack (Fenrir - Magic?)" },
            { PlayerAction.PlayerFenrirAttackCrossbow, "Attack (Fenrir - Crossbow?)" },
            { PlayerAction.PlayerFenrirAttackSpear, "Attack (Fenrir - Spear?)" },
            { PlayerAction.PlayerFenrirAttackOneSword, "Attack (Fenrir - 1H Sword?)" },
            { PlayerAction.PlayerFenrirAttackBow, "Attack (Fenrir - Bow?)" },
            { PlayerAction.PlayerFenrirSkill, "Skill (Fenrir - Generic?)" },
            { PlayerAction.PlayerFenrirSkillTwoSword, "Skill (Fenrir - Dual Wield?)" },
            { PlayerAction.PlayerFenrirSkillOneRight, "Skill (Fenrir - 1H Right?)" },
            { PlayerAction.PlayerFenrirSkillOneLeft, "Skill (Fenrir - 1H Left?)" },
            { PlayerAction.PlayerFenrirDamage, "Get Hit (Fenrir)" },
            { PlayerAction.PlayerFenrirDamageTwoSword, "Get Hit (Fenrir - Dual Wield?)" },
            { PlayerAction.PlayerFenrirDamageOneRight, "Get Hit (Fenrir - 1H Right?)" },
            { PlayerAction.PlayerFenrirDamageOneLeft, "Get Hit (Fenrir - 1H Left?)" },
            // { PlayerAction.PlayerFenrirRun, "Run (Fenrir)" },
            // { PlayerAction.PlayerFenrirRunTwoSword, "Run (Fenrir - Dual Wield?)" },
            // { PlayerAction.PlayerFenrirRunOneRight, "Run (Fenrir - 1H Right?)" },
            // { PlayerAction.PlayerFenrirRunOneLeft, "Run (Fenrir - 1H Left?)" },
            { PlayerAction.PlayerFenrirRunMagom, "Run (Fenrir - Magic User?)" },
            { PlayerAction.PlayerFenrirRunTwoSwordMagom, "Run (Fenrir - Magic User Dual Wield?)" },
            { PlayerAction.PlayerFenrirRunOneRightMagom, "Run (Fenrir - Magic User 1H Right?)" },
            { PlayerAction.PlayerFenrirRunOneLeftMagom, "Run (Fenrir - Magic User 1H Left?)" },
            { PlayerAction.PlayerFenrirRunElf, "Run (Fenrir - Elf?)" },
            { PlayerAction.PlayerFenrirRunTwoSwordElf, "Run (Fenrir - Elf Dual Wield?)" },
            { PlayerAction.PlayerFenrirRunOneRightElf, "Run (Fenrir - Elf 1H Right?)" },
            { PlayerAction.PlayerFenrirRunOneLeftElf, "Run (Fenrir - Elf 1H Left?)" },
            // { PlayerAction.PlayerFenrirStand, "Stand (Fenrir)" },
            // { PlayerAction.PlayerFenrirStandTwoSword, "Stand (Fenrir - Dual Wield?)" },
            // { PlayerAction.PlayerFenrirStandOneRight, "Stand (Fenrir - 1H Right?)" },
            // { PlayerAction.PlayerFenrirStandOneLeft, "Stand (Fenrir - 1H Left?)" },
            // { PlayerAction.PlayerFenrirWalk, "Walk (Fenrir)" },
            // { PlayerAction.PlayerFenrirWalkTwoSword, "Walk (Fenrir - Dual Wield?)" },
            // { PlayerAction.PlayerFenrirWalkOneRight, "Walk (Fenrir - 1H Right?)" },
            // { PlayerAction.PlayerFenrirWalkOneLeft, "Walk (Fenrir - 1H Left?)" },
            // { PlayerAction.PlayerAttackBowUp, "Attack (Bow - Upwards?)" },
            // { PlayerAction.PlayerAttackCrossbowUp, "Attack (Crossbow - Upwards?)" },
            // { PlayerAction.PlayerAttackFlyBowUp, "Attack (Flying Bow - Upwards?)" },
            // { PlayerAction.PlayerAttackFlyCrossbowUp, "Attack (Flying Crossbow - Upwards?)" },
            // { PlayerAction.PlayerAttackRideBowUp, "Attack (Riding Bow - Upwards?)" },
            // { PlayerAction.PlayerAttackRideCrossbowUp, "Attack (Riding Crossbow - Upwards?)" },
            { PlayerAction.PlayerAttackOneFlash, "Attack (One Flash - MG?)" },
            { PlayerAction.PlayerAttackRush, "Attack (Rush - DK?)" },
            { PlayerAction.PlayerAttackDeathCannon, "Attack (Death Cannon - MG?)" },
            { PlayerAction.PlayerAttackRemoval, "Attack (Removal - Elf?)" },
            { PlayerAction.PlayerAttackStun, "Attack (Stun - DK?)" },
            // { PlayerAction.PlayerHighShock, "Get Hit (High Shock?)" },
            // { PlayerAction.PlayerStopTwoHandSwordTwo, "Stop/Idle (2H Sword Alt?)" },
            // { PlayerAction.PlayerWalkTwoHandSwordTwo, "Walk (2H Sword Alt?)" },
            // { PlayerAction.PlayerRunTwoHandSwordTwo, "Run (2H Sword Alt?)" },
            // { PlayerAction.PlayerAttackTwoHandSwordTwo, "Attack (2H Sword Alt?)" },
            { PlayerAction.PlayerSkillHand1, "Skill (Hand 1 - Magic?)" },
            { PlayerAction.PlayerSkillHand2, "Skill (Hand 2 - Magic?)" },
            { PlayerAction.PlayerSkillWeapon1, "Skill (Weapon 1)" },
            { PlayerAction.PlayerSkillWeapon2, "Skill (Weapon 2)" },
            { PlayerAction.PlayerSkillElf1, "Skill (Elf 1)" },
            { PlayerAction.PlayerSkillTeleport, "Skill (Teleport)" },
            { PlayerAction.PlayerSkillFlash, "Skill (Flash / Mass Teleport?)" },
            { PlayerAction.PlayerSkillInferno, "Skill (Inferno)" },
            { PlayerAction.PlayerSkillHell, "Skill (Hellfire Cast?)" },
            { PlayerAction.PlayerSkillSleep, "Skill (Sleep)" },
            { PlayerAction.PlayerSkillSleepUni, "Skill (Sleep - Unicorn)" },
            { PlayerAction.PlayerSkillSleepDino, "Skill (Sleep - Dinorant)" },
            { PlayerAction.PlayerSkillSleepFenrir, "Skill (Sleep - Fenrir)" },
            { PlayerAction.PlayerSkillChainLightning, "Skill (Chain Lightning)" },
            { PlayerAction.PlayerSkillChainLightningUni, "Skill (Chain Lightning - Unicorn)" },
            { PlayerAction.PlayerSkillChainLightningDino, "Skill (Chain Lightning - Dinorant)" },
            { PlayerAction.PlayerSkillChainLightningFenrir, "Skill (Chain Lightning - Fenrir)" },
            { PlayerAction.PlayerSkillLightningOrb, "Skill (Lightning Shock?)" },
            { PlayerAction.PlayerSkillLightningOrbUni, "Skill (Lightning Shock - Unicorn)" },
            { PlayerAction.PlayerSkillLightningOrbDino, "Skill (Lightning Shock - Dinorant)" },
            { PlayerAction.PlayerSkillLightningOrbFenrir, "Skill (Lightning Shock - Fenrir)" },
            { PlayerAction.PlayerSkillDrainLife, "Skill (Drain Life)" },
            { PlayerAction.PlayerSkillDrainLifeUni, "Skill (Drain Life - Unicorn)" },
            { PlayerAction.PlayerSkillDrainLifeDino, "Skill (Drain Life - Dinorant)" },
            { PlayerAction.PlayerSkillDrainLifeFenrir, "Skill (Drain Life - Fenrir)" },
            { PlayerAction.PlayerSkillSummon, "Skill (Summon)" },
            { PlayerAction.PlayerSkillSummonUni, "Skill (Summon - Unicorn)" },
            { PlayerAction.PlayerSkillSummonDino, "Skill (Summon - Dinorant)" },
            { PlayerAction.PlayerSkillSummonFenrir, "Skill (Summon - Fenrir)" },
            { PlayerAction.PlayerSkillBlowOfDestruction, "Skill (Blow of Destruction)" },
            { PlayerAction.PlayerSkillSwellOfMagicPower, "Skill (Swell Life / Greater Fortitude)" },
            { PlayerAction.PlayerSkillMultishotBowStand, "Skill (Multi-Shot - Bow, Standing)" },
            { PlayerAction.PlayerSkillMultishotBowFlying, "Skill (Multi-Shot - Bow, Flying)" },
            { PlayerAction.PlayerSkillMultishotCrossbowStand, "Skill (Multi-Shot - Crossbow, Standing)" },
            { PlayerAction.PlayerSkillMultishotCrossbowFlying, "Skill (Multi-Shot - Crossbow, Flying)" },
            { PlayerAction.PlayerSkillRecovery, "Skill (Recovery / Heal?)" },
            { PlayerAction.PlayerSkillGiganticstorm, "Skill (Gigantic Storm)" },
            { PlayerAction.PlayerSkillFlamestrike, "Skill (Flame Strike)" },
            { PlayerAction.PlayerSkillLightningShock, "Skill (Lightning Shock)" },
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
            { PlayerAction.PlayerUnknownFemale1, "Emote (Shrug? Female 1)" },
            { PlayerAction.PlayerCry1, "Emote (Cry 1)" },
            { PlayerAction.PlayerCryFemale1, "Emote (Cry Female 1)" },
            { PlayerAction.PlayerAwkward1, "Emote (Awkward 1)" },
            { PlayerAction.PlayerAwkwardFemale1, "Emote (Awkward Female 1)" },
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
            { PlayerAction.PlayerComeUp, "Get Up" },
            { PlayerAction.PlayerShock, "Get Hit / Shock" },
            { PlayerAction.PlayerDie1, "Die 1" },
            { PlayerAction.PlayerDie2, "Die 2" },
            { PlayerAction.PlayerSitFemale1, "Sit Female Pose 1" },
            { PlayerAction.PlayerSitFemale2, "Sit Female Pose 2" },
            { PlayerAction.PlayerHealing1, "Healing 1" },
            { PlayerAction.PlayerHealingFemale1, "Healing Female 1" },
            { PlayerAction.PlayerPose1, "Pose 1" },
            { PlayerAction.PlayerPoseFemale1, "Pose Female 1" },
            { PlayerAction.PlayerJack1, "Jack O'Lantern Emote 1" },
            { PlayerAction.PlayerJack2, "Jack O'Lantern Emote 2" },
            { PlayerAction.PlayerSanta1, "Santa Emote 1" },
            { PlayerAction.PlayerSanta2, "Santa Emote 2" },
            { PlayerAction.PlayerChangeUp, "3rd Class Change Up Effect?" },
            { PlayerAction.PlayerRecoverSkill, "Skill Recovery?" },
            { PlayerAction.PlayerSkillRide, "Skill (Riding)" },
            { PlayerAction.PlayerSkillRideFly, "Skill (Flying Riding)" },
            { PlayerAction.PlayerStopArm1, "Stop/Idle (Armed Alt 1?)" },
            { PlayerAction.PlayerStopArm2, "Stop/Idle (Armed Alt 2?)" },
            { PlayerAction.PlayerStopArm3, "Stop/Idle (Armed Alt 3?)" },
            { PlayerAction.PlayerFenrirAttackMagic, "Attack (Fenrir Magic?)" },
            { PlayerAction.PlayerFenrirAttackCrossbow, "Attack (Fenrir Crossbow?)" },
            { PlayerAction.PlayerFenrirAttackSpear, "Attack (Fenrir Spear?)" },
            { PlayerAction.PlayerFenrirAttackOneSword, "Attack (Fenrir 1H Sword?)" },
            { PlayerAction.PlayerFenrirAttackBow, "Attack (Fenrir Bow?)" },
            { PlayerAction.PlayerFenrirRunMagom, "Run (Fenrir - Magic User?)" },
            { PlayerAction.PlayerFenrirRunTwoSwordMagom, "Run (Fenrir - Magic User Dual Wield?)" },
            { PlayerAction.PlayerFenrirRunOneRightMagom, "Run (Fenrir - Magic User 1H Right?)" },
            { PlayerAction.PlayerFenrirRunOneLeftMagom, "Run (Fenrir - Magic User 1H Left?)" },
            { PlayerAction.PlayerFenrirRunElf, "Run (Fenrir - Elf?)" },
            { PlayerAction.PlayerFenrirRunTwoSwordElf, "Run (Fenrir - Elf Dual Wield?)" },
            { PlayerAction.PlayerFenrirRunOneRightElf, "Run (Fenrir - Elf 1H Right?)" },
            { PlayerAction.PlayerFenrirRunOneLeftElf, "Run (Fenrir - Elf 1H Left?)" },
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
            { PlayerAction.PlayerStopRagefighter, "Stop/Idle (Rage Fighter)" },
        };

        public static string GetActionName(PlayerAction action)
        {
            return ActionNames.TryGetValue(action, out var name) ? name : $"Unknown Action ({(int)action})";
        }

        public static string GetActionName(int actionIndex)
        {
            return GetActionName((PlayerAction)actionIndex);
        }
    }
}