using Client.Main.Content;
using Client.Main.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Noria
{

    public class EoTheCraftsmanPlaceObject : ModelObject
    {
        public EoTheCraftsmanPlaceObject()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Object4/Object19.bmd");
            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }

        public override void DrawMesh(int mesh)
        {
            if (mesh == 2)
            {
                BlendState = BlendState.NonPremultiplied;
                BlendMesh = 1;
                BlendMeshState = BlendState.Additive;
                LightEnabled = true;
                IsTransparent = true;
            }

            base.DrawMesh(mesh);

            IsTransparent = false;
            BlendMeshState = BlendState.Opaque;
            BlendState = Blendings.Alpha;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        }
    }
}
