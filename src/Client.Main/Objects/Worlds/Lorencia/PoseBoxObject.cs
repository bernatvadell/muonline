using Client.Data;
using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Lorencia
{
    public class PoseBoxObject : ModelObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Object1/PoseBox01.bmd");
            await base.Load();
        }

        public override void DrawMesh(int mesh)
        { }
    }
}
