using System.Collections.Generic;

namespace Client.Data.BMD
{
    /// <summary>
    /// Static definitions for skills based on original MU client source code.
    /// Provides skill type and animation mappings that are not stored in BMD files.
    /// </summary>
    public static class SkillDefinitions
    {
        /// <summary>
        /// Mapping of skill IDs to their types (AREA/TARGET/SELF).
        /// Based on SendRequestMagic vs SendRequestMagicContinue in original client.
        /// </summary>
        private static readonly Dictionary<int, SkillType> SkillTypes = new()
        {
            // ID 1-10: Wizard Skills
            { 1, SkillType.Target },   // Poison
            { 2, SkillType.Target },   // Meteorite
            { 3, SkillType.Target },   // Lightning
            { 4, SkillType.Target },   // Fire Ball
            { 5, SkillType.Area },     // Flame
            { 6, SkillType.Self },     // Teleport
            { 7, SkillType.Target },   // Ice
            { 8, SkillType.Area },     // Twister
            { 9, SkillType.Area },   // Evil Spirit
            { 10, SkillType.Area },    // Hellfire

            // ID 11-25: Mixed Skills
            { 11, SkillType.Target },  // Power Wave
            { 12, SkillType.Target },  // Aqua Beam
            { 13, SkillType.Area },    // Cometfall
            { 14, SkillType.Area },    // Inferno
            { 15, SkillType.Self },    // Teleport Ally
            { 16, SkillType.Target },    // Soul Barrier
            { 17, SkillType.Target },  // Energy Ball
            { 18, SkillType.Self },    // Defense
            { 19, SkillType.Target },    // Falling Slash
            { 20, SkillType.Target },  // Lunge
            { 21, SkillType.Target },  // Uppercut
            { 22, SkillType.Target },    // Cyclone
            { 23, SkillType.Target },  // Slash
            { 24, SkillType.Target },  // Triple Shot
            { 26, SkillType.Target },  // Heal

            // ID 27-52: Elf/Summoner Skills
            { 27, SkillType.Self },    // Greater Defense
            { 28, SkillType.Self },    // Greater Damage
            { 30, SkillType.Self },    // Summon Goblin
            { 31, SkillType.Self },    // Summon Stone Golem
            { 32, SkillType.Self },    // Summon Assassin
            { 33, SkillType.Self },    // Summon Elite Yeti
            { 34, SkillType.Self },    // Summon Dark Knight
            { 35, SkillType.Self },    // Summon Bali
            { 36, SkillType.Self },    // Summon Soldier
            { 38, SkillType.Area },    // Decay
            { 39, SkillType.Area },    // Ice Storm
            { 40, SkillType.Target },  // Nova
            { 41, SkillType.Area },    // Twisting Slash
            { 42, SkillType.Area },    // Rageful Blow
            { 43, SkillType.Target },  // Death Stab
            { 44, SkillType.Target },  // Crescent Moon Slash
            { 45, SkillType.Target },  // Lance
            { 46, SkillType.Area },    // Starfall
            { 47, SkillType.Target },  // Impale
            { 48, SkillType.Self },    // Swell Life
            { 49, SkillType.Target },  // Fire Breath
            { 51, SkillType.Target },  // Ice Arrow
            { 52, SkillType.Target },  // Penetration

            // ID 55-79: Dark Lord/Mixed Skills
            { 55, SkillType.Area },    // Fire Slash
            { 56, SkillType.Target },  // Power Slash
            { 57, SkillType.Area },    // Spiral Slash
            { 60, SkillType.Self },    // Force
            { 61, SkillType.Target },  // Fire Burst
            { 62, SkillType.Area },    // Earthshake
            { 63, SkillType.Self },    // Summon
            { 64, SkillType.Self },    // Increase Critical Damage
            { 65, SkillType.Target },  // Electric Spike
            { 66, SkillType.Target },  // Force Wave
            { 67, SkillType.Area },    // Stun
            { 68, SkillType.Self },    // Cancel Stun
            { 69, SkillType.Self },    // Swell Mana
            { 70, SkillType.Self },    // Invisibility
            { 71, SkillType.Self },    // Cancel Invisibility
            { 72, SkillType.Self },    // Abolish Magic
            { 73, SkillType.Target },  // Mana Rays
            { 74, SkillType.Target },  // Fire Blast
            { 76, SkillType.Area },    // Plasma Storm
            { 77, SkillType.Self },    // Infinity Arrow
            { 78, SkillType.Area },    // Fire Scream
            { 79, SkillType.Area },    // Explosion

            // ID 200-225: Summoner Skills
            { 200, SkillType.Self },   // Summon Monster
            { 201, SkillType.Self },   // Magic Attack Immunity
            { 202, SkillType.Self },   // Physical Attack Immunity
            { 203, SkillType.Self },   // Potion of Bless
            { 204, SkillType.Self },   // Potion of Soul
            { 210, SkillType.Self },   // Spell of Protection
            { 211, SkillType.Self },   // Spell of Restriction
            { 212, SkillType.Self },   // Spell of Pursuit
            { 213, SkillType.Target }, // Shield-Burn
            { 214, SkillType.Target }, // Drain Life
            { 215, SkillType.Area },   // Chain Lightning
            { 217, SkillType.Self },   // Damage Reflection
            { 218, SkillType.Self },   // Berserker
            { 219, SkillType.Target }, // Sleep
            { 221, SkillType.Target }, // Weakness
            { 222, SkillType.Target }, // Innovation
            { 223, SkillType.Area },   // Explosion
            { 224, SkillType.Target }, // Requiem
            { 225, SkillType.Area },   // Pollution

            // ID 230-270: Dark Lord/Rage Fighter Skills
            { 230, SkillType.Area },   // Lightning Shock
            { 232, SkillType.Area },   // Strike of Destruction
            { 233, SkillType.Self },   // Expansion of Wizardry
            { 234, SkillType.Self },   // Recovery
            { 235, SkillType.Area },   // Multi-Shot
            { 236, SkillType.Target }, // Flame Strike
            { 237, SkillType.Area },   // Gigantic Storm
            { 238, SkillType.Area },   // Chaotic Diseier
            { 260, SkillType.Target }, // Killing Blow
            { 261, SkillType.Target }, // Beast Uppercut
            { 262, SkillType.Target }, // Chain Drive
            { 263, SkillType.Target }, // Dark Side
            { 264, SkillType.Target }, // Dragon Roar
            { 265, SkillType.Area },   // Dragon Slasher
            { 266, SkillType.Self },   // Ignore Defense
            { 267, SkillType.Self },   // Increase Health
            { 268, SkillType.Self },   // Increase Block
            { 269, SkillType.Target }, // Charge
            { 270, SkillType.Target }, // Phoenix Shot

            // ID 495: Earth Prison
            { 495, SkillType.Area },   // Earth Prison

            // ID 565: Blood Howling
            { 565, SkillType.Self },   // Blood Howling
        };

