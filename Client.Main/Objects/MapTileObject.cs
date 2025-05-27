using Client.Main.Content;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    public class MapTileObject : ModelObject
    {
        private ILogger _logger = ModelObject.AppLoggerFactory?.CreateLogger<MapTileObject>();

        public MapTileObject()
        {
            BlendState = BlendState.AlphaBlend;
        }

        public override async Task Load()
        {
            var modelPath = Path.Join($"Object{World.WorldIndex}", $"Object{(Type + 1).ToString().PadLeft(2, '0')}.bmd");

            Model = await BMDLoader.Instance.Prepare(modelPath);

            if (Model == null)
                _logger?.LogDebug($"Can't load MapObject for model: {modelPath}");

            await base.Load();
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }
    }
}
