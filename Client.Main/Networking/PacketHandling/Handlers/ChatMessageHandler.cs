using Client.Main.Controls.UI;           // For ChatLogWindow, NotificationManager
using Client.Main.Core.Utilities;       // For PacketHandlerAttribute
using Client.Main.Scenes;               // For GameScene
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Client.Main.Networking.PacketHandling.Handlers
{
    /// <summary>
    /// Implements IGamePacketHandler to process server and chat messages.
    /// </summary>
    public class ChatMessageHandler : IGamePacketHandler
    {
        // ──────────────────────────── Fields ────────────────────────────
        private readonly ILogger<ChatMessageHandler> _logger;
        private static readonly List<(ServerMessage.MessageType Type, string Message)> _pendingServerMessages = new();
        private static readonly object _pendingServerMessagesLock = new();
        private static readonly Regex _leadingZerosRegex = new Regex(
            @"^0+(?=[a-zA-Z])",
            RegexOptions.Compiled);

        // ───────────────────────── Constructors ─────────────────────────
        public ChatMessageHandler(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ChatMessageHandler>();
        }

        // ─────────────────────── Packet Handlers ────────────────────────

        [PacketHandler(0x0D, PacketRouter.NoSubCode)]  // ServerMessage (0x0D)
        public Task HandleServerMessageAsync(Memory<byte> packet)
        {
            var serverMsg = new ServerMessage(packet);
            string original = serverMsg.Message;
            string cleaned = _leadingZerosRegex.Replace(original, "");

            var scene = MuGame.Instance?.ActiveScene as GameScene;
            if (scene == null)
            {
                _logger.LogWarning(
                    "GameScene is null when handling ServerMessage (0x0D). Queuing message: Type={Type}, Content='{Message}'",
                    serverMsg.Type, cleaned);

                lock (_pendingServerMessagesLock)
                {
                    _pendingServerMessages.Add((serverMsg.Type, cleaned));
                }
                return Task.CompletedTask;
            }

            try
            {
                _logger.LogInformation(
                    "Received ServerMessage (0x0D): Type={Type}, Original='{Original}', Cleaned='{Cleaned}'",
                    serverMsg.Type, original, cleaned);

                // Queue for UI-thread processing in GameScene
                if (serverMsg.Type != ServerMessage.MessageType.BlueNormal)
                {
                    scene.ShowNotificationMessage(serverMsg.Type, cleaned);
                }
                
                // Also print to console with prefix based on message type
                string prefix = serverMsg.Type switch
                {
                    ServerMessage.MessageType.GoldenCenter => "[GOLDEN]: ",
                    ServerMessage.MessageType.BlueNormal => "[SYSTEM]: ",
                    ServerMessage.MessageType.GuildNotice => "[GUILD_NOTICE]: ",
                    _ => "[SERVER]: "
                };
                Console.WriteLine($"{prefix}{cleaned}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing ServerMessage (0x0D).");
            }

            return Task.CompletedTask;
        }

        [PacketHandler(0x00, PacketRouter.NoSubCode)]  // ChatMessage (0x00)
        public Task HandleChatMessageAsync(Memory<byte> packet)
        {
            var scene = MuGame.Instance?.ActiveScene as GameScene;
            if (scene == null)
            {
                _logger.LogWarning(
                    "GameScene is null when handling ChatMessage (0x00). Cannot display message.");
                return Task.CompletedTask;
            }

            try
            {
                var chatMsg = new ChatMessage(packet);
                _logger.LogInformation(
                    "Received ChatMessage (0x00): From={Sender}, Type={Type}, Message='{Message}'",
                    chatMsg.Sender, chatMsg.Type, chatMsg.Message);

                string sender = chatMsg.Sender;
                string rawText = chatMsg.Message;
                var type = chatMsg.Type;

                MuGame.ScheduleOnMainThread(() =>
                {
                    var chatLog = scene.Controls.OfType<ChatLogWindow>().FirstOrDefault();
                    if (chatLog != null)
                    {
                        Models.MessageType uiType;
                        string display = rawText;

                        if (type == MUnique.OpenMU.Network.Packets.ServerToClient.ChatMessage.ChatMessageType.Whisper)
                        {
                            uiType = Models.MessageType.Whisper;
                        }
                        else if (rawText.StartsWith("~"))
                        {
                            uiType = Models.MessageType.Party;
                            display = rawText[1..].TrimStart();
                        }
                        else if (rawText.StartsWith("@"))
                        {
                            uiType = Models.MessageType.Guild;
                            display = rawText[1..].TrimStart();
                        }
                        else if (rawText.StartsWith("$"))
                        {
                            uiType = Models.MessageType.Gens;
                            display = rawText[1..].TrimStart();
                        }
                        else
                        {
                            uiType = Models.MessageType.Chat;
                        }

                        chatLog.AddMessage(sender, display, uiType);
                    }
                    else
                    {
                        _logger.LogWarning("ChatLogWindow not found for ChatMessage from {Sender}.", sender);
                    }
                });

                string prefix = type switch
                {
                    ChatMessage.ChatMessageType.Whisper => $"Whisper [{sender}]: ",
                    ChatMessage.ChatMessageType.Normal => $"[{sender}]: ",
                    _ => $"[{sender} ({type})]: "
                };
                Console.WriteLine($"{prefix}{rawText}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing ChatMessage (0x00).");
            }

            return Task.CompletedTask;
        }

        // ───────────────────────── Static API ──────────────────────────

        /// <summary>
        /// Retrieves and clears any queued server messages for processing when the scene is ready.
        /// </summary>
        public static List<(ServerMessage.MessageType Type, string Message)> TakePendingServerMessages()
        {
            lock (_pendingServerMessagesLock)
            {
                var copy = new List<(ServerMessage.MessageType, string)>(_pendingServerMessages);
                _pendingServerMessages.Clear();
                return copy;
            }
        }
    }
}