        /// <summary>
        /// Mapping of skill IDs to their animation IDs.
        /// Based on SetAction calls in original client ZzzInterface.cpp.
        /// Returns -1 for skills using generic magic/attack animations.
        /// </summary>
        private static readonly Dictionary<int, int> SkillAnimations = BuildSkillAnimations();

        /// <summary>
        /// Mapping of skill IDs to their sound file paths.
        /// Based on original client sound effects for each skill.
        /// </summary>
        private static readonly Dictionary<int, string> SkillSounds = BuildSkillSounds();

        /// <summary>
        /// Gets the skill type for a given skill ID.
        /// Returns TARGET by default if not found.
        /// </summary>
        public static SkillType GetSkillType(int skillId)
        {
            return SkillTypes.TryGetValue(skillId, out var type) ? type : SkillType.Target;
        }

        /// <summary>
        /// Gets the animation ID for a given skill ID.
        /// Returns -1 if the skill uses generic magic/attack animation.
        /// </summary>
        public static int GetSkillAnimation(int skillId)
        {
            return SkillAnimations.TryGetValue(skillId, out var animId) ? animId : -1;
        }

        /// <summary>
        /// Gets the sound file path for a given skill ID.
        /// Returns null if the skill has no specific sound mapping.
        /// </summary>
        public static string? GetSkillSound(int skillId)
        {
            return SkillSounds.TryGetValue(skillId, out var soundPath) ? soundPath : null;
        }

        private static Dictionary<int, int> BuildSkillAnimations()
        {
            var map = new Dictionary<int, int>();

            void Add(int skillId, int animationId)
            {
                if (!map.TryAdd(skillId, animationId))
                {
                    // Prefer the first mapping we encountered (matches Main 5.2 behaviour).
                    return;
                }
            }

            // WARRIOR/KNIGHT SKILLS
            Add(22, 63);    // Cyclone → PlayerAttackSkillSword4
            Add(19, 62);    // Falling Slash → PlayerAttackSkillWheel
            Add(43, 71);    // Death Stab → PlayerAttackDeathstab
            Add(41, 65);    // Twisting Slash → PlayerAttackSkillWheel

            // WIZARD/SUMMONER SKILLS
            Add(6, 151);    // Teleport → PlayerSkillTeleport
            Add(10, 154);   // HellFire → PlayerSkillHell
            Add(13, 154);   // Cometfall → PlayerSkillHell
            Add(14, 153);   // Inferno → PlayerSkillInferno
            Add(40, 152);   // Nova → PlayerSkillFlash
            Add(63, 172);   // Summon → PlayerSkillSummon
            Add(214, 168);  // Drain Life → PlayerSkillDrainLife
            Add(215, 160);  // Chain Lightning → PlayerSkillChainLightning
            Add(219, 156);  // Sleep → PlayerSkillSleep
            Add(230, 185);  // Lightning Shock → PlayerSkillLightningShock
            Add(221, 156);  // Weakness → PlayerSkillSleep
            Add(222, 156);  // Innovation → PlayerSkillSleep
            Add(218, 166);  // Berserker → PlayerSkillSleep

            // DARK LORD SKILLS
            Add(78, 184);   // Fire Scream → PlayerSkillFlamestrike
            Add(232, 176);  // Strike of Destruction → PlayerSkillBlowOfDestruction
            Add(64, 71);    // Increase Critical Damage → PlayerSkillVitality
            Add(65, 71);    // Electric Spike → PlayerSkillVitality
            Add(56, 146);   // Power Slash → PlayerAttackTwoHandSwordTwo
            Add(57, 65);    // Spiral Slash → PlayerAttackSkillWheel

            // ELF SKILLS
            Add(234, 246);  // Recovery → PlayerRecoverSkill
            Add(46, 178);   // Starfall → PlayerSkillMultishotBowStand
            Add(235, 178);  // Multi-Shot → PlayerSkillMultishotBowStand

            // RAGE FIGHTER SKILLS
            Add(260, 247);  // Killing Blow → PlayerSkillThrust
            Add(261, 248);  // Beast Uppercut → PlayerSkillStamp
            Add(262, 249);  // Chain Drive → PlayerSkillGiantswing
            Add(263, 250);  // Dark Side → PlayerSkillDarksideReady
            Add(264, 252);  // Dragon Roar → PlayerSkillDragonkick
            Add(265, 253);  // Dragon Slasher → PlayerSkillDragonlore
            Add(270, 254);  // Phoenix Shot → PlayerSkillPhoenixShot
            Add(48, 255);   // Swell Life → PlayerSkillAttUpOurforces

            // SPECIAL SKILLS
            Add(67, 140);   // Stun → PlayerAttackStun
            Add(76, 102);   // Plasma Storm → PlayerFenrirSkill
            Add(565, 257);  // Blood Howling → PlayerSkillBloodHowling

            return map;
        }

        /// <summary>
        /// Checks if a skill is an area/directional skill.
        /// </summary>
        public static bool IsAreaSkill(int skillId)
        {
            return GetSkillType(skillId) == SkillType.Area;
        }

        /// <summary>
        /// Checks if a skill is a target-required skill.
        /// </summary>
        public static bool IsTargetSkill(int skillId)
        {
            return GetSkillType(skillId) == SkillType.Target;
        }

        /// <summary>
        /// Checks if a skill is a self-cast skill.
        /// </summary>
        public static bool IsSelfSkill(int skillId)
        {
            return GetSkillType(skillId) == SkillType.Self;
        }

        private static Dictionary<int, string> BuildSkillSounds()
        {
            var map = new Dictionary<int, string>();

            // WARRIOR/KNIGHT SKILLS
            map[18] = "Sound/sKnightDefense.wav";        // Defense (ID 18)
            map[19] = "Sound/sKnightSkill1.wav";         // Falling Slash (ID 19)
            map[20] = "Sound/sKnightSkill2.wav";         // Lunge (ID 20)
            map[21] = "Sound/sKnightSkill3.wav";         // Uppercut (ID 21)
            map[22] = "Sound/sKnightSkill4.wav";         // Cyclone (ID 22)
            map[41] = "Sound/sKnightSkill4.wav";         // Twisting Slash (ID 41)

            // ELF SKILLS
            map[26] = "Sound/sKnightDefense.wav";        // Healing (ID 26)
            map[24] = "Sound/eRaidShoot.wav";            // Triple Shot (ID 24)

            // WIZARD SKILLS
            map[4] = "Sound/sMagic.wav";                 // Fireball (ID 4)
            map[11] = "Sound/sMagic.wav";                // Power Wave (ID 11)
            map[3] = "Sound/eThunder.wav";               // Lightning (ID 3)
            map[6] = "Sound/eTelekinesis.wav";           // Teleport (ID 6)
            map[2] = "Sound/eMeteorite.wav";             // Meteorite (ID 2)
            map[7] = "Sound/sIce.wav";                   // Ice (ID 7)
            map[1] = "Sound/sEvil.wav";                  // Poison (ID 1)
            map[5] = "Sound/sFlame.wav";                 // Flame (ID 5)
            map[8] = "Sound/sTornado.wav";               // Twister (ID 8)
            map[9] = "Sound/sEvil.wav";                  // Evil Spirits (ID 9)
            map[10] = "Sound/sHellFire.wav";             // Hellfire (ID 10)
            map[12] = "Sound/sAquaFlash.wav";            // Aqua Beam (ID 12)
            map[13] = "Sound/eExplosion.wav";            // Cometfall (ID 13)
            map[14] = "Sound/sFlame.wav";                // Inferno (ID 14)
            map[15] = "Sound/eTelekinesis.wav";          // Teleport Ally (ID 15)
            map[16] = "Sound/eSoulBarrier.wav";          // Soul Barrier (ID 16)
            map[17] = "Sound/sMagic.wav";                // Energy Ball (ID 17)

            // MIXED SKILLS
            map[52] = "Sound/ePiercing.wav";             // Penetration (ID 52)
            map[51] = "Sound/eIceArrow.wav";             // Ice Arrow (ID 51)
            map[49] = "Sound/sKnightSkill1.wav";         // Fire Breath (ID 49)
            map[47] = "Sound/eRidingSpear.wav";          // Impale (ID 47)
            map[48] = "Sound/eSwellLife.wav";            // Greater Fortitude (ID 48)
            map[56] = "Sound/eRaidShoot.wav";            // Raid (ID 56)

            // DARK LORD SKILLS
            map[62] = "Sound/sDarkEarthQuake.wav";       // Earthshake (ID 62)
            map[61] = "Sound/eFirebust.wav";             // Fire Burst (ID 61)
            map[78] = "Sound/Darklord_firescream.wav";   // Fire Scream (ID 78)

            // SUMMONER SKILLS
            map[214] = "Sound/SE_Ch_summoner_skill07_lifedrain.wav";      // Drain Life (ID 214)
            map[219] = "Sound/SE_Ch_summoner_skill03_sleep.wav";          // Sleep (ID 219)
            map[215] = "Sound/SE_Ch_summoner_skill08_chainlightning.wav"; // Chain Lightning (ID 215)
            map[65] = "Sound/sDarkElecSpike.wav";        // Electric Surge (ID 65)
            map[221] = "Sound/SE_Ch_summoner_weakness.wav";               // Weakness (ID 221)
            map[222] = "Sound/SE_Ch_summoner_innovation.wav";             // Innovation (ID 222)

            // MASTER SKILLS
            map[218] = "Sound/Berserker.wav";            // Berserker (ID 218)
            map[230] = "Sound/lightning_shock.wav";      // Lightning Shock (ID 230)
            map[237] = "Sound/gigantic_storm.wav";       // Gigantic Storm (ID 237)
            map[236] = "Sound/flame_strike.wav";         // Flame Strike (ID 236)
            map[238] = "Sound/caotic.wav";               // Chaotic Diseier (ID 238)
            map[233] = "Sound/SwellofMagicPower.wav";    // Swell of Magicpower (ID 233)
            map[232] = "Sound/BLOW_OF_DESTRUCTION.wav";  // Destruction (ID 232)
            map[235] = "Sound/multi_shot.wav";           // Multi-Shot (ID 235)
            map[234] = "Sound/recover.wav";              // Recovery (ID 234)

            // RAGE FIGHTER SKILLS
            map[263] = "Sound/Ragefighter/Rage_Darkside.wav";    // Darkside (ID 263)
            map[265] = "Sound/Ragefighter/Rage_Dragonlower.wav"; // Dragon Lore (ID 265)
            map[264] = "Sound/Ragefighter/Rage_Dragonkick.wav";  // Dragon Slayer (ID 264)
            map[270] = "Sound/Ragefighter/Rage_Giantswing.wav";  // Phoenix Shot (ID 270)
            map[269] = "Sound/Ragefighter/Rage_Stamp.wav";       // Charge (ID 269)

            return map;
        }
    }
}
