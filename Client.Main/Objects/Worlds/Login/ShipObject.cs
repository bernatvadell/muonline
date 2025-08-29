using Client.Main.Content;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using Client.Main.Controllers;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Worlds.Login
{
    public class ShipObject : ModelObject
    {
        public override async Task Load()
        {
            var idx = (Type + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object95/Object{idx}.bmd");
            Position = new Vector3(Position.X, Position.Y, Position.Z + 15f);
            await base.Load();
        }
        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            base.Draw(gameTime);
        }

        public override void DrawMesh(int mesh)
        {
            var originalBlendState = GraphicsDevice.BlendState;
            var originalBlendMeshState = BlendMeshState;
            var originalRasterizerState = GraphicsDevice.RasterizerState;
            var originalDepthStencilState = GraphicsDevice.DepthStencilState;

            if (mesh == 1 || mesh == 21)
            {
                GraphicsDevice.RasterizerState = GraphicsManager.GetCachedRasterizerState(0.0000005f, CullMode.None);

                var depthState = new DepthStencilState
                {
                    DepthBufferEnable = true,
                    DepthBufferWriteEnable = true,
                    DepthBufferFunction = CompareFunction.Less
                };
                GraphicsDevice.DepthStencilState = depthState;

                IsTransparent = false;
                LightEnabled = true;
                BlendState = BlendState.NonPremultiplied;
                BlendMesh = -1;
            }
            else if (mesh == 5 || mesh == 4)
            {
                LightEnabled = true;
                IsTransparent = false;
                BlendState = BlendState.NonPremultiplied;
            }
            else
            {
                IsTransparent = false;
                LightEnabled = true;
            }

            base.DrawMesh(mesh);

            GraphicsDevice.RasterizerState = originalRasterizerState;
            GraphicsDevice.DepthStencilState = originalDepthStencilState;
            BlendMeshState = originalBlendMeshState;
            GraphicsDevice.BlendState = originalBlendState;
        }
    }

}
