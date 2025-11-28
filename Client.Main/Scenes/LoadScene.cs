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
using System.Runtime.InteropServices;
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

        // Download tuning - balanced for all platforms
        private const int ChunkSize = 2 * 1024 * 1024;            // 2 MB per chunk (safer for mobile)
        private const int BufferSize = 128 * 1024;                 // 128 KB read buffer
        private const int MaxRetryAttempts = 3;
        private const int RetryDelayMs = 1000;

        private static readonly TimeSpan HttpTimeout = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan SpeedUpdateInterval = TimeSpan.FromMilliseconds(300);

        // Parallel chunks based on platform
        private static int MaxParallelChunks => IsAndroid ? 4 : 6;

        // UI Layout
        private const int ProgressBarHeight = 24;
        private const int ProgressBarMargin = 60;
        private const int ProgressBarBottomOffset = 80;

        #endregion

        #region Platform Detection

        private static bool IsAndroid =>
#if ANDROID
            true;
#else
            RuntimeInformation.IsOSPlatform(OSPlatform.Create("ANDROID"));
#endif

        private static bool IsDesktop =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        #endregion

        #region Fields

        // Separate clients for metadata vs download (no decompression for downloads!)
        private static readonly Lazy<HttpClient> MetadataClient = new(CreateMetadataClient, LazyThreadSafetyMode.ExecutionAndPublication);
        private static readonly Lazy<HttpClient> DownloadClient = new(CreateDownloadClient, LazyThreadSafetyMode.ExecutionAndPublication);

        private Texture2D _backgroundTexture;
        private Texture2D _pixelTexture;
        private BasicEffect _basicEffect;

        private readonly object _progressLock = new();
        private DownloadProgress _currentProgress = new();

        private CancellationTokenSource _loadingCts;
        private bool _isDisposed;

        private readonly string _dataPathUrl = Constants.DataPathUrl;

        // Speed calculation with sliding window
        private readonly ConcurrentQueue<(long Ticks, long Bytes)> _speedSamples = new();
        private const int MaxSpeedSamples = 12;

        #endregion

        #region Progress Data

        private sealed class DownloadProgress
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

        /// <summary>
        /// Client for HEAD requests and metadata - can use compression
        /// </summary>
        private static HttpClient CreateMetadataClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.None, // We want raw size
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5
            };

#if DEBUG
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
#endif

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MuClient", "1.0"));
            return client;
        }

        /// <summary>
        /// Client for actual file downloads - NO decompression to get accurate byte counts
        /// </summary>
        private static HttpClient CreateDownloadClient()
        {
            var handler = new HttpClientHandler
            {
                // CRITICAL: No automatic decompression for downloads!
                // This ensures Content-Length matches actual bytes we read
                AutomaticDecompression = DecompressionMethods.None,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                MaxConnectionsPerServer = MaxParallelChunks + 2
            };

#if DEBUG
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
#endif

            var client = new HttpClient(handler)
            {
                Timeout = HttpTimeout
            };

            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MuClient", "1.0"));
            client.DefaultRequestHeaders.Connection.Add("keep-alive");

            // Don't ask for compressed content - we want raw bytes
            // (If server sends compressed anyway, Content-Length will match)

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
                    UiScaler.VirtualSize.Y, 0, 0, 1),
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

                if (CheckAssetsExist(extractPath))
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
            string[] urls = { _dataPathUrl, Constants.DefaultDataPathUrl };
            Exception lastError = null;

            foreach (var url in urls.Where(u => !string.IsNullOrEmpty(u)))
            {
                try
                {
                    UpdateProgress(p =>
                    {
                        p.StatusText = "Connecting...";
                        p.IsDownloading = true;
                        p.Progress = 0;
                    });

                    // Clear speed samples for fresh calculation
                    while (_speedSamples.TryDequeue(out _)) { }

                    await DownloadFileAsync(url, localZip, ct);

                    // Verify download completed correctly
                    if (!File.Exists(localZip))
                        throw new IOException("Download failed - file not created");

                    var fileInfo = new FileInfo(localZip);
                    if (fileInfo.Length == 0)
                        throw new IOException("Download failed - file is empty");

                    lastError = null;
                    break;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    lastError = ex;
                    Debug.WriteLine($"[LoadScene] Download failed from {url}: {ex.Message}");
                    SafeDeleteFile(localZip);
                    UpdateProgress(p => p.StatusText = "Trying alternative source...");
                    await Task.Delay(RetryDelayMs, ct);
                }
            }

            if (lastError != null)
                throw new InvalidOperationException("Failed to download from all sources.", lastError);

            // Extract
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
                UpdateProgress(p => { p.StatusText = text; p.Progress = prog; }));

            UpdateProgress(p => { p.StatusText = "Starting..."; p.Progress = 1f; });
            await Task.Delay(200, ct);
            MuGame.Instance.ChangeScene(nextScene);
        }

        #endregion

        #region Download Implementation

        private async Task DownloadFileAsync(string url, string destination, CancellationToken ct)
        {
            // Step 1: Get file info
            var (totalSize, supportsRanges) = await GetFileInfoAsync(url, ct);

            Debug.WriteLine($"[LoadScene] File size: {totalSize}, Ranges: {supportsRanges}");

            // Step 2: Choose download strategy
            if (totalSize > 0 && supportsRanges && totalSize > ChunkSize * 2)
            {
                await DownloadParallelAsync(url, destination, totalSize, ct);
            }
            else
            {
                await DownloadSequentialAsync(url, destination, totalSize, ct);
            }
        }

        private async Task<(long Size, bool SupportsRanges)> GetFileInfoAsync(string url, CancellationToken ct)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                using var response = await MetadataClient.Value.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();

                long size = response.Content.Headers.ContentLength ?? -1;
                bool ranges = response.Headers.AcceptRanges.Contains("bytes");

                return (size, ranges);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LoadScene] HEAD request failed: {ex.Message}");
                return (-1, false);
            }
        }

        #endregion

        #region Sequential Download

        private async Task DownloadSequentialAsync(string url, string destination, long expectedSize, CancellationToken ct)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

            try
            {
                using var response = await DownloadClient.Value.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                // Use actual Content-Length from response (more reliable)
                long totalBytes = response.Content.Headers.ContentLength ?? expectedSize;
                if (totalBytes <= 0) totalBytes = expectedSize;

                UpdateProgress(p =>
                {
                    p.TotalBytes = totalBytes;
                    p.DownloadedBytes = 0;
                    p.StatusText = "Downloading...";
                });

                var stopwatch = Stopwatch.StartNew();
                var lastUpdate = Stopwatch.StartNew();
                long downloadedBytes = 0;

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
                        ReportProgress(downloadedBytes, totalBytes);
                    }
                }

                await dest.FlushAsync(ct);
                ReportProgress(downloadedBytes, downloadedBytes); // Use actual size for 100%

                UpdateProgress(p => p.StatusText = "Download complete!");
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        #endregion

        #region Parallel Chunked Download

        private async Task DownloadParallelAsync(string url, string destination, long totalSize, CancellationToken ct)
        {
            UpdateProgress(p =>
            {
                p.TotalBytes = totalSize;
                p.DownloadedBytes = 0;
                p.StatusText = "Downloading...";
            });

            // Pre-allocate file
            await using (var fs = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                fs.SetLength(totalSize);
            }

            // Calculate chunks
            var chunks = new List<ChunkInfo>();
            long position = 0;
            int index = 0;

            while (position < totalSize)
            {
                long end = Math.Min(position + ChunkSize - 1, totalSize - 1);
                chunks.Add(new ChunkInfo
                {
                    Index = index++,
                    Start = position,
                    End = end,
                    Size = end - position + 1
                });
                position = end + 1;
            }

            // Track progress per chunk
            var chunkDownloaded = new long[chunks.Count];
            var lastUpdateTime = Stopwatch.StartNew();
            var downloadStart = Stopwatch.StartNew();

            // Semaphore to limit parallelism
            using var semaphore = new SemaphoreSlim(MaxParallelChunks);

            var tasks = chunks.Select(async chunk =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    await DownloadChunkWithRetryAsync(url, destination, chunk, bytes =>
                    {
                        // Track this chunk's progress (not cumulative, just this chunk)
                        Interlocked.Exchange(ref chunkDownloaded[chunk.Index], bytes);

                        if (lastUpdateTime.Elapsed >= SpeedUpdateInterval)
                        {
                            lock (_progressLock)
                            {
                                if (lastUpdateTime.Elapsed >= SpeedUpdateInterval)
                                {
                                    lastUpdateTime.Restart();
                                    long total = chunkDownloaded.Sum();
                                    ReportProgress(total, totalSize);
                                }
                            }
                        }
                    }, ct);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToArray();

            await Task.WhenAll(tasks);

            // Final progress
            ReportProgress(totalSize, totalSize);
            UpdateProgress(p => p.StatusText = "Download complete!");

            // Verify file size
            var actualSize = new FileInfo(destination).Length;
            if (actualSize != totalSize)
            {
                throw new IOException($"Download size mismatch: expected {totalSize}, got {actualSize}");
            }
        }

        private sealed class ChunkInfo
        {
            public int Index { get; set; }
            public long Start { get; set; }
            public long End { get; set; }
            public long Size { get; set; }
        }

        private async Task DownloadChunkWithRetryAsync(
            string url, string destination, ChunkInfo chunk,
            Action<long> onProgress, CancellationToken ct)
        {
            Exception lastError = null;

            for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
            {
                try
                {
                    await DownloadChunkAsync(url, destination, chunk, onProgress, ct);
                    return;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    lastError = ex;
                    Debug.WriteLine($"[LoadScene] Chunk {chunk.Index} attempt {attempt} failed: {ex.Message}");

                    // Reset progress for this chunk on retry
                    onProgress?.Invoke(0);

                    if (attempt < MaxRetryAttempts)
                        await Task.Delay(RetryDelayMs * attempt, ct);
                }
            }

            throw new IOException($"Chunk {chunk.Index} failed after {MaxRetryAttempts} attempts", lastError);
        }

        private async Task DownloadChunkAsync(
            string url, string destination, ChunkInfo chunk,
            Action<long> onProgress, CancellationToken ct)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Range = new RangeHeaderValue(chunk.Start, chunk.End);

                using var response = await DownloadClient.Value.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                if (response.StatusCode != HttpStatusCode.PartialContent &&
                    response.StatusCode != HttpStatusCode.OK)
                {
                    throw new HttpRequestException($"Unexpected status: {response.StatusCode}");
                }

                await using var source = await response.Content.ReadAsStreamAsync(ct);
                await using var dest = new FileStream(destination, FileMode.Open, FileAccess.Write, FileShare.Write,
                    BufferSize, FileOptions.Asynchronous);

                dest.Seek(chunk.Start, SeekOrigin.Begin);

                long bytesWritten = 0;
                long maxBytes = chunk.Size; // Don't read more than chunk size!
                int bytesRead;

                while (bytesWritten < maxBytes &&
                       (bytesRead = await source.ReadAsync(buffer.AsMemory(0, (int)Math.Min(BufferSize, maxBytes - bytesWritten)), ct)) > 0)
                {
                    await dest.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    bytesWritten += bytesRead;
                    onProgress?.Invoke(bytesWritten);
                }

                // Verify we got the expected amount
                if (bytesWritten != chunk.Size)
                {
                    throw new IOException($"Chunk size mismatch: expected {chunk.Size}, got {bytesWritten}");
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        #endregion

        #region Progress Tracking

        private void ReportProgress(long downloaded, long total)
        {
            // Clamp to prevent > 100%
            downloaded = Math.Min(downloaded, total);

            // Add sample for speed calculation
            long now = Stopwatch.GetTimestamp();
            _speedSamples.Enqueue((now, downloaded));

            while (_speedSamples.Count > MaxSpeedSamples)
                _speedSamples.TryDequeue(out _);

            // Calculate speed
            double speed = 0;
            var samples = _speedSamples.ToArray();

            if (samples.Length >= 2)
            {
                var oldest = samples[0];
                var newest = samples[^1];
                double seconds = (newest.Ticks - oldest.Ticks) / (double)Stopwatch.Frequency;

                if (seconds > 0.1)
                    speed = (newest.Bytes - oldest.Bytes) / seconds;
            }

            // Calculate ETA
            TimeSpan? eta = null;
            if (speed > 100 && total > 0 && downloaded < total)
            {
                double remaining = total - downloaded;
                double seconds = remaining / speed;
                if (seconds is > 0 and < 86400)
                    eta = TimeSpan.FromSeconds(seconds);
            }

            UpdateProgress(p =>
            {
                p.DownloadedBytes = downloaded;
                p.TotalBytes = total;
                p.Progress = total > 0 ? Math.Min(1f, (float)downloaded / total) : 0;
                p.SpeedBytesPerSecond = speed;
                p.EstimatedTimeRemaining = eta;
                p.IsDownloading = true;
            });
        }

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
                string outputFullPath = Path.GetFullPath(outputDirectory);

                foreach (var entry in entries)
                {
                    ct.ThrowIfCancellationRequested();

                    string relativePath = NormalizeEntryPath(entry.FullName);
                    string fullPath = Path.GetFullPath(Path.Combine(outputDirectory, relativePath));

                    // Security: prevent path traversal
                    if (!fullPath.StartsWith(outputFullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.WriteLine($"[Extract] Skipping unsafe path: {entry.FullName}");
                        continue;
                    }

                    string dir = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);

                    try
                    {
                        entry.ExtractToFile(fullPath, overwrite: true);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Extract] Failed: {entry.Name} - {ex.Message}");
                    }

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

            // Remove common root prefixes
            foreach (var prefix in new[] { "Data/", "data/" })
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
            catch (Exception ex) { Debug.WriteLine($"[Delete] {ex.Message}"); }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 0) return "?";
            string[] units = { "B", "KB", "MB", "GB" };
            int i = 0;
            double size = bytes;
            while (size >= 1024 && i < units.Length - 1) { size /= 1024; i++; }
            return $"{size:F1} {units[i]}";
        }

        private static string FormatSpeed(double bytesPerSecond)
        {
            if (bytesPerSecond <= 0) return "-- MB/s";
            if (bytesPerSecond >= 1024 * 1024)
                return $"{bytesPerSecond / (1024 * 1024):F1} MB/s";
            if (bytesPerSecond >= 1024)
                return $"{bytesPerSecond / 1024:F0} KB/s";
            return $"{bytesPerSecond:F0} B/s";
        }

        private static string FormatEta(TimeSpan? eta)
        {
            if (!eta.HasValue) return "--:--";
            var ts = eta.Value;
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            return $"{ts.Minutes}:{ts.Seconds:D2}";
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
                GraphicsManager.Instance.Sprite, SpriteSortMode.Deferred,
                BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone,
                null, UiScaler.SpriteTransform);

            GraphicsManager.Instance.Sprite.Draw(_backgroundTexture,
                new Rectangle(0, 0, UiScaler.VirtualSize.X, UiScaler.VirtualSize.Y), Color.White);
        }

        private void DrawProgressUI()
        {
            var progress = GetProgress();
            var sprite = GraphicsManager.Instance.Sprite;
            var font = GraphicsManager.Instance.Font;

            int screenW = UiScaler.VirtualSize.X;
            int screenH = UiScaler.VirtualSize.Y;
            int barW = screenW - ProgressBarMargin * 2;
            int barX = ProgressBarMargin;
            int barY = screenH - ProgressBarBottomOffset;

            using var scope = new SpriteBatchScope(sprite, SpriteSortMode.Deferred,
                BlendState.AlphaBlend, SamplerState.PointClamp,
                DepthStencilState.None, null, null, UiScaler.SpriteTransform);

            // Background shadow
            DrawRect(sprite, barX - 3, barY - 3, barW + 6, ProgressBarHeight + 6, new Color(0, 0, 0, 120));

            // Bar background
            DrawRect(sprite, barX, barY, barW, ProgressBarHeight, new Color(30, 32, 40));
            DrawRect(sprite, barX, barY, barW, ProgressBarHeight / 2, new Color(38, 40, 50));

            // Progress fill
            if (progress.Progress > 0.001f)
            {
                int fillW = (int)(barW * Math.Min(progress.Progress, 1f));
                if (fillW > 2)
                {
                    // Gradient effect
                    DrawRect(sprite, barX + 1, barY + 1, fillW - 2, ProgressBarHeight - 2, new Color(40, 150, 90));
                    DrawRect(sprite, barX + 1, barY + 1, fillW - 2, ProgressBarHeight / 2 - 1, new Color(60, 190, 115));

                    // Top shine
                    DrawRect(sprite, barX + 1, barY + 1, fillW - 2, 3, new Color(255, 255, 255, 35));
                }
            }

            // Border
            DrawBorder(sprite, barX, barY, barW, ProgressBarHeight, new Color(60, 65, 80), 1);

            if (font == null) return;

            // Percentage
            string pctText = $"{progress.Progress * 100:F0}%";
            DrawTextCentered(sprite, font, pctText, barX + barW / 2, barY + ProgressBarHeight / 2, Color.White, 0.65f);

            // Status above bar
            DrawTextShadow(sprite, font, progress.StatusText, barX, barY - 28, Color.White, 0.6f);

            // Stats below bar (only when downloading)
            if (progress.IsDownloading && progress.TotalBytes > 0)
            {
                int y = barY + ProgressBarHeight + 10;
                var gray = new Color(160, 165, 180);

                // Size (left)
                string sizeText = $"{FormatBytes(progress.DownloadedBytes)} / {FormatBytes(progress.TotalBytes)}";
                DrawTextShadow(sprite, font, sizeText, barX, y, gray, 0.5f);

                // Speed (center)
                string speedText = FormatSpeed(progress.SpeedBytesPerSecond);
                DrawTextCentered(sprite, font, speedText, barX + barW / 2, y + 5, new Color(90, 170, 240), 0.5f);

                // ETA (right)
                string etaText = FormatEta(progress.EstimatedTimeRemaining);
                var etaSize = font.MeasureString(etaText) * 0.5f;
                DrawTextShadow(sprite, font, etaText, barX + barW - (int)etaSize.X, y, gray, 0.5f);
            }
        }

        private void DrawRect(SpriteBatch sprite, int x, int y, int w, int h, Color color)
        {
            if (_pixelTexture != null && w > 0 && h > 0)
                sprite.Draw(_pixelTexture, new Rectangle(x, y, w, h), color);
        }

        private void DrawBorder(SpriteBatch sprite, int x, int y, int w, int h, Color color, int t)
        {
            if (_pixelTexture == null) return;
            sprite.Draw(_pixelTexture, new Rectangle(x, y, w, t), color);
            sprite.Draw(_pixelTexture, new Rectangle(x, y + h - t, w, t), color);
            sprite.Draw(_pixelTexture, new Rectangle(x, y, t, h), color);
            sprite.Draw(_pixelTexture, new Rectangle(x + w - t, y, t, h), color);
        }

        private static void DrawTextShadow(SpriteBatch sprite, SpriteFont font, string text, int x, int y, Color color, float scale)
        {
            if (string.IsNullOrEmpty(text)) return;
            var pos = new Vector2(x, y);
            sprite.DrawString(font, text, pos + Vector2.One, Color.Black * 0.7f, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            sprite.DrawString(font, text, pos, color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }

        private static void DrawTextCentered(SpriteBatch sprite, SpriteFont font, string text, int cx, int cy, Color color, float scale)
        {
            if (string.IsNullOrEmpty(text)) return;
            var size = font.MeasureString(text) * scale;
            var pos = new Vector2(cx - size.X / 2, cy - size.Y / 2);
            sprite.DrawString(font, text, pos + Vector2.One, Color.Black * 0.7f, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            sprite.DrawString(font, text, pos, color, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
        }

        #endregion

        #region Disposal

        public override void Dispose()
        {
            if (_isDisposed) return;
            _loadingCts?.Cancel();
            _loadingCts?.Dispose();
            _basicEffect?.Dispose();
            _pixelTexture?.Dispose();
            _isDisposed = true;
            base.Dispose();
        }

        #endregion
    }
}