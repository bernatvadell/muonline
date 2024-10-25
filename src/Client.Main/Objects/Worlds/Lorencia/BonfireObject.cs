using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Lorencia
{
    public class BonfireObject : ModelObject
    {
        public BonfireObject()
        {
            LightEnabled = true;
            BlendMesh = 1;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Object1/Bonfire01.bmd");
            await base.Load();
        }
    }
}
