using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using Client.Main.Core.Utilities;
using System;
using System.Threading.Tasks; // For PacketHandlerAttribute
using Client.Main.Controls.UI; // For ChatLogWindow
using System.Linq;             // For OfType()

namespace Client.Main.Networking.PacketHandling.Handlers
{
    /// <summary>
    /// Handles chat and server message packets.
    /// </summary>
    public class ChatMessageHandler : IGamePacketHandler
    {
        private readonly ILogger<ChatMessageHandler> _logger;

        public ChatMessageHandler(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ChatMessageHandler>();
        }

        [PacketHandler(0x0D, PacketRouter.NoSubCode)] // ServerMessage
        public Task HandleServerMessageAsync(Memory<byte> packet)
        {
            try
            {
                var message = new ServerMessage(packet);
                _logger.LogInformation("💬 Received ServerMessage: Type={Type}, Content='{Message}'", message.Type, message.Message);

                MuGame.ScheduleOnMainThread(() =>
                {
                    var chatLog = MuGame.Instance?.ActiveScene?.Controls?.OfType<ChatLogWindow>().FirstOrDefault();
                    if (chatLog != null)
                    {
                        var uiMessageType = message.Type switch
                        {
                            ServerMessage.MessageType.GoldenCenter => Models.MessageType.Info,
                            ServerMessage.MessageType.BlueNormal => Models.MessageType.System,
                            ServerMessage.MessageType.GuildNotice => Models.MessageType.Guild, // Guild Notice might be better as Guild type
                            _ => Models.MessageType.System
                        };
                        // Server messages usually don't have a specific sender ID in this context
                        chatLog.AddMessage(string.Empty, message.Message, uiMessageType);
                    }
                    else
                    {
                        _logger.LogWarning("ChatLogWindow not found in active scene to display ServerMessage.");
                    }
                });

                // Keep console output for debugging if desired
                string prefix = message.Type switch
                {
                    ServerMessage.MessageType.GoldenCenter => "[GOLDEN]: ",
                    ServerMessage.MessageType.BlueNormal => "[SYSTEM]: ",
                    ServerMessage.MessageType.GuildNotice => "[GUILD_NOTICE]: ", // Differentiate notice from regular guild chat
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
                // Use the correct ChatMessage struct based on the provided definition
                var message = new MUnique.OpenMU.Network.Packets.ServerToClient.ChatMessage(packet);
                _logger.LogInformation("💬 Received ChatMessage (Packet 0x00): From={Sender}, Type={Type}, Content='{Message}'", message.Sender, message.Type, message.Message);

                // Capture data needed for the UI thread
                string sender = message.Sender;
                string rawText = message.Message; // Get the raw message text
                MUnique.OpenMU.Network.Packets.ServerToClient.ChatMessage.ChatMessageType packetType = message.Type;

                MuGame.ScheduleOnMainThread(() =>
                {
                    var chatLog = MuGame.Instance?.ActiveScene?.Controls?.OfType<ChatLogWindow>().FirstOrDefault();
                    if (chatLog != null)
                    {
                        Models.MessageType uiMessageType;
                        string displayText = rawText; // Start with the raw text

                        if (packetType == MUnique.OpenMU.Network.Packets.ServerToClient.ChatMessage.ChatMessageType.Whisper)
                        {
                            uiMessageType = Models.MessageType.Whisper;
                            // Whisper messages usually don't have prefixes to strip
                        }
                        else // Handle Normal messages (which might contain prefixes)
                        {
                            // Check for prefixes and determine UI type
                            if (rawText.StartsWith("~"))
                            {
                                uiMessageType = Models.MessageType.Party;
                                displayText = rawText.Substring(1).TrimStart(); // Remove prefix for display
                            }
                            else if (rawText.StartsWith("@"))
                            {
                                uiMessageType = Models.MessageType.Guild;
                                displayText = rawText.Substring(1).TrimStart(); // Remove prefix for display
                            }
                            else if (rawText.StartsWith("$")) // Assuming '$' for Gens, adjust if different
                            {
                                uiMessageType = Models.MessageType.Gens;
                                displayText = rawText.Substring(1).TrimStart(); // Remove prefix for display
                            }
                            // Add checks for other potential prefixes (e.g., GM messages might have a specific format)
                            // else if (IsGmMessage(sender, rawText)) { uiMessageType = Models.MessageType.GM; }
                            else
                            {
                                uiMessageType = Models.MessageType.Chat; // Default to normal chat
                            }
                        }

                        // Add the processed message to the chat log
                        chatLog.AddMessage(sender, displayText, uiMessageType);
                    }
                    else
                    {
                        _logger.LogWarning("ChatLogWindow not found in active scene to display ChatMessage from {Sender}.", sender);
                    }
                });

                // Keep console output for debugging if desired (using raw text here)
                string prefix = message.Type switch
                {
                    MUnique.OpenMU.Network.Packets.ServerToClient.ChatMessage.ChatMessageType.Whisper => $"귓속말 [{message.Sender}]: ",
                    MUnique.OpenMU.Network.Packets.ServerToClient.ChatMessage.ChatMessageType.Normal => $"[{message.Sender}]: ", // Will log with prefix if present
                    _ => $"[{message.Sender} ({message.Type})]: "
                };
                Console.WriteLine($"{prefix}{rawText}"); // Log raw text to console

            }
            catch (Exception ex) { _logger.LogError(ex, "💥 Error parsing ChatMessage (00)."); }
            return Task.CompletedTask;
        }

        // **** REMOVED UNNECESSARY HELPER METHOD ****
        // The mapping logic is now directly inside HandleChatMessageAsync
        // private Models.MessageType MapPacketChatTypeToUiType(MUnique.OpenMU.Network.Packets.ServerToClient.ChatMessage.ChatMessageType packetType) { ... }
    }
}