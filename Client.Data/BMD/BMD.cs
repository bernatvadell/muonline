namespace Client.Data.BMD
{
    public class BMD
    {
        public byte Version { get; set; } = 0x0C;
        public string Name { get; set; } = string.Empty;

        public BMDTextureMesh[] Meshes { get; set; } = [];
        public BMDTextureBone[] Bones { get; set; } = [];
        public BMDTextureAction[] Actions { get; set; } = [];
    }
}
