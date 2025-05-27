// File: Client.Main/Objects/Player/PlayerEquipment.cs
using Client.Main.Models;
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
        /// Determines weapon type based on character class (temporary until we have real equipment)
        /// </summary>
        public WeaponType GetWeaponTypeForClass(CharacterClassNumber characterClass)
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