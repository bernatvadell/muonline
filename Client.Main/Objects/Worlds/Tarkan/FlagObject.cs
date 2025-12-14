using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Tarkan
{
    public class FlagObject : ModelObject
    {
        public override async Task Load()
        {
            BlendState = BlendState.NonPremultiplied;
            var idx = (Type + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object9/Object{idx}.bmd");
            await base.Load();
        }
    }
}