using System;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using Client.Main.Controls;
using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Game;
using Client.Main.Controls.UI.Game.Map;
using Client.Main.Models;
using Client.Main.Objects;
using Client.Main.Objects.Player;
using Client.Main.Worlds;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;

namespace Client.Main.Scenes
{
    internal sealed class GameSceneMapController
    {
        private readonly GameScene _scene;
        private readonly MainControl _main;
        private readonly ProgressBarControl _progressBar;
        private readonly ChatLogWindow _chatLog;
        private readonly ChatInputBoxControl _chatInput;
        private readonly MapListControl _mapListControl;
        private readonly DebugPanel _debugPanel;
        private readonly CursorControl _cursor;
        private readonly GameSceneScopeImportController _scopeImportController;
        private readonly ILogger _logger;

        private LoadingScreenControl _loadingScreen;
        private bool _isChangingWorld;
        private MapNameControl _currentMapNameControl;

        public GameSceneMapController(
            GameScene scene,
            MainControl main,
            ProgressBarControl progressBar,
            ChatLogWindow chatLog,
            ChatInputBoxControl chatInput,
            MapListControl mapListControl,
            DebugPanel debugPanel,
            CursorControl cursor,
            GameSceneScopeImportController scopeImportController,
            ILogger logger)
        {
            _scene = scene;
            _main = main;
            _progressBar = progressBar;
            _chatLog = chatLog;
            _chatInput = chatInput;
            _mapListControl = mapListControl;
            _debugPanel = debugPanel;
            _cursor = cursor;
            _scopeImportController = scopeImportController;
            _logger = logger;
        }

        public bool IsChangingWorld => _isChangingWorld;
        public LoadingScreenControl LoadingScreen => _loadingScreen;

        public void EnsureLoadingScreen(string message = "Loading Game...")
        {
            if (_loadingScreen == null)
            {
                _loadingScreen = new LoadingScreenControl { Visible = true, Message = message };
                _scene.Controls.Add(_loadingScreen);
                _loadingScreen.BringToFront();
            }
            else
            {
                _loadingScreen.Visible = true;
                _loadingScreen.Message = message;
            }
        }

        public void DisposeLoadingScreen()
        {
            if (_loadingScreen != null)
            {
                _scene.Controls.Remove(_loadingScreen);
                _loadingScreen.Dispose();
                _loadingScreen = null;
            }
        }

        public void UpdateLoadProgress(string message, float progress)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                if (_loadingScreen == null)
                    return;

                _loadingScreen.Message = message;
                _loadingScreen.Progress = progress;
            });
        }

        public void UpdateLoading(GameTime gameTime)
        {
            _loadingScreen?.Update(gameTime);
        }

        public async Task ChangeMap([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type worldType)
        {
            _isChangingWorld = true;

            _scopeImportController?.ClearObjectTracking();

            EnsureLoadingScreen($"Loading {worldType.Name}...");
            _loadingScreen.Progress = 0f;
            _main.Visible = false;

            var previousWorld = _scene.World;

            if (previousWorld is { Objects: { } objects })
            {
                objects.Detach(_scene.Hero);
            }

            _scene.Hero.Reset();

            var nextWorld = (WorldControl)Activator.CreateInstance(worldType);
            if (nextWorld is WalkableWorldControl walkable)
                walkable.Walker = _scene.Hero;

            _scene.Controls.Add(nextWorld);
            _scene.SetWorldInternal(nextWorld);
            _scene.World.Objects.Add(_scene.Hero);

            _loadingScreen.Progress = 0.1f;
            _logger?.LogDebug($"GameScene.ChangeMap<{worldType.Name}>: Initializing new world...");
            await nextWorld.Initialize();
            _logger?.LogDebug($"GameScene.ChangeMap<{worldType.Name}>: New world initialized. Status: {nextWorld.Status}");
            _loadingScreen.Progress = 0.7f;

            if (previousWorld != null)
            {
                _scene.Controls.Remove(previousWorld);
                previousWorld.Dispose();
                _logger?.LogDebug("GameScene.ChangeMap: Disposed previous world.");
            }

            if (_scene.World.Status == GameControlStatus.Ready)
            {
                _loadingScreen.Progress = 0.8f;
                _logger?.LogDebug("GameScene.ChangeMap: World is Ready. Importing pending objects...");
                await (_scopeImportController?.ImportPendingRemotePlayersAsync() ?? Task.CompletedTask);
                await (_scopeImportController?.ImportPendingNpcsMonstersAsync() ?? Task.CompletedTask);
                await (_scopeImportController?.ImportPendingDroppedItemsAsync() ?? Task.CompletedTask);
            }
            else
            {
                _logger?.LogDebug($"GameScene.ChangeMap: World not ready after Initialize (Status: {_scene.World.Status}). Pending objects may not import correctly.");
            }
            _loadingScreen.Progress = 0.95f;

            await MuGame.Network.SendClientReadyAfterMapChangeAsync();

            DisposeLoadingScreen();
            if (_progressBar != null)
            {
                _progressBar.Visible = false;
            }
            _main.Visible = true;
            _isChangingWorld = false;

            UpdateMapName();
            _logger?.LogDebug($"GameScene.ChangeMap<{worldType.Name}>: ChangeMap completed.");
        }

        public void UpdateMapName()
        {
            if (string.IsNullOrEmpty(_scene.World?.Name))
                return;

            if (_currentMapNameControl != null)
            {
                _scene.Controls.Remove(_currentMapNameControl);
                _currentMapNameControl = null;
            }

            _currentMapNameControl = new MapNameControl { LabelText = _scene.World.Name };
            _scene.Controls.Add(_currentMapNameControl);
            _currentMapNameControl.BringToFront();
            _chatLog?.BringToFront();
            _chatInput?.BringToFront();
            _mapListControl?.BringToFront();
            _debugPanel?.BringToFront();
            _cursor?.BringToFront();
        }
    }
}
