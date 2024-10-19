using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Client.Data;
using Client.Data.Texture;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Content
{
    public class TextureLoader
    {
        public static TextureLoader Instance { get; } = new TextureLoader();

        private readonly ConcurrentDictionary<string, Task<TextureData>> _textureTasks = new();
        private readonly ConcurrentDictionary<string, ClientTexture> _textures = new();
        private GraphicsDevice _graphicsDevice;

        private readonly Dictionary<string, BaseReader<TextureData>> _readers = new()
        {
            { ".ozt", new OZTReader() },
            { ".tga", new OZTReader() },
            { ".ozj", new OZJReader() },
            { ".jpg", new OZJReader() },
            { ".ozp", new OZPReader() },
            { ".png", new OZPReader() },
            { ".ozd", new OZDReader() },
            { ".dds", new OZDReader() }
        };

        public void SetGraphicsDevice(GraphicsDevice graphicsDevice) => _graphicsDevice = graphicsDevice;

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

                if (!_readers.TryGetValue(ext, out var reader))
                {
                    Debug.WriteLine($"Unsupported file extension: {ext}");
                    return null;
                }

                string fullPath = FindTexturePath(dataPath, ext);
                if (fullPath == null) return null;

                var data = await reader.Load(fullPath);
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

        private string FindTexturePath(string dataPath, string ext)
        {
            string fullPath = Path.ChangeExtension(dataPath, _readers[ext].GetType().Name.ToLowerInvariant().Replace("reader", ""));

            if (!File.Exists(fullPath))
            {
                var parentFolder = Directory.GetParent(fullPath);
                if (parentFolder != null)
                {
                    var newFullPath = Path.Combine(parentFolder.FullName, "texture", Path.GetFileName(fullPath));
                    if (File.Exists(newFullPath))
                        return newFullPath;
                }
            }

            if (!File.Exists(fullPath))
            {
                Debug.WriteLine($"Texture file not found: {fullPath}");
                return null;
            }

            return fullPath;
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
                    case "a": script.Alpha = true; break;
                    case "r": script.Bright = true; break;
                    case "h": script.HiddenMesh = true; break;
                    case "s": script.StreamMesh = true; break;
                    case "n": script.NoneBlendMesh = true; break;
                    case "dc": script.ShadowMesh = 1; break; // NoneTexture
                    case "dt": script.ShadowMesh = 2; break; // Texture
                    default: return null;
                }

                return script;
            }

            return null;
        }

        public TextureData Get(string path) =>
            string.IsNullOrWhiteSpace(path) ? null :
            _textures.TryGetValue(path.ToLowerInvariant(), out var value) ? value.Info : null;

        public TextureScript GetScript(string path) =>
            string.IsNullOrWhiteSpace(path) ? null :
            _textures.TryGetValue(path.ToLowerInvariant(), out var value) ? value.Script : null;

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

            int pixelCount = texture.Width * texture.Height;
            int components = textureData.Info.Components;

            if (components != 3 && components != 4)
            {
                Debug.WriteLine($"Unsupported texture components: {components} for texture {path}");
                return null;
            }

            Color[] pixelData = new Color[pixelCount];
            byte[] data = textureData.Info.Data;

            Parallel.For(0, pixelData.Length, (i) =>
            {
                int dataIndex = i * components;
                byte r = data[dataIndex];
                byte g = data[dataIndex + 1];
                byte b = data[dataIndex + 2];
                byte a = components == 4 ? data[dataIndex + 3] : (byte)255;
                pixelData[i] = new Color(r, g, b, a);
            });

            texture.SetData(pixelData);
            return texture;
        }
    }
}