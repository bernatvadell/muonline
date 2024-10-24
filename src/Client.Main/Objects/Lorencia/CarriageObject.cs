using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Lorencia
{
    public class CarriageObject : ModelObject
    {
        public CarriageObject()
        {
            LightEnabled = true;
        }

        public override async Task Load()
        {
            var idx = (Type - (ushort)ModelType.Carriage01 + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object1/Carriage{idx}.bmd");
            await base.Load();
        }

        public override void DrawMesh(int mesh)
        {
            if (mesh == 2)
                BlendState = Blendings.InverseDestinationBlend;

            base.DrawMesh(mesh);

            BlendState = Blendings.Alpha;
        }
    }
}
