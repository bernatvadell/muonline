using System;
using System.Threading.Tasks;
using Client.Main.Controls.UI;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Microsoft.Extensions.Logging;

namespace Client.Main.Scenes
{
    internal sealed class GameSceneDuelController
    {
        private readonly GameScene _scene;
        private readonly ChatLogWindow _chatLog;
        private readonly ILogger _logger;

        public GameSceneDuelController(GameScene scene, ChatLogWindow chatLog, ILogger logger)
        {
            _scene = scene;
            _chatLog = chatLog;
            _logger = logger;
        }

        public bool TryHandleDuelChatCommand(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            string command = message.Trim();
            if (command.Length > 0 && (command[0] == '~' || command[0] == '@' || command[0] == '$'))
            {
                command = command.Substring(1).TrimStart();
            }

            if (command.StartsWith("/duelend", StringComparison.OrdinalIgnoreCase))
            {
                TrySendDuelStopRequest();
                return true;
            }

            if (command.StartsWith("/duelstart", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string targetName = parts.Length >= 2 ? parts[1] : null;
                TrySendDuelStartRequest(targetName);
                return true;
            }

            return false;
        }

        public void OnDuelRequestedFromContextMenu(ushort targetPlayerId, string targetPlayerName)
        {
            if (_scene.World == null || _scene.Hero == null)
            {
                return;
            }

            PlayerObject targetPlayer = null;
            var players = _scene.World.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p != null && p.NetworkId == targetPlayerId)
                {
                    targetPlayer = p;
                    break;
                }
            }

            if (targetPlayer == null)
            {
                _chatLog?.AddMessage("System", "Target player is no longer available.", MessageType.Error);
                return;
            }

            if (!CanRequestDuelWithTarget(targetPlayer, explicitTarget: true, out string reason))
            {
                _chatLog?.AddMessage("System", reason, MessageType.Error);
                return;
            }

