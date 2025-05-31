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
                { 6, "Arena" },
                { 7, "Atlans" },
                { 8, "Tarkan" },
                { 9, "Devil Square" },
                { 10, "Icarus" },
                { 11, "Blood Castle 1" },
                { 12, "Blood Castle 2" },
                { 13, "Blood Castle 3" },
                { 14, "Blood Castle 4" },
                { 15, "Blood Castle 5" },
                { 16, "Blood Castle 6" },
                { 17, "Blood Castle 7" },
                { 18, "Chaos Castle 1" },
                { 19, "Chaos Castle 2" },
                { 20, "Chaos Castle 3" },
                { 21, "Chaos Castle 4" },
                { 22, "Chaos Castle 5" },
                { 23, "Chaos Castle 6" },
                { 24, "Kalima 1" },
                { 25, "Kalima 2" },
                { 26, "Kalima 3" },
                { 27, "Kalima 4" },
                { 28, "Kalima 5" },
                { 29, "Kalima 6" },
                { 30, "Valley of Loren" },
                { 31, "Land of Trial" },
                { 32, "Devil Square" },
                { 33, "Aida" },
                { 34, "Crywolf" },
                { 36, "Kalima" },
                { 37, "Kantru1" },
                { 38, "Kantru2" },
                { 39, "Kantru3" },
                { 40, "Silent" },
                { 41, "T42" },
                { 42, "T43" },
                { 45, "IllusionTemple" },
                { 46, "IllusionTemple" },
                { 47, "IllusionTemple" },
                { 48, "IllusionTemple" },
                { 49, "IllusionTemple" },
                { 50, "IllusionTemple" },
                { 51, "Elbeland" },
                { 52, "Blood Castle" },
                { 53, "Chaos Castle" },
                { 56, "(null)" },
                { 57, "Raklion" },
                { 58, "Buhwajang" },
                { 62, "Santa" },
                { 63, "Vulcan" },
                { 64, "Terrain64" },
                { 65, "65_Doppelganger1" },
                { 66, "66_Doppelganger2" },
                { 67, "67_Doppelganger3" },
                { 68, "68_Doppelganger4" },
                { 69, "69_ImperialGuardian1" },
                { 70, "70_ImperialGuardian2" },
                { 71, "71_ImperialGuardian3" },
                { 72, "72_ImperialGuardian4" },
                { 79, "79_LorenMarket" },
                { 80, "80_Karutan1" },
                { 81, "81_Karutan2" },
                { 82, "82_Doppelganger_Renewal" },
                { 91, "91_Acheron" },
                { 92, "91_Acheron" },
                { 95, "95_Debenter" },
                { 96, "96_Debenter_ArcaBattle" },
                { 97, "97_ChaosCastleSurvival" },
                { 98, "98_IllussionTempleLeague" },
                { 99, "99_IllussionTempleLeague2" },
                { 100, "100_UrkMontain" },
                { 102, "102_TormentedSquare" },
                { 110, "110_Nars" },
                { 112, "112_Ferea" },
                { 113, "113_NixieLake" },
                { 114, "114_Terrain115" },
                { 115, "115_Terrain116" },
                { 116, "DeepDugeon1" },
                { 117, "DeepDugeon2" },
                { 118, "DeepDugeon3" },
                { 119, "DeepDugeon4" },
                { 120, "DeepDugeon5" },
                { 121, "PlaceOfQualification" },
                { 122, "SwampOfDarkness" },
                { 123, "KuberaMine1" },
                { 124, "KuberaMine1" },
                { 125, "KuberaMine1" },
                { 126, "KuberaMine1" },
                { 127, "KuberaMine1" },
                { 128, "World129" },
                { 129, "World130" },
                { 130, "World131" },
                { 131, "World132" },
            };
        }

        /// <summary>
        /// Gets the name of the map based on its number.
        /// </summary>
        /// <param name="mapId">The map number (ID).</param>
        /// <returns>The name of the map, or a default string if not found.</returns>
        public static string GetMapName(ushort mapId)
        {
            return MapNames.TryGetValue(mapId, out var name) ? name : $"Unknown Map ({mapId})";
        }
    }
}