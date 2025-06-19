using System;

namespace Client.Main.Core.Utilities
{
    /// <summary>
    /// Describes a game world, linking a class to a specific map ID and its display name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class WorldInfoAttribute : Attribute
    {
        /// <summary>
        /// The unique numeric identifier for the map (e.g., 0 for Lorencia).
        /// </summary>
        public ushort MapId { get; }

        /// <summary>
        /// The display name of the map (e.g., "Lorencia").
        /// </summary>
        public string DisplayName { get; }

        public WorldInfoAttribute(ushort mapId, string displayName)
        {
            MapId = mapId;
            DisplayName = displayName;
        }
    }
}
