using Client.Data;
using Client.Data.Texture;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Client.Main.Content
{

    public class TextureLoader
    {
        public static TextureLoader Instance { get; } = new TextureLoader();

        private readonly OZTReader _tgaReader = new();
        private readonly OZJReader _jpgReader = new();
        private readonly Dictionary<string, Task<TextureData>> _textureTasks = [];
        private readonly ConcurrentDictionary<string, ClientTexture> _textures = [];
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

                BaseReader<TextureData> reader;
                string fullPath = "";

                if (ext == ".ozt" || ext == ".tga")
                {
                    reader = _tgaReader;
                    fullPath = Path.ChangeExtension(dataPath, ".ozt");
                }
                else if (ext == ".ozj" || ext == ".jpg")
                {
                    reader = _jpgReader;
                    fullPath = Path.ChangeExtension(dataPath, ".ozj");
                }
                else
                    throw new NotImplementedException($"Extension {ext} not implemented.");

                if (!File.Exists(fullPath))
                {
                    var parentFolder = Directory.GetParent(fullPath);
                    var newFullPath = Path.Combine(parentFolder.FullName, "texture", Path.GetFileName(fullPath));
                    if (File.Exists(newFullPath))
                        fullPath = newFullPath;
                }

                data = await reader.Load(fullPath);

                var clientTexture = new ClientTexture
                {
                    Info = data,
                    Script = ParseScript(path)
                };

                _textures.TryAdd(path, clientTexture);

                return clientTexture.Info;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Failed to load asset {path}: {e.Message}");
                return null;
            }
        }

        private static TextureScript ParseScript(string fileName)
        {
            if (fileName.Contains("mu_rgb_lights.jpg"))
                return new TextureScript { Bright = true };

            var tokens = Path.GetFileNameWithoutExtension(fileName).Split('_');

            if (tokens.Length > 1)
            {
                var script = new TextureScript();
                var token = tokens[^1].ToLowerInvariant();

                switch (token)
                {
                    case "a":
                        script.Alpha = true;
                        break;
                    case "r":
                        script.Bright = true;
                        break;
                    case "h":
                        script.HiddenMesh = true;
                        break;
                    case "s":
                        script.StreamMesh = true;
                        break;
                    case "n":
                        script.NoneBlendMesh = true;
                        break;
                    case "dc":
                        script.ShadowMesh = 1; // NoneTexture
                        break;
                    case "dt":
                        script.ShadowMesh = 2; // Texture
                        break;
                    default:
                        return null;
                }
            }

            return null;
        }

        public TextureData Get(string path)
        {
            path = path.ToLowerInvariant();

            if (_textures.TryGetValue(path, out ClientTexture value))
                return value.Info;

            return null;
        }

        public TextureScript GetScript(string path)
        {
            path = path.ToLowerInvariant();

            if (_textures.TryGetValue(path, out ClientTexture value))
                return value.Script;

            return null;
        }

        public Texture2D GetTexture2D(string path)
        {
            if (path == null)
                return null;
            path = path.ToLowerInvariant();

            if (!_textures.TryGetValue(path, out ClientTexture textureData))
                return null;

            if (textureData.Texture != null)
                return textureData.Texture;

            if (textureData.Info.Width == 0 || textureData.Info.Height == 0 || textureData.Info.Data == null)
                return null;

            var texture = textureData.Texture = new Texture2D(_graphicsDevice, (int)textureData.Info.Width, (int)textureData.Info.Height);

            Color[] pixelData = new Color[(int)textureData.Info.Width * (int)textureData.Info.Height];

            if (textureData.Info.Components == 3)
            {
                for (int i = 0; i < pixelData.Length; i++)
                {
                    byte r = textureData.Info.Data[i * 3];
                    byte g = textureData.Info.Data[i * 3 + 1];
                    byte b = textureData.Info.Data[i * 3 + 2];
                    byte a = 255;
                    pixelData[i] = new Color(r, g, b, a);
                }
            }
            else if (textureData.Info.Components == 4)
            {
                for (int i = 0; i < pixelData.Length; i++)
                {
                    byte r = textureData.Info.Data[i * 4];
                    byte g = textureData.Info.Data[i * 4 + 1];
                    byte b = textureData.Info.Data[i * 4 + 2];
                    byte a = textureData.Info.Data[i * 4 + 3];

                    pixelData[i] = new Color(r, g, b, a);
                }
            }

            texture.SetData(pixelData);

            return texture;
        }
    }
}
