using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Client.Main.Controls.UI.Common;
using System.Threading.Tasks;
using Client.Main.Scenes;
using Client.Main.Networking;
using Client.Main.Networking.Services;
using Microsoft.Extensions.Logging;
using Client.Main.Core.Client; // ClientConnectionState
using Client.Main;
using Client.Main.Controllers;
using MUnique.OpenMU.Network.Packets; // LogOutType

namespace Client.Main.Controls.UI.Game
{
    public class PauseMenuControl : UIControl
    {
        private readonly ILogger _logger = MuGame.AppLoggerFactory?.CreateLogger<PauseMenuControl>();
        private EventHandler<System.Collections.Generic.List<(string Name, MUnique.OpenMU.Network.Packets.CharacterClassNumber Class, ushort Level, byte[] Appearance)>> _characterListHandler;
        private EventHandler<LogOutType> _logoutResponseHandler;
        private sealed class PanelControl : UIControl { }
        private PanelControl _panel;
        private LabelControl _titleLabel;
        private ButtonControl _btnCharacterSelect;
        private ButtonControl _btnServerSelect;
        private ButtonControl _btnOptions;
        private ButtonControl _btnExit;
        private bool _returnInProgress;
        private bool _exitInProgress;
        private OptionsPanelControl _optionsPanel;

        public event EventHandler CharacterSelectClicked;
        public event EventHandler ServerSelectClicked;
        public event EventHandler OptionsClicked;
        public event EventHandler ExitClicked;

        public PauseMenuControl()
        {
            Visible = false;
            Interactive = true;
            AutoViewSize = false;
            // Cover the full screen; BaseScene will position us at 0,0
            ViewSize = new Point(UiScaler.VirtualSize.X, UiScaler.VirtualSize.Y);
            ControlSize = ViewSize;
            BackgroundColor = new Color(0, 0, 0, 180); // semi-transparent overlay

            // Center panel
            _panel = new PanelControl
            {
                AutoViewSize = false,
                ControlSize = new Point(360, 260),
                ViewSize = new Point(360, 260),
                Align = Models.ControlAlign.HorizontalCenter | Models.ControlAlign.VerticalCenter,
                BackgroundColor = new Color(20, 20, 30, 230),
                BorderColor = new Color(80, 80, 120, 255),
                BorderThickness = 1,
                Interactive = true
            };
            Controls.Add(_panel);

            _titleLabel = new LabelControl
            {
                Text = "Menu",
                FontSize = 18f,
                TextColor = Color.White,
                X = 0,
                Y = 18,
                Align = Models.ControlAlign.HorizontalCenter
            };
            _panel.Controls.Add(_titleLabel);

            int btnWidth = 280;
            int btnHeight = 38;
            int x = (_panel.ViewSize.X - btnWidth) / 2;
            int y = 60;
            int spacing = 12;

            _btnCharacterSelect = CreateButton("Return to Character Select", x, y, btnWidth, btnHeight);
            _btnCharacterSelect.Click += async (s, e) =>
            {
                if (_returnInProgress) return; // prevent reentrancy / double-requests
                _returnInProgress = true;
                try
                {
                    CharacterSelectClicked?.Invoke(this, EventArgs.Empty);
                    await HandleReturnToCharacterSelectAsync();
                }
                finally
                {
                    _returnInProgress = false;
                }
            };
            _panel.Controls.Add(_btnCharacterSelect);
            y += btnHeight + spacing;

            _btnServerSelect = CreateButton("Return to Server Select", x, y, btnWidth, btnHeight);
            _btnServerSelect.Click += async (s, e) => { ServerSelectClicked?.Invoke(this, EventArgs.Empty); await HandleReturnToServerSelectAsync(); };
            _panel.Controls.Add(_btnServerSelect);
            y += btnHeight + spacing;

            _btnOptions = CreateButton("Options", x, y, btnWidth, btnHeight);
            _btnOptions.Click += (s, e) =>
            {
                OptionsClicked?.Invoke(this, EventArgs.Empty);
                ToggleOptionsPanel();
            };
            _panel.Controls.Add(_btnOptions);
            y += btnHeight + spacing;

            _btnExit = CreateButton("Exit Game", x, y, btnWidth, btnHeight);
            _btnExit.Click += async (s, e) =>
            {
                if (_exitInProgress) return;
                _exitInProgress = true;
                try
                {
                    ExitClicked?.Invoke(this, EventArgs.Empty);
                    await HandleExitAsync();
                }
                finally
                {
                    _exitInProgress = false;
                }
            };
            _panel.Controls.Add(_btnExit);
        }

        private static ButtonControl CreateButton(string text, int x, int y, int width, int height)
        {
            return new ButtonControl
            {
                Text = text,
                X = x,
                Y = y,
                ControlSize = new Point(width, height),
                ViewSize = new Point(width, height),
                AutoViewSize = false,
                BackgroundColor = new Color(50, 50, 80, 200),
                HoverBackgroundColor = new Color(70, 70, 110, 220),
                PressedBackgroundColor = new Color(40, 40, 70, 220),
                FontSize = 14f,
                TextColor = Color.White
            };
        }

        private void ToggleOptionsPanel()
        {
            if (_optionsPanel == null)
            {
                _optionsPanel = new OptionsPanelControl(this)
                {
                    Visible = false
                };
                Controls.Add(_optionsPanel);
                _optionsPanel.BringToFront();
            }

            bool show = !_optionsPanel.Visible;
            _optionsPanel.Visible = show;
            _panel.Visible = !show;

            if (show)
            {
                _optionsPanel.Refresh();
                _optionsPanel.BringToFront();
            }
        }

        // --- Internal handlers (network-aware) ---
        private async Task HandleReturnToCharacterSelectAsync()
        {
            try
            {
                Visible = false;
                if (_optionsPanel != null)
                {
                    _optionsPanel.Visible = false;
                }
                _panel.Visible = true;

                // Close NPC/Vault before switching
                try
                {
                    NpcShopControl.Instance.Visible = false;
                    VaultControl.Instance.Visible = false;
                    var svc = MuGame.Network?.GetCharacterService();
                    if (svc != null)
                        _ = svc.SendCloseNpcRequestAsync();
                    MuGame.Network?.GetCharacterState()?.ClearShopItems();
                }
                catch { }

                var net = MuGame.Network;
                if (net == null || !net.IsConnected)
                {
                    MuGame.Instance.ChangeScene(new LoginScene());
                    return;
                }

                UnsubscribeCharacterListHandler(net);
                UnsubscribeLogoutHandler(net);

                var characterListTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                void CharacterListHandler(object sender, System.Collections.Generic.List<(string Name, MUnique.OpenMU.Network.Packets.CharacterClassNumber Class, ushort Level, byte[] Appearance)> list)
                {
                    try
                    {
                        var next = new SelectCharacterScene(list, net);
                        MuGame.Instance.ChangeScene(next);
                    }
                    finally
                    {
                        try { net.CharacterListReceived -= CharacterListHandler; } catch { }
                        _characterListHandler = null;
                        characterListTcs.TrySetResult(true);
                    }
                }
                _characterListHandler = CharacterListHandler;
                net.CharacterListReceived += _characterListHandler;

                var logoutTcs = new TaskCompletionSource<LogOutType>(TaskCreationOptions.RunContinuationsAsynchronously);
                void LogoutHandler(object sender, LogOutType type)
                {
                    logoutTcs.TrySetResult(type);
                }
                _logoutResponseHandler = LogoutHandler;
                net.LogoutResponseReceived += _logoutResponseHandler;

                _logger?.LogInformation("PauseMenu: Sending logout request (BackToCharacterSelection). Current state: {State}", net.CurrentState);
                await net.GetCharacterService().SendLogoutRequestAsync(LogOutType.BackToCharacterSelection);

                var logoutCompleted = await Task.WhenAny(logoutTcs.Task, Task.Delay(6000));
                if (logoutCompleted != logoutTcs.Task)
                {
                    _logger?.LogWarning("Logout response timed out. Staying in game.");
                    UnsubscribeLogoutHandler(net);
                    UnsubscribeCharacterListHandler(net);
                    Visible = true;
                    return;
                }

                var logoutResult = await logoutTcs.Task;
                UnsubscribeLogoutHandler(net);

                if (logoutResult != LogOutType.BackToCharacterSelection)
                {
                    _logger?.LogInformation("Logout returned type {Type}; aborting character selection flow.", logoutResult);
                    UnsubscribeCharacterListHandler(net);

                    if (logoutResult == LogOutType.BackToServerSelection)
                    {
                        MuGame.Instance.ChangeScene(new LoginScene());
                    }
                    else
                    {
                        Visible = true;
                    }
                    return;
                }

                // Wait for the refreshed character list which is requested after logout response.
                var listCompleted = await Task.WhenAny(characterListTcs.Task, Task.Delay(6000));
                if (listCompleted != characterListTcs.Task)
                {
                    UnsubscribeCharacterListHandler(net);
                    _logger?.LogWarning("Character list response timed out after logout. Staying in game.");

                    var cached = net.GetCachedCharacterList();
                    if (cached != null && cached.Count > 0)
                    {
                        try
                        {
                            _logger?.LogInformation("Using cached character list as fallback after timeout.");
                            MuGame.Instance.ChangeScene(new SelectCharacterScene(cached.ToList(), net));
                            return;
                        }
                        catch { /* if anything fails, reopen menu below */ }
                    }

                    Visible = true;
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error while returning to character select");
                // Keep the current scene; allow user to retry instead of forcing LoginScene
                Visible = true;

            }
        }

        private async Task HandleReturnToServerSelectAsync()
        {
            try
            {
                Visible = false;
                if (_optionsPanel != null)
                {
                    _optionsPanel.Visible = false;
                }
                _panel.Visible = true;

                // Close NPC/Vault before switching
                try
                {
                    NpcShopControl.Instance.Visible = false;
                    VaultControl.Instance.Visible = false;
                    var svc = MuGame.Network?.GetCharacterService();
                    if (svc != null)
                        _ = svc.SendCloseNpcRequestAsync();
                    MuGame.Network?.GetCharacterState()?.ClearShopItems();
                }
                catch { }

                var net = MuGame.Network;
                if (net == null || !net.IsConnected)
                {
                    MuGame.Instance.ChangeScene(new LoginScene());
                    return;
                }

                UnsubscribeCharacterListHandler(net);
                UnsubscribeLogoutHandler(net);

                var logoutTcs = new TaskCompletionSource<LogOutType>(TaskCreationOptions.RunContinuationsAsynchronously);
                void LogoutHandler(object sender, LogOutType type)
                {
                    logoutTcs.TrySetResult(type);
                }
                _logoutResponseHandler = LogoutHandler;
                net.LogoutResponseReceived += _logoutResponseHandler;

                _logger?.LogInformation("PauseMenu: Sending logout request (BackToServerSelection). Current state: {State}", net.CurrentState);
                await net.GetCharacterService().SendLogoutRequestAsync(LogOutType.BackToServerSelection);

                var completed = await Task.WhenAny(logoutTcs.Task, Task.Delay(6000));
                if (completed != logoutTcs.Task)
                {
                    _logger?.LogWarning("Logout response timed out. Staying in game.");
                    UnsubscribeLogoutHandler(net);
                    Visible = true;
                    return;
                }

                var logoutResult = await logoutTcs.Task;
                UnsubscribeLogoutHandler(net);

                if (logoutResult != LogOutType.BackToServerSelection)
                {
                    _logger?.LogInformation("Logout returned type {Type}; keeping player in current scene.", logoutResult);
                    Visible = true;
                    return;
                }

                try
                {
                    _ = net.ConnectToConnectServerAsync();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "PauseMenu: Failed to initiate connect server reconnect after logout.");
                }

                MuGame.Instance.ChangeScene(new LoginScene());
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error while returning to server select");
                MuGame.Instance.ChangeScene(new LoginScene());
            }
        }