            var characterService = MuGame.Network?.GetCharacterService();
            if (characterService == null)
            {
                _chatLog?.AddMessage("System", "CharacterService not available.", MessageType.Error);
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    await characterService.SendDuelStartRequestAsync(targetPlayerId, targetPlayerName);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to send duel request.");
                    MuGame.ScheduleOnMainThread(() =>
                    {
                        _chatLog?.AddMessage("System", "Failed to send duel request.", MessageType.Error);
                    });
                }
            });
        }

        public bool IsDuelAttackTarget(PlayerObject targetPlayer)
        {
            var state = MuGame.Network?.GetCharacterState();
            if (state == null || !state.IsDuelActive || targetPlayer == null)
            {
                return false;
            }

            ushort enemyId = state.GetDuelPlayerId(Core.Client.CharacterState.DuelPlayerType.Enemy);
            if (enemyId == 0)
            {
                return false;
            }

            return targetPlayer.NetworkId == enemyId;
        }

        private void TrySendDuelStopRequest()
        {
            if (IsInChaosCastle())
            {
                _chatLog?.AddMessage("System", "You can't use duel commands in Chaos Castle.", MessageType.System);
                return;
            }

            var state = MuGame.Network?.GetCharacterState();
            if (state == null)
            {
                return;
            }

            if (!state.IsDuelActive)
            {
                return;
            }

            var characterService = MuGame.Network?.GetCharacterService();
            if (characterService == null)
            {
                _chatLog?.AddMessage("System", "CharacterService not available.", MessageType.Error);
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    await characterService.SendDuelStopRequestAsync();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to send duel stop request.");
                    MuGame.ScheduleOnMainThread(() =>
                    {
                        _chatLog?.AddMessage("System", "Failed to stop duel.", MessageType.Error);
                    });
                }
            });
        }

        private void TrySendDuelStartRequest(string targetName = null)
        {
            if (_scene.Hero == null || _scene.World == null)
            {
                return;
            }

            if (IsInChaosCastle())
            {
                _chatLog?.AddMessage("System", "You can't use duel commands in Chaos Castle.", MessageType.System);
                return;
            }

            var state = MuGame.Network?.GetCharacterState();
            if (state == null)
            {
                return;
            }

            if (state.IsDuelActive)
            {
                _chatLog?.AddMessage("System", "You are already in a duel.", MessageType.System);
                return;
            }

            if (state.Level < 30)
            {
                _chatLog?.AddMessage("System", "You must be at least level 30 to start a duel.", MessageType.Error);
                return;
            }

            var targetPlayer = FindDuelTarget(targetName);
            if (targetPlayer == null)
            {
                _chatLog?.AddMessage("System", "No valid duel target found nearby.", MessageType.Error);
                return;
            }

            if (!CanRequestDuelWithTarget(targetPlayer, explicitTarget: !string.IsNullOrWhiteSpace(targetName), out string reason))
            {
                _chatLog?.AddMessage("System", reason, MessageType.Error);
                return;
            }

            var characterService = MuGame.Network?.GetCharacterService();
            if (characterService == null)
            {
                _chatLog?.AddMessage("System", "CharacterService not available.", MessageType.Error);
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    await characterService.SendDuelStartRequestAsync(targetPlayer.NetworkId, targetPlayer.Name);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to send duel start request.");
                    MuGame.ScheduleOnMainThread(() =>
                    {
                        _chatLog?.AddMessage("System", "Failed to send duel request.", MessageType.Error);
                    });
                }
            });
        }

        private PlayerObject FindDuelTarget(string targetName)
        {
            if (_scene.Hero == null || _scene.World == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(targetName))
            {
                var players = _scene.World.Players;
                for (int i = 0; i < players.Count; i++)
                {
                    var p = players[i];
                    if (p == null || p == _scene.Hero)
                    {
                        continue;
                    }

                    if (string.Equals(p.Name, targetName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (p.IsDead)
                        {
                            return null;
                        }
                        return p;
                    }
                }

                return null;
            }

            var heroTile = _scene.Hero.Location;
            Models.Direction heroDir = _scene.Hero.Direction;
            Models.Direction oppositeHeroDir = (Models.Direction)(((int)heroDir + 4) & 7);
            var candidates = _scene.World.Players;
            for (int i = 0; i < candidates.Count; i++)
            {
                var p = candidates[i];
                if (p == null || p == _scene.Hero)
                {
                    continue;
                }

                if (p.IsDead)
                {
                    continue;
                }

                if (Math.Abs(p.Location.X - heroTile.X) <= 1 && Math.Abs(p.Location.Y - heroTile.Y) <= 1)
                {
                    if (p.Direction == oppositeHeroDir)
                    {
                        return p;
                    }
                }
            }

            return null;
        }

        private bool CanRequestDuelWithTarget(PlayerObject target, bool explicitTarget, out string reason)
        {
            reason = string.Empty;
            if (_scene.Hero == null || target == null || target == _scene.Hero)
            {
                reason = "Invalid duel target.";
                return false;
            }

            if (target.IsDead)
            {
                reason = "You can't duel a dead player.";
                return false;
            }

            var state = MuGame.Network?.GetCharacterState();
            if (state == null)
            {
                reason = "Character state not available.";
                return false;
            }

            if (state.IsDuelActive)
            {
                reason = "You are already in a duel.";
                return false;
            }

            if (state.Level < 30)
            {
                reason = "You must be at least level 30 to start a duel.";
                return false;
            }

            if (Math.Abs(target.Location.X - _scene.Hero.Location.X) > 1 || Math.Abs(target.Location.Y - _scene.Hero.Location.Y) > 1)
            {
                reason = "You must be standing next to the target player to start a duel.";
                return false;
            }

            if (!explicitTarget)
            {
                Models.Direction oppositeHeroDir = (Models.Direction)(((int)_scene.Hero.Direction + 4) & 7);
                if (target.Direction != oppositeHeroDir)
                {
                    reason = "You need to face the target player to start a duel.";
                    return false;
                }
            }

            return true;
        }

        private bool IsInChaosCastle()
        {
            if (_scene.World == null)
            {
                return false;
            }

            int mapId = _scene.World.WorldIndex - 1;
            if (mapId < 0)
            {
                return false;
            }

            if (mapId >= 18 && mapId <= 23)
            {
                return true;
            }

            if (mapId == 53 || mapId == 97)
            {
                return true;
            }

            return false;
        }
    }
}
