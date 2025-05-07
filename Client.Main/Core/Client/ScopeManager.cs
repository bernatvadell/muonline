using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using Client.Main.Core.Models; // For ScopeObject and derived types
using Client.Main.Core.Utilities;
using System;
using System.Collections.Generic;
using System.Linq; // For ItemDatabase

namespace Client.Main.Core.Client
{
    /// <summary>
    /// Manages objects that are currently within the player's scope of view or interaction range.
    /// This class is responsible for tracking players, NPCs, items, and money that are visible to the client.
    /// </summary>
    public class ScopeManager
    {
        private readonly ILogger<ScopeManager> _logger;
        private readonly CharacterState _characterState; // Needed for position-based calculations to determine proximity
        private readonly ConcurrentDictionary<ushort, ScopeObject> _objectsInScope = new(); // Thread-safe dictionary to store objects in scope, using masked IDs as keys

        /// <summary>
        /// Initializes a new instance of the <see cref="ScopeManager"/> class.
        /// </summary>
        /// <param name="loggerFactory">The logger factory used to create a logger for this scope manager.</param>
        /// <param name="characterState">The character state which provides information about the player's current status and position.</param>
        public ScopeManager(ILoggerFactory loggerFactory, CharacterState characterState)
        {
            _logger = loggerFactory.CreateLogger<ScopeManager>();
            _characterState = characterState;
        }

        /// <summary>
        /// Adds or updates a player object in the scope.
        /// If a player with the same masked ID already exists, its information is updated; otherwise, a new player is added.
        /// </summary>
        /// <param name="maskedId">The masked ID of the player.</param>
        /// <param name="rawId">The raw ID of the player.</param>
        /// <param name="x">The X-coordinate of the player's position.</param>
        /// <param name="y">The Y-coordinate of the player's position.</param>
        /// <param name="name">The name of the player.</param>
        public void AddOrUpdatePlayerInScope(ushort maskedId, ushort rawId, byte x, byte y, string name)
        {
            var player = new PlayerScopeObject(maskedId, rawId, x, y, name);
            _objectsInScope.AddOrUpdate(maskedId, player, (_, existing) =>
            {
                existing.PositionX = x;
                existing.PositionY = y;
                ((PlayerScopeObject)existing).Name = name; // Update player name in case it has changed
                existing.LastUpdate = DateTime.UtcNow; // Update the last update timestamp
                return existing;
            });
            _logger.LogTrace("Scope Add/Update: Player {Name} ({Id:X4}, Raw: {RawId:X4}) at [{X},{Y}]", name, maskedId, rawId, x, y);
        }

        /// <summary>
        /// Adds or updates an NPC object in the scope.
        /// If an NPC with the same masked ID already exists, its information is updated; otherwise, a new NPC is added.
        /// </summary>
        /// <param name="maskedId">The masked ID of the NPC.</param>
        /// <param name="rawId">The raw ID of the NPC.</param>
        /// <param name="x">The X-coordinate of the NPC's position.</param>
        /// <param name="y">The Y-coordinate of the NPC's position.</param>
        /// <param name="typeNumber">The type number of the NPC.</param>
        /// <param name="name">The optional name of the NPC.</param>
        public void AddOrUpdateNpcInScope(ushort maskedId, ushort rawId, byte x, byte y, ushort typeNumber, string name = null)
        {
            var npc = new NpcScopeObject(maskedId, rawId, x, y, typeNumber, name);
            _objectsInScope.AddOrUpdate(maskedId, npc, (_, existing) =>
            {
                existing.PositionX = x;
                existing.PositionY = y;
                ((NpcScopeObject)existing).TypeNumber = typeNumber; // Update NPC type number if it has changed
                ((NpcScopeObject)existing).Name = name; // Update NPC name if it has changed
                existing.LastUpdate = DateTime.UtcNow; // Update the last update timestamp
                return existing;
            });
            _logger.LogTrace("Scope Add/Update: NPC Type {Type} ({Id:X4}, Raw: {RawId:X4}) at [{X},{Y}]", typeNumber, maskedId, rawId, x, y);
        }

        /// <summary>
        /// Adds or updates an item object in the scope.
        /// If an item with the same masked ID already exists, its information is updated; otherwise, a new item is added.
        /// </summary>
        /// <param name="maskedId">The masked ID of the item.</param>
        /// <param name="rawId">The raw ID of the item.</param>
        /// <param name="x">The X-coordinate of the item's position.</param>
        /// <param name="y">The Y-coordinate of the item's position.</param>
        /// <param name="itemData">The raw data of the item.</param>
        public void AddOrUpdateItemInScope(ushort maskedId, ushort rawId, byte x, byte y, ReadOnlySpan<byte> itemData)
        {
            var item = new ItemScopeObject(maskedId, rawId, x, y, itemData);
            _objectsInScope.AddOrUpdate(maskedId, item, (_, existing) =>
            {
                existing.PositionX = x;
                existing.PositionY = y;
                // Item data itself usually doesn't change while on the ground, so no update needed for ItemData/Description
                existing.LastUpdate = DateTime.UtcNow; // Update the last update timestamp
                return existing;
            });
            _logger.LogTrace("Scope Add/Update: Item ({Id:X4}, Raw: {RawId:X4}) at [{X},{Y}]", maskedId, rawId, x, y);
        }

        /// <summary>
        /// Adds or updates money object in the scope.
        /// If money with the same masked ID already exists, its information is updated; otherwise, new money is added.
        /// </summary>
        /// <param name="maskedId">The masked ID of the money.</param>
        /// <param name="rawId">The raw ID of the money.</param>
        /// <param name="x">The X-coordinate of the money's position.</param>
        /// <param name="y">The Y-coordinate of the money's position.</param>
        /// <param name="amount">The amount of money.</param>
        public void AddOrUpdateMoneyInScope(ushort maskedId, ushort rawId, byte x, byte y, uint amount)
        {
            var money = new MoneyScopeObject(maskedId, rawId, x, y, amount);
            _objectsInScope.AddOrUpdate(maskedId, money, (_, existing) =>
            {
                existing.PositionX = x;
                existing.PositionY = y;
                ((MoneyScopeObject)existing).Amount = amount; // Update money amount, in case of merging drops
                existing.LastUpdate = DateTime.UtcNow; // Update the last update timestamp
                return existing;
            });
            _logger.LogTrace("Scope Add/Update: Money ({Id:X4}, Raw: {RawId:X4}) Amount {Amount} at [{X},{Y}]", maskedId, rawId, amount, x, y);
        }

        /// <summary>
        /// Removes an object from the scope based on its masked ID.
        /// </summary>
        /// <param name="maskedId">The masked ID of the object to remove.</param>
        /// <returns><c>true</c> if the object was successfully removed; otherwise, <c>false</c>.</returns>
        public bool RemoveObjectFromScope(ushort maskedId)
        {
            if (_objectsInScope.TryRemove(maskedId, out var removedObject))
            {
                _logger.LogTrace("🔭 Scope Remove: ID {Id:X4} ({Type}) - Success", maskedId, removedObject.ObjectType);
                return true;
            }
            else
            {
                _logger.LogTrace("🔭 Scope Remove: ID {Id:X4} - Failed (Not Found)", maskedId);
                return false;
            }
        }

        /// <summary>
        /// Tries to update the position of an object in the scope.
        /// </summary>
        /// <param name="maskedId">The masked ID of the object to update.</param>
        /// <param name="x">The new X-coordinate.</param>
        /// <param name="y">The new Y-coordinate.</param>
        /// <returns><c>true</c> if the object's position was updated; otherwise, <c>false</c> if the object was not found in scope.</returns>
        public bool TryUpdateScopeObjectPosition(ushort maskedId, byte x, byte y)
        {
            if (_objectsInScope.TryGetValue(maskedId, out ScopeObject scopeObject))
            {
                scopeObject.PositionX = x;
                scopeObject.PositionY = y;
                scopeObject.LastUpdate = DateTime.UtcNow; // Update the last update timestamp
                return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if the scope contains an object with the specified masked ID.
        /// </summary>
        /// <param name="maskedId">The masked ID to check for.</param>
        /// <returns><c>true</c> if an object with the given ID is in the scope; otherwise, <c>false</c>.</returns>
        public bool ScopeContains(ushort maskedId)
        {
            return _objectsInScope.ContainsKey(maskedId);
        }

        /// <summary>
        /// Gets all objects in the scope of a specific type.
        /// </summary>
        /// <param name="type">The <see cref="ScopeObjectType"/> to filter by.</param>
        /// <returns>An enumerable collection of <see cref="ScopeObject"/> of the specified type.</returns>
        public IEnumerable<ScopeObject> GetScopeItems(ScopeObjectType type)
        {
            // Return a snapshot to avoid issues with collection modification during iteration
            return _objectsInScope.Values.Where(obj => obj.ObjectType == type).ToList();
        }

        /// <summary>
        /// Clears the scope, removing all objects. Optionally keeps the player's own character in scope.
        /// </summary>
        /// <param name="clearSelf">If set to <c>true</c>, clears even the player's own character from scope. Default is <c>false</c>.</param>
        public void ClearScope(bool clearSelf = false)
        {
            if (clearSelf || _characterState.Id == 0xFFFF)
            {
                _objectsInScope.Clear();
                _logger.LogInformation("🔭 Scope Cleared (All).");
            }
            else
            {
                // Keep self, remove others
                if (_objectsInScope.TryGetValue(_characterState.Id, out var self))
                {
                    _objectsInScope.Clear();
                    _objectsInScope.TryAdd(_characterState.Id, self); // Re-add self to scope
                    _logger.LogInformation("🔭 Scope Cleared (Others). Kept Self ({Id:X4})", _characterState.Id);
                }
                else
                {
                    // This case should ideally not happen if Id is set, but handle defensively
                    _objectsInScope.Clear();
                    _logger.LogWarning("🔭 Scope Cleared (All - Self ID {Id:X4} not found in scope).", _characterState.Id);
                }
            }
        }

        /// <summary>
        /// Gets a formatted string representation of all objects currently in scope.
        /// </summary>
        /// <returns>A string that lists all objects in scope, using their <see cref="ScopeObject.ToString()"/> representation.</returns>
        public string GetScopeListDisplay()
        {
            var sb = new StringBuilder();
            sb.AppendLine("--- Objects in Scope ---");
            if (_objectsInScope.IsEmpty)
            {
                sb.AppendLine(" (Scope is empty)");
            }
            else
            {
                foreach (var kvp in _objectsInScope.OrderBy(o => o.Key)) // Order by ID for consistent output
                {
                    sb.AppendLine($"  {kvp.Value}"); // Uses the ToString() override of ScopeObject for display
                }
            }
            sb.AppendLine($"--- Total: {_objectsInScope.Count} ---");
            return sb.ToString();
        }

        /// <summary>
        /// Tries to get the name of a scope object based on its raw ID.
        /// </summary>
        /// <param name="rawId">The raw ID of the object.</param>
        /// <param name="name">When this method returns, contains the name of the object, or <c>null</c> if the object is not found or has no name.</param>
        /// <returns><c>true</c> if the object's name was successfully retrieved; otherwise, <c>false</c>.</returns>
        public bool TryGetScopeObjectName(ushort rawId, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out string name)
        {
            ushort maskedId = (ushort)(rawId & 0x7FFF); // Always mask the raw ID before lookup

            if (_objectsInScope.TryGetValue(maskedId, out ScopeObject scopeObject))
            {
                switch (scopeObject.ObjectType)
                {
                    case ScopeObjectType.Player:
                        name = ((PlayerScopeObject)scopeObject).Name;
                        return true;
                    case ScopeObjectType.Npc:
                    case ScopeObjectType.Monster:
                        var npcObject = (NpcScopeObject)scopeObject;
                        // Use NpcDatabase primarily
                        name = NpcDatabase.GetNpcName(npcObject.TypeNumber);
                        // Fallback to Name property if DB fails but Name exists (though unlikely needed now)
                        if (name.StartsWith("Unknown") && !string.IsNullOrWhiteSpace(npcObject.Name))
                        {
                            name = npcObject.Name;
                        }
                        return true;
                    case ScopeObjectType.Item:
                        name = ((ItemScopeObject)scopeObject).ItemDescription;
                        return true;
                    case ScopeObjectType.Money:
                        name = $"Zen ({((MoneyScopeObject)scopeObject).Amount})";
                        return true;
                    default:
                        name = $"Unknown Object Type ({scopeObject.ObjectType})";
                        _logger.LogWarning("Found object with ID {MaskedId:X4} but unknown type {Type}", maskedId, scopeObject.ObjectType);
                        return true; // Technically found, but type is unknown
                }
            }

            name = null;
            _logger.LogTrace("Object with masked ID {MaskedId:X4} (from RawID {RawId:X4}) not found in scope.", maskedId, rawId);
            return false;
        }

        /// <summary>
        /// Finds the raw ID of the nearest pickupable item (Item or Money) in scope.
        /// </summary>
        /// <returns>The raw ID of the nearest pickupable item, or <c>null</c> if no items or money are in pickup range.</returns>
        public ushort? FindNearestPickupItemRawId()
        {
            double minDistanceSq = double.MaxValue;
            ScopeObject nearestObject = null;

            // Consider both ItemScopeObject and MoneyScopeObject as pickupable items
            var groundItems = GetScopeItems(ScopeObjectType.Item).Concat(GetScopeItems(ScopeObjectType.Money));

            foreach (var obj in groundItems)
            {
                if (obj.PositionX == 0 && obj.PositionY == 0)
                {
                    continue; // Ignore items with invalid (0,0) position
                }

                double distSq = DistanceSquared(_characterState.PositionX, _characterState.PositionY, obj.PositionX, obj.PositionY);
                if (distSq < minDistanceSq)
                {
                    minDistanceSq = distSq;
                    nearestObject = obj;
                }
            }

            const double maxPickupDistanceSq = 5 * 5; // Example: Maximum pickup distance squared (5 units)
            if (nearestObject != null && minDistanceSq <= maxPickupDistanceSq)
            {
                _logger.LogInformation("Nearest object found: {Object} at distance squared {DistanceSq}", nearestObject, minDistanceSq);
                return nearestObject.RawId;
            }
            else if (nearestObject != null)
            {
                _logger.LogInformation("Nearest object {Object} is too far away (Distance Squared: {DistanceSq})", nearestObject, minDistanceSq);
                return null;
            }
            else
            {
                _logger.LogInformation("No items or money found nearby.");
                return null;
            }
        }

        /// <summary>
        /// Calculates the squared distance between two points.
        /// </summary>
        /// <param name="x1">The X-coordinate of the first point.</param>
        /// <param name="y1">The Y-coordinate of the first point.</param>
        /// <param name="x2">The X-coordinate of the second point.</param>
        /// <param name="y2">The Y-coordinate of the second point.</param>
        /// <returns>The squared distance between the two points.</returns>
        private static double DistanceSquared(double x1, double y1, double x2, double y2)
        {
            double dx = x1 - x2;
            double dy = y1 - y2;
            return (dx * dx) + (dy * dy);
        }
    }
}