#nullable enable
using System;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Models;
using Microsoft.Xna.Framework;

namespace Client.Main.Controls.UI.Game.Buffs
{
    /// <summary>
    /// Single buff slot display - shows buff icon and basic info.
    /// </summary>
    public class BuffSlotControl : UIControl
    {
        private ActiveBuffState? _buff;
        private readonly LabelControl _buffIdLabel;

        public const int SLOT_SIZE = 36;

        public ActiveBuffState? Buff
        {
            get => _buff;
            set
            {
                _buff = value;
                UpdateDisplay();
            }
        }

        public BuffSlotControl()
        {
            AutoViewSize = false;
            ControlSize = new Point(SLOT_SIZE, SLOT_SIZE);
            ViewSize = ControlSize;
            Interactive = false;

            // Buff ID/Name label (centered)
            _buffIdLabel = new LabelControl
            {
                Text = "",
                TextColor = Color.White,
                X = 2,
                Y = 8,
                ViewSize = new Point(SLOT_SIZE - 4, SLOT_SIZE - 4),
                TextAlign = HorizontalAlign.Center,
                Scale = 0.65f
            };
            Controls.Add(_buffIdLabel);

            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            if (_buff != null)
            {
                string buffName = GetBuffName(_buff.EffectId);
                _buffIdLabel.Text = buffName;
                _buffIdLabel.Visible = true;
                _buffIdLabel.TextColor = Color.Cyan;

                // Subtle background
                BackgroundColor = new Color(0, 100, 150) * 0.6f;
                BorderColor = Color.Transparent;
                BorderThickness = 0;
            }
            else
            {
                _buffIdLabel.Visible = false;
                BackgroundColor = Color.Transparent;
                BorderColor = Color.Transparent;
                BorderThickness = 0;
            }
        }

        private string GetBuffName(byte effectId)
        {
            // Map effect IDs to readable names based on eBuffState enum
            return effectId switch
            {
                // Buffs
                1 => "ATK+",     // eBuff_Attack
                2 => "DEF+",     // eBuff_Defense
                3 => "NPC",      // eBuff_HelpNpc
                4 => "WIZD",     // eBuff_WizDefense
                5 => "CRIT",     // eBuff_AddCriticalDamage
                6 => "INF",      // eBuff_InfinityArrow
                7 => "AG+",      // eBuff_AddAG
                8 => "LIFE",     // eBuff_Life
                9 => "MP+",      // eBuff_AddMana
                10 => "BLESS",   // eBuff_BlessPotion
                11 => "SOUL",    // eBuff_SoulPotion
                12 => "REM",     // eBuff_RemovalMagic
                18 => "CLOAK",   // eBuff_Cloaking
                19 => "+SKL",    // eBuff_AddSkill
                20 => "CROWN",   // eBuff_CastleCrown
                28 => "GM",      // eBuff_GMEffect
                29 => "PC1",     // eBuff_PcRoomSeal1
                30 => "PC2",     // eBuff_PcRoomSeal2
                31 => "PC3",     // eBuff_PcRoomSeal3
                32 => "QUICK",   // eBuff_CursedTempleQuickness
                33 => "SUBL",    // eBuff_CursedTempleSublimation
                34 => "PROT",    // eBuff_CursedTempleProdection
                40 => "SEAL1",   // eBuff_Seal1
                41 => "SEAL2",   // eBuff_Seal2
                42 => "SEAL3",   // eBuff_Seal3
                43 => "SEAL4",   // eBuff_Seal4
                44 => "ELI1",    // eBuff_EliteScroll1
                45 => "ELI2",    // eBuff_EliteScroll2
                46 => "ELI3",    // eBuff_EliteScroll3
                47 => "ELI4",    // eBuff_EliteScroll4
                48 => "ELI5",    // eBuff_EliteScroll5
                49 => "ELI6",    // eBuff_EliteScroll6
                50 => "SEC1",    // eBuff_SecretPotion1
                51 => "SEC2",    // eBuff_SecretPotion2
                52 => "SEC3",    // eBuff_SecretPotion3
                53 => "SEC4",    // eBuff_SecretPotion4
                54 => "SEC5",    // eBuff_SecretPotion5

                // DeBuffs
                55 => "POI",     // eDeBuff_Poison
                56 => "FRZE",    // eDeBuff_Freeze
                57 => "HARD",    // eDeBuff_Harden
                58 => "DEF-",    // eDeBuff_Defense
                59 => "ATK-",    // eDeBuff_Attack
                60 => "MAG-",    // eDeBuff_MagicPower
                61 => "STUN",    // eDeBuff_Stun
                62 => "INVM",    // eDeBuff_InvincibleMagic
                63 => "INVMA",   // eDeBuff_InvincibleMagicAttack
                64 => "INVP",    // eDeBuff_InvinciblePhysAttack
                65 => "REST",    // eDeBuff_CursedTempleRestraint

                71 => "THRN",    // eBuff_Thorns
                72 => "SLEP",    // eDeBuff_Sleep
                73 => "BLND",    // eDeBuff_Blind
                74 => "NEIL",    // eDeBuff_NeilDOT
                75 => "SAHA",    // eDeBuff_SahamuttDOT
                76 => "ATK↓",    // eDeBuff_AttackDown
                77 => "DEF↓",    // eDeBuff_DefenseDown
                78 => "CHER1",   // eBuff_CherryBlossom_Liguor
                79 => "CHER2",   // eBuff_CherryBlossom_RiceCake
                80 => "CHER3",   // eBuff_CherryBlossom_Petal
                81 => "BERK",    // eBuff_Berserker
                82 => "SWELL",   // eBuff_SwellOfMagicPower
                83 => "FLME",    // eDeBuff_FlameStrikeDamage
                84 => "STRM",    // eDeBuff_GiganticStormDamage
                85 => "LGHT",    // eDeBuff_LightningShockDamage
                86 => "BLOW",    // eDeBuff_BlowOfDestruction
                87 => "HP↑",     // eBuff_Seal_HpRecovery
                88 => "MP↑",     // eBuff_Seal_MpRecovery
                89 => "BATL",    // eBuff_Scroll_Battle
                90 => "STRN",    // eBuff_Scroll_Strengthen
                91 => "XMAS",    // eBuff_BlessingOfXmax
                92 => "CURE",    // eBuff_CureOfSanta
                93 => "SAFE",    // eBuff_SafeGuardOfSanta
                94 => "STR+",    // eBuff_StrengthOfSanta
                95 => "DEF+",    // eBuff_DefenseOfSanta
                96 => "SPD+",    // eBuff_QuickOfSanta
                97 => "LUCK",    // eBuff_LuckOfSanta
                98 => "DUEL",    // eBuff_DuelWatch
                99 => "GCHM",    // eBuff_GuardCharm
                100 => "ICHM",   // eBuff_ItemGuardCharm
                101 => "ASCE",   // eBuff_AscensionSealMaster
                102 => "WLTH",   // eBuff_WealthSealMaster
                103 => "GLDR",   // eBuff_HonorOfGladiator
                105 => "DOPP",   // eBuff_Doppelganger_Ascension
                112 => "EXP+",   // eBuff_PartyExpBonus
                113 => "AG++",   // eBuff_AG_Addition
                114 => "SD+",    // eBuff_SD_Addition
                119 => "WLT2",   // eBuff_NewWealthSeal
                120 => "STAM",   // eDeBuff_Discharge_Stamina
                121 => "HEAL",   // eBuff_Scroll_Healing
                122 => "HAWK",   // EFFECT_HAWK_FIGURINE
                123 => "GOAT",   // EFFECT_GOAT_FIGURINE
                124 => "OAK",    // EFFECT_OAK_CHARM
                125 => "MAPL",   // EFFECT_MAPLE_CHARM
                126 => "GOAK",   // EFFECT_GOLDEN_OAK_CHARM
                127 => "GMAP",   // EFFECT_GOLDEN_MAPLE_CHARM
                128 => "HORS",   // EFFECT_WORN_HORSESHOE
                129 => "OATK",   // eBuff_Att_up_Ourforces
                130 => "OHP",    // eBuff_Hp_up_Ourforces
                131 => "ODEF",   // eBuff_Def_up_Ourforces
                134 => "IRON",   // EFFECT_IRON_DEFENSE
                135 => "LIFE+",  // EFFECT_GREATER_LIFE_ENHANCED
                136 => "LIFE*",  // EFFECT_GREATER_LIFE_MASTERED
                137 => "DTHS",   // EFFECT_DEATH_STAB_ENHANCED
                138 => "MCIR",   // EFFECT_MAGIC_CIRCLE_IMPROVED
                139 => "MCIR+",  // EFFECT_MAGIC_CIRCLE_ENHANCED
                140 => "MSHD",   // EFFECT_MANA_SHIELD_MASTERED
                141 => "FSTB",   // EFFECT_FROZEN_STAB_MASTERED
                142 => "BLS",    // EFFECT_BLESS
                143 => "INF+",   // EFFECT_INFINITY_ARROW_IMPROVED
                144 => "BLN+",   // EFFECT_BLIND_IMPROVED
                145 => "DRNL",   // EFFECT_DRAIN_LIFE_ENHANCED
                146 => "ISTO",   // EFFECT_ICE_STORM_ENHANCED
                147 => "EPRS",   // EFFECT_EARTH_PRISON
                148 => "CRT*",   // EFFECT_GREATER_CRITICAL_DAMAGE_MASTERED
                149 => "CRT+",   // EFFECT_GREATER_CRITICAL_DAMAGE_EXTENDED
                150 => "SWD",    // EFFECT_SWORD_POWER_IMPROVED
                151 => "SWD+",   // EFFECT_SWORD_POWER_ENHANCED
                152 => "SWD*",   // EFFECT_SWORD_POWER_MASTERED
                153 => "DFSR",   // EFFECT_GREATER_DEFENSE_SUCCESS_RATE_IMPROVED
                154 => "DFSR+",  // EFFECT_GREATER_DEFENSE_SUCCESS_RATE_ENHANCED
                155 => "FIT",    // EFFECT_FITNESS_IMPROVED
                157 => "DRGN",   // EFFECT_DRAGON_ROAR_ENHANCED
                158 => "CHND",   // EFFECT_CHAIN_DRIVER_ENHANCED
                159 => "POIA",   // EFFECT_POISON_ARROW
                160 => "POIA+",  // EFFECT_POISON_ARROW_IMPROVED
                161 => "BLS+",   // EFFECT_BLESS_IMPROVED
                162 => "DMG-",   // EFFECT_LESSER_DAMAGE_IMPROVED
                163 => "DEF-",   // EFFECT_LESSER_DEFENSE_IMPROVED
                164 => "FRSL",   // EFFECT_FIRE_SLASH_ENHANCED
                165 => "IRON+",  // EFFECT_IRON_DEFENSE_IMPROVED
                166 => "BHWL",   // EFFECT_BLOOD_HOWLING
                167 => "BHWL+",  // EFFECT_BLOOD_HOWLING_IMPROVED
                174 => "SD/2",   // EFFECT_PENTAGRAM_JEWEL_HALF_SD
                175 => "MP/2",   // EFFECT_PENTAGRAM_JEWEL_HALF_MP
                176 => "SPD/2",  // EFFECT_PENTAGRAM_JEWEL_HALF_SPEED
                177 => "HP/2",   // EFFECT_PENTAGRAM_JEWEL_HALF_HP
                178 => "PSTUN",  // EFFECT_PENTAGRAM_JEWEL_STUN
                179 => "FIRE",   // EFFECT_ARCA_FIRETOWER
                180 => "WATR",   // EFFECT_ARCA_WATERTOWER
                181 => "ERTH",   // EFFECT_ARCA_EARTHTOWER
                182 => "WIND",   // EFFECT_ARCA_WINDTOWER
                183 => "DARK",   // EFFECT_ARCA_DARKNESSTOWER
                184 => "DPTH",   // EFFECT_ARCA_DEATHPENALTY
                186 => "SLOW",   // EFFECT_PENTAGRAM_JEWEL_SLOW
                187 => "ARCH",   // EFFECT_ARCA_ARCHERONBUFF
                190 => "TAL1",   // EFFECT_TALISMAN_OF_ASCENSION1
                191 => "TAL2",   // EFFECT_TALISMAN_OF_ASCENSION2
                192 => "TAL3",   // EFFECT_TALISMAN_OF_ASCENSION3
                193 => "SEL3",   // EFFECT_SEAL_OF_ASCENSION3
                194 => "MSEL",   // EFFECT_MASTER_SEAL_OF_ASCENSION2
                195 => "BLGT",   // EFFECT_BLESSING_OF_LIGHT
                196 => "MDEF",   // EFFECT_MASTER_SCROLL_OF_DEFENSE
                197 => "MMAG",   // EFFECT_MASTER_SCROLL_OF_MAGIC_DAMAGE
                198 => "MLIF",   // EFFECT_MASTER_SCROLL_OF_LIFE
                199 => "MMAN",   // EFFECT_MASTER_SCROLL_OF_MANA
                200 => "MDMG",   // EFFECT_MASTER_SCROLL_OF_DAMAGE
                201 => "MHEAL",  // EFFECT_MASTER_SCROLL_OF_HEALING
                202 => "MBAT",   // EFFECT_MASTER_SCROLL_OF_BATTLE
                203 => "MSTR",   // EFFECT_MASTER_SCROLL_OF_STRENGTH
                204 => "MQCK",   // EFFECT_MASTER_SCROLL_OF_QUICK

                _ => $"E{effectId}" // Generic format for unknown effects
            };
        }
    }
}
