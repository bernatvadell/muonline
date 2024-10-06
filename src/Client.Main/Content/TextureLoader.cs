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

        private readonly OZTReader _tgaReader = new ();
        private readonly OZJReader _jpgReader = new ();
        private readonly Dictionary<string, Task<TextureData>> _textureTasks = [];
        private readonly Dictionary<string, TextureData> _textures = [];
        private readonly Dictionary<string, Texture2D> _texture2dCache = [];
        private GraphicsDevice _graphicsDevice;

        public void SetGraphicsDevice(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
        }

        public Task<TextureData> Prepare(string path)
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

        private async Task<TextureData> InternalPrepare(string path)
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

                return data;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to load asset {path}: {e.Message}");
                return null;
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
            if (path == null)
                return null;

            path = path.ToLowerInvariant();

            lock (_texture2dCache)
            {
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
