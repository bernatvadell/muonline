using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Client.Main.Controllers;
using Client.Main.Controls.UI;
using Client.Main.Helpers;
using Client.Main.Models;
using Client.Main.Worlds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Scenes
{
    public class LoadScene : BaseScene
    {
        #region Constants & Configuration

        // Download configuration - tuned for maximum speed
        private const int ChunkSize = 4 * 1024 * 1024;           // 4 MB per chunk
        private const int BufferSize = 256 * 1024;                // 256 KB read buffer
        private const int MaxParallelChunks = 8;                  // Parallel download streams
        private const int MaxRetryAttempts = 3;
        private const int RetryDelayMs = 1000;

        private static readonly TimeSpan HttpTimeout = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan SpeedUpdateInterval = TimeSpan.FromMilliseconds(250);

        // UI Layout
        private const int ProgressBarHeight = 24;
        private const int ProgressBarMargin = 60;
        private const int ProgressBarBottomOffset = 80;

        #endregion

        #region Fields

        private static readonly Lazy<HttpClient> LazyHttpClient = new(CreateHttpClient, LazyThreadSafetyMode.ExecutionAndPublication);
        private static HttpClient Http => LazyHttpClient.Value;

        private Texture2D _backgroundTexture;
        private Texture2D _pixelTexture;
        private BasicEffect _basicEffect;

        private readonly object _progressLock = new();
        private DownloadProgress _currentProgress = new();

        private CancellationTokenSource _loadingCts;
        private bool _isDisposed;

        private readonly string _dataPathUrl = Constants.DataPathUrl;

        // Speed calculation with sliding window
        private readonly ConcurrentQueue<(DateTime Time, long Bytes)> _speedSamples = new();
        private const int MaxSpeedSamples = 20;

        #endregion

        #region Progress Data Structure

        private class DownloadProgress
        {
            public string StatusText { get; set; } = "Initializing...";
            public float Progress { get; set; }
            public long DownloadedBytes { get; set; }
            public long TotalBytes { get; set; }
            public double SpeedBytesPerSecond { get; set; }
            public TimeSpan? EstimatedTimeRemaining { get; set; }
            public bool IsDownloading { get; set; }
        }

        #endregion

        #region HttpClient Configuration

        private static HttpClient CreateHttpClient()
        {
            var handler = new SocketsHttpHandler
            {
                // Connection pooling for parallel requests
                MaxConnectionsPerServer = MaxParallelChunks + 2,
                EnableMultipleHttp2Connections = true,

                // Keep connections alive
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),

                // Performance tuning - larger window for faster transfers
                InitialHttp2StreamWindowSize = 4 * 1024 * 1024,

                // Compression
                AutomaticDecompression = DecompressionMethods.All,

                // Connection settings
                ConnectTimeout = TimeSpan.FromSeconds(30),

#if DEBUG
                SslOptions = { RemoteCertificateValidationCallback = (_, _, _, _) => true }
#endif
            };

            var client = new HttpClient(handler)
            {
                Timeout = HttpTimeout,
                DefaultRequestVersion = HttpVersion.Version20,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
            };

            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MuClient", "1.0"));
            client.DefaultRequestHeaders.Connection.Add("keep-alive");

            return client;
        }

        #endregion

        #region Lifecycle

        public LoadScene()
        {
            _loadingCts = new CancellationTokenSource();
        }

        public override async Task Load()
        {
            await base.Load();

            try
            {
                _backgroundTexture = MuGame.Instance.Content.Load<Texture2D>("Background");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LoadScene] Background load failed: {ex.Message}");
            }

            // Create 1x1 white pixel for drawing shapes
            _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            InitializeGraphics();
            await InitializeWorld();
        }

        private void InitializeGraphics()
        {
            _basicEffect = new BasicEffect(GraphicsDevice)
            {
                VertexColorEnabled = true,
                TextureEnabled = false,
                Projection = Matrix.CreateOrthographicOffCenter(
                    0, UiScaler.VirtualSize.X,
                    UiScaler.VirtualSize.Y, 0,
                    0, 1),
                View = Matrix.Identity,
                World = Matrix.Identity
            };
        }

        private async Task InitializeWorld()
        {
            var loadWorld = new LoadWorld();
            Controls.Add(loadWorld);
            await loadWorld.Initialize();
            World = loadWorld;
        }

        public override void AfterLoad()
        {
            base.AfterLoad();
            _ = PerformInitialLoadAndTransitionAsync();
        }

        #endregion

        #region Core Loading Logic

        private async Task PerformInitialLoadAndTransitionAsync()
        {
            var ct = _loadingCts.Token;

            try
            {
                string localZip = Path.Combine(Constants.DataPath, "Data.zip");
                string extractPath = Constants.DataPath;

                EnsureDirectoryExists(extractPath);

                bool assetsExist = CheckAssetsExist(extractPath);

                if (assetsExist)
                {
                    UpdateProgress(p =>
                    {
                        p.StatusText = "Assets found – skipping download.";
                        p.Progress = 1f;
                    });
                    await Task.Delay(500, ct);
                }
                else
                {
                    await DownloadAndExtractAssetsAsync(localZip, extractPath, ct);
                }

                await TransitionToNextSceneAsync(ct);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[LoadScene] Loading cancelled.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LoadScene] Critical error: {ex}");
                UpdateProgress(p => p.StatusText = $"Error: {ex.Message}");
                await Task.Delay(5000, CancellationToken.None);
                await TransitionToNextSceneAsync(CancellationToken.None);
            }
        }

        private async Task DownloadAndExtractAssetsAsync(string localZip, string extractPath, CancellationToken ct)
        {
            bool success = false;
            string[] urls = { _dataPathUrl, Constants.DefaultDataPathUrl };

            foreach (var url in urls.Where(u => !string.IsNullOrEmpty(u)))
            {
                try
                {
                    UpdateProgress(p =>
                    {
                        p.StatusText = "Connecting...";
                        p.IsDownloading = true;
                    });

                    await DownloadFileParallelAsync(url, localZip, ct);
                    success = true;
                    break;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LoadScene] Download failed from {url}: {ex.Message}");
                    UpdateProgress(p => p.StatusText = "Trying alternative source...");
                    await Task.Delay(RetryDelayMs, ct);
                }
            }

            if (!success)
                throw new InvalidOperationException("Failed to download assets from all sources.");

            UpdateProgress(p =>
            {
                p.StatusText = "Extracting...";
                p.IsDownloading = false;
                p.Progress = 0f;
            });

            await ExtractZipWithProgressAsync(localZip, extractPath, ct);

            UpdateProgress(p => p.StatusText = "Cleaning up...");
            SafeDeleteFile(localZip);
        }

        private async Task TransitionToNextSceneAsync(CancellationToken ct)
        {
            Type nextSceneType = Constants.ENTRY_SCENE == typeof(LoadScene)
                ? typeof(LoginScene)
                : Constants.ENTRY_SCENE;

            UpdateProgress(p =>
            {
                p.StatusText = $"Loading {nextSceneType.Name}...";
                p.Progress = 0f;
                p.IsDownloading = false;
            });

            var nextScene = (BaseScene)Activator.CreateInstance(nextSceneType)!;
            await nextScene.InitializeWithProgressReporting((text, prog) =>
            {
                UpdateProgress(p =>
                {
                    p.StatusText = text;
                    p.Progress = prog;
                });
            });

            UpdateProgress(p =>
            {
                p.StatusText = "Starting...";
                p.Progress = 1f;
            });

            await Task.Delay(200, ct);
            MuGame.Instance.ChangeScene(nextScene);
        }

        #endregion

        #region Parallel Chunked Download

        private async Task DownloadFileParallelAsync(string url, string destination, CancellationToken ct)
        {
            // Get file size and check range support
            long totalSize;
            bool supportsRanges;

            using (var headRequest = new HttpRequestMessage(HttpMethod.Head, url))
            {
                using var headResponse = await Http.SendAsync(headRequest, ct);
                headResponse.EnsureSuccessStatusCode();

                totalSize = headResponse.Content.Headers.ContentLength ?? -1;
                supportsRanges = headResponse.Headers.AcceptRanges.Contains("bytes");
            }

            // Fall back to single stream if parallel download not possible
            if (totalSize <= 0 || !supportsRanges || totalSize < ChunkSize * 2)
            {
                await DownloadFileSingleStreamAsync(url, destination, ct);
                return;
            }

            UpdateProgress(p =>
            {
                p.TotalBytes = totalSize;
                p.DownloadedBytes = 0;
                p.StatusText = "Downloading...";
            });

            // Calculate chunks
            var chunks = CalculateChunks(totalSize);
            var chunkProgress = new long[chunks.Count];

            // Pre-allocate file
            await using (var fs = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.SetLength(totalSize);
            }

            var stopwatch = Stopwatch.StartNew();
            var lastSpeedUpdate = Stopwatch.StartNew();

            // Download chunks in parallel
            await Parallel.ForEachAsync(
                chunks.Select((chunk, index) => (Chunk: chunk, Index: index)),
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = MaxParallelChunks,
                    CancellationToken = ct
                },
                async (item, token) =>
                {
                    await DownloadChunkWithRetryAsync(
                        url, destination, item.Chunk.Start, item.Chunk.End, item.Index,
                        bytes =>
                        {
                            chunkProgress[item.Index] = bytes;
                            long total = chunkProgress.Sum();

                            if (lastSpeedUpdate.Elapsed >= SpeedUpdateInterval)
                            {
                                lock (_progressLock)
                                {
                                    lastSpeedUpdate.Restart();
                                }
                                UpdateDownloadProgress(total, totalSize, stopwatch.Elapsed);
                            }
                        },
                        token);
                });

            // Final progress update
            UpdateDownloadProgress(totalSize, totalSize, stopwatch.Elapsed);
            UpdateProgress(p => p.StatusText = "Download complete!");
        }

        private async Task DownloadChunkWithRetryAsync(
            string url, string destination,
            long start, long end, int chunkIndex,
            Action<long> onProgress,
            CancellationToken ct)
        {
            for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
            {
                try
                {
                    await DownloadChunkAsync(url, destination, start, end, onProgress, ct);
                    return;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LoadScene] Chunk {chunkIndex} attempt {attempt} failed: {ex.Message}");
                    if (attempt == MaxRetryAttempts) throw;
                    await Task.Delay(RetryDelayMs * attempt, ct);
                }
            }
        }

        private async Task DownloadChunkAsync(
            string url, string destination,
            long start, long end,
            Action<long> onProgress,
            CancellationToken ct)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Range = new RangeHeaderValue(start, end);

                using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                await using var sourceStream = await response.Content.ReadAsStreamAsync(ct);
                await using var destStream = new FileStream(
                    destination, FileMode.Open, FileAccess.Write, FileShare.Write,
                    BufferSize, FileOptions.Asynchronous);

                destStream.Seek(start, SeekOrigin.Begin);

                long chunkDownloaded = 0;
                int bytesRead;

                while ((bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, BufferSize), ct)) > 0)
                {
                    await destStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    chunkDownloaded += bytesRead;
                    onProgress?.Invoke(chunkDownloaded);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private async Task DownloadFileSingleStreamAsync(string url, string destination, CancellationToken ct)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

            try
            {
                using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                long totalBytes = response.Content.Headers.ContentLength ?? -1;
                long downloadedBytes = 0;

                UpdateProgress(p =>
                {
                    p.TotalBytes = totalBytes;
                    p.DownloadedBytes = 0;
                    p.StatusText = "Downloading...";
                });

                var stopwatch = Stopwatch.StartNew();
                var lastUpdate = Stopwatch.StartNew();

                await using var source = await response.Content.ReadAsStreamAsync(ct);
                await using var dest = new FileStream(destination, FileMode.Create, FileAccess.Write,
                    FileShare.None, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

                int bytesRead;
                while ((bytesRead = await source.ReadAsync(buffer.AsMemory(0, BufferSize), ct)) > 0)
                {
                    await dest.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    downloadedBytes += bytesRead;

                    if (lastUpdate.Elapsed >= SpeedUpdateInterval)
                    {
                        lastUpdate.Restart();
                        UpdateDownloadProgress(downloadedBytes, totalBytes, stopwatch.Elapsed);
                    }
                }

                UpdateDownloadProgress(downloadedBytes, totalBytes, stopwatch.Elapsed);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private List<(long Start, long End)> CalculateChunks(long totalSize)
        {
            var chunks = new List<(long Start, long End)>();
            long position = 0;

            while (position < totalSize)
            {
                long end = Math.Min(position + ChunkSize - 1, totalSize - 1);
                chunks.Add((position, end));
                position = end + 1;
            }

            return chunks;
        }

        private void UpdateDownloadProgress(long downloaded, long total, TimeSpan elapsed)
        {
            // Add sample for speed calculation
            _speedSamples.Enqueue((DateTime.UtcNow, downloaded));
            while (_speedSamples.Count > MaxSpeedSamples)
                _speedSamples.TryDequeue(out _);

            // Calculate speed from sliding window
            double speed = 0;
            if (_speedSamples.Count >= 2)
            {
                var samples = _speedSamples.ToArray();
                var oldest = samples.First();
                var newest = samples.Last();
                var timeDiff = (newest.Time - oldest.Time).TotalSeconds;

                if (timeDiff > 0)
                    speed = (newest.Bytes - oldest.Bytes) / timeDiff;
            }

            // Calculate ETA
            TimeSpan? eta = null;
            if (speed > 0 && total > 0)
            {
                var remaining = total - downloaded;
                var seconds = remaining / speed;
                if (seconds < 86400) // Less than 24 hours
                    eta = TimeSpan.FromSeconds(seconds);
            }

            UpdateProgress(p =>
            {
                p.DownloadedBytes = downloaded;
                p.TotalBytes = total;
                p.Progress = total > 0 ? (float)downloaded / total : 0;
                p.SpeedBytesPerSecond = speed;
                p.EstimatedTimeRemaining = eta;
                p.IsDownloading = true;
            });
        }

        #endregion

        #region Extraction

        private async Task ExtractZipWithProgressAsync(string zipPath, string outputDirectory, CancellationToken ct)
        {
            await Task.Run(() =>
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var entries = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToArray();

                if (entries.Length == 0)
                    throw new InvalidDataException("ZIP archive is empty.");

                int processed = 0;
                var stopwatch = Stopwatch.StartNew();

                foreach (var entry in entries)
                {
                    ct.ThrowIfCancellationRequested();

                    string relativePath = NormalizeEntryPath(entry.FullName);
                    string fullPath = Path.GetFullPath(Path.Combine(outputDirectory, relativePath));

                    // Security check - prevent path traversal
                    if (!fullPath.StartsWith(Path.GetFullPath(outputDirectory), StringComparison.OrdinalIgnoreCase))
                        continue;

                    var dir = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);

                    try { entry.ExtractToFile(fullPath, true); }
                    catch (IOException ex) { Debug.WriteLine($"[Extract] {entry.Name}: {ex.Message}"); }

                    processed++;

                    if (stopwatch.ElapsedMilliseconds >= 100)
                    {
                        stopwatch.Restart();
                        float progress = (float)processed / entries.Length;
                        UpdateProgress(p =>
                        {
                            p.Progress = progress;
                            p.StatusText = $"Extracting: {entry.Name}";
                        });
                    }
                }

                UpdateProgress(p =>
                {
                    p.Progress = 1f;
                    p.StatusText = "Extraction complete!";
                });
            }, ct);
        }

        private static string NormalizeEntryPath(string entryFullName)
        {
            string normalized = entryFullName.Replace('\\', '/');
            string[] prefixes = { "Data/", "data/" };

            foreach (var prefix in prefixes)
            {
                if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    normalized = normalized[prefix.Length..];
                    break;
                }
            }

            return normalized.Replace('/', Path.DirectorySeparatorChar);
        }

        #endregion

        #region Progress Management

        private void UpdateProgress(Action<DownloadProgress> update)
        {
            lock (_progressLock)
            {
                update(_currentProgress);
            }
        }

        private DownloadProgress GetProgress()
        {
            lock (_progressLock)
            {
                return new DownloadProgress
                {
                    StatusText = _currentProgress.StatusText,
                    Progress = _currentProgress.Progress,
                    DownloadedBytes = _currentProgress.DownloadedBytes,
                    TotalBytes = _currentProgress.TotalBytes,
                    SpeedBytesPerSecond = _currentProgress.SpeedBytesPerSecond,
                    EstimatedTimeRemaining = _currentProgress.EstimatedTimeRemaining,
                    IsDownloading = _currentProgress.IsDownloading
                };
            }
        }

        #endregion

        #region Helpers

        private static void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        private static bool CheckAssetsExist(string dataPath)
        {
            try
            {
                return Directory.EnumerateFileSystemEntries(dataPath)
                    .Any(e => !e.EndsWith("Data.zip", StringComparison.OrdinalIgnoreCase));
            }
            catch { return false; }
        }

        private static void SafeDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) { Debug.WriteLine($"[Delete] {path}: {ex.Message}"); }
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int i = 0;
            double size = bytes;

            while (size >= 1024 && i < suffixes.Length - 1)
            {
                size /= 1024;
                i++;
            }

            return $"{size:F1} {suffixes[i]}";
        }

        private static string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond >= 1024 * 1024)
                return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
            if (bytesPerSecond >= 1024)
                return $"{bytesPerSecond / 1024:F1} KB/s";
            return $"{bytesPerSecond:F0} B/s";
        }

        private static string FormatEta(TimeSpan? eta)
        {
            if (!eta.HasValue) return "";

            var ts = eta.Value;
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            if (ts.TotalMinutes >= 1)
                return $"{ts.Minutes}m {ts.Seconds}s";
            return $"{ts.Seconds}s";
        }

        #endregion

        #region Update & Draw

        public override void Update(GameTime gameTime)
        {
            if (Status == GameControlStatus.NonInitialized)
                _ = Initialize();

            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(12, 12, 20));

            DrawBackground();
            DrawProgressUI();
        }

        private new void DrawBackground()
        {
            if (_backgroundTexture == null) return;

            using var scope = new SpriteBatchScope(
                GraphicsManager.Instance.Sprite,
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                UiScaler.SpriteTransform);

            GraphicsManager.Instance.Sprite.Draw(
                _backgroundTexture,
                new Rectangle(0, 0, UiScaler.VirtualSize.X, UiScaler.VirtualSize.Y),
                Color.White);
        }

        private void DrawProgressUI()
        {
            var progress = GetProgress();
            var sprite = GraphicsManager.Instance.Sprite;

            int screenWidth = UiScaler.VirtualSize.X;
            int screenHeight = UiScaler.VirtualSize.Y;

            int barWidth = screenWidth - (ProgressBarMargin * 2);
            int barX = ProgressBarMargin;
            int barY = screenHeight - ProgressBarBottomOffset;

            using var scope = new SpriteBatchScope(
                sprite,
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.None,
                null,
                null,
                UiScaler.SpriteTransform);

            // === PROGRESS BAR BACKGROUND ===
            // Outer glow/shadow
            DrawRect(sprite, barX - 2, barY - 2, barWidth + 4, ProgressBarHeight + 4,
                new Color(0, 0, 0, 150));

            // Background gradient simulation (dark to slightly lighter)
            DrawRect(sprite, barX, barY, barWidth, ProgressBarHeight,
                new Color(25, 28, 35));
            DrawRect(sprite, barX, barY, barWidth, ProgressBarHeight / 2,
                new Color(35, 38, 45));

            // === PROGRESS BAR FILL ===
            if (progress.Progress > 0.001f)
            {
                int fillWidth = Math.Max(4, (int)(barWidth * progress.Progress));

                // Main gradient fill
                var baseColor = new Color(45, 160, 95);
                var lightColor = new Color(65, 200, 120);
                var darkColor = new Color(35, 130, 75);

                // Bottom part (darker)
                DrawRect(sprite, barX + 1, barY + ProgressBarHeight / 2,
                    fillWidth - 2, ProgressBarHeight / 2 - 1, darkColor);

                // Top part (lighter)
                DrawRect(sprite, barX + 1, barY + 1,
                    fillWidth - 2, ProgressBarHeight / 2, lightColor);

                // Shine effect on top
                DrawRect(sprite, barX + 1, barY + 1,
                    fillWidth - 2, 4, new Color(255, 255, 255, 40));

                // Animated pulse glow (optional, based on time)
                float pulse = (float)(0.5 + 0.5 * Math.Sin(DateTime.Now.Ticks / 2000000.0));
                DrawRect(sprite, barX + fillWidth - 20, barY,
                    20, ProgressBarHeight, new Color(100, 255, 150, (int)(30 * pulse)));
            }

            // === BORDER ===
            DrawBorder(sprite, barX, barY, barWidth, ProgressBarHeight,
                new Color(70, 75, 85), 1);

            // Inner highlight
            DrawRect(sprite, barX + 1, barY + 1, barWidth - 2, 1,
                new Color(255, 255, 255, 20));

            // === TEXT ELEMENTS ===
            var font = GraphicsManager.Instance.Font;
            if (font == null) return;

            // Percentage centered on bar
            string percentText = $"{progress.Progress * 100:F0}%";
            DrawTextCentered(sprite, font, percentText,
                barX + barWidth / 2, barY + ProgressBarHeight / 2,
                Color.White, 0.7f, true);

            // Status text above bar
            DrawTextWithShadow(sprite, font, progress.StatusText,
                barX, barY - 30, Color.White, 0.65f);

            // Download stats below bar
            if (progress.IsDownloading && progress.TotalBytes > 0)
            {
                int statsY = barY + ProgressBarHeight + 12;

                // Size: left aligned
                string sizeText = $"{FormatBytes(progress.DownloadedBytes)} / {FormatBytes(progress.TotalBytes)}";
                DrawTextWithShadow(sprite, font, sizeText, barX, statsY,
                    new Color(170, 175, 190), 0.55f);

                // Speed: centered
                if (progress.SpeedBytesPerSecond > 0)
                {
                    string speedText = $"⚡ {FormatSpeed(progress.SpeedBytesPerSecond)}";
                    DrawTextCentered(sprite, font, speedText,
                        barX + barWidth / 2, statsY + 6,
                        new Color(100, 180, 255), 0.55f, true);
                }

                // ETA: right aligned
                if (progress.EstimatedTimeRemaining.HasValue)
                {
                    string etaText = $"⏱ {FormatEta(progress.EstimatedTimeRemaining)}";
                    var etaSize = font.MeasureString(etaText) * 0.55f;
                    DrawTextWithShadow(sprite, font, etaText,
                        barX + barWidth - (int)etaSize.X, statsY,
                        new Color(170, 175, 190), 0.55f);
                }
            }
        }

        #region Drawing Helpers

        private void DrawRect(SpriteBatch sprite, int x, int y, int width, int height, Color color)
        {
            if (_pixelTexture == null || width <= 0 || height <= 0) return;
            sprite.Draw(_pixelTexture, new Rectangle(x, y, width, height), color);
        }

        private void DrawBorder(SpriteBatch sprite, int x, int y, int width, int height, Color color, int thickness)
        {
            if (_pixelTexture == null) return;

            // Top
            sprite.Draw(_pixelTexture, new Rectangle(x, y, width, thickness), color);
            // Bottom
            sprite.Draw(_pixelTexture, new Rectangle(x, y + height - thickness, width, thickness), color);
            // Left
            sprite.Draw(_pixelTexture, new Rectangle(x, y, thickness, height), color);
            // Right
            sprite.Draw(_pixelTexture, new Rectangle(x + width - thickness, y, thickness, height), color);
        }

        private void DrawTextWithShadow(SpriteBatch sprite, SpriteFont font, string text,
            int x, int y, Color color, float scale)
        {
            if (string.IsNullOrEmpty(text) || font == null) return;

            var pos = new Vector2(x, y);
            sprite.DrawString(font, text, pos + new Vector2(1, 1), Color.Black * 0.8f,
                0, Vector2.Zero, scale, SpriteEffects.None, 0);
            sprite.DrawString(font, text, pos, color,
                0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }

        private void DrawTextCentered(SpriteBatch sprite, SpriteFont font, string text,
            int centerX, int centerY, Color color, float scale, bool withShadow)
        {
            if (string.IsNullOrEmpty(text) || font == null) return;

            var size = font.MeasureString(text) * scale;
            var pos = new Vector2(centerX - size.X / 2, centerY - size.Y / 2);

            if (withShadow)
            {
                sprite.DrawString(font, text, pos + new Vector2(1, 1), Color.Black * 0.8f,
                    0, Vector2.Zero, scale, SpriteEffects.None, 0);
            }

            sprite.DrawString(font, text, pos, color,
                0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }

        #endregion

        #endregion

        #region Disposal

        public override void Dispose()
        {
            if (_isDisposed) return;

            _loadingCts?.Cancel();
            _loadingCts?.Dispose();
            _loadingCts = null;

            _basicEffect?.Dispose();
            _pixelTexture?.Dispose();

            _isDisposed = true;
            base.Dispose();
        }

        #endregion
    }
}