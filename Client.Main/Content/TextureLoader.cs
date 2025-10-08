using System.Collections.Concurrent;
using Client.Data;
using Client.Data.Texture;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Extensions.Logging;

namespace Client.Main.Content
{
    public class TextureLoader
    {
        public static TextureLoader Instance { get; } = new TextureLoader();

        private readonly ConcurrentDictionary<string, Task<TextureData>> _textureTasks = new();
        private readonly ConcurrentDictionary<string, ClientTexture> _textures = new();
        // Note: ConcurrentDictionary does not accept null values; store string.Empty as "not found" sentinel
        private readonly ConcurrentDictionary<string, string> _pathExistsCache = new();
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

        private ILogger _logger = MuGame.AppLoggerFactory?.CreateLogger<TextureLoader>();

        // Precompiled logging messages to minimize overhead in hot paths
        private static readonly Action<ILogger, string, Exception> _logUnsupportedExt =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1001, nameof(_logUnsupportedExt)), "Unsupported file extension: {Ext}");
        private static readonly Action<ILogger, string, Exception> _logFailedLoadData =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1002, nameof(_logFailedLoadData)), "Failed to load texture data from: {Path}");
        private static readonly Action<ILogger, string, Exception> _logFileNotFound =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1003, nameof(_logFileNotFound)), "Texture file not found: {Path}");
        private static readonly Action<ILogger, string, Exception> _logFailedAsset =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1004, nameof(_logFailedAsset)), "Failed to load asset {Path}");

        public void SetGraphicsDevice(GraphicsDevice graphicsDevice) => _graphicsDevice = graphicsDevice;

        public Task<TextureData> Prepare(string path)
        { 
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));

            string normalizedKey = path.ToLowerInvariant();
            return _textureTasks.GetOrAdd(normalizedKey, key => InternalPrepare(key));
        }

        public async Task<Texture2D> PrepareAndGetTexture(string path)
        {
            await Prepare(path);
            return GetTexture2D(path);
        }

        private async Task<TextureData> InternalPrepare(string path)
        {
            try
            {
                var dataPath = Path.Combine(Constants.DataPath, path);
                string ext = Path.GetExtension(path)?.ToLowerInvariant();

                if (!_readers.TryGetValue(ext, out var reader))
                {
                    if (_logger != null && _logger.IsEnabled(LogLevel.Debug)) _logUnsupportedExt(_logger, ext ?? string.Empty, null);
                    return null;
                }

                string fullPath = FindTexturePath(dataPath, ext);
                if (fullPath == null) return null;

                var data = await reader.Load(fullPath);
                if (data == null)
                {
                    if (_logger != null && _logger.IsEnabled(LogLevel.Debug)) _logFailedLoadData(_logger, fullPath ?? string.Empty, null);
                    return null;
                }

                var clientTexture = new ClientTexture
                {
                    Info = data,
                    Script = ParseScript(path)
                };

                _textures.TryAdd(path.ToLowerInvariant(), clientTexture);
                return clientTexture.Info;
            }
            catch (Exception)
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Debug)) _logFailedAsset(_logger, path ?? string.Empty, null);
                return null;
            }
        }

        private string FindTexturePath(string dataPath, string ext)
        {
            string expectedExtension = _readers[ext].GetType().Name.ToLowerInvariant().Replace("reader", "");
            string expectedFilePath = Path.ChangeExtension(dataPath, expectedExtension);

            string actualPath = GetActualPath(expectedFilePath);
            if (actualPath != null)
                return actualPath;

            string parentFolder = Path.GetDirectoryName(expectedFilePath);
            if (!string.IsNullOrEmpty(parentFolder))
            {
                string newFullPath = Path.Combine(parentFolder, "texture", Path.GetFileName(expectedFilePath));
                actualPath = GetActualPath(newFullPath);
                if (actualPath != null)
                    return actualPath;
            }

            if (_logger != null && _logger.IsEnabled(LogLevel.Debug)) _logFileNotFound(_logger, expectedFilePath ?? string.Empty, null);
            return null;
        }

        private string GetActualPath(string path)
        {
            // Check cache first
            if (_pathExistsCache.TryGetValue(path, out var cachedPath))
                return string.IsNullOrEmpty(cachedPath) ? null : cachedPath;

            string result = null;

            if (File.Exists(path))
            {
                result = path;
            }
            else
            {
                // Try case-insensitive path resolution for both directories and files
                result = ResolveCaseInsensitivePath(path);
            }

            // Cache the result (store empty string for "not found" to avoid null values)
            _pathExistsCache.TryAdd(path, result ?? string.Empty);
            return result;
        }

        private string ResolveCaseInsensitivePath(string path)
        {
            // Start from the base path and resolve each component case-insensitively
            if (string.IsNullOrEmpty(path))
                return null;

            // Split path into components
            var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string currentPath = parts[0];

            // Handle absolute paths (starting with / or drive letter)
            bool isAbsolute = Path.IsPathRooted(path);
            if (isAbsolute && string.IsNullOrEmpty(currentPath))
            {
                currentPath = Path.DirectorySeparatorChar.ToString();
            }

            for (int i = (isAbsolute && string.IsNullOrEmpty(parts[0])) ? 1 : 1; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]))
                    continue;

                string nextPath = Path.Combine(currentPath, parts[i]);

                // Check if exact path exists
                if (Directory.Exists(nextPath) || File.Exists(nextPath))
                {
                    currentPath = nextPath;
                    continue;
                }

                // Try case-insensitive lookup
                string found = null;
                bool isLastPart = i == parts.Length - 1;

                if (Directory.Exists(currentPath))
                {
                    if (isLastPart)
                    {
                        // Last part could be a file
                        foreach (var file in Directory.GetFiles(currentPath))
                        {
                            if (string.Equals(Path.GetFileName(file), parts[i], StringComparison.OrdinalIgnoreCase))
                            {
                                found = file;
                                break;
                            }
                        }
                    }

                    if (found == null)
                    {
                        // Try directories
                        foreach (var dir in Directory.GetDirectories(currentPath))
                        {
                            if (string.Equals(Path.GetFileName(dir), parts[i], StringComparison.OrdinalIgnoreCase))
                            {
                                found = dir;
                                break;
                            }
                        }
                    }
                }

                if (found == null)
                    return null;

                currentPath = found;
            }

            return File.Exists(currentPath) || Directory.Exists(currentPath) ? currentPath : null;
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

            string normalizedKey = path.ToLowerInvariant();

            if (!_textures.TryGetValue(normalizedKey, out ClientTexture clientTexture))
                return null;

            if (clientTexture.Texture != null)
                return clientTexture.Texture;

            var textureInfo = clientTexture.Info;
            if (textureInfo?.Width == 0 || textureInfo?.Height == 0 || textureInfo.Data == null)
                return null;

            Texture2D texture;

            if (textureInfo.IsCompressed)
            {
                texture = new Texture2D(_graphicsDevice, textureInfo.Width, textureInfo.Height, false, textureInfo.Format);
                texture.SetData(textureInfo.Data);
            }
            else
            {
                texture = new Texture2D(_graphicsDevice, textureInfo.Width, textureInfo.Height);
                int pixelCount = texture.Width * texture.Height;
                int components = textureInfo.Components;

                if (components != 3 && components != 4)
                {
                    _logger?.LogDebug("Unsupported texture components: {Components} for texture {Path}", components, path);
                    return null;
                }

                var pool = System.Buffers.ArrayPool<Color>.Shared;
                Color[] pixelData = pool.Rent(pixelCount);
                try
                {
                    byte[] data = textureInfo.Data;
                    for (int i = 0; i < pixelCount; i++)
                    {
                        int dataIndex = i * components;
                        byte r = data[dataIndex];
                        byte g = data[dataIndex + 1];
                        byte b = data[dataIndex + 2];
                        byte a = components == 4 ? data[dataIndex + 3] : (byte)255;
                        pixelData[i] = new Color(r, g, b, a);
                    }
                    texture.SetData(pixelData, 0, pixelCount);
                }
                finally
                {
                    pool.Return(pixelData);
                }
            }

            clientTexture.Texture = texture;
            return texture;
        }
    }
}
