using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Devias
{
    public class TreeObject : ModelObject
    {
        public override async Task Load()
        {
            var idx = (Type + 1).ToString().PadLeft(2, '0');
            Model = await BMDLoader.Instance.Prepare($"Object3/Object{idx}.bmd");
            await base.Load();
        }
    }
}
