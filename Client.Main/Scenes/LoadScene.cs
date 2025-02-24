using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Client.Main.Controllers;
using Client.Main.Controls.UI;
using Client.Main.Worlds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Scenes
{
    public class Metadata
    {
        public long TotalSize { get; set; }
        public double Version { get; set; }
        public required List<FileMetadata> Files { get; set; }
    }

    public class FileMetadata
    {
        public required string Path { get; set; }
        public required long Size { get; set; }
    }

    [JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(Metadata))]
    [JsonSerializable(typeof(List<FileMetadata>))]
    public partial class MetadataContext : JsonSerializerContext
    {
    }

    public class LoadScene : BaseScene
    {
        #region Fields & Constants

        private LabelControl _statusLabel;
        private float _progress; // value from 0 to 1
        private string _statusText;

        // Scene background
        private Texture2D _backgroundTexture;

        // BasicEffect for drawing
        private BasicEffect _basicEffect;

        // Progress bar constants – width will be calculated dynamically
        private const int ProgressBarHeight = 30;
        private const int ProgressBarY = 700;

        private string _dataPathUrl = Constants.DataPathUrl;

        #endregion

        #region Constructor & Loading

        public LoadScene()
        {
            _progress = 0f;
            _statusText = "Initializing...";

            // Increased font size for better readability
            _statusLabel = new LabelControl
            {
                Text = _statusText,
                X = 50,
                Y = MuGame.Instance.Height - 50,
                FontSize = 24, // larger font
                TextColor = Color.White
            };

            Controls.Add(_statusLabel);
        }

        public override async Task Load()
        {
            Console.WriteLine("LoadScene.Load");

            // Load the background
            _backgroundTexture = MuGame.Instance.Content.Load<Texture2D>("Background");

            // BasicEffect initialization
            _basicEffect = new BasicEffect(MuGame.Instance.GraphicsDevice)
            {
                VertexColorEnabled = true,
                Projection = Matrix.CreateOrthographicOffCenter(0, MuGame.Instance.Width, MuGame.Instance.Height, 0, 0, 1),
                View = Matrix.Identity,
                World = Matrix.Identity
            };

            await ChangeWorldAsync<LoadWorld>();
            await base.Load();
        }

        public override void AfterLoad()
        {
            base.AfterLoad();
            // Using Constants.DataPath instead of AppDomain.CurrentDomain.BaseDirectory
            string extractPath = Client.Main.Constants.DataPath;
            Console.WriteLine($"ExtractPath: {extractPath}");

            // Ensure that the folder exists
            Directory.CreateDirectory(extractPath);

            // Start downloading resources
            StartDownloadingDataZip();
        }

        #endregion

        #region Download & Extraction

        private async Task DownloadFileWithProgressAsync(string url, string destination)
        {
            HttpClient client = new HttpClient();
            try
            {
                // Download response headers with ResponseHeadersRead option
                var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalSize = response.Content.Headers.ContentLength;
                var totalBytesRead = 0L;
                var buffer = new byte[81920];

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    int bytesRead;
                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        totalBytesRead += bytesRead;
                        float progress = totalSize.HasValue ? (float)totalBytesRead / totalSize.Value : 0;
                        UpdateStatus($"Downloading assets... {(progress * 100):F0}%", progress);
                    }
                    await fileStream.FlushAsync();
                }

                if (totalSize.HasValue && totalBytesRead != totalSize.Value)
                {
                    throw new Exception($"Incomplete download. Expected {totalSize.Value} bytes, but received {totalBytesRead}.");
                }
            }
            finally
            {
                client.Dispose();
            }
        }

        private async void StartDownloadingDataZip()
        {
            string localZipPath = Path.Combine(Client.Main.Constants.DataPath, "Data.zip");
            string extractPath = Client.Main.Constants.DataPath;
            string zipUrl = _dataPathUrl;

            // Checking if the folder already contains files (other than Data.zip)
            bool hasOtherEntries = Directory.EnumerateFileSystemEntries(Client.Main.Constants.DataPath)
                                            .Any(entry => !string.Equals(Path.GetFileName(entry), "Data.zip", StringComparison.OrdinalIgnoreCase));

            if (hasOtherEntries)
            {
                Console.WriteLine("Assets already downloaded, skipping download and extraction.");
                MuGame.Instance.ChangeScene<LoginScene>();
                return;
            }

            try
            {
                UpdateStatus("Downloading game assets...", 0);
                // Attempt to download data from the primary URL
                await DownloadFileWithProgressAsync(zipUrl, localZipPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Download error from {zipUrl}: {ex.Message}");
                // Set alternative URL
                zipUrl = Constants.DefaultDataPathUrl;
                UpdateStatus("Primary URL failed. Trying default assets URL...", 0);
                await DownloadFileWithProgressAsync(zipUrl, localZipPath);
            }

            try
            {
                UpdateStatus("Download complete. Extracting assets...", 0);
                await ExtractZipFileWithProgressAsync(localZipPath, extractPath);
                UpdateStatus("Extraction complete!", 1);

                if (File.Exists(localZipPath))
                    File.Delete(localZipPath);

                await Task.Delay(500);
                MuGame.Instance.ChangeScene<LoginScene>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during extraction: {ex.Message}");
                UpdateStatus($"Error: {ex.Message}", _progress);
            }
        }

        private async Task ExtractZipFileWithProgressAsync(string zipPath, string extractPath)
        {
            await Task.Run(() =>
            {
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    int totalEntries = archive.Entries.Count;
                    int processedEntries = 0;

                    foreach (var entry in archive.Entries)
                    {
                        try
                        {
                            // Get entry path and remove "Data/" prefix if present
                            string relativePath = entry.FullName;
                            if (relativePath.StartsWith("Data/") || relativePath.StartsWith("Data\\"))
                            {
                                relativePath = relativePath.Substring(5); // remove "Data/" (5 characters)
                            }

                            string fullPath = Path.Combine(extractPath, relativePath);

                            // If the entry is a directory, create it
                            if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                            {
                                Directory.CreateDirectory(fullPath);
                            }
                            else
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                                entry.ExtractToFile(fullPath, overwrite: true);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error extracting {entry.FullName}: {ex.Message}");
                        }
                        processedEntries++;
                        float progress = (float)processedEntries / totalEntries;
                        UpdateStatus($"Extracting assets... {(progress * 100):F0}%", progress);
                    }
                }
            });
        }

        #endregion

        #region Helpers

        private void UpdateStatus(string status, float progress)
        {
            _statusText = status;
            _progress = progress;
            if (_statusLabel != null)
            {
                _statusLabel.Text = status;
            }
        }

        private VertexPositionColor[] CreateRectangleVertices(Vector2 pos, Vector2 size, Color color)
        {
            return new VertexPositionColor[]
            {
                new VertexPositionColor(new Vector3(pos.X, pos.Y, 0), color),
                new VertexPositionColor(new Vector3(pos.X + size.X, pos.Y, 0), color),
                new VertexPositionColor(new Vector3(pos.X, pos.Y + size.Y, 0), color),
                new VertexPositionColor(new Vector3(pos.X + size.X, pos.Y + size.Y, 0), color)
            };
        }

        #endregion

        #region Drawing

        public override void Draw(GameTime gameTime)
        {
            if (_basicEffect != null)
            {
                DrawSceneBackground();
                DrawProgressBar();
            }
            base.Draw(gameTime);
        }

        private void DrawSceneBackground()
        {
            if (_backgroundTexture != null)
            {
                var spriteBatch = GraphicsManager.Instance.Sprite;
                spriteBatch.Begin();
                spriteBatch.Draw(_backgroundTexture,
                    new Rectangle(0, 0, MuGame.Instance.Width, MuGame.Instance.Height),
                    Color.White);
                spriteBatch.End();
            }
        }

        // Progress bar drawn on the full screen width
        private void DrawProgressBar()
        {
            if (_basicEffect == null)
                return;

            // Use the full screen width
            int fullWidth = MuGame.Instance.Width;
            var bgPos = new Vector2(0, ProgressBarY);
            var bgSize = new Vector2(fullWidth, ProgressBarHeight);
            var progressSize = new Vector2(fullWidth * _progress, ProgressBarHeight);

            var bgVertices = CreateRectangleVertices(bgPos, bgSize, Color.DarkGray);
            var progressVertices = CreateRectangleVertices(bgPos, progressSize, Color.Green);

            _basicEffect.TextureEnabled = false;
            _basicEffect.VertexColorEnabled = true;

            foreach (var pass in _basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                MuGame.Instance.GraphicsDevice.DrawUserPrimitives(
                    PrimitiveType.TriangleStrip,
                    bgVertices,
                    0,
                    2);
            }
            foreach (var pass in _basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                MuGame.Instance.GraphicsDevice.DrawUserPrimitives(
                    PrimitiveType.TriangleStrip,
                    progressVertices,
                    0,
                    2);
            }
        }

        #endregion
    }
}