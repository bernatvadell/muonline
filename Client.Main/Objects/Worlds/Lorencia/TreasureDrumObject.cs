using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Lorencia
{
    public class TreasureDrumObject : ModelObject
    {
        public TreasureDrumObject()
        {
            LightEnabled = true;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Object1/TreasureDrum01.bmd");
            await base.Load();
        }
    }
}
