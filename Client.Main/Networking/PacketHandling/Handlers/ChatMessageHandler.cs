using Client.Main.Controls.UI;           // For ChatLogWindow, NotificationManager
using Client.Main.Core.Utilities;       // For PacketHandlerAttribute
using Client.Main.Scenes;               // For GameScene
using Client.Main.Objects.Effects;       // For ChatBubbleObject
using Client.Main.Objects.Player;        // For PlayerObject
using Client.Main.Controls;              // For WalkableWorldControl
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using System;
using System.Collections.Generic;
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
            @"^0+(?=\S)",  // Remove leading zeros followed by any non-whitespace character
            RegexOptions.Compiled);
        private static readonly object _lastMsgLock = new();
        private static (DateTime Time, string Text) _lastBlueSystemMessage;

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

                // for contextual popups (e.g., buy failed reason), remember last BlueNormal
                if (serverMsg.Type == ServerMessage.MessageType.BlueNormal && !string.IsNullOrWhiteSpace(cleaned))
                {
                    lock (_lastMsgLock)
                    {
                        _lastBlueSystemMessage = (DateTime.UtcNow, cleaned);
                    }

                    // Check if this message is from a recently talked-to NPC
                    var characterState = MuGame.Network?.GetCharacterState();
                    if (characterState != null && characterState.LastNpcNetworkId != 0)
                    {
                        ushort npcId = characterState.LastNpcNetworkId;
                        ushort npcType = characterState.LastNpcTypeNumber;

                        // Try to show chat bubble above the NPC
                        MuGame.ScheduleOnMainThread(() =>
                        {
                            if (scene?.World == null) return;

                            var world = scene.World;
                            if (world.TryGetWalkerById(npcId, out var npc) && npc != null)
                            {
                                string npcName = NpcDatabase.GetNpcName(npcType);
                                var bubble = new ChatBubbleObject(cleaned, npcId, npcName);
                                world.Objects.Add(bubble);

                                _logger.LogDebug("Created chat bubble for NPC {NpcId} ({NpcName}): '{Message}'", npcId, npcName, cleaned);
                            }
                        });
                    }
                }

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

        [PacketHandler(0x01, PacketRouter.NoSubCode)]  // ObjectMessage (0x01)
        public Task HandleObjectMessageAsync(Memory<byte> packet)
        {
            try
            {
                var msg = new ObjectMessage(packet);
                ushort targetId = (ushort)(msg.ObjectId & 0x7FFF);
                string text = msg.Message ?? string.Empty;

                _logger.LogInformation("Received ObjectMessage (0x01): Target={TargetId:X4}, Text='{Text}'", targetId, text);

                MuGame.ScheduleOnMainThread(() =>
                {
                    var scene = MuGame.Instance?.ActiveScene as GameScene;
                    if (scene?.World == null)
                    {
                        return;
                    }

                    var world = scene.World;
                    if (world.TryGetWalkerById(targetId, out var target) && target != null)
                    {
                        // Reuse chat bubble UI to show NPC/player overhead messages.
                        var bubble = new ChatBubbleObject(text, targetId, target.DisplayName);
                        world.Objects.Add(bubble);
                    }

                    scene.ChatLog?.AddMessage("System", text, Client.Main.Models.MessageType.System);
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing ObjectMessage (0x01).");
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

                var dispatch = new ChatDispatch(scene, sender, rawText, type);
                MuGame.ScheduleOnMainThread(ProcessChatOnMainThread, dispatch);

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

        private void ProcessChatOnMainThread(ChatDispatch dispatch)
        {
            var scene = dispatch.Scene;
            if (scene == null)
            {
                _logger.LogWarning("ChatLogWindow not found for ChatMessage from {Sender}.", dispatch.Sender);
                return;
            }

            var chatLog = scene.ChatLog;
            if (chatLog == null)
            {
                _logger.LogWarning("ChatLogWindow not found for ChatMessage from {Sender}.", dispatch.Sender);
                return;
            }

            var type = dispatch.Type;
            var rawText = dispatch.RawText;

            Models.MessageType uiType;
            string display = rawText;

            if (type == ChatMessage.ChatMessageType.Whisper)
            {
                uiType = Models.MessageType.Whisper;
                // Play whisper sound when receiving a whisper message
                Controllers.SoundController.Instance.PlayBuffer("Sound/iWhisper.wav");
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

            chatLog.AddMessage(dispatch.Sender, display, uiType);

            if (scene.World is WalkableWorldControl world)
            {
                PlayerObject player = null;
                var players = world.Players;
                for (int i = 0; i < players.Count; i++)
                {
                    var candidate = players[i];
                    if (candidate != null &&
                        string.Equals(candidate.Name, dispatch.Sender, StringComparison.OrdinalIgnoreCase))
                    {
                        player = candidate;
                        break;
                    }
                }

                if (player == null && world is WalkableWorldControl walk &&
                    walk.Walker is PlayerObject hero &&
                    string.Equals(hero.Name, dispatch.Sender, StringComparison.OrdinalIgnoreCase))
                {
                    player = hero;
                }

                if (player != null)
                {
                    ChatBubbleObject existingBubble = null;
                    var objects = world.Objects.GetSnapshot();
                    for (int i = 0; i < objects.Count; i++)
                    {
                        if (objects[i] is ChatBubbleObject bubble &&
                            bubble.TargetId == player.NetworkId)
                        {
                            existingBubble = bubble;
                            break;
                        }
                    }
                    
                    if (existingBubble != null)
                    {
                        existingBubble.AppendMessage(display);
                    }
                    else
                    {
                        var bubble = new ChatBubbleObject(display, player.NetworkId, dispatch.Sender);
                        world.Objects.Add(bubble);
                    }
                }
            }
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

        /// <summary>
        /// Returns last BlueNormal message if it's not older than maxAgeMs. Otherwise null.
        /// </summary>
        public static string TryGetRecentBlueSystemMessage(int maxAgeMs = 1500)
        {
            lock (_lastMsgLock)
            {
                if (string.IsNullOrEmpty(_lastBlueSystemMessage.Text)) return null;
                if ((DateTime.UtcNow - _lastBlueSystemMessage.Time).TotalMilliseconds <= maxAgeMs)
                {
                    return _lastBlueSystemMessage.Text;
                }
                return null;
            }
        }

        private sealed class ChatDispatch
        {
            public GameScene Scene { get; }
            public string Sender { get; }
            public string RawText { get; }
            public ChatMessage.ChatMessageType Type { get; }

            public ChatDispatch(GameScene scene, string sender, string rawText, ChatMessage.ChatMessageType type)
            {
                Scene = scene;
                Sender = sender;
                RawText = rawText;
                Type = type;
            }
        }
    }
}
