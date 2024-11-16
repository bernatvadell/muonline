using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Devias
{
    public class TreeObject : ModelObject
    {
        public override async Task Load()
        {
            BlendState = BlendState.AlphaBlend;
            LightEnabled = true;
            BlendMesh = 1;
            BlendMeshState = BlendState.Opaque;
            Model = await BMDLoader.Instance.Prepare($"Object3/Object0{Type + 1}.bmd");
            await base.Load();
        }

        public override void DrawMesh(int mesh)
        {
            BlendState = new BlendState
            {
                ColorSourceBlend = Blend.One,
                ColorDestinationBlend = Blend.Zero,
                AlphaSourceBlend = Blend.One,
                AlphaDestinationBlend = Blend.Zero,
                ColorBlendFunction = BlendFunction.Add,
                AlphaBlendFunction = BlendFunction.Add
            };

            GraphicsDevice.DepthStencilState = new DepthStencilState
            {
                DepthBufferEnable = true,
                DepthBufferWriteEnable = true,
                DepthBufferFunction = CompareFunction.LessEqual
            };

            base.DrawMesh(mesh);

            BlendState = Blendings.Alpha;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        }
    }
}
