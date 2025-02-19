using Client.Main.Controls.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace Client.Main.Scenes
{
    public class LoadScene : BaseScene
    {
        private LabelControl _statusLabel;
        private float _progress; // Valor entre 0 y 1
        private string _statusText;
        private bool _isDownloadComplete;

        // Cola de acciones para actualizar la interfaz gráfica desde el hilo principal
        private readonly Queue<Action> _uiActions = new Queue<Action>();

        public LoadScene()
        {
            _progress = 0f;
            _statusText = "Initializing...";
            _isDownloadComplete = false;

            // Inicializar la etiqueta de estado
            _statusLabel = new LabelControl
            {
                Text = _statusText,
                X = 100,
                Y = 350,
                FontSize = 12,
                TextColor = Color.White
            };

            // Agregar controles a la escena
            Controls.Add(_statusLabel);
        }

        public override async Task Load()
        {
            await base.Load();

            // Comprobar si la carpeta Data ya existe
            string extractPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Data");
            if (Directory.Exists(extractPath))
            {
                _statusText = "Data folder already exists. Skipping download and extraction.";
                EnqueueUIUpdate(() => _statusLabel.Text = _statusText);

                // Cambiar directamente a la escena de inicio de sesión
                await Task.Delay(1000); // Esperar un momento antes de cambiar de escena
                MuGame.Instance.ChangeScene<LoginScene>();
                return;
            }

            // Si no existe, iniciar la descarga y extracción
            StartDownloadingAssets();
        }

        private async void StartDownloadingAssets()
        {
            string downloadUrl = "http://192.168.100.8:8081/Data.zip";
            string tempFilePath = Path.Combine(Path.GetTempPath(), "Data.zip");
            string extractPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "./");

            try
            {
                _statusText = "Connecting to server...";
                EnqueueUIUpdate(() => _statusLabel.Text = _statusText);

                using (HttpClient client = new HttpClient())
                {
                    // Configurar el cliente para informar el progreso
                    using (var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                    {
                        response.EnsureSuccessStatusCode();
                        long? totalBytes = response.Content.Headers.ContentLength;
                        long receivedBytes = 0;

                        using (var contentStream = await response.Content.ReadAsStreamAsync())
                        using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            byte[] buffer = new byte[8192];
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

                                    // Encolar la actualización de la interfaz gráfica
                                    EnqueueUIUpdate(() =>
                                    {
                                        _statusLabel.Text = _statusText;
                                    });
                                }
                            }
                        }
                    }
                }

                // Descomprimir el archivo ZIP con progreso
                _statusText = "Extracting assets...";
                EnqueueUIUpdate(() => _statusLabel.Text = _statusText);

                Directory.CreateDirectory(extractPath); // Crear carpeta para los archivos extraídos
                await ExtractWithProgress(tempFilePath, extractPath);

                // Marcar como completado
                _isDownloadComplete = true;
                _statusText = "Download complete!";
                EnqueueUIUpdate(() =>
                {
                    _statusLabel.Text = _statusText;
                });

                // Cambiar a la escena de inicio de sesión
                await Task.Delay(500); // Esperar un momento antes de cambiar de escena
                EnqueueUIUpdate(() =>
                {
                    MuGame.Instance.ChangeScene<LoginScene>();
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during download or extraction: {ex.Message}");
                _statusText = "Download failed!";
                EnqueueUIUpdate(() =>
                {
                    _statusLabel.Text = _statusText;
                });
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

                    // Encolar la actualización de la interfaz gráfica
                    EnqueueUIUpdate(() =>
                    {
                        _statusLabel.Text = _statusText;
                    });

                    // Simular un pequeño retraso para visualizar el progreso
                    await Task.Delay(10);
                }
            }
        }

        public override void Update(GameTime gameTime)
        {
            // Procesar todas las acciones encoladas para la interfaz gráfica
            ProcessUIActions();

            // Permitir al usuario saltar la pantalla de descarga si ya está completa
            if (_isDownloadComplete && MuGame.Instance.Keyboard.IsKeyDown(Keys.Enter))
            {
                MuGame.Instance.ChangeScene<LoginScene>();
            }
        }

        public override void Draw(GameTime gameTime)
        {
            // Dibujar fondo o elementos adicionales si es necesario
            MuGame.Instance.GraphicsDevice.Clear(Color.Black);
            base.Draw(gameTime);
        }

        // Método para encolar actualizaciones de la interfaz gráfica
        private void EnqueueUIUpdate(Action action)
        {
            lock (_uiActions)
            {
                _uiActions.Enqueue(action);
            }
        }

        // Método para procesar todas las acciones encoladas
        private void ProcessUIActions()
        {
            lock (_uiActions)
            {
                while (_uiActions.Count > 0)
                {
                    var action = _uiActions.Dequeue();
                    action?.Invoke();
                }
            }
        }
    }
}