using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Client.Main.Controllers;
using Client.Main.Controls.UI;
using Client.Main.Helpers;
using Client.Main.Worlds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;
using Client.Main.Models;

namespace Client.Main.Scenes
{

    public class LoadScene : BaseScene
    {
        #region Fields & Constants

        private LabelControl _statusLabel;
        private float _progress; // value from 0 to 1
        private string _statusText;

        private Texture2D _backgroundTexture;
        private BasicEffect _basicEffect;

        private const int ProgressBarHeight = 30;
        private const int ProgressBarY = 700; // Adjusted Y for visibility
        private string _dataPathUrl = Constants.DataPathUrl;

        #endregion

        #region Constructor & Loading

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

        public override async Task Load()
        {
            await base.Load(); // Important: BaseScene.Load initializes its own controls like DebugPanel
            Console.WriteLine("LoadScene.Load: Base Load complete.");

            _backgroundTexture = MuGame.Instance.Content.Load<Texture2D>("Background");
            Console.WriteLine("LoadScene.Load: Background texture loaded.");

            _basicEffect = new BasicEffect(MuGame.Instance.GraphicsDevice)
            {
                VertexColorEnabled = true,
                Projection = Matrix.CreateOrthographicOffCenter(0, MuGame.Instance.Width, MuGame.Instance.Height, 0, 0, 1),
                View = Matrix.Identity,
                World = Matrix.Identity
            };
            Console.WriteLine("LoadScene.Load: BasicEffect initialized.");

            // Load LoadWorld for its camera settings, but don't make it block.
            // The primary purpose of LoadScene now is to manage transitions.
            var loadWorld = new LoadWorld();
            Controls.Add(loadWorld); // Add to controls to be managed
            await loadWorld.Initialize(); // Initialize it
            World = loadWorld; // Set as current world for LoadScene
            Console.WriteLine("LoadScene.Load: LoadWorld initialized and set.");
        }

        public override void AfterLoad()
        {
            base.AfterLoad();
            Console.WriteLine("LoadScene.AfterLoad: Starting initial load and transition process.");
            // Start the main loading and transition logic asynchronously
            // This allows the LoadScene's Update/Draw loop to run and show progress
            _ = PerformInitialLoadAndTransitionAsync();
        }

        #endregion

        #region Core Loading Orchestration

        private async Task PerformInitialLoadAndTransitionAsync()
        {
            string localZipPath = Path.Combine(Constants.DataPath, "Data.zip");
            string extractPath = Constants.DataPath;
            string zipUrl = _dataPathUrl;

            bool hasOtherEntries = Directory.EnumerateFileSystemEntries(Constants.DataPath)
                                            .Any(entry => !string.Equals(Path.GetFileName(entry), "Data.zip", StringComparison.OrdinalIgnoreCase));

            if (hasOtherEntries)
            {
                UpdateStatus("Assets found. Skipping download.", 1.0f);
                await Task.Delay(500); // Brief pause to see the message
            }
            else
            {
                try
                {
                    UpdateStatus("Downloading game assets...", 0.0f);
                    await DownloadFileWithProgressAsync(zipUrl, localZipPath, UpdateStatus);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Download error from {zipUrl}: {ex.Message}");
                    zipUrl = Constants.DefaultDataPathUrl;
                    UpdateStatus($"Primary URL failed. Trying default assets URL...", 0.0f);
                    await DownloadFileWithProgressAsync(zipUrl, localZipPath, UpdateStatus);
                }

                try
                {
                    UpdateStatus("Download complete. Extracting assets...", 0.0f);
                    await ExtractZipFileWithProgressAsync(localZipPath, extractPath, UpdateStatus);
                    UpdateStatus("Extraction complete!", 1.0f);

                    if (File.Exists(localZipPath))
                        File.Delete(localZipPath);
                    await Task.Delay(500); // Brief pause
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during extraction: {ex.Message}");
                    UpdateStatus($"Error: {ex.Message}", _progress);
                    // Optionally, prevent transitioning if extraction fails critically
                    // For now, we'll let it try to transition.
                    // return; 
                }
            }

            // --- Transition to the next scene (e.g., LoginScene) ---
            Type nextSceneType = Constants.ENTRY_SCENE;
            if (nextSceneType == typeof(LoadScene)) // Prevent recursion
            {
                Debug.WriteLine("ENTRY_SCENE was LoadScene. Defaulting to LoginScene to prevent recursion.");
                nextSceneType = typeof(LoginScene);
            }

            UpdateStatus($"Preparing {nextSceneType.Name}...", 0.0f);

            BaseScene nextSceneInstance = null;
            try
            {
                nextSceneInstance = (BaseScene)Activator.CreateInstance(nextSceneType);
                if (nextSceneInstance == null)
                {
                    throw new InvalidOperationException($"Could not create instance of {nextSceneType.Name}");
                }

                // InitializeWithProgressReporting will handle its own progress from 0.0 to 1.0
                // The UpdateStatus callback passed here will be called by the nextSceneInstance
                await nextSceneInstance.InitializeWithProgressReporting(
                    (msg, sceneProgress) => UpdateStatus($"{nextSceneType.Name}: {msg}", sceneProgress)
                );

                UpdateStatus("Transitioning...", 1.0f);
                await Task.Delay(300);
                MuGame.Instance.ChangeScene(nextSceneInstance);
            }
            catch (Exception ex)
            {
                string errorMsg = $"Error loading {nextSceneType.Name}: {ex.Message}";
                Console.WriteLine(errorMsg + Environment.NewLine + ex.StackTrace);
                UpdateStatus(errorMsg, 1.0f);
            }
        }

        #endregion

        #region Download & Extraction with Progress Reporting

        private async Task DownloadFileWithProgressAsync(string url, string destination, Action<string, float> progressCallback)
        {
            using HttpClient client = new HttpClient();
            try
            {
                var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalSize = response.Content.Headers.ContentLength ?? -1L;
                var totalBytesRead = 0L;
                var buffer = new byte[81920]; // 80KB buffer

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, true))
                {
                    int bytesRead;
                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalBytesRead += bytesRead;
                        float progress = (totalSize > 0) ? (float)totalBytesRead / totalSize : 0f;
                        string message = $"Downloading assets... {(progress * 100):F0}%";
                        if (totalSize > 0) message += $" ({totalBytesRead / (1024.0 * 1024.0):F1}MB / {totalSize / (1024.0 * 1024.0):F1}MB)";
                        progressCallback?.Invoke(message, progress);
                    }
                    await fileStream.FlushAsync();
                }

                if (totalSize > 0 && totalBytesRead != totalSize)
                {
                    throw new Exception($"Incomplete download. Expected {totalSize} bytes, received {totalBytesRead}.");
                }
                progressCallback?.Invoke("Download complete.", 1.0f);
            }
            catch (Exception ex)
            {
                progressCallback?.Invoke($"Download Error: {ex.Message}", _progress); // Keep last progress
                throw; // Re-throw to be handled by PerformInitialLoadAndTransitionAsync
            }
        }

        private async Task ExtractZipFileWithProgressAsync(string zipPath, string extractPath, Action<string, float> progressCallback)
        {
            await Task.Run(() =>
            {
                try
                {
                    using ZipArchive archive = ZipFile.OpenRead(zipPath);
                    int totalEntries = archive.Entries.Count(e => !e.FullName.EndsWith("/") && !e.FullName.EndsWith("\\")); // Count files only
                    if (totalEntries == 0)
                    {
                        progressCallback?.Invoke("No files to extract.", 1.0f);
                        return;
                    }
                    int processedEntries = 0;

                    foreach (var entry in archive.Entries)
                    {
                        string relativePath = entry.FullName;
                        if (relativePath.StartsWith("Data/") || relativePath.StartsWith("Data\\"))
                        {
                            relativePath = relativePath.Substring(5);
                        }
                        if (string.IsNullOrEmpty(relativePath) || entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                        {
                            continue; // Skip directory entries or empty relative paths
                        }

                        string fullPath = Path.Combine(extractPath, relativePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                        entry.ExtractToFile(fullPath, overwrite: true);

                        processedEntries++;
                        float progress = (float)processedEntries / totalEntries;
                        progressCallback?.Invoke($"Extracting: {Path.GetFileName(entry.Name)} ({(progress * 100):F0}%)", progress);
                    }
                    progressCallback?.Invoke("Extraction Finalizing...", 1.0f);
                }
                catch (Exception ex)
                {
                    progressCallback?.Invoke($"Extraction Error: {ex.Message}", _progress);
                    throw; // Re-throw
                }
            });
        }

        #endregion

        #region Helpers & Drawing

        private void UpdateStatus(string status, float progress)
        {
            // Ensure updates happen on the main thread if called from a background thread
            MuGame.ScheduleOnMainThread(() =>
            {
                _statusText = status;
                _progress = MathHelper.Clamp(progress, 0f, 1f); // Clamp progress
                if (_statusLabel != null)
                {
                    _statusLabel.Text = _statusText;
                }
                // Debug.WriteLine($"LoadScene Status: [{(_progress*100):F0}%] {_statusText}"); // For console logging
            });
        }

        public override void Update(GameTime gameTime)
        {
            // The base.Update will update child controls (_statusLabel)
            // We don't call Initialize() here as LoadScene is special.
            // If it's not ready, it means its own Load/AfterLoad is still running or PerformInitialLoadAndTransitionAsync is active.
            if (Status != GameControlStatus.Ready && Status != GameControlStatus.Initializing)
            {
                // If not even initializing, try to kickstart (should have happened via MuGame)
                if (Status == GameControlStatus.NonInitialized) _ = Initialize();
                return;
            }

            // Even if "Ready", LoadScene might be orchestrating next scene's load.
            // So, its Update logic is minimal, primarily for its UI children.
            base.Update(gameTime);
        }


        private VertexPositionColor[] CreateRectangleVertices(Vector2 pos, Vector2 size, Color color)
        {
            return
            [
                new VertexPositionColor(new Vector3(pos.X, pos.Y, 0), color),
                new VertexPositionColor(new Vector3(pos.X + size.X, pos.Y, 0), color),
                new VertexPositionColor(new Vector3(pos.X, pos.Y + size.Y, 0), color),
                new VertexPositionColor(new Vector3(pos.X + size.X, pos.Y + size.Y, 0), color)
            ];
        }

        public override void Draw(GameTime gameTime)
        {
            // LoadScene always draws its own UI, regardless of its World's status,
            // as long as its basic graphical elements are loaded.
            if (_basicEffect == null || _backgroundTexture == null)
            {
                // Fallback clear if essential resources aren't ready
                GraphicsDevice.Clear(Color.Black);
                // Still try to draw child controls like the status label if they are ready
                if (_statusLabel?.Status == GameControlStatus.Ready)
                {
                    using (new SpriteBatchScope(GraphicsManager.Instance.Sprite, SpriteSortMode.Deferred, BlendState.AlphaBlend))
                    {
                        _statusLabel.Draw(gameTime);
                    }
                }
                return;
            }

            DrawSceneBackground();
            DrawProgressBar();

            // Draw UI controls (like _statusLabel) using the Scene's default SpriteBatch drawing logic
            // This is simpler than calling base.Draw(gameTime) which might have other assumptions.
            using (new SpriteBatchScope(
                      GraphicsManager.Instance.Sprite,
                      SpriteSortMode.Deferred,
                      BlendState.AlphaBlend, // Ensure AlphaBlend for UI text with shadow
                      SamplerState.PointClamp, // Good for pixel art fonts if used
                      DepthStencilState.None)) // UI usually doesn't need depth testing
            {
                // Iterate only specific controls intended for LoadScene UI
                if (_statusLabel != null && _statusLabel.Visible)
                {
                    _statusLabel.Draw(gameTime);
                }
                // Add other LoadScene-specific UI controls here if any
            }
        }

        private void DrawSceneBackground()
        {
            if (_backgroundTexture == null) return;
            using (new SpriteBatchScope(GraphicsManager.Instance.Sprite)) // Default SpriteBatchScope for simple texture draw
            {
                GraphicsManager.Instance.Sprite.Draw(
                    _backgroundTexture,
                    new Rectangle(0, 0, MuGame.Instance.Width, MuGame.Instance.Height),
                    Color.White);
            }
        }

        private void DrawProgressBar()
        {
            if (_basicEffect == null) return;

            int barWidth = MuGame.Instance.Width - 100; // Margin of 50px on each side
            int barX = 50;

            var bgPos = new Vector2(barX, ProgressBarY);
            var bgSize = new Vector2(barWidth, ProgressBarHeight);
            var progressFillSize = new Vector2(barWidth * _progress, ProgressBarHeight);

            var bgVertices = CreateRectangleVertices(bgPos, bgSize, Color.DarkSlateGray); // Darker background
            var progressVertices = CreateRectangleVertices(bgPos, progressFillSize, Color.ForestGreen); // A nice green

            _basicEffect.TextureEnabled = false;
            _basicEffect.VertexColorEnabled = true;

            foreach (var pass in _basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                MuGame.Instance.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, bgVertices, 0, 2);
                if (_progress > 0) // Only draw progress fill if there's progress
                {
                    MuGame.Instance.GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleStrip, progressVertices, 0, 2);
                }
            }
        }
        #endregion
    }
}