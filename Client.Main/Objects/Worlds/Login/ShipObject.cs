using Client.Main.Content;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using Client.Main.Controllers;
using Microsoft.Xna.Framework;
using Client.Main.Graphics;

namespace Client.Main.Objects.Worlds.Login
{
    public class ShipObject : ModelObject
    {
        public override async Task Load()
        {
            var idx = (Type + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object95/Object{idx}.bmd");
            Position = new Vector3(Position.X, Position.Y, Position.Z + 15f);
            IsTransparent = false;
            LightEnabled = true;
            await base.Load();
        }

        public override void DrawMesh(int mesh)
        {
            if (mesh == 1 || mesh == 21)
            {
                BlendState = BlendState.NonPremultiplied;
            }
            else if (mesh == 5 || mesh == 4)
            {
                BlendState = BlendState.NonPremultiplied;
            }

            base.DrawMesh(mesh);

            BlendState = Blendings.Alpha;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
        }
    }

}
