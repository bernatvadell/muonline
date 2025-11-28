using System;
using System.Collections.Concurrent;
using System.Collections.Generic; // Added for Dictionary
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        private readonly ConcurrentDictionary<string, Lazy<Task<TextureData>>> _textureTasks = new();
        private readonly ConcurrentDictionary<string, ClientTexture> _textures = new();

        // Cache: Key -> Resolved Full Path (or empty if not found)
        private readonly ConcurrentDictionary<string, string> _pathResolutionCache = new();

        private readonly CancellationTokenSource _cleanupCts = new();
        private readonly TimeSpan _cleanupInterval = TimeSpan.FromSeconds(60);
        private readonly TimeSpan _textureTtl = TimeSpan.FromMinutes(5);
        private readonly Task _cleanupTask;
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

        // Precompiled logging messages
        private static readonly Action<ILogger, string, Exception> _logUnsupportedExt =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1001, nameof(_logUnsupportedExt)), "Unsupported file extension: {Ext}");
        private static readonly Action<ILogger, string, Exception> _logFailedLoadData =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1002, nameof(_logFailedLoadData)), "Failed to load texture data from: {Path}");
        private static readonly Action<ILogger, string, Exception> _logFileNotFound =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1003, nameof(_logFileNotFound)), "Texture file not found: {Path}");
        private static readonly Action<ILogger, string, Exception> _logFailedAsset =
            LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1004, nameof(_logFailedAsset)), "Failed to load asset {Path}");

        private TextureLoader()
        {
            _cleanupTask = Task.Run(() => CleanupLoopAsync(_cleanupCts.Token));
        }

        public void SetGraphicsDevice(GraphicsDevice graphicsDevice) => _graphicsDevice = graphicsDevice;

        public Task<TextureData> Prepare(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path cannot be null or whitespace.", nameof(path));

            string normalizedKey = NormalizePathKey(path);
            var lazyTextureTask = _textureTasks.GetOrAdd(
                normalizedKey,
                key => new Lazy<Task<TextureData>>(() => Task.Run(() => InternalPrepare(path)))); // Pass original path to InternalPrepare

            return lazyTextureTask.Value;
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
                // Note: path is relative here (e.g. "Interface/GF_logo.ozj")
                var dataPath = Path.Combine(Constants.DataPath, path);
                string ext = Path.GetExtension(path)?.ToLowerInvariant();

                if (string.IsNullOrEmpty(ext) || !_readers.TryGetValue(ext, out var reader))
                {
                    if (_logger != null && _logger.IsEnabled(LogLevel.Debug)) _logUnsupportedExt(_logger, ext ?? string.Empty, null);
                    return null;
                }

                string fullPath = FindTexturePath(dataPath, ext);
                if (fullPath == null) return null;

                var data = await reader.Load(fullPath);
                if (data == null)
                {
                    if (_logger != null && _logger.IsEnabled(LogLevel.Debug)) _logFailedLoadData(_logger, fullPath, null);
                    return null;
                }

                var clientTexture = new ClientTexture
                {
                    Info = data,
                    Script = ParseScript(path),
                    LastAccessUtc = DateTime.UtcNow
                };

                _textures.TryAdd(NormalizePathKey(path), clientTexture);
                return clientTexture.Info;
            }
            catch (Exception ex)
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Debug)) _logFailedAsset(_logger, path ?? string.Empty, ex);
                return null;
            }
        }

        private string FindTexturePath(string fullPath, string ext)
        {
            if (!_readers.TryGetValue(ext, out var reader)) return null;

            // Determine expected extension based on reader type logic (legacy MU logic)
            string expectedExtension = reader.GetType().Name.ToLowerInvariant().Replace("reader", "");

            // 1. Try path with correct extension
            string expectedFilePath = Path.ChangeExtension(fullPath, expectedExtension);
            string actualPath = ResolveCaseInsensitivePath(expectedFilePath);

            if (actualPath != null)
                return actualPath;

            // 2. Try MU specific fallback: look in "texture" subdirectory of the parent
            // e.g., if looking for Data/World1/Terrain.jpg, look in Data/World1/texture/Terrain.jpg
            string parentFolder = Path.GetDirectoryName(expectedFilePath);
            if (!string.IsNullOrEmpty(parentFolder))
            {
                string fileName = Path.GetFileName(expectedFilePath);
                string fallbackPath = Path.Combine(parentFolder, "texture", fileName);
                actualPath = ResolveCaseInsensitivePath(fallbackPath);

                if (actualPath != null)
                    return actualPath;
            }

            if (_logger != null && _logger.IsEnabled(LogLevel.Debug)) _logFileNotFound(_logger, expectedFilePath, null);
            return null;
        }

        /// <summary>
        /// Resolves a file path on a case-sensitive file system by finding the actual existing file 
        /// matching the path case-insensitively. Handles both mixed-case directories and filenames.
        /// </summary>
        private string ResolveCaseInsensitivePath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return null;

            string cacheKey = NormalizePathKey(fullPath);

            // Check cache
            if (_pathResolutionCache.TryGetValue(cacheKey, out var cachedPath))
                return string.IsNullOrEmpty(cachedPath) ? null : cachedPath;

            // Fast path: Check if file exists exactly as requested
            if (File.Exists(fullPath))
            {
                _pathResolutionCache.TryAdd(cacheKey, fullPath);
                return fullPath;
            }

            // Slow path: Resolve path components
            string resolvedPath = RecursiveResolvePath(fullPath);

            // Update cache (store empty string for failure to avoid repeated failed lookups)
            _pathResolutionCache.TryAdd(cacheKey, resolvedPath ?? string.Empty);

            return resolvedPath;
        }

        private string RecursiveResolvePath(string path)
        {
            // If the path points to an existing directory or file, we are good.
            if (File.Exists(path) || Directory.Exists(path))
                return path;

            // Get the parent directory
            string parent = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(parent))
                return null; // Root reached and not found

            // Recursively resolve the parent first
            // This ensures we find the "Anchor" (e.g. Constants.DataPath) correctly
            string resolvedParent = RecursiveResolvePath(parent);
            if (resolvedParent == null)
                return null; // Parent chain broken

            // Now that we have a valid parent, try to find the child (file or dir) ignoring case
            string childName = Path.GetFileName(path);

            try
            {
                // Check files in the resolved parent
                var files = Directory.GetFiles(resolvedParent);
                foreach (var file in files)
                {
                    if (string.Equals(Path.GetFileName(file), childName, StringComparison.OrdinalIgnoreCase))
                        return file;
                }

                // Check directories in the resolved parent
                var dirs = Directory.GetDirectories(resolvedParent);
                foreach (var dir in dirs)
                {
                    if (string.Equals(Path.GetFileName(dir), childName, StringComparison.OrdinalIgnoreCase))
                        return dir;
                }
            }
            catch (Exception ex)
            {
                // Access denied or IO error (common on Android root dirs)
                // We ignore this and return null because we can't search here.
                _logger?.LogTrace($"Error listing directory {resolvedParent}: {ex.Message}");
            }

            return null;
        }

        private static string NormalizePathKey(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Trim().Replace('\\', '/').ToLowerInvariant();
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
            _textures.TryGetValue(NormalizePathKey(path), out var value) ? TouchAndReturn(value).Info : null;

        public TextureScript GetScript(string path) =>
            string.IsNullOrWhiteSpace(path) ? null :
            _textures.TryGetValue(NormalizePathKey(path), out var value) ? TouchAndReturn(value).Script : null;

        public Texture2D GetTexture2D(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            string normalizedKey = NormalizePathKey(path);

            if (!_textures.TryGetValue(normalizedKey, out ClientTexture clientTexture))
                return null;

            Touch(clientTexture);

            if (clientTexture.Texture != null && !clientTexture.Texture.IsDisposed)
                return clientTexture.Texture;

            var textureInfo = clientTexture.Info;
            if (textureInfo?.Width == 0 || textureInfo?.Height == 0 || textureInfo.Data == null)
                return null;

            // Ensure we create texture on Main Thread if possible, usually MonoGame handles this,
            // but Thread safety is good. Here we assume we are in Draw/Update or don't care.
            try
            {
                Texture2D texture;
                bool isCompressed = textureInfo.IsCompressed;

#if ANDROID
                // Android doesn't support DXT (S3TC) compression natively on most GPUs.
                // Use software decompression for DXT formats.
                if (isCompressed && (
                    textureInfo.Format == SurfaceFormat.Dxt1 ||
                    textureInfo.Format == SurfaceFormat.Dxt3 ||
                    textureInfo.Format == SurfaceFormat.Dxt5))
                {
                    byte[] decompressedData = null;

                    if (textureInfo.Format == SurfaceFormat.Dxt1)
                        decompressedData = DxtDecoder.DecompressDXT1(textureInfo.Data, textureInfo.Width, textureInfo.Height);
                    else if (textureInfo.Format == SurfaceFormat.Dxt3)
                        decompressedData = DxtDecoder.DecompressDXT3(textureInfo.Data, textureInfo.Width, textureInfo.Height);
                    else if (textureInfo.Format == SurfaceFormat.Dxt5)
                        decompressedData = DxtDecoder.DecompressDXT5(textureInfo.Data, textureInfo.Width, textureInfo.Height);

                    if (decompressedData != null)
                    {
                        // Create texture as uncompressed Color format (RGBA8888)
                        texture = new Texture2D(_graphicsDevice, textureInfo.Width, textureInfo.Height, false, SurfaceFormat.Color);
                        texture.SetData(decompressedData);

                        clientTexture.Texture = texture;
                        return texture;
                    }
                }
#endif

                if (isCompressed)
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
                            // Bounds check
                            if (dataIndex + (components - 1) >= data.Length) break;

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
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed creating Texture2D for {Path}", path);
                return null;
            }
        }

        private ClientTexture TouchAndReturn(ClientTexture texture)
        {
            Touch(texture);
            return texture;
        }

        private void Touch(ClientTexture texture)
        {
            if (texture != null)
            {
                texture.LastAccessUtc = DateTime.UtcNow;
            }
        }

        private async Task CleanupLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_cleanupInterval, token).ConfigureAwait(false);
                    CleanupStaleTextures();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "TextureLoader cleanup loop error.");
                }
            }
        }

        private void CleanupStaleTextures()
        {
            var cutoff = DateTime.UtcNow - _textureTtl;
            var staleKeys = _textures
                .Where(kvp =>
                    kvp.Value != null &&
                    kvp.Value.LastAccessUtc <= cutoff &&
                    (kvp.Value.Texture == null || kvp.Value.Texture.IsDisposed))
                .Select(kvp => kvp.Key)
                .ToArray();

            if (staleKeys.Length == 0)
            {
                return;
            }

            // Dispose GPU resources on the main thread
            Client.Main.MuGame.ScheduleOnMainThread(() =>
            {
                foreach (var key in staleKeys)
                {
                    if (_textures.TryRemove(key, out var removed))
                    {
                        try
                        {
                            if (removed.Texture != null && !removed.Texture.IsDisposed)
                                removed.Texture.Dispose();
                            removed.Texture = null;
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogDebug(ex, "Failed disposing texture {Path} during cleanup.", key);
                        }

                        _textureTasks.TryRemove(key, out _);
                        _pathResolutionCache.TryRemove(key, out _); // Clean cache too
                    }
                }
            });
        }
    }
}