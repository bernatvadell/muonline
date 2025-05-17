using System;
using System.Buffers;
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
        #region ── stałe & pola ──────────────────────────────────────────────

        private const int BufferSize = 1 * 1024 * 1024;              // 1 MB
        private static readonly TimeSpan ProgressTick = TimeSpan.FromMilliseconds(200);

        private static readonly HttpClient Http;

        private LabelControl _statusLabel;
        private float _progress;        // 0-1
        private string _statusText;

        private Texture2D _backgroundTexture;
        private BasicEffect _basicEffect;

        private const int ProgressBarHeight = 30;
        private const int ProgressBarY = 700;
        private readonly string _dataPathUrl = Constants.DataPathUrl;

        #endregion

        #region ── HttpClient ─────────────────────

        static LoadScene()
        {
            var handler = new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                EnableMultipleHttp2Connections = true,
                SslOptions =
                {
                    RemoteCertificateValidationCallback = (_,__,___,____) => true // DEV-only
                }
            };

            Http = new HttpClient(handler)
            {
                Timeout = Timeout.InfiniteTimeSpan,
                DefaultRequestVersion = HttpVersion.Version30,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrLower
            };

            Http.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("MuClient", "1.0"));
        }

        #endregion

        #region

        public LoadScene()
        {
            _progress = 0f;
            _statusText = "Initializing...";

            _statusLabel = new LabelControl
            {
                Text = _statusText,
                X = 50, // Margin from left
                Y = MuGame.Instance.Height - 80, // Position above progress bar
                FontSize = 20, // Slightly smaller for more text
                TextColor = Color.White,
                ShadowColor = Color.Black * 0.7f,
                HasShadow = true,
                ShadowOffset = new Vector2(1, 1)
            };
            Controls.Add(_statusLabel);
        }

        #endregion

        #region

        public override async Task Load()
        {
            await base.Load();
            _backgroundTexture = MuGame.Instance.Content.Load<Texture2D>("Background");

            _basicEffect = new BasicEffect(MuGame.Instance.GraphicsDevice)
            {
                VertexColorEnabled = true,
                Projection = Matrix.CreateOrthographicOffCenter
                             (0, MuGame.Instance.Width, MuGame.Instance.Height, 0, 0, 1),
                View = Matrix.Identity,
                World = Matrix.Identity
            };

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

        #region Core Loading Orchestration

        private async Task PerformInitialLoadAndTransitionAsync()
        {
            string localZip = Path.Combine(Constants.DataPath, "Data.zip");
            string extractPath = Constants.DataPath;
            string url = _dataPathUrl;

            bool alreadyHaveAssets = Directory.EnumerateFileSystemEntries(Constants.DataPath)
                                             .Any(e => !e.EndsWith("Data.zip",
                                                                   StringComparison.OrdinalIgnoreCase));

            if (alreadyHaveAssets)
            {
                UpdateStatus("Assets found – skipping download.", 1);
                await Task.Delay(500);
            }
            else
            {
                try
                {
                    UpdateStatus("Downloading assets…", 0);
                    await DownloadFileWithProgressAsync(url, localZip, UpdateStatus);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Primary URL failed: {ex.Message}");
                    url = Constants.DefaultDataPathUrl;
                    UpdateStatus("Retrying with default URL…", 0);
                    await DownloadFileWithProgressAsync(url, localZip, UpdateStatus);
                }

                UpdateStatus("Extracting assets…", 0);
                await ExtractZipFileWithProgressAsync(localZip, extractPath, UpdateStatus);

                UpdateStatus("Cleaning up…", 1);
                if (File.Exists(localZip)) File.Delete(localZip);
            }

            await TransitionToEntrySceneAsync();
        }

        private async Task TransitionToEntrySceneAsync()
        {
            Type nextSceneType = Constants.ENTRY_SCENE == typeof(LoadScene)
                                   ? typeof(LoginScene)
                                   : Constants.ENTRY_SCENE;

            UpdateStatus($"Loading {nextSceneType.Name}…", 0);

            var nextScene = (BaseScene)Activator.CreateInstance(nextSceneType)!;
            await nextScene.InitializeWithProgressReporting(UpdateStatus);

            UpdateStatus("Transitioning…", 1);
            await Task.Delay(300);
            MuGame.Instance.ChangeScene(nextScene);
        }

        #endregion

        #region ── DownloadFileWithProgressAsync ─────────────────────────────

        private async Task DownloadFileWithProgressAsync(
            string url, string destination,
            Action<string, float>? report,
            CancellationToken ct = default)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

            try
            {
                using var resp = await Http.GetAsync(url,
                               HttpCompletionOption.ResponseHeadersRead, ct);
                resp.EnsureSuccessStatusCode();

                long total = resp.Content.Headers.ContentLength ?? -1;
                long done = 0;
                var sw = Stopwatch.StartNew();

                await using var src = await resp.Content.ReadAsStreamAsync(ct);
                await using var dst = new FileStream(destination, FileMode.Create,
                                    FileAccess.Write, FileShare.None,
                                    BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

                while (true)
                {
                    int n = await src.ReadAsync(buffer.AsMemory(0, BufferSize), ct);
                    if (n == 0) break;

                    await dst.WriteAsync(buffer.AsMemory(0, n), ct);
                    done += n;

                    if (sw.Elapsed >= ProgressTick)
                    {
                        sw.Restart();
                        float pr = total > 0 ? (float)done / total : 0;
                        report?.Invoke($"Downloading... {pr * 100:F0}% ({done / 1_048_576:F1} MB)", pr);
                    }
                }
                await dst.FlushAsync(ct);
                report?.Invoke("Download complete.", 1);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        #endregion

        #region ── ExtractZipFileWithProgressAsync ───────────────────────────

        private async Task ExtractZipFileWithProgressAsync(
            string zip, string outDir,
            Action<string, float> report,
            CancellationToken ct = default)
        {
            await Task.Run(() =>
            {
                using var archive = ZipFile.OpenRead(zip);
                var files = archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).ToArray();
                int done = 0; var sw = Stopwatch.StartNew();

                foreach (var entry in files)
                {
                    ct.ThrowIfCancellationRequested();

                    string rel = entry.FullName.TrimStart("Data/".ToCharArray())
                                                 .TrimStart("Data\\".ToCharArray());
                    string full = Path.Combine(outDir, rel);

                    Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                    entry.ExtractToFile(full, true);

                    done++;
                    if (sw.Elapsed >= ProgressTick)
                    {
                        sw.Restart();
                        float pr = (float)done / files.Length;
                        report?.Invoke($"Extracting... {pr * 100:F0}% ({entry.Name})", pr);
                    }
                }
                report?.Invoke("Extracting completed.", 1);
            }, ct);
        }

        #endregion

        #region ── UpdateStatus ──────────────────────

        private void UpdateStatus(string text, float progress)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                _statusText = text;
                _progress = MathHelper.Clamp(progress, 0, 1);
                _statusLabel.Text = _statusText;
            });
        }

        public override void Update(GameTime gameTime)
        {
            if (Status == GameControlStatus.NonInitialized)
                _ = Initialize();

            base.Update(gameTime);
        }

        private VertexPositionColor[] CreateRect(Vector2 pos, Vector2 size, Color col) =>
        [
            new(new Vector3(pos.X,           pos.Y,            0), col),
            new(new Vector3(pos.X+size.X,    pos.Y,            0), col),
            new(new Vector3(pos.X,           pos.Y+size.Y,     0), col),
            new(new Vector3(pos.X+size.X,    pos.Y+size.Y,     0), col)
        ];

        public override void Draw(GameTime gameTime)
        {
            if (_basicEffect == null || _backgroundTexture == null)
            {
                GraphicsDevice.Clear(Color.Black);
                if (_statusLabel.Status == GameControlStatus.Ready)
                {
                    using (new SpriteBatchScope(GraphicsManager.Instance.Sprite))
                        _statusLabel.Draw(gameTime);
                }
                return;
            }

            DrawSceneBackground();
            DrawProgressBar();

            using (new SpriteBatchScope(GraphicsManager.Instance.Sprite,
                                        SpriteSortMode.Deferred,
                                        BlendState.AlphaBlend,
                                        SamplerState.PointClamp,
                                        DepthStencilState.None))
            {
                if (_statusLabel.Visible) _statusLabel.Draw(gameTime);
            }
        }

        private void DrawSceneBackground()
        {
            using (new SpriteBatchScope(GraphicsManager.Instance.Sprite))
            {
                GraphicsManager.Instance.Sprite.Draw(
                    _backgroundTexture,
                    new Rectangle(0, 0, MuGame.Instance.Width, MuGame.Instance.Height),
                    Color.White);
            }
        }

        private void DrawProgressBar()
        {
            int w = MuGame.Instance.Width - 100;
            int x = 50;

            var bg = CreateRect(new Vector2(x, ProgressBarY),
                                     new Vector2(w, ProgressBarHeight),
                                     Color.DarkSlateGray);
            var prog = CreateRect(new Vector2(x, ProgressBarY),
                                     new Vector2(w * _progress, ProgressBarHeight),
                                     Color.ForestGreen);

            _basicEffect.TextureEnabled = false;
            _basicEffect.VertexColorEnabled = true;

            foreach (var pass in _basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, bg, 0, 2);
                if (_progress > 0)
                    GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, prog, 0, 2);
            }
        }
        #endregion
    }
}