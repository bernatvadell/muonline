// File: Client.Main/Objects/Player/PlayerEquipment.cs
using Client.Main.Models;
using Client.Main.Controls.UI.Game.Inventory;
using MUnique.OpenMU.Network.Packets;

namespace Client.Main.Objects.Player
{
    public enum WeaponType
    {
        None = 0,
        Sword = 1,
        TwoHandSword = 2,
        Spear = 3,
        Bow = 4,
        Crossbow = 5,
        Staff = 6,
        Scepter = 7,
        Shield = 8,
        Scythe = 9,
        Fist = 10, // Rage Fighter
        Book = 11  // Summoner
    }

    public class PlayerEquipment
    {
        public WeaponType MainWeapon { get; set; } = WeaponType.None;
        public WeaponType OffHand { get; set; } = WeaponType.None;
        
        // Na przyszłość - pełne wyposażenie
        public byte[] Helmet { get; set; } = new byte[0];
        public byte[] Armor { get; set; } = new byte[0];
        public byte[] Pants { get; set; } = new byte[0];
        public byte[] Gloves { get; set; } = new byte[0];
        public byte[] Boots { get; set; } = new byte[0];
        public byte[] Wings { get; set; } = new byte[0];
        
        /// <summary>
        /// Returns the actual equipped weapon type based on item definitions
        /// </summary>
        public WeaponType GetEquippedWeaponType(ItemDefinition leftHandItem, ItemDefinition rightHandItem, byte leftHandItemGroup, byte rightHandItemGroup)
        {
            // Check right hand (Main weapon)
            if (rightHandItem != null)
            {
                var weaponType = GetWeaponTypeFromItemGroup(rightHandItemGroup, rightHandItem);
                if (weaponType != WeaponType.None)
                {
                    // Check if it's a two-handed weapon
                    if (rightHandItem.TwoHanded)
                    {
                        return weaponType == WeaponType.Sword ? WeaponType.TwoHandSword : weaponType;
                    }
                    return weaponType;
                }
            }

            // Check left hand first (secondary weapon/shield)
            if (leftHandItem != null)
            {
                var weaponType = GetWeaponTypeFromItemGroup(leftHandItemGroup, leftHandItem);
                if (weaponType != WeaponType.None)
                {
                    return weaponType;
                }
            }
            
            return WeaponType.None;
        }
        
        /// <summary>
        /// Converts item group to weapon type
        /// </summary>
        private static WeaponType GetWeaponTypeFromItemGroup(byte group, ItemDefinition itemDef)
        {
            return group switch
            {
                0 => WeaponType.Sword,           // ITEM_GROUP_SWORD
                1 => WeaponType.TwoHandSword,    // ITEM_GROUP_AXE (axes)
                2 => WeaponType.Scepter,         // ITEM_GROUP_MACE_SCEPTER (mace/hammer/scepter)
                3 => WeaponType.Spear,           // ITEM_GROUP_SPEAR
                4 => GetBowOrCrossbow(itemDef),  // ITEM_GROUP_BOW_CROSSBOW
                5 => GetStaffOrBook(itemDef),    // ITEM_GROUP_STAFF (staff/stick/book)
                6 => WeaponType.Shield,          // ITEM_GROUP_SHIELD
                _ => WeaponType.None
            };
        }
        
        /// <summary>
        /// Determines if item is bow or crossbow based on item name
        /// </summary>
        private static WeaponType GetBowOrCrossbow(ItemDefinition itemDef)
        {
            if (itemDef?.Name == null) return WeaponType.Bow; // Default to bow
            
            string name = itemDef.Name.ToLowerInvariant();
            if (name.Contains("crossbow") || name.Contains("kusza"))
                return WeaponType.Crossbow;
            
            return WeaponType.Bow;
        }
        
        /// <summary>
        /// Determines if item is staff or book based on item name
        /// </summary>
        private static WeaponType GetStaffOrBook(ItemDefinition itemDef)
        {
            if (itemDef?.Name == null) return WeaponType.Staff; // Default to staff
            
            string name = itemDef.Name.ToLowerInvariant();
            if (name.Contains("book") || name.Contains("księga") || name.Contains("ksiega"))
                return WeaponType.Book;
            
            return WeaponType.Staff;
        }
        
        /// <summary>
        /// Determines weapon type based on character class (fallback when no weapon equipped)
        /// </summary>
        public WeaponType GetDefaultWeaponTypeForClass(CharacterClassNumber characterClass)
        {
            return characterClass switch
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
        }
    }
}