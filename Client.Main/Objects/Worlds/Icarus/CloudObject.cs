using Client.Main.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Icarus
{
    public class CloudObject : ModelObject
    {
        public override async Task Load()
        {
            LightEnabled = true;
            Model = await BMDLoader.Instance.Prepare($"Object11/cloud.bmd");
            await base.Load();
        }
    }
}
