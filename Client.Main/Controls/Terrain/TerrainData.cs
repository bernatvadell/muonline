using Client.Data.ATT;
using Client.Data.MAP;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace Client.Main.Controls.Terrain
{
    /// <summary>
    /// A data container for all terrain-related assets and properties.
    /// This object is typically populated by the TerrainLoader.
    /// </summary>
    public class TerrainData
    {
        public TerrainAttribute Attributes { get; set; }
        public TerrainMapping Mapping { get; set; }
        public Texture2D[] Textures { get; set; }
        public Color[] HeightMap { get; set; }
        public Color[] LightData { get; set; }
        public Vector3[] Normals { get; set; }
        public Color[] FinalLightMap { get; set; }
        public float[] GrassWind { get; set; }
        public Texture2D HeightMapTexture { get; set; }

        public Dictionary<int, string> TextureMappingFiles { get; set; } = GetDefaultTextureMappings();

        private static Dictionary<int, string> GetDefaultTextureMappings()
        {
            return new Dictionary<int, string>
            {
                {   0, "TileGrass01.ozj" },
                {   1, "TileGrass02.ozj" },
                {   2, "TileGround01.ozj" },
                {   3, "TileGround02.ozj" },
                {   4, "TileGround03.ozj" },
                {   5, "TileWater01.ozj" },
                {   6, "TileWood01.ozj" },
                {   7, "TileRock01.ozj" },
                {   8, "TileRock02.ozj" },
                {   9, "TileRock03.ozj" },
                {  10, "TileRock04.ozj" },
                {  11, "TileRock05.ozj" },
                {  12, "TileRock06.ozj" },
                {  13, "TileRock07.ozj" },
                {  30, "TileGrass01.ozt" },
                {  31, "TileGrass02.ozt" },
                {  32, "TileGrass03.ozt" },
                { 100, "leaf01.ozt" },
                { 101, "leaf02.ozj" },
                { 102, "rain01.ozt" },
                { 103, "rain02.ozt" },
                { 104, "rain03.ozt" }
            };
        }
    }
}