        private async Task HandleExitAsync()
        {
            try
            {
                Visible = false;
                if (_optionsPanel != null)
                {
                    _optionsPanel.Visible = false;
                }
                _panel.Visible = true;

                var net = MuGame.Network;
                if (net != null && net.IsConnected)
                {
                    UnsubscribeLogoutHandler(net);

                    var logoutTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    void LogoutHandler(object sender, LogOutType type)
                    {
                        if (type == LogOutType.CloseGame)
                        {
                            logoutTcs.TrySetResult(true);
                        }
                    }

                    _logoutResponseHandler = LogoutHandler;
                    net.LogoutResponseReceived += _logoutResponseHandler;

                    _logger?.LogInformation("PauseMenu: Sending logout request (CloseGame). Current state: {State}", net.CurrentState);
                    try
                    {
                        await net.GetCharacterService().SendLogoutRequestAsync(LogOutType.CloseGame);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "PauseMenu: Logout request (CloseGame) failed, proceeding with local shutdown.");
                        logoutTcs.TrySetResult(false);
                    }

                    await Task.WhenAny(logoutTcs.Task, Task.Delay(3000));

                    UnsubscribeLogoutHandler(net);
                }

                MuGame.ScheduleOnMainThread(() =>
                {
#if !IOS
                    MuGame.Instance.Exit();
#endif
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "PauseMenu: Error while exiting the game. Forcing shutdown.");
#if !IOS
                MuGame.ScheduleOnMainThread(() => MuGame.Instance.Exit());
#endif
            }
        }

        private void ApplyBackgroundMusicSetting(bool enabled)
        {
            if (!enabled)
            {
                SoundController.Instance.StopBackgroundMusic();
                return;
            }

            var scene = MuGame.Instance?.ActiveScene as BaseScene;
            var music = scene?.World?.BackgroundMusicPath;
            if (!string.IsNullOrEmpty(music))
            {
                SoundController.Instance.PlayBackgroundMusic(music);
                SoundController.Instance.ApplyBackgroundMusicVolume();
            }
        }

        private void ApplyGraphicsSettings()
        {
            MuGame.ScheduleOnMainThread(() => MuGame.Instance?.ApplyGraphicsOptions());
        }

        private void ApplyBackgroundMusicVolume()
        {
            if (!Constants.BACKGROUND_MUSIC)
            {
                return;
            }
            SoundController.Instance.ApplyBackgroundMusicVolume();
        }

        private void ApplySoundEffectsVolume()
        {
            SoundController.Instance.ApplySoundEffectsVolume();
        }

        private void ApplyDebugPanelSetting()
        {
            if (MuGame.Instance?.ActiveScene is BaseScene scene && scene.DebugPanel != null)
            {
                scene.DebugPanel.Visible = Constants.SHOW_DEBUG_PANEL;
                if (Constants.SHOW_DEBUG_PANEL)
                {
                    scene.DebugPanel.BringToFront();
                }
            }
        }

        public override void Update(GameTime time)
        {
            base.Update(time);

            if (!Visible)
            {
                return;
            }

            if (_optionsPanel == null || !_optionsPanel.Visible)
            {
                if (_panel != null)
                {
                    _panel.Visible = true;
                }
            }
        }

        public override void Dispose()
        {
            try
            {
                var net = MuGame.Network;
                UnsubscribeCharacterListHandler(net);
                UnsubscribeLogoutHandler(net);
            }
            finally
            {
                base.Dispose();
            }
        }

        private void UnsubscribeCharacterListHandler(NetworkManager net)
        {
            if (net != null && _characterListHandler != null)
            {
                try { net.CharacterListReceived -= _characterListHandler; } catch { }
            }
            _characterListHandler = null;
        }

        private void UnsubscribeLogoutHandler(NetworkManager net)
        {
            if (net != null && _logoutResponseHandler != null)
            {
                try { net.LogoutResponseReceived -= _logoutResponseHandler; } catch { }
            }
            _logoutResponseHandler = null;
        }

        private sealed class OptionsPanelControl : UIControl
        {
            private readonly PauseMenuControl _owner;
            private readonly List<IOptionRow> _options = new();
            private readonly ButtonControl _closeButton;
            private readonly int _panelWidth;

            public OptionsPanelControl(PauseMenuControl owner)
            {
                _owner = owner;
                AutoViewSize = false;
                ControlSize = new Point(460, 580);
                ViewSize = ControlSize;
                Align = Models.ControlAlign.HorizontalCenter | Models.ControlAlign.VerticalCenter;
                BackgroundColor = new Color(20, 20, 30, 230);
                BorderColor = new Color(80, 80, 120, 255);
                BorderThickness = 1;
                Interactive = true;
                _panelWidth = ControlSize.X;

                var title = new LabelControl
                {
                    Text = "Options",
                    FontSize = 18f,
                    TextColor = Color.White,
                    Align = Models.ControlAlign.HorizontalCenter,
                    X = 0,
                    Y = 18
                };
                Controls.Add(title);

                int currentY = 60;
                int rowHeight = 22;

                AddOption("Background Music", () => Constants.BACKGROUND_MUSIC, value =>
                {
                    Constants.BACKGROUND_MUSIC = value;
                    _owner.ApplyBackgroundMusicSetting(value);
                }, ref currentY, rowHeight);

                AddOption("Sound Effects", () => Constants.SOUND_EFFECTS, value =>
                {
                    Constants.SOUND_EFFECTS = value;
                    _owner.ApplySoundEffectsVolume();
                }, ref currentY, rowHeight);
                AddVolumeControl("Music Volume", () => Constants.BACKGROUND_MUSIC_VOLUME, value =>
                {
                    Constants.BACKGROUND_MUSIC_VOLUME = value;
                    _owner.ApplyBackgroundMusicVolume();
                }, ref currentY, rowHeight);
                AddVolumeControl("Effects Volume", () => Constants.SOUND_EFFECTS_VOLUME, value =>
                {
                    Constants.SOUND_EFFECTS_VOLUME = value;
                    _owner.ApplySoundEffectsVolume();
                }, ref currentY, rowHeight);
                AddOption("Draw Bounding Boxes", () => Constants.DRAW_BOUNDING_BOXES, value => Constants.DRAW_BOUNDING_BOXES = value, ref currentY, rowHeight);
                AddOption("Draw Bounding Boxes (Interactives)", () => Constants.DRAW_BOUNDING_BOXES_INTERACTIVES, value => Constants.DRAW_BOUNDING_BOXES_INTERACTIVES = value, ref currentY, rowHeight);
                AddOption("Draw Grass", () => Constants.DRAW_GRASS, value => Constants.DRAW_GRASS = value, ref currentY, rowHeight);
                AddOption("Low Quality Switch", () => Constants.ENABLE_LOW_QUALITY_SWITCH, value => Constants.ENABLE_LOW_QUALITY_SWITCH = value, ref currentY, rowHeight);
                AddOption("Low Quality in Login", () => Constants.ENABLE_LOW_QUALITY_IN_LOGIN_SCENE, value => Constants.ENABLE_LOW_QUALITY_IN_LOGIN_SCENE = value, ref currentY, rowHeight);

                // Render scale options
                AddOption("Render Scale: 300%", () => Math.Abs(Constants.RENDER_SCALE - 3.0f) < 0.01f, value =>
                {
                    if (value) { SetRenderScale(3.0f); }
                }, ref currentY, rowHeight);
                AddOption("Render Scale: 200%", () => Math.Abs(Constants.RENDER_SCALE - 2.0f) < 0.01f, value => {
                    if (value) { SetRenderScale(2.0f); }
                }, ref currentY, rowHeight);
                AddOption("Render Scale: 150%", () => Math.Abs(Constants.RENDER_SCALE - 1.5f) < 0.01f, value => {
                    if (value) { SetRenderScale(1.5f); }
                }, ref currentY, rowHeight);
                AddOption("Render Scale: 125%", () => Math.Abs(Constants.RENDER_SCALE - 1.25f) < 0.01f, value => {
                    if (value) { SetRenderScale(1.25f); }
                }, ref currentY, rowHeight);
                AddOption("Render Scale: 100%", () => Math.Abs(Constants.RENDER_SCALE - 1.0f) < 0.01f, value => {
                    if (value) { SetRenderScale(1.0f); }
                }, ref currentY, rowHeight);
                AddOption("Render Scale: 75%", () => Math.Abs(Constants.RENDER_SCALE - 0.75f) < 0.01f, value => {
                    if (value) { SetRenderScale(0.75f); }
                }, ref currentY, rowHeight);

                AddOption("High Quality Textures", () => Constants.HIGH_QUALITY_TEXTURES, value => Constants.HIGH_QUALITY_TEXTURES = value, ref currentY, rowHeight);
                AddOption("Disable V-Sync (Higher FPS)", () => Constants.DISABLE_VSYNC, value => {
                    Constants.DISABLE_VSYNC = value;
                    _owner.ApplyGraphicsSettings(); // Apply V-Sync changes
                }, ref currentY, rowHeight);
                AddOption("Dynamic Lighting Shader", () => Constants.ENABLE_DYNAMIC_LIGHTING_SHADER, value => Constants.ENABLE_DYNAMIC_LIGHTING_SHADER = value, ref currentY, rowHeight);
                AddOption("Optimize for Integrated GPU", () => Constants.OPTIMIZE_FOR_INTEGRATED_GPU, value => Constants.OPTIMIZE_FOR_INTEGRATED_GPU = value, ref currentY, rowHeight);
                AddOption("Debug Lighting Areas", () => Constants.DEBUG_LIGHTING_AREAS, value => Constants.DEBUG_LIGHTING_AREAS = value, ref currentY, rowHeight);
                AddOption("Item Material Shader", () => Constants.ENABLE_ITEM_MATERIAL_SHADER, value => Constants.ENABLE_ITEM_MATERIAL_SHADER = value, ref currentY, rowHeight);
                AddOption("Monster Material Shader", () => Constants.ENABLE_MONSTER_MATERIAL_SHADER, value => Constants.ENABLE_MONSTER_MATERIAL_SHADER = value, ref currentY, rowHeight);
                AddOption("Unlimited FPS", () => Constants.UNLIMITED_FPS, value => Constants.UNLIMITED_FPS = value, ref currentY, rowHeight, _owner.ApplyGraphicsSettings);
                AddOption("Debug Panel", () => Constants.SHOW_DEBUG_PANEL, value =>
                {
                    Constants.SHOW_DEBUG_PANEL = value;
                    _owner.ApplyDebugPanelSetting();
                }, ref currentY, rowHeight);

                _closeButton = new ButtonControl
                {
                    Text = "Back",
                    ControlSize = new Point(140, 32),
                    ViewSize = new Point(140, 32),
                    X = (ControlSize.X - 140) / 2,
                    Y = currentY + 10,
                    AutoViewSize = false,
                    BackgroundColor = new Color(50, 50, 80, 200),
                    HoverBackgroundColor = new Color(70, 70, 110, 220),
                    PressedBackgroundColor = new Color(40, 40, 70, 220),
                    FontSize = 14f,
                    TextColor = Color.White
                };
                _closeButton.Click += (s, e) => _owner.ToggleOptionsPanel();
                Controls.Add(_closeButton);
            }

            private void SetRenderScale(float scale)
            {
                Constants.RENDER_SCALE = scale;
                GraphicsManager.Instance.UpdateRenderScale();
                RefreshOptions();
            }

            private void RefreshOptions()
            {
                foreach (var option in _options)
                {
                    option.Refresh();
                }
            }

            private void AddOption(string label, Func<bool> getter, Action<bool> setter, ref int currentY, int rowHeight, Action onChanged = null)
            {
                var option = new OptionToggle(label, getter, value =>
                {
                    setter(value);
                    onChanged?.Invoke();
                }, currentY, _panelWidth);
                option.AddTo(Controls);
                _options.Add(option);
                currentY += rowHeight;
            }

            public void Refresh()
            {
                foreach (var option in _options)
                {
                    option.Refresh();
                }
            }

            private void AddVolumeControl(string label, Func<float> getter, Action<float> setter, ref int currentY, int rowHeight)
            {
                var option = new OptionVolume(label, getter, setter, currentY, _panelWidth);
                option.AddTo(Controls);
                _options.Add(option);
                currentY += rowHeight;
            }

            private interface IOptionRow
            {
                void AddTo(ChildrenCollection<GameControl> controls);
                void Refresh();
            }

            private sealed class OptionToggle : IOptionRow
            {
                private readonly LabelControl _label;
                private readonly ButtonControl _button;
                private readonly Func<bool> _getter;
                private readonly Action<bool> _setter;

                public OptionToggle(string label, Func<bool> getter, Action<bool> setter, int y, int panelWidth)
                {
                    _getter = getter;
                    _setter = setter;

                    _label = new LabelControl
                    {
                        Text = label,
                        X = 20,
                        Y = y,
                        FontSize = 12f,
                        TextColor = Color.White
                    };

                    _button = new ButtonControl
                    {
                        ControlSize = new Point(110, 26),
                        ViewSize = new Point(110, 26),
                        AutoViewSize = false,
                        X = panelWidth - 150,
                        Y = y - 4,
                        BackgroundColor = new Color(50, 50, 80, 200),
                        HoverBackgroundColor = new Color(70, 70, 110, 220),
                        PressedBackgroundColor = new Color(40, 40, 70, 220),
                        FontSize = 12f,
                        TextColor = Color.White
                    };
                    _button.Click += (s, e) =>
                    {
                        bool newValue = !_getter();
                        _setter(newValue);
                        Refresh();
                    };

                    Refresh();
                }

                public void AddTo(ChildrenCollection<GameControl> controls)
                {
                    controls.Add(_label);
                    controls.Add(_button);
                }

                public void Refresh()
                {
                    bool value = _getter();
                    _button.Text = value ? "On" : "Off";
                }
            }

            private sealed class OptionVolume : IOptionRow
            {
                private readonly LabelControl _label;
                private readonly LabelControl _valueLabel;
                private readonly ButtonControl _minusButton;
                private readonly ButtonControl _plusButton;
                private readonly Func<float> _getter;
                private readonly Action<float> _setter;
                private const float MaxValue = 100f;
                private const float Step = 5f;

                public OptionVolume(string label, Func<float> getter, Action<float> setter, int y, int panelWidth)
                {
                    _getter = getter;
                    _setter = setter;

                    _label = new LabelControl
                    {
                        Text = label,
                        X = 20,
                        Y = y,
                        FontSize = 12f,
                        TextColor = Color.White
                    };

                    _valueLabel = new LabelControl
                    {
                        X = panelWidth - 210,
                        Y = y,
                        FontSize = 12f,
                        TextColor = Color.LightGray,
                        Align = Models.ControlAlign.HorizontalCenter,
                        ControlSize = new Point(70, 24),
                        ViewSize = new Point(70, 24)
                    };

                    _minusButton = new ButtonControl
                    {
                        Text = "-",
                        ControlSize = new Point(28, 24),
                        ViewSize = new Point(28, 24),
                        AutoViewSize = false,
                        X = panelWidth - 130,
                        Y = y - 2,
                        BackgroundColor = new Color(50, 50, 80, 200),
                        HoverBackgroundColor = new Color(70, 70, 110, 220),
                        PressedBackgroundColor = new Color(40, 40, 70, 220),
                        FontSize = 12f,
                        TextColor = Color.White
                    };

                    _plusButton = new ButtonControl
                    {
                        Text = "+",
                        ControlSize = new Point(28, 24),
                        ViewSize = new Point(28, 24),
                        AutoViewSize = false,
                        X = panelWidth - 96,
                        Y = y - 2,
                        BackgroundColor = new Color(50, 50, 80, 200),
                        HoverBackgroundColor = new Color(70, 70, 110, 220),
                        PressedBackgroundColor = new Color(40, 40, 70, 220),
                        FontSize = 12f,
                        TextColor = Color.White
                    };

                    _minusButton.Click += (s, e) => AdjustVolume(-Step);
                    _plusButton.Click += (s, e) => AdjustVolume(Step);

                    Refresh();
                }

                private void AdjustVolume(float delta)
                {
                    float value = MathHelper.Clamp(_getter() + delta, 0f, MaxValue);
                    value = (float)Math.Round(value);
                    _setter(value);
                    Refresh();
                }

                public void AddTo(ChildrenCollection<GameControl> controls)
                {
                    controls.Add(_label);
                    controls.Add(_valueLabel);
                    controls.Add(_minusButton);
                    controls.Add(_plusButton);
                }

                public void Refresh()
                {
                    float value = MathHelper.Clamp(_getter(), 0f, MaxValue);
                    _valueLabel.Text = $"{Math.Round(value)}%";
                }
            }
        }

    }
}
