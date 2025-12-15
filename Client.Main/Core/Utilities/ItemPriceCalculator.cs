using Client.Main.Controls.UI.Game.Inventory;

namespace Client.Main.Core.Utilities
{
    /// <summary>
    /// Calculates item prices based on Season 6 ItemValue() logic from ZzzInfomation.cpp.
    /// </summary>
    public static class ItemPriceCalculator
    {
        private const long MAX_PRICE = 3_000_000_000L;

        public enum PriceMode
        {
            Buy = 0,
            Sell = 1,
            Repair = 2
        }

        public static int CalculateBuyPrice(InventoryItem item)
        {
            if (item == null) return 0;
            return (int)CalculatePrice(item, PriceMode.Buy);
        }

        public static int CalculateSellPrice(InventoryItem item)
        {
            if (item == null) return 0;
            return (int)CalculatePrice(item, PriceMode.Sell);
        }

        public static int CalculateRepairPrice(InventoryItem item, bool npcDiscount = true)
        {
            if (item == null) return 0;
            if (!IsRepairable(item)) return 0;
            return (int)CalculateRepairPriceInternal(item, npcDiscount);
        }

        /// <summary>
        /// Checks if an item can be repaired based on Season 6 repair ban list from ZzzInventory.cpp RepairAllGold()
        /// </summary>
        public static bool IsRepairable(InventoryItem item)
        {
            if (item == null) return false;

            var def = item.Definition;
            if (def == null) return false;

            // Items with no durability cannot be repaired
            if (def.BaseDurability == 0) return false;

            byte group = (byte)def.Group;
            short id = (short)def.Id;

            // Group 13 (Helpers/Pets/Misc) - most banned except Dark Horse/Raven
            if (group == 13)
            {
                // Dark Horse (4), Dark Raven (5), Horn of Fenrir (37) can be repaired
                if (id == 4 || id == 5) return true;
                // Everything else in group 13 is banned
                return false;
            }

            // Group 14 (Potions/Scrolls) - all banned
            if (group == 14) return false;

            // Bolts and Arrows
            if (group == 4 && (id == 7 || id == 15)) return false;

            // Orbs (group 12, id 7-11 roughly, need exact mapping)
            // Simplified: ban group 12 id 7-20 (orbs and some other banned items)
            if (group == 12 && id >= 7 && id <= 20) return false;

            // Wings 130-135 (season items)
            if (group == 12 && id >= 130 && id <= 135) return false;

            // If not in banned list, item can be repaired
            return true;
        }

        /// <summary>
        /// Calculates repair price using Season 6 formula from ZzzInventory.cpp
        /// </summary>
        private static long CalculateRepairPriceInternal(InventoryItem item, bool npcDiscount)
        {
            var def = item.Definition;
            if (def == null) return 0;

            int maxDur = def.BaseDurability;
            if (maxDur == 0) return 0;

            const long maximumBasePrice = 400_000_000;
            byte group = (byte)def.Group;
            short id = (short)def.Id;

            // Check if it's a trainable pet (Dark Horse, Dark Raven)
            bool isPet = (group == 13 && (id == 4 || id == 5 || id == 37));

            // Calculate base price (buying price)
            var basePrice = Math.Min(CalculatePrice(item, PriceMode.Buy) / (isPet ? 1 : 3), maximumBasePrice);
            basePrice = RoundPrice(basePrice);

            // Season 6 formula: 3.5 * sqrt(basePrice) * sqrt(sqrt(basePrice)) * missingDurabilityRatio + 1
            float squareRootOfBasePrice = (float)Math.Sqrt(basePrice);
            float squareRootOfSquareRoot = (float)Math.Sqrt(squareRootOfBasePrice);
            float missingDurability = 1.0f - ((float)item.Durability / maxDur);
            float repairPrice = (3.5f * squareRootOfBasePrice * squareRootOfSquareRoot * missingDurability) + 1.0f;

            // Penalty for destroyed items (durability = 0)
            if (item.Durability <= 0)
            {
                if (isPet)
                    repairPrice *= 2.0f;  // DestroyedPetPenalty
                else
                    repairPrice *= 1.4f;  // DestroyedItemPenalty
            }

            // No NPC discount means 2.5x more expensive
            if (!npcDiscount)
            {
                repairPrice *= 2.5f;
            }

            return RoundPrice((long)repairPrice);
        }

        private static long RoundPrice(long price)
        {
            if (price >= 1000)
                return price / 100 * 100;
            else if (price >= 100)
                return price / 10 * 10;
            return price;
        }

        private static long CalculatePrice(InventoryItem item, PriceMode mode)
        {
            var def = item.Definition;
            var details = item.Details;
            int currentDur = item.Durability;
            int maxDur = def?.BaseDurability ?? 0;

            if (def == null) return 0;

            byte group = (byte)def.Group;
            short id = (short)def.Id;
            int level = details.Level;

            // Count excellent options (each bit in excByte & 0x3F)
            int excellentCount = 0;
            if (item.RawData != null && item.RawData.Length > 3)
            {
                byte excByte = item.RawData[3];
                excellentCount = CountBits((byte)(excByte & 0x3F));
            }

            long gold;

            // 1) If ItemAttribute::iZen != 0 (Money or ItemValue in BMD)
            if (def.Money > 0)
            {
                gold = def.Money;
                if (mode != PriceMode.Buy)
                    gold /= 3;
                return FinalRounding(gold, mode);
            }
            if (def.ItemValue > 0)
            {
                gold = def.ItemValue;
                if (mode != PriceMode.Buy)
                    gold /= 3;
                return FinalRounding(gold, mode);
            }

            // 2) Calculate Level2 = DropLevel + upgradeLevel*3
            // OpenMU uses: dropLevel = definition.DropLevel + (item.Level * 3)
            // This matches the original formula where Level = DropLevel (not RequiredLevel)
            int upgradeLevel = details.Level; // +0 to +15
            int level2 = def.DropLevel + upgradeLevel * 3;

            // Add 25 if item has any excellent flags
            if (details.IsExcellent)
                level2 += 25;

            // 3) Hardcoded prices
            if (TryGetHardcodedPrice(group, id, level, currentDur, maxDur, out long hardcoded))
            {
                gold = hardcoded;
                // Hardcoded prices for sell mode (repair has its own method)
                if (mode == PriceMode.Sell)
                {
                    gold /= 3;
                }

                // Skip durability correction for specific items
                if (mode == PriceMode.Sell && !SkipDurabilityCorrection(group, id))
                {
                    gold = ApplyDurabilityCorrection(gold, currentDur, maxDur);
                }

                return FinalRounding(gold, mode);
            }

            // 4) p->Value branch (regular potions)
            if (def.ItemValue > 0 && IsRegularPotion(group, id))
            {
                gold = def.ItemValue * def.ItemValue * 10 / 12;

                // Large Healing/Mana potion override
                if (IsLargePotion(group, id))
                {
                    gold = 1500 * (level + 1);
                    if (maxDur > 0)
                        gold *= currentDur;
                    return gold; // Skip normal potion processing
                }

                // For ITEM_POTION..ITEM_ANTIDOTE types
                if (group >= 14 && group <= 14) // ITEM_POTION group
                {
                    if (level > 0)
                        gold *= (long)Math.Pow(2, level);

                    if (maxDur > 0)
                        gold = gold / 10 * 10 * currentDur / maxDur;

                    if (mode != PriceMode.Buy)
                    {
                        gold /= 3;
                        gold = gold / 10 * 10;
                    }
                    return gold;
                }
            }

            // 5) Special wings/capes/misc (Type==12/13/15 with exclusions)
            if (IsSpecialWingOrCape(group, id))
            {
                gold = 100 + (long)Math.Pow(level2, 3);

                // Add for AT_LIFE_REGENERATION options (not implemented in our structure)
                // gold += gold * SpecialValue for each option

                // Apply sell division (repair has its own method)
                if (mode == PriceMode.Sell)
                {
                    gold /= 3;
                }

                return FinalRounding(gold, mode);
            }

            // 6) Default equipment formula
            // Additional Level2 boost for upgrade level >=5
            if (upgradeLevel >= 5)
            {
                int[] boosts = [ 0, 0, 0, 0, 0, 4, 10, 25, 45, 65, 95, 135, 185, 245, 305, 365 ];
                if (upgradeLevel < boosts.Length)
                    level2 += boosts[upgradeLevel];
            }

            // Check if it's an old wing/cape (special formula)
            if (IsOldWingOrCape(group, id))
            {
                gold = 40_000_000L + (40 + level2) * (long)level2 * level2 * 11;
            }
            else
            {
                gold = 100L + (40 + level2) * (long)level2 * level2 / 8;
            }

            // Type 0..6 and not TwoHanded -> *0.8
            if (group >= 0 && group <= 6 && !def.TwoHanded)
                gold = gold * 80 / 100;

            // Apply option modifiers
            gold = ApplyOptionModifiers(gold, details, excellentCount);

            // Limit
            gold = Math.Min(gold, MAX_PRICE);

            // Division by 3 for sell (repair has its own method now)
            if (mode == PriceMode.Sell)
            {
                gold /= 3;
            }

            // Post-division hardcodes (Fenrir components, etc.) - would override here

            // Durability correction (only for goldType==1/Sell, with exceptions)
            if (mode == PriceMode.Sell && !SkipDurabilityCorrection(group, id))
            {
                gold = ApplyDurabilityCorrection(gold, currentDur, maxDur);
            }

            // Final rounding
            return FinalRounding(gold, mode);
        }

        private static bool TryGetHardcodedPrice(byte group, short id, int level, int currentDur, int maxDur, out long price)
        {
            price = 0;
            int dur = currentDur;

            switch (group, id)
            {
                // BOLT (group 4, id 7)
                case (4, 7):
                    int[] boltPrices = [ 100, 1400, 2200, 3000 ];
                    if (level < boltPrices.Length && maxDur > 0)
                        price = (long)boltPrices[level] * currentDur / maxDur;
                    else
                        price = 100;
                    return true;

                // ARROWS (group 4, id 15)
                case (4, 15):
                    int[] arrowPrices = [ 70, 1200, 2000, 2800 ];
                    if (level < arrowPrices.Length && maxDur > 0)
                        price = (long)arrowPrices[level] * currentDur / maxDur;
                    else
                        price = 70;
                    return true;

                // Jewels
                case (14, 13): price = 9_000_000; return true;     // Jewel of Bless
                case (14, 14): price = 6_000_000; return true;     // Jewel of Soul
                case (12, 15): price = 810_000; return true;       // Jewel of Chaos
                case (14, 16): price = 45_000_000; return true;    // Jewel of Life
                case (14, 22): price = 36_000_000; return true;    // Jewel of Creation
                case (14, 31): price = 60_000_000; return true;    // Jewel of Guardian

                // Potions +141/+142/+143/+144
                case (14, 141): price = 224_000 * 3; return true;
                case (14, 142): price = 182_000 * 3; return true;
                case (14, 143): price = 157_000 * 3; return true;
                case (14, 144): price = 121_000 * 3; return true;

                // Loch's Feather
                case (13, 14):
                    price = level == 0 ? 180_000 : 7_500_000;
                    return true;

                // Horn of Dinorant (base price, options not implemented)
                case (13, 3):
                    price = 960_000;
                    // + 300,000 for each AT_DAMAGE_ABSORB / AT_IMPROVE_AG_MAX / AT_IMPROVE_ATTACK_SPEED
                    return true;

                // Fruits
                case (13, 15): price = 33_000_000; return true;

                // Scroll of Archangel / Blood Bone (lvl1..8)
                case (13, 16):
                case (13, 17):
                    int[] scrollPrices = [ 0, 10_000, 50_000, 100_000, 300_000, 500_000, 800_000, 1_000_000, 1_200_000 ];
                    if (level > 0 && level < scrollPrices.Length)
                        price = scrollPrices[level];
                    return true;

                // Invisibility Cloak
                case (13, 18):  // Correct ID mapping
                    if (level == 1)
                        price = 150_000;
                    else
                        price = 600_000 + (60_000 * (level - 1));
                    return true;

                // Lost Map
                case (14, 28): price = 600_000; return true;

                // Symbol of Kundun
                case (14, 29): price = (long)dur * 30_000; return true;

                // Suspicious Scrap of Paper
                case (14, 101): price = (long)dur * 30_000; return true;

                // Gaion's Order
                case (14, 102): price = (long)dur * 30_000; return true;

                // Secromicon fragments (1st-6th)
                case (14, 103): // First
                case (14, 104): // Second
                case (14, 105): // Third
                case (14, 106): // Fourth
                case (14, 107): // Fifth
                case (14, 108): // Sixth
                case (14, 109): // Complete
                    price = (long)dur * 30_000;
                    return true;


                // Devil's Eye
                case (14, 17):
                    int[] eyePrices = [ 30_000, 10_000, 50_000, 100_000, 300_000, 500_000, 800_000, 1_000_000 ];
                    if (level < eyePrices.Length)
                        price = eyePrices[level];
                    return true;

                // Devil's Key
                case (14, 18):
                    int[] keyPrices = [ 30_000, 15_000, 75_000, 150_000, 450_000, 750_000, 1_200_000, 1_500_000 ];
                    if (level < keyPrices.Length)
                        price = keyPrices[level];
                    return true;

                // Devil's Invitation
                case (14, 19):  // Correct ID
                    if (level == 1)
                        price = 60_000;
                    else if (level == 2)
                        price = 84_000;
                    else if (level > 2)
                        price = (level - 1) * 60_000;
                    return true;

                // Old Scroll / Illusion Sorcerer Covenant / Scroll of Blood
                case (13, 49): // Old Scroll
                case (13, 50): // Illusion Sorcerer Covenant
                case (13, 51): // Scroll of Blood
                    if (level == 1)
                        price = 500_000;
                    else if (level >= 2 && level <= 6)
                        price = (level + 1) * 200_000;
                    return true;

                // Flame of Condor / Feather of Condor
                case (13, 52): price = 3_000_000; return true;
                case (13, 53): price = 3_000_000; return true;

                // Armor of Guardsman
                case (13, 29): price = 5_000; return true;

                // Christmas Star / Firecracker
                case (14, 51): price = 200_000; return true;  // Christmas Star
                case (14, 63): price = 200_000; return true;  // Firecracker

                // Cherry Blossom items
                case (14, 85): price = 300L * dur; return true;  // Wine
                case (14, 86): price = 300L * dur; return true;  // Rice Cake
                case (14, 87): price = 300L * dur; return true;  // Flower Petal
                case (14, 90): price = 300L * dur; return true;  // Golden Branch

                // Town Portal
                case (14, 10): price = 750; return true;

                // Siege Potion
                case (14, 62):
                    price = level == 0 ? 900_000L * dur : 450_000L * dur;
                    return true;

                // Helper +7
                case (13, 7):
                    price = level == 0 ? 1_500_000 : 1_200_000;
                    return true;

                // Life Stone
                case (13, 11):
                    price = level == 0 ? 100_000 : 2_400_000;
                    return true;

                // Shield Potions (small/med/large)
                case (14, 35): price = 2_000L * dur; return true;
                case (14, 36): price = 4_000L * dur; return true;
                case (14, 37): price = 6_000L * dur; return true;

                // Complex Potions (small/med/large)
                case (14, 38): price = 2_500L * dur; return true;
                case (14, 39): price = 5_000L * dur; return true;
                case (14, 40): price = 7_500L * dur; return true;

                // Potion +100
                case (14, 100): price = 100L * 3 * dur; return true;

                // Remedy of Love
                case (14, 20): price = 900; return true;

                // Rena
                case (14, 21):
                    price = (level == 3) ? dur * 3_900 : 9_000;
                    return true;

                // Ale
                case (14, 9): price = 750; return true;

                // Spirit Pet
                case (13, 31):
                    price = (level == 0) ? 30_000_000 : (level == 1) ? 15_000_000 : 0;
                    return true;

                // Wizard's Ring
                case (13, 20):
                    price = (level == 0) ? 30_000 : 0;
                    return true;

                // Gem of Secret
                case (12, 26):
                    price = (level == 0) ? 60_000 : 0;
                    return true;

                // Fenrir components
                case (13, 32): price = 150 * dur; return true;       // Splinter of Armor
                case (13, 33): price = 300 * dur; return true;       // Bless of Guardian
                case (13, 34): price = 3_000 * dur; return true;     // Claw of Beast
                case (13, 35): price = 30_000; return true;          // Fragment of Horn
                case (13, 36): price = 90_000; return true;          // Broken Horn
                case (13, 37): price = 150_000; return true;         // Horn of Fenrir

                // Halloween
                case (14, 45):
                case (14, 46):
                case (14, 47):
                case (14, 48):
                case (14, 49):
                case (14, 50):
                    price = 150L * dur;
                    return true;

                // Helper 71-75
                case (13, 71):
                case (13, 72):
                case (13, 73):
                case (13, 74):
                case (13, 75):
                    price = 2_000_000;
                    return true;

                // Fixed 1,000 zen items
                case (14, 112):
                case (14, 113):
                case (14, 121):
                case (14, 122):
                case (14, 123):
                case (14, 124):
                case (13, 80):  // Pet Panda
                case (13, 81):  // Panda Ring
                // case (13, 32):  // Demon
                // case (13, 33):  // Spirit of Guardian
                case (13, 109):
                case (13, 110):
                case (13, 111):
                case (13, 112):
                case (13, 113):
                case (13, 114):
                case (13, 115):
                    price = 1_000;
                    return true;

                // Skeleton Ring/Pet
                case (13, 38):
                case (13, 9):
                    price = 2_000;
                    return true;

                // Wings 130-135
                case (12, 130):
                case (12, 131):
                case (12, 132):
                case (12, 133):
                case (12, 134):
                case (12, 135):
                    price = 80;
                    return true;
            }

            return false;
        }

        private static bool IsRegularPotion(byte group, short id)
        {
            // Regular potions that use p->Value formula
            return group == 14 && id >= 0 && id <= 9;
        }

        private static bool IsLargePotion(byte group, short id)
        {
            // Large Healing Potion (14,2) or Large Mana Potion (14,5)
            return group == 14 && (id == 2 || id == 5);
        }

        private static bool IsSpecialWingOrCape(byte group, short id)
        {
            // (Type==12 && ip->Type > ITEM_WINGS_OF_DARKNESS && not in Wing of Storm..Dimension && != Cape of Overrule)
            // || Type==13 || Type==15
            // This is complex - simplified version
            if (group == 13 || group == 15)
                return true;

            // Type 12 special cases
            if (group == 12 && id > 6 && !IsOldWingOrCape(group, id))
                return true;

            return false;
        }

        private static bool IsOldWingOrCape(byte group, short id)
        {
            // Wings <= Wings of Darkness (0-6), Cape of Lord, Wings of Storm..Dimension, Cape of Overrule
            // Specific wing IDs that use the 40M base formula
            if (group == 12)
            {
                // Wings of Elf, Heaven, Satan, Warrior, Destruction, Spirits, Darkness
                if (id >= 0 && id <= 6)
                    return true;

                // Cape of Lord
                if (id == 30)
                    return true;

                // Wings of Storm..Dimension (36-43)
                if (id >= 36 && id <= 43)
                    return true;

                // Cape of Overrule
                if (id == 50)
                    return true;

                // Cape of Fighter/Emperor (49, ?)
                if (id == 49)
                    return true;
            }

            return false;
        }

        private static long ApplyOptionModifiers(long gold, ItemDatabase.ItemDetails details, int excellentCount)
        {
            // FIXED: Apply options in correct order as per OpenMU implementation

            // 1. Skill options (+150%)
            // NOTE: OpenMU excludes certain "worthless" skills (ForceWave=66, Explosion=223, Requiem=224, Pollution=225)
            // but we don't have access to skill ID in current structure, so all skills get +150%
            if (details.HasSkill)
            {
                gold += (long)(gold * 1.5);
            }

            // 2. Luck (+25%)
            if (details.HasLuck)
            {
                gold += gold * 25 / 100;
            }

            // 3. Blue option level (blocking, additional dmg, etc.)
            // OpenMU formula: level 1 = +60%, else = +70% * 2^(level-1)
            int optionLevel = details.OptionLevel;
            switch (optionLevel)
            {
                case 0:
                    break;
                case 1:
                    gold += (long)(gold * 0.6);
                    break;
                default:
                    gold += (long)(gold * 0.7 * Math.Pow(2, optionLevel - 1));
                    break;
            }

            // 4. Wing options (+25% each) - not available in current structure
            // TODO: Add wing option counting when structure is available

            // 5. Excellent options (doubles for each)
            for (int i = 0; i < excellentCount; i++)
            {
                gold += gold; // Double for each excellent option
            }

            // 6. Guardian option (+16%) - not available in current structure
            // TODO: Add guardian check when structure is available

            return gold;
        }

        private static int CountBits(byte value)
        {
            int count = 0;
            while (value != 0)
            {
                count++;
                value &= (byte)(value - 1); // Clear the lowest set bit
            }
            return count;
        }

        private static bool SkipDurabilityCorrection(byte group, short id)
        {
            // Items that skip durability correction (go straight to EXIT_CALCULATE)
            // Includes: transformation ring, wizard's ring, armor of guardsman, bolt, arrows,
            // all types >= ITEM_POTION, orbs, quest items, etc.

            // Group 13 (misc/pets/events)
            if (group == 13)
                return true;

            // Group 14 (potions/scrolls)
            if (group == 14)
                return true;

            // Specific items
            if (group == 4 && (id == 7 || id == 15)) // Bolt, Arrows
                return true;

            // Wings 130-135
            if (group == 12 && id >= 130 && id <= 135)
                return true;

            return false;
        }

        private static long ApplyDurabilityCorrection(long gold, int currentDur, int maxDur)
        {
            // Gold -= Gold*0.6*(1 - Durability/maxDur)
            if (maxDur > 0)
            {
                double durRatio = (double)currentDur / maxDur;
                double correction = gold * 0.6 * (1.0 - durRatio);
                gold -= (long)correction;
            }

            return Math.Max(0, gold);
        }

        private static long FinalRounding(long gold, PriceMode mode)
        {
            // Final rounding: >=1000 -> /100*100, >=100 -> /10*10
            if (gold >= 1000)
                return gold / 100 * 100;
            else if (gold >= 100)
                return gold / 10 * 10;

            return Math.Max(0, Math.Min(gold, int.MaxValue));
        }
    }
}
