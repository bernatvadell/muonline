namespace Client.Main.Models
{
    public enum PlayerAction
    {
        Set, // 0

        StopMale = 1,
        StopFemale = 2,
        StopSummoner = 3,
        StopSword = 9,
        StopTwoHandSword = 10,
        StopSpear = 11,
        StopScythe = 12,
        StopBow = 13,
        StopCrossbow = 14,
        StopWand = 16,
        // StopArm1 = 16, // Scepter?
        // StopArm2 = 12, // Shield?
        // StopArm3 = 13, // Armed Alt?
        // 14-16 missing
        StopFlying = 17,
        StopFlying2 = 18,
        StopFlyCrossbow = 19,
        StopRide = 35, // Fenrir/Horse/Uniria
        StopRideWeapon = 36,
        // PlayerDefense1 = 48, // Defense / Block

        // 22-37 missing (likely other skills/emotes)
        AttackFist = 107,

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

        Run = 75,
        RunSword = 76,
        RunTwoSword = 77,
        RunTwoHandSword = 78,
        RunSpear = 79,
        RunBow = 80,
        RunCrossbow = 81,
        RunWand = 83,
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
        // PlayerFenrirAttackDarklordStrike = 107, // Attack (Fenrir - DL Strike?)
        PlayerFenrirAttackDarklordSword = 108, // Attack (Fenrir - DL Sword?)
        PlayerFenrirAttackDarklordTeleport = 109, // Attack (Fenrir - DL Teleport?)
        PlayerFenrirAttackDarklordFlash = 110, // Attack (Fenrir - DL Flash?)
        PlayerFenrirAttackTwoSword = 111, // Attack (Fenrir - Dual Wield?)
        PlayerFenrirAttackMagic = 112, // Attack (Fenrir - Magic?)
        PlayerFenrirAttackCrossbow = 113, // Attack (Fenrir - Crossbow?)
        PlayerFenrirAttackSpear = 114, // Attack (Fenrir - Spear?)
        PlayerFenrirAttackOneSword = 115, // Attack (Fenrir - 1H Sword?)
        PlayerFenrirAttackBow = 116, // Attack (Fenrir - Bow?)
        // PlayerFenrirSkill = 117, // Skill (Fenrir - Generic?)
        // PlayerFenrirSkillTwoSword = 118, // Skill (Fenrir - Dual Wield?)
        // PlayerFenrirSkillOneRight = 119, // Skill (Fenrir - 1H Right?)
        // PlayerFenrirSkillOneLeft = 120, // Skill (Fenrir - 1H Left?)
        // PlayerFenrirDamage = 121, // Get Hit (Fenrir)
        // PlayerFenrirDamageTwoSword = 122, // Get Hit (Fenrir - Dual Wield?)
        // PlayerFenrirDamageOneRight = 123, // Get Hit (Fenrir - 1H Right?)
        // PlayerFenrirDamageOneLeft = 124, // Get Hit (Fenrir - 1H Left?)

        // 125 missing

        PlayerAttackSpear1 = 117, // Attack (Spear 1)
        PlayerAttackScythe1 = 119, // Attack (Scythe 1)
        PlayerAttackScythe2 = 120, // Attack (Scythe 2)
        PlayerAttackScythe3 = 121, // Attack (Scythe 3)
        PlayerAttackBow = 122, // Attack (Bow)
        PlayerAttackCrossbow = 123, // Attack (Crossbow)
        PlayerAttackFlyBow = 124, // Attack (Flying Bow)
        PlayerAttackFlyCrossbow = 125, // Attack (Flying Crossbow)

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
        // PlayerUnknownFemale1 = 314, // Shrug?
        // PlayerCry1 = 315,
        // PlayerCryFemale1 = 316,
        // PlayerAwkward1 = 317,
        // PlayerAwkwardFemale1 = 318,
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
        PlayerDie1 = 314, // Die 1
        PlayerDie2 = 315, // Die 2
        PlayerDie3 = 316, // Die 3
        PlayerDie4 = 317, // Die 4
        PlayerDie5 = 318, // Die 5

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

    public enum PlayerActionS6
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
}