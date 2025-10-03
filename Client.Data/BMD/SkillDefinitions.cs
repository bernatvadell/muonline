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
            { 16, SkillType.Self },    // Soul Barrier
            { 17, SkillType.Target },  // Energy Ball
            { 18, SkillType.Self },    // Defense
            { 19, SkillType.Area },    // Falling Slash
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
            { 38, SkillType.Target },  // Decay
            { 39, SkillType.Area },    // Ice Storm
            { 40, SkillType.Target },  // Nova
            { 41, SkillType.Area },    // Twisting Slash
            { 42, SkillType.Target },  // Rageful Blow
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
            { 232, SkillType.Target }, // Strike of Destruction
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
            Add(19, 65);    // Falling Slash → PlayerAttackSkillWheel
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
    }
}
