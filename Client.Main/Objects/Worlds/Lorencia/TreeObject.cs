using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Lorencia
{
    public class TreeObject : ModelObject
    {
        public TreeObject()
        {
            LightEnabled = true;
        }

        public override async Task Load()
        {
            var idx = (Type - (ushort)ModelType.Tree01 + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object1/Tree{idx}.bmd");
            await base.Load();

            float terrainHeight = World.Terrain.RequestTerrainHeight(Position.X, Position.Y);

            if (Position.Z < terrainHeight)
            {
                return;
            }

            if (Type == 9)
            {
                float baseHeight = terrainHeight + 10f;
                Position = new Vector3(Position.X, Position.Y, baseHeight);
            }
        }
    }
}
