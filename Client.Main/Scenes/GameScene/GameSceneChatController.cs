using System;
using System.Threading.Tasks;
using Client.Main.Controls.UI;
using Client.Main.Models;
using Microsoft.Extensions.Logging;

namespace Client.Main.Scenes
{
    internal sealed class GameSceneChatController
    {
        private readonly GameSceneMapController _mapController;
        private readonly GameSceneDuelController _duelController;
        private readonly ChatLogWindow _chatLog;
        private readonly ILogger _logger;

        public GameSceneChatController(
            GameSceneMapController mapController,
            GameSceneDuelController duelController,
            ChatLogWindow chatLog,
            ILogger logger)
        {
            _mapController = mapController;
            _duelController = duelController;
            _chatLog = chatLog;
            _logger = logger;
        }

        public void OnChatMessageSendRequested(object sender, ChatMessageEventArgs e)
        {
            if (_mapController?.IsChangingWorld == true || MuGame.Network == null || !MuGame.Network.IsConnected)
            {
                _chatLog.AddMessage("System", "Cannot send message while disconnected or changing maps.", MessageType.Error);
                return;
            }

            if (e.MessageType != MessageType.Whisper && _duelController?.TryHandleDuelChatCommand(e.Message) == true)
            {
                return;
            }
            if (e.MessageType == MessageType.Whisper)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await MuGame.Network.SendWhisperMessageAsync(e.Receiver, e.Message);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to send whisper message");
                        MuGame.ScheduleOnMainThread(() =>
                        {
                            _chatLog?.AddMessage("System", "Failed to send whisper message.", MessageType.Error);
                        });
                    }
                });
            }
            else
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await MuGame.Network.SendPublicChatMessageAsync(e.Message);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to send chat message");
                        MuGame.ScheduleOnMainThread(() =>
                        {
                            _chatLog?.AddMessage("System", "Failed to send message.", MessageType.Error);
                        });
                    }
                });
            }
        }
    }
}
