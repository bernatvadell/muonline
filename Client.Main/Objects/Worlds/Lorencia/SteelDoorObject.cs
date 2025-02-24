using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Lorencia
{
    public class SteelDoorObject : ModelObject
    {
        public SteelDoorObject()
        {
            LightEnabled = true;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Object1/SteelDoor01.bmd");
            await base.Load();
        }
    }
}
