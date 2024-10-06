using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Lorencia
{
    public class PoseBoxObject : ModelObject
    {
        public override async Task Load(GraphicsDevice graphicsDevice)
        {
            Model = await BMDLoader.Instance.Prepare($"Object1/PoseBox01.bmd");
            await base.Load(graphicsDevice);
        }

        public override void DrawMesh(int mesh)
        { }
    }
}
