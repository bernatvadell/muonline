using System.Collections.Generic;

namespace Client.Main.Core.Utilities
{
    /// <summary>
    /// Provides mapping between map numbers and their names.
    /// </summary>
    public static class MapDatabase
    {
        private static readonly Dictionary<ushort, string> MapNames = InitializeMapData();

        private static Dictionary<ushort, string> InitializeMapData()
        {
            // Based on the provided map initializer names and common map IDs
            return new Dictionary<ushort, string>
            {
                { 0, "Lorencia" },
                { 1, "Dungeon" },
                { 2, "Devias" },
                { 3, "Noria" },
                { 4, "Lost Tower" },
                { 5, "Exile" },
                { 6, "Arena" }, // Also known as Stadium
                { 7, "Atlans" },
                { 8, "Tarkan" },
                { 9, "Devil Square" }, // Discriminator distinguishes levels 1-4
                { 10, "Icarus" },
                // Add more maps here if needed based on server version
                // { 11, "Blood Castle 1" }, // Example
                // { 32, "Devil Square" }, // Example for DS 5-7 map number if different
            };
        }

        /// <summary>
        /// Gets the name of the map based on its number.
        /// </summary>
        /// <param name="mapId">The map number (ID).</param>
        /// <returns>The name of the map, or a default string if not found.</returns>
        public static string GetMapName(ushort mapId)
        {
            // Special handling for Devil Square maps with discriminator
            if (mapId == 9)
            {
                // We don't have the discriminator here easily, so return generic name
                return "Devil Square";
                // If you could pass discriminator: return $"Devil Square {discriminator}";
            }

            return MapNames.TryGetValue(mapId, out var name) ? name : $"Unknown Map ({mapId})";
        }
    }
}