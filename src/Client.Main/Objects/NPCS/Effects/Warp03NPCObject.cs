using Client.Main.Content;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;

namespace Client.Main.Objects.NPCS.Effects
{
    public class Warp03NPCObject : ModelObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/warp03.bmd");
            BlendState = BlendState.Additive;
            Alpha = 1f;
            Light = new Vector3(0.8f, 0.8f, 0.8f);
            await base.Load();
        }
    }
}
