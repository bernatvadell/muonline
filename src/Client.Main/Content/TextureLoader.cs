using Client.Data.Texture;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Client.Main.Content
{
    public class TextureLoader
    {
        public static TextureLoader Instance { get; } = new TextureLoader();

        private OZTReader _tgaReader = new OZTReader();
        private OZJReader _jpgReader = new OZJReader();
        private Dictionary<string, Task> _textureTasks = new Dictionary<string, Task>();
        private Dictionary<string, TextureData> _textures = new Dictionary<string, TextureData>();
        private Dictionary<string, Texture2D> _texture2dCache = new Dictionary<string, Texture2D>();
        private GraphicsDevice _graphicsDevice;

        public void SetGraphicsDevice(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
        }

        public Task Prepare(string path)
        {
            lock (_textureTasks)
            {
                path = path.ToLowerInvariant();

                if (_textureTasks.TryGetValue(path, out var task))
                    return task;

                _textureTasks.Add(path, task = InternalPrepare(path));

                return task;
            }
        }

        private async Task InternalPrepare(string path)
        {
            try
            {
                var dataPath = Path.Combine(Constants.DataPath, path);
                var ext = Path.GetExtension(path).ToLowerInvariant();
                TextureData data;

                if (ext == ".ozt" || ext == ".tga")
                    data = await _tgaReader.Load(Path.ChangeExtension(dataPath, ".ozt"));
                else if (ext == ".ozj" || ext == ".jpg")
                    data = await _jpgReader.Load(Path.ChangeExtension(dataPath, ".ozj"));
                else
                    throw new NotImplementedException($"Extension {ext} not implemented.");

                lock (_textures)
                    _textures.Add(path, data);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to load asset {path}: {e.Message}");
            }
        }

        public TextureData Get(string path)
        {
            path = path.ToLowerInvariant();

            if (_textures.ContainsKey(path))
                return _textures[path];

            return null;
        }

        public Texture2D GetTexture2D(string path)
        {
            lock (_texture2dCache)
            {
                if (path == null)
                    return null;

                path = path.ToLowerInvariant();

                if (_texture2dCache.TryGetValue(path, out Texture2D value))
                    return value;

                _texture2dCache.Add(path, null);

                var textureData = Get(path);

                if (textureData == null)
                    return null;

                if (textureData.Width == 0 || textureData.Height == 0 || textureData.Data == null)
                    return null;

                var texture = new Texture2D(_graphicsDevice, (int)textureData.Width, (int)textureData.Height);

                Color[] pixelData = new Color[(int)textureData.Width * (int)textureData.Height];

                if (textureData.Components == 3)
                {
                    for (int i = 0; i < pixelData.Length; i++)
                    {
                        byte r = textureData.Data[i * 3];
                        byte g = textureData.Data[i * 3 + 1];
                        byte b = textureData.Data[i * 3 + 2];

                        pixelData[i] = new Color(r, g, b, (byte)255);
                    }
                }
                else if (textureData.Components == 4)
                {
                    for (int i = 0; i < pixelData.Length; i++)
                    {
                        byte r = textureData.Data[i * 4];
                        byte g = textureData.Data[i * 4 + 1];
                        byte b = textureData.Data[i * 4 + 2];
                        byte a = textureData.Data[i * 4 + 3];
                        pixelData[i] = new Color(r, g, b, a);
                    }
                }

                texture.SetData(pixelData);

                _texture2dCache[path] = texture;

                return texture;
            }
        }
    }
}
