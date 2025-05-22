using Client.Main.Content;
using Client.Main.Objects.Effects;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using System.IO;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Login
{
    public class TorchObject : ModelObject
    {
        private ILogger _logger = ModelObject.AppLoggerFactory?.CreateLogger<ModelObject>();

        public TorchObject()
        {
            BoundingBoxColor = Color.Blue;
            ParentBoneLink = 1;
            Children.Add(new FlareEffect()
            {
                Scale = 4f
            });
        }

        public override async Task Load()
        {
            var modelPath = Path.Join($"Object74", $"Object38.bmd");

            Model = await BMDLoader.Instance.Prepare(modelPath);

            if (Model == null)
                _logger?.LogDebug($"Can't load MapObject for model: {modelPath}");

            await base.Load();
        }
    }
}
