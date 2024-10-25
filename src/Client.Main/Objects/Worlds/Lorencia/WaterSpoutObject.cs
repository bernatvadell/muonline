using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Lorencia
{
    public class WaterSpoutObject : ModelObject
    {
        public WaterSpoutObject()
        {
            LightEnabled = true;
            Light = new Microsoft.Xna.Framework.Vector3();
            BlendMesh = 3;
            BlendMeshState = BlendState.Additive;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Object1/Waterspout01.bmd");
            await base.Load();
        }
    }
}
