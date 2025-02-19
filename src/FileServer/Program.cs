using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

class TcpServer
{
    static async Task Main(string[] args)
    {
        int port = 8082; // Puerto del servidor
        string baseDirectory = "./ServerFiles"; // Carpeta donde están los archivos

        if (!Directory.Exists(baseDirectory))
        {
            Directory.CreateDirectory(baseDirectory);
            Console.WriteLine($"Created directory: {baseDirectory}");
        }

        TcpListener listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        Console.WriteLine("Server started. Waiting for connections...");

        while (true)
        {
            TcpClient client = await listener.AcceptTcpClientAsync();
            Console.WriteLine("Client connected.");
            _ = HandleClientAsync(client, baseDirectory);
        }
    }

    private static async Task HandleClientAsync(TcpClient client, string baseDirectory)
    {
        try
        {
            using (NetworkStream stream = client.GetStream())
            {
                // Leer la solicitud del cliente
                byte[] requestBuffer = new byte[1024];
                int bytesRead = await stream.ReadAsync(requestBuffer, 0, requestBuffer.Length);
                string request = Encoding.UTF8.GetString(requestBuffer, 0, bytesRead).Trim();

                Console.WriteLine($"Request received: {request}");

                // Procesar la solicitud (ejemplo: "DOWNLOAD Data.zip")
                if (request.StartsWith("DOWNLOAD"))
                {
                    string fileName = request.Substring("DOWNLOAD ".Length).Trim();
                    string filePath = Path.Combine(baseDirectory, fileName);

                    if (File.Exists(filePath))
                    {
                        // Enviar el tamaño del archivo
                        FileInfo fileInfo = new FileInfo(filePath);
                        byte[] sizeBuffer = BitConverter.GetBytes(fileInfo.Length);
                        await stream.WriteAsync(sizeBuffer, 0, sizeBuffer.Length);

                        // Enviar el archivo
                        using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                        {
                            byte[] buffer = new byte[65536];
                            int bytesSent;
                            while ((bytesSent = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                            {
                                await stream.WriteAsync(buffer, 0, bytesSent);
                            }
                        }

                        Console.WriteLine("File sent successfully.");
                    }
                    else
                    {
                        Console.WriteLine("File not found.");
                        await stream.WriteAsync(new byte[8], 0, 8); // Enviar tamaño 0 si no existe
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client: {ex.Message}");
        }
        finally
        {
            client.Close();
        }
    }
}