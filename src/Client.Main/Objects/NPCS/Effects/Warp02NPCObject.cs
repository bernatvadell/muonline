using Client.Main.Content;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.NPCS.Effects
{
    public class Warp02NPCObject : ModelObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/warp02.bmd");
            BlendState = BlendState.Additive;
            await base.Load();
        }
    }
}
