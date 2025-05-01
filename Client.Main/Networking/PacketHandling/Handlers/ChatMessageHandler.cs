using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using Client.Main.Client; // For SimpleLoginClient
using Client.Main.Core.Utilities;
using System;
using System.Threading.Tasks; // For PacketHandlerAttribute

namespace Client.Main.Networking.PacketHandling.Handlers
{
    /// <summary>
    /// Handles chat and server message packets.
    /// </summary>
    public class ChatMessageHandler : IGamePacketHandler
    {
        private readonly ILogger<ChatMessageHandler> _logger;
        // private readonly SimpleLoginClient _client; // Not strictly needed if just writing to Console

        public ChatMessageHandler(ILoggerFactory loggerFactory/*, SimpleLoginClient client*/)
        {
            _logger = loggerFactory.CreateLogger<ChatMessageHandler>();
            // _client = client;
        }

        [PacketHandler(0x0D, PacketRouter.NoSubCode)] // ServerMessage
        public Task HandleServerMessageAsync(Memory<byte> packet)
        {
            try
            {
                var message = new ServerMessage(packet);
                _logger.LogInformation("💬 Received ServerMessage: Type={Type}, Content='{Message}'", message.Type, message.Message);
                string prefix = message.Type switch
                {
                    ServerMessage.MessageType.GoldenCenter => "[GOLDEN]: ",
                    ServerMessage.MessageType.BlueNormal => "[SYSTEM]: ",
                    ServerMessage.MessageType.GuildNotice => "[GUILD]: ",
                    _ => "[SERVER]: "
                };
                Console.WriteLine($"{prefix}{message.Message}");
            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing ServerMessage (0D)."); }
            return Task.CompletedTask;
        }

        [PacketHandler(0x00, PacketRouter.NoSubCode)] // ChatMessage
        public Task HandleChatMessageAsync(Memory<byte> packet)
        {
            try
            {
                var message = new ChatMessage(packet);
                _logger.LogInformation("💬 Received ChatMessage: From={Sender}, Type={Type}, Content='{Message}'", message.Sender, message.Type, message.Message);
                string prefix = message.Type switch
                {
                    ChatMessage.ChatMessageType.Whisper => $"귓속말 [{message.Sender}]: ",
                    ChatMessage.ChatMessageType.Normal => $"[{message.Sender}]: ",
                    _ => $"[{message.Sender} ({message.Type})]: "
                };
                Console.WriteLine($"{prefix}{message.Message}");
            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing ChatMessage (00)."); }
            return Task.CompletedTask;
        }
    }
}