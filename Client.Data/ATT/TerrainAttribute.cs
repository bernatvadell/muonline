namespace Client.Data.ATT
{
    public class TerrainAttribute
    {
        public byte Index { get; set; }
        public byte Version { get; set; }
        public byte Width { get; set; }
        public byte Height { get; set; }

        public TWFlags[] TerrainWall { get; } = new TWFlags[Constants.TERRAIN_SIZE * Constants.TERRAIN_SIZE];
    }
}
