using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Lorencia
{
    public class StreetLightObject : ModelObject
    {
        public StreetLightObject()
        {
            LightEnabled = true;
            BlendMesh = 1;
            BlendMeshState = BlendState.Additive;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Object1/StreetLight01.bmd");
            await base.Load();
        }
    }
}
