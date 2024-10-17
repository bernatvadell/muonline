using Client.Data;
using Client.Data.Texture;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Concurrent;
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
        private readonly OZPReader _pngReader = new();
        private readonly OZDReader _ddsReader = new();
        private readonly ConcurrentDictionary<string, Task<TextureData>> _textureTasks = new ConcurrentDictionary<string, Task<TextureData>>();
        private readonly ConcurrentDictionary<string, ClientTexture> _textures = new ConcurrentDictionary<string, ClientTexture>();
        private GraphicsDevice _graphicsDevice;

        public void SetGraphicsDevice(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
        }

        public Task<TextureData> Prepare(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));

            string normalizedPath = path.ToLowerInvariant();

            return _textureTasks.GetOrAdd(normalizedPath, InternalPrepare);
        }

        private async Task<TextureData> InternalPrepare(string normalizedPath)
        {
            try
            {
                var dataPath = Path.Combine(Constants.DataPath, normalizedPath);
                string ext = Path.GetExtension(normalizedPath)?.ToLowerInvariant();
                TextureData data;

                BaseReader<TextureData> reader;
                string fullPath = string.Empty;


                switch (ext)
                {
                    case ".ozt":
                    case ".tga":
                        reader = _tgaReader;
                        fullPath = Path.ChangeExtension(dataPath, ".ozt");
                        break;
                    case ".ozj":
                    case ".jpg":
                        reader = _jpgReader;
                        fullPath = Path.ChangeExtension(dataPath, ".ozj");
                        break;
                    case ".ozp":
                    case ".png":
                        reader = _pngReader;
                        fullPath = Path.ChangeExtension(dataPath, ".ozp");
                        break;
                    case ".ozd":
                    case ".dds":
                        reader = _ddsReader;
                        fullPath = Path.ChangeExtension(dataPath, ".ozd");
                        break;
                    default:
                        Debug.WriteLine($"Unsupported file extension: {ext}");
                        throw new NotImplementedException($"Extension {ext} not implemented.");
                }

                if (!File.Exists(fullPath))
                {
                    var parentFolder = Directory.GetParent(fullPath);
                    if (parentFolder != null)
                    {
                        var newFullPath = Path.Combine(parentFolder.FullName, "texture", Path.GetFileName(fullPath));
                        if (File.Exists(newFullPath))
                            fullPath = newFullPath;
                    }
                }

                if (!File.Exists(fullPath))
                {
                    Debug.WriteLine($"Texture file not found: {fullPath}");
                    return null;
                }

                data = await reader.Load(fullPath);

                if (data == null)
                {
                    Debug.WriteLine($"Failed to load texture data from: {fullPath}");
                    return null;
                }

                var clientTexture = new ClientTexture
                {
                    Info = data,
                    Script = ParseScript(normalizedPath)
                };

                _textures.TryAdd(normalizedPath, clientTexture);

                return clientTexture.Info;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Failed to load asset {normalizedPath}: {e.Message}");
                return null;
            }
        }

        private static TextureScript ParseScript(string fileName)
        {
            if (fileName.Contains("mu_rgb_lights.jpg", StringComparison.OrdinalIgnoreCase))
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

                return script;
            }

            return null;
        }

        public TextureData Get(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            string normalizedPath = path.ToLowerInvariant();

            if (_textures.TryGetValue(normalizedPath, out ClientTexture value))
                return value.Info;

            return null;
        }

        public TextureScript GetScript(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            string normalizedPath = path.ToLowerInvariant();

            if (_textures.TryGetValue(normalizedPath, out ClientTexture value))
                return value.Script;

            return null;
        }

        public Texture2D GetTexture2D(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            string normalizedPath = path.ToLowerInvariant();

            if (!_textures.TryGetValue(normalizedPath, out ClientTexture textureData))
                return null;

            if (textureData.Texture != null)
                return textureData.Texture;

            if (textureData.Info?.Width == 0 || textureData.Info?.Height == 0 || textureData.Info.Data == null)
                return null;

            var texture = new Texture2D(_graphicsDevice, (int)textureData.Info.Width, (int)textureData.Info.Height);
            textureData.Texture = texture;

            int width = (int)textureData.Info.Width;
            int height = (int)textureData.Info.Height;
            int pixelCount = width * height;
            int components = textureData.Info.Components;

            if (components != 3 && components != 4)
            {
                Debug.WriteLine($"Unsupported texture components: {components} for texture {path}");
                return null;
            }

            Color[] pixelData = new Color[pixelCount];
            byte[] data = textureData.Info.Data;

            if (components == 3)
            {
                Parallel.For(0, pixelCount, i =>
                {
                    int dataIndex = i * 3;
                    byte r = data[dataIndex];
                    byte g = data[dataIndex + 1];
                    byte b = data[dataIndex + 2];
                    byte a = 255;
                    pixelData[i] = new Color(r, g, b, a);
                });
            }
            else // components == 4
            {
                Parallel.For(0, pixelCount, i =>
                {
                    int dataIndex = i * 4;
                    byte r = data[dataIndex];
                    byte g = data[dataIndex + 1];
                    byte b = data[dataIndex + 2];
                    byte a = data[dataIndex + 3];
                    pixelData[i] = new Color(r, g, b, a);
                });
            }

            texture.SetData(pixelData);

            return texture;
        }
    }
}
