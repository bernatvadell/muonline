using Client.Main.Controls.UI;
using Client.Main.Worlds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Scenes
{
    public class LoadScene : BaseScene
    {
        private LabelControl _statusLabel;
        private float _progress; // Valor entre 0 y 1
        private string _statusText;
        private bool _isDownloadComplete;

        // Textura del fondo
        private Texture2D _backgroundTexture;

        // Barra de progreso
        private const int ProgressBarWidth = 600;
        private const int ProgressBarHeight = 30;
        private const int ProgressBarX = 100;
        private const int ProgressBarY = 400;

        // BasicEffect para dibujar
        private BasicEffect _basicEffect;

        public LoadScene()
        {
            _progress = 0f;
            _statusText = "Initializing...";
            _isDownloadComplete = false;

            // Inicializar la etiqueta de estado
            _statusLabel = new LabelControl
            {
                Text = _statusText,
                X = 50,
                Y = MuGame.Instance.Height - 50, // Ajustar posición según la altura de la ventana
                FontSize = 12,
                TextColor = Color.White
            };

            // Agregar controles a la escena
            Controls.Add(_statusLabel);
        }

        public override async Task Load()
        {
            Console.WriteLine("LoadScene.Load");

            // Cargar la textura del fondo
            _backgroundTexture = MuGame.Instance.Content.Load<Texture2D>("Background");

            // Inicializar BasicEffect
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

            // Comprobar si la carpeta Data ya existe
            string extractPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Data");
            Console.WriteLine($"ExtractPath: {extractPath}");

            // if (Directory.Exists(extractPath))
            // {
            //     _statusText = "Data folder already exists. Skipping download and extraction.";
            //     EnqueueUIUpdate(() => _statusLabel.Text = _statusText);
            //     // Cambiar directamente a la escena de inicio de sesión
            //     await Task.Delay(1000); // Esperar un momento antes de cambiar de escena
            //     MuGame.Instance.ChangeScene<LoginScene>();
            //     return;
            // }

            // Si no existe, iniciar la descarga y extracción
            StartDownloadingAssets(true);
        }

        private async void StartDownloadingAssets(bool useHTTP)
        {
            if (useHTTP)
            {
                await StartDownloadingAssetsHTTP();
            }
            else
            {
                await StartDownloadingAssetsTCP();
            }
        }

        private async Task StartDownloadingAssetsTCP()
        {
            string serverIp = "192.168.100.8";
            int serverPort = 8082; // Puerto del servidor TCP
            string tempFilePath = Path.Combine(Path.GetTempPath(), "Data.zip");
            string extractPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "./");

            try
            {
                _statusText = "Connecting to server...";
                _statusLabel.Text = _statusText;

                using (TcpClient client = new TcpClient())
                {
                    await client.ConnectAsync(serverIp, serverPort);
                    using (NetworkStream stream = client.GetStream())
                    {
                        // Enviar solicitud al servidor (por ejemplo, el nombre del archivo)
                        string request = "DOWNLOAD Data.zip";
                        byte[] requestBytes = Encoding.UTF8.GetBytes(request);
                        await stream.WriteAsync(requestBytes, 0, requestBytes.Length);

                        // Leer la respuesta del servidor (tamaño del archivo)
                        byte[] sizeBuffer = new byte[8];
                        await stream.ReadExactlyAsync(sizeBuffer);
                        long totalBytes = BitConverter.ToInt64(sizeBuffer, 0);

                        // Descargar el archivo
                        long receivedBytes = 0;
                        byte[] buffer = new byte[65536]; // Buffer grande para mejorar rendimiento
                        using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                        {
                            int bytesRead;
                            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead);
                                receivedBytes += bytesRead;

                                if (totalBytes > 0)
                                {
                                    _progress = (float)receivedBytes / totalBytes;
                                    _statusText = $"Downloading assets... {(_progress * 100):F0}%";
                                    _statusLabel.Text = _statusText;
                                }
                            }
                        }
                    }
                }

                // Descomprimir el archivo ZIP con progreso
                _statusText = "Extracting assets...";
                _statusLabel.Text = _statusText;

                Directory.CreateDirectory(extractPath); // Crear carpeta para los archivos extraídos
                await ExtractWithProgress(tempFilePath, extractPath);

                // Marcar como completado
                _isDownloadComplete = true;
                _statusText = "Download complete!";
                _statusLabel.Text = _statusText;

                // Cambiar a la escena de inicio de sesión
                await Task.Delay(500); // Esperar un momento antes de cambiar de escena
                MuGame.Instance.ChangeScene<LoginScene>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during download or extraction: {ex.Message}");
                _statusText = $"Download failed! {ex.Message}";
                _statusLabel.Text = _statusText;
            }
        }

        private async 
        Task
        StartDownloadingAssetsHTTP()
        {
            string downloadUrl = "http://192.168.100.8:8081/Data.zip";
            string tempFilePath = Path.Combine(Path.GetTempPath(), "Data.zip");
            string extractPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "./");

            try
            {
                _statusText = "Connecting to server...";
                _statusLabel.Text = _statusText;
                // SocketsHttpHandler handler = new SocketsHttpHandler();
                SocketsHttpHandler handler = new SocketsHttpHandler
                {
                    // AllowAutoRedirect = true,
                    // PooledConnectionLifetime = TimeSpan.FromMinutes(10), // Reutiliza conexiones
                    // PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30), // Evita timeout rápido
                    // MaxConnectionsPerServer = 10 // Permite más conexiones simultáneas
                };

                using (HttpClient client = new HttpClient(handler))
                {
                    // Configurar el cliente para informar el progreso
                    using (var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        long? totalBytes = response.Content.Headers.ContentLength;
                        long receivedBytes = 0;

                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None,  bufferSize: 81920, useAsync: true))
                        {
                            byte[] buffer = new byte[81920];
                            int bytesRead;

                            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await fileStream.WriteAsync(buffer, 0, bytesRead);
                                receivedBytes += bytesRead;

                                // Calcular el progreso
                                if (totalBytes.HasValue)
                                {
                                    _progress = (float)receivedBytes / totalBytes.Value;
                                    _statusText = $"Downloading assets... {(_progress * 100):F0}%";
                                    Console.WriteLine($"Downloading assets... {(_progress * 100):F0}%");
                                    _statusLabel.Text = _statusText;
                                }
                            }
                        }
                    }
                }

                // using (var client = new WebClient())
                // {
                //     client.DownloadProgressChanged += (s, e) => 
                //     {
                //         _progress = (float)e.BytesReceived / e.TotalBytesToReceive;
                //         _statusText = $"Downloading assets... {(_progress * 100):F0}%";
                //         Console.WriteLine($"Downloading assets... {(_progress * 100):F0}%");
                //         _statusLabel.Text = _statusText;
                //     };
                    
                //     await client.DownloadFileTaskAsync(new Uri(downloadUrl), tempFilePath);
                // }
                // Descomprimir el archivo ZIP con progreso
                _statusText = "Extracting assets...";
                _statusLabel.Text = _statusText;

                Directory.CreateDirectory(extractPath); // Crear carpeta para los archivos extraídos
                await ExtractWithProgress(tempFilePath, extractPath);

                // Marcar como completado
                _isDownloadComplete = true;
                _statusText = "Download complete!";
                _statusLabel.Text = _statusText;

                // Cambiar a la escena de inicio de sesión
                await Task.Delay(500); // Esperar un momento antes de cambiar de escena
                MuGame.Instance.ChangeScene<LoginScene>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during download or extraction: {ex.Message}");
                _statusText = "Download failed!";
                _statusLabel.Text = _statusText;
            }
        }

        private async Task DownloadFile(HttpClient client, string url, string savePath)
        {
            using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                long? totalBytes = response.Content.Headers.ContentLength;
                long receivedBytes = 0;

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 65536, useAsync: true))
                {
                    byte[] buffer = new byte[65536]; // 64 KB en lugar de 10 MB
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        await fileStream.FlushAsync(); // Asegura que los datos se escriban en disco
                        receivedBytes += bytesRead;

                        if (totalBytes.HasValue)
                        {
                            float progress = (float)receivedBytes / totalBytes.Value;
                            // Console.WriteLine($"Downloading... {(progress * 100):F0}%");
                        }
                    }
                }
            }
        }

        private async Task ExtractWithProgress(string zipPath, string extractPath)
        {
            using (var archive = ZipFile.OpenRead(zipPath))
            {
                int totalEntries = archive.Entries.Count;
                int extractedEntries = 0;

                foreach (var entry in archive.Entries)
                {
                    string fullPath = Path.Combine(extractPath, entry.FullName);

                    // Crear directorios si es necesario
                    if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                    {
                        Directory.CreateDirectory(fullPath);
                        continue;
                    }

                    // Extraer el archivo, reemplazando si ya existe
                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                    entry.ExtractToFile(fullPath, overwrite: true);

                    // Actualizar el progreso
                    extractedEntries++;
                    float progress = (float)extractedEntries / totalEntries;
                    _statusText = $"Extracting assets... {progress * 100:F0}%";
                    Console.WriteLine($"Extracting assets... {progress * 100:F0}%");
                    _statusLabel.Text = _statusText;

                    // Simular un pequeño retraso para visualizar el progreso
                    await Task.Delay(10);
                }
            }
        }

        public override void Draw(GameTime gameTime)
        {
            // Dibujar el fondo
            _DrawBackground();

            // Dibujar la barra de progreso
            // DrawProgressBar();

            // Dibujar los controles
            base.Draw(gameTime);
        }

        private void _DrawBackground()
        {
            if (_backgroundTexture != null)
            {
                // Definir los vértices para el fondo
                var vertices = new VertexPositionTexture[]
                {
                    new VertexPositionTexture(new Vector3(0, 0, 0), new Vector2(0, 0)),
                    new VertexPositionTexture(new Vector3(MuGame.Instance.Width, 0, 0), new Vector2(1, 0)),
                    new VertexPositionTexture(new Vector3(0, MuGame.Instance.Height, 0), new Vector2(0, 1)),
                    new VertexPositionTexture(new Vector3(MuGame.Instance.Width, MuGame.Instance.Height, 0), new Vector2(1, 1))
                };

                // Configurar BasicEffect
                _basicEffect.TextureEnabled = true;
                _basicEffect.Texture = _backgroundTexture;

                foreach (var pass in _basicEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    MuGame.Instance.GraphicsDevice.DrawUserPrimitives(
                        PrimitiveType.TriangleStrip,
                        vertices,
                        0,
                        2);
                }
            }
        }

        private void DrawProgressBar()
        {
            // Calcular el ancho actual de la barra de progreso
            int currentWidth = (int)(ProgressBarWidth * _progress);

            // Definir los vértices para el fondo de la barra de progreso
            var backgroundVertices = new VertexPositionColor[]
            {
                new VertexPositionColor(new Vector3(ProgressBarX, ProgressBarY, 0), Color.DarkGray),
                new VertexPositionColor(new Vector3(ProgressBarX + ProgressBarWidth, ProgressBarY, 0), Color.DarkGray),
                new VertexPositionColor(new Vector3(ProgressBarX, ProgressBarY + ProgressBarHeight, 0), Color.DarkGray),
                new VertexPositionColor(new Vector3(ProgressBarX + ProgressBarWidth, ProgressBarY + ProgressBarHeight, 0), Color.DarkGray)
            };

            // Definir los vértices para el progreso actual
            var progressVertices = new VertexPositionColor[]
            {
                new VertexPositionColor(new Vector3(ProgressBarX, ProgressBarY, 0), Color.Green),
                new VertexPositionColor(new Vector3(ProgressBarX + currentWidth, ProgressBarY, 0), Color.Green),
                new VertexPositionColor(new Vector3(ProgressBarX, ProgressBarY + ProgressBarHeight, 0), Color.Green),
                new VertexPositionColor(new Vector3(ProgressBarX + currentWidth, ProgressBarY + ProgressBarHeight, 0), Color.Green)
            };

            // Dibujar el fondo de la barra de progreso
            _basicEffect.TextureEnabled = false;
            _basicEffect.VertexColorEnabled = true;

            foreach (var pass in _basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                MuGame.Instance.GraphicsDevice.DrawUserPrimitives(
                    PrimitiveType.TriangleStrip,
                    backgroundVertices,
                    0,
                    2);
            }

            // Dibujar el progreso actual
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
    }
}