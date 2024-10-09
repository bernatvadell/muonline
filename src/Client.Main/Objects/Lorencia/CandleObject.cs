using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Lorencia
{
    public class CandleObject : ModelObject
    {
        public CandleObject()
        {
            LightEnabled = true;
            BlendMesh = 1;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Object1/Candle01.bmd");
            await base.Load();
        }
    }
}
