using System.Text;
using MUnique.OpenMU.Network.Packets; // For CharacterClassNumber
using Client.Main.Core.Utilities;
using System; // For ItemDatabase, DateTime, ReadOnlyMemory, ReadOnlySpan, Convert

namespace Client.Main.Core.Models
{
    /// <summary>
    /// Enumeration defining the types of objects that can be in the scope of the player.
    /// </summary>
    public enum ScopeObjectType
    {
        Player, // Represents another player character
        Npc, // Represents a Non-Player Character, like a shop keeper or quest giver
        Monster, // Represents a Monster, an enemy character
        Item, // Represents an Item dropped on the ground
        Money // Represents Zen, the in-game currency, dropped on the ground
    }

    /// <summary>
    /// Abstract base class for all objects that can be in the player's scope.
    /// Provides common properties and methods for all scope objects.
    /// </summary>
    public abstract class ScopeObject
    {
        // Properties

        /// <summary>
        /// Gets the masked identifier of the scope object. This ID is used as a key in the scope management.
        /// </summary>
        public ushort Id { get; init; } // MASKED ID (used as key for efficient lookup in scope)

        /// <summary>
        /// Gets the original raw identifier of the scope object as received from the server.
        /// </summary>
        public ushort RawId { get; init; } // Original Raw ID from server (unmasked)

        /// <summary>
        /// Gets or sets the X-coordinate of the object's position on the map.
        /// </summary>
        public byte PositionX { get; set; }

        /// <summary>
        /// Gets or sets the Y-coordinate of the object's position on the map.
        /// </summary>
        public byte PositionY { get; set; }

        /// <summary>
        /// Gets the type of the scope object. Must be implemented by derived classes.
        /// </summary>
        public abstract ScopeObjectType ObjectType { get; }

        /// <summary>
        /// Gets or sets the timestamp of the last update received for this scope object.
        /// </summary>
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;

        // Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeObject"/> class.
        /// </summary>
        /// <param name="maskedId">The masked identifier of the scope object.</param>
        /// <param name="rawId">The raw identifier of the scope object.</param>
        /// <param name="x">The X-coordinate of the object's position.</param>
        /// <param name="y">The Y-coordinate of the object's position.</param>
        protected ScopeObject(ushort maskedId, ushort rawId, byte x, byte y)
        {
            Id = maskedId;
            RawId = rawId;
            PositionX = x;
            PositionY = y;
        }

        // Methods

        /// <inheritdoc />
        public override string ToString()
        {
            return $"ID: {Id:X4} (Raw: {RawId:X4}) ({ObjectType}) at [{PositionX},{PositionY}]";
        }
    }

    /// <summary>
    /// Represents a player character object within the scope.
    /// Inherits from <see cref="ScopeObject"/> and adds player-specific properties.
    /// </summary>
    public class PlayerScopeObject : ScopeObject
    {
        // Properties
        public string Name { get; set; }
        public CharacterClassNumber Class { get; set; } // Player's character class.

        public override ScopeObjectType ObjectType => ScopeObjectType.Player;

        // Constructors
        public PlayerScopeObject(ushort maskedId, ushort rawId,
                                 byte x, byte y, string name,
                                 CharacterClassNumber cls = CharacterClassNumber.DarkWizard)
            : base(maskedId, rawId, x, y)
        {
            Name = name;
            Class = cls;
        }

        // Methods
        public override string ToString()
            => $"ID: {Id:X4} ({RawId:X4})  Player: {Name}  Class: {Class}  @[{PositionX},{PositionY}]";
    }

    /// <summary>
    /// Represents a Non-Player Character (NPC) or Monster object within the scope.
    /// Inherits from <see cref="ScopeObject"/> and adds NPC-specific properties.
    /// </summary>
    public class NpcScopeObject : ScopeObject // Also used for Monsters, as they share similar properties
    {
        // Properties

        /// <summary>
        /// Gets or sets the name of the NPC, can be null or empty if the NPC has no specific name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the type number of the NPC, identifying its specific kind.
        /// </summary>
        public ushort TypeNumber { get; set; }

        /// <inheritdoc />
        public override ScopeObjectType ObjectType => ScopeObjectType.Npc; // Could differentiate based on TypeNumber range if needed (e.g., for monsters vs NPCs)

        // Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="NpcScopeObject"/> class.
        /// </summary>
        /// <param name="maskedId">The masked identifier of the NPC scope object.</param>
        /// <param name="rawId">The raw identifier of the NPC scope object.</param>
        /// <param name="x">The X-coordinate of the NPC's position.</param>
        /// <param name="y">The Y-coordinate of the NPC's position.</param>
        /// <param name="typeNumber">The type number of the NPC.</param>
        /// <param name="name">The optional name of the NPC.</param>
        public NpcScopeObject(ushort maskedId, ushort rawId, byte x, byte y, ushort typeNumber, string name = null)
            : base(maskedId, rawId, x, y)
        {
            TypeNumber = typeNumber;
            Name = name;
        }

        // Methods

        /// <inheritdoc />
        public override string ToString()
        {
            // Use NpcDatabase to get the name/designation
            string identifier = NpcDatabase.GetNpcName(this.TypeNumber);
            return $"ID: {Id:X4} (Raw: {RawId:X4}) (NPC: {identifier}) at [{PositionX},{PositionY}]";
        }
    }

    /// <summary>
    /// Represents an Item object dropped on the ground within the scope.
    /// Inherits from <see cref="ScopeObject"/> and adds item-specific properties.
    /// </summary>
    public class ItemScopeObject : ScopeObject
    {
        // Properties

        /// <summary>
        /// Gets the description of the item, usually its name and basic attributes.
        /// </summary>
        public string ItemDescription { get; }

        /// <summary>
        /// Gets the original item data as received from the server.
        /// </summary>
        public ReadOnlyMemory<byte> ItemData { get; } // Store original data for potential future use

        /// <inheritdoc />
        public override ScopeObjectType ObjectType => ScopeObjectType.Item;

        // Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ItemScopeObject"/> class.
        /// </summary>
        /// <param name="maskedId">The masked identifier of the item scope object.</param>
        /// <param name="rawId">The raw identifier of the item scope object.</param>
        /// <param name="x">The X-coordinate of the item's position.</param>
        /// <param name="y">The Y-coordinate of the item's position.</param>
        /// <param name="itemData">The raw data of the item.</param>
        public ItemScopeObject(ushort maskedId, ushort rawId, byte x, byte y, ReadOnlySpan<byte> itemData)
            : base(maskedId, rawId, x, y)
        {
            ItemData = itemData.ToArray(); // Store a copy of the item data to prevent external modification
            ItemDescription = ItemDatabase.GetItemName(ItemData.Span) ?? $"Unknown (Data: {Convert.ToHexString(ItemData.Span)})"; // Get item name from database or show raw data if name is not found
        }

        // Methods

        /// <inheritdoc />
        public override string ToString()
        {
            return $"ID: {Id:X4} (Raw: {RawId:X4}) (Item: {ItemDescription}) at [{PositionX},{PositionY}]";
        }
    }

    /// <summary>
    /// Represents a Money (Zen) object dropped on the ground within the scope.
    /// Inherits from <see cref="ScopeObject"/> and adds money-specific properties.
    /// </summary>
    public class MoneyScopeObject : ScopeObject
    {
        // Properties

        /// <summary>
        /// Gets or sets the amount of money (Zen) this object represents.
        /// </summary>
        public uint Amount { get; set; }

        /// <inheritdoc />
        public override ScopeObjectType ObjectType => ScopeObjectType.Money;

        // Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MoneyScopeObject"/> class.
        /// </summary>
        /// <param name="maskedId">The masked identifier of the money scope object.</param>
        /// <param name="rawId">The raw identifier of the money scope object.</param>
        /// <param name="x">The X-coordinate of the money's position.</param>
        /// <param name="y">The Y-coordinate of the money's position.</param>
        /// <param name="amount">The amount of money.</param>
        public MoneyScopeObject(ushort maskedId, ushort rawId, byte x, byte y, uint amount)
            : base(maskedId, rawId, x, y)
        {
            Amount = amount;
        }

        // Methods

        /// <inheritdoc />
        public override string ToString()
        {
            return $"ID: {Id:X4} (Raw: {RawId:X4}) (Money: {Amount}) at [{PositionX},{PositionY}]";
        }
    }
}