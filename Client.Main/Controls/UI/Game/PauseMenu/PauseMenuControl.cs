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
using Client.Main.Controls.UI.Game;
using Client.Main.Graphics;
using Client.Main.Helpers;
using MUnique.OpenMU.Network.Packets; // LogOutType
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Controls.UI.Game.PauseMenu
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
        private ButtonControl _btnResume;
        private bool _returnInProgress;
        private bool _exitInProgress;
        private OptionsPanelControl _optionsPanel;

        public event EventHandler ResumeClicked;
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
                ControlSize = new Point(360, 400),
                ViewSize = new Point(360, 400),
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
            int btnHeight = 40;
            int x = (_panel.ViewSize.X - btnWidth) / 2;
            int y = 60;
            int spacing = 18;

            _btnResume = CreateButton("Resume", x, y, btnWidth, btnHeight);
            _btnResume.Click += (s, e) =>
            {
                ResumeClicked?.Invoke(this, EventArgs.Empty);
                Visible = false;
                if (_optionsPanel != null)
                {
                    _optionsPanel.Visible = false;
                }
            };
            _panel.Controls.Add(_btnResume);
            y += btnHeight + spacing;

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

        private void ApplyQualityPreset(GraphicsQualityPreset preset, Action onComplete = null)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                var adapter = GraphicsManager.Instance?.GraphicsDevice?.Adapter ?? GraphicsAdapter.DefaultAdapter;
                GraphicsQualityManager.ApplyPreset(preset, adapter, _logger);
                MuGame.Instance?.ApplyGraphicsOptions();
                GraphicsManager.Instance?.UpdateRenderScale();
                onComplete?.Invoke();
            });

            if (MuGame.AppSettings?.Graphics != null)
            {
                MuGame.AppSettings.Graphics.QualityPreset = preset.ToString();
            }
            MuGame.PersistGraphicsPreset(preset);
        }

        private void SetVSync(bool enabled)
        {
            Constants.DISABLE_VSYNC = !enabled;
            if (enabled)
                Constants.UNLIMITED_FPS = false;
            ApplyGraphicsSettings();
        }

        private void SetUnlimitedFps(bool enabled)
        {
            Constants.UNLIMITED_FPS = enabled;
            if (enabled)
                Constants.DISABLE_VSYNC = true;
            ApplyGraphicsSettings();
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
            private readonly List<GameControl> _dynamicControls = new();
            private const int ContentStartY = 175;
            private const int OptionRowHeight = 26;
            private readonly ButtonControl _closeButton;
            private readonly int _panelWidth;

            public OptionsPanelControl(PauseMenuControl owner)
            {
                _owner = owner;
                AutoViewSize = false;
                ControlSize = new Point(480, 700);
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

                int categoryStartY = 60;
                int categoryX = 20;
                int categoryWidth = 130;
                int categoryHeight = 26;
                int categorySpacing = 10;
                int categoriesPerRow = 3;
                int categoryIndex = 0;

                AddCategoryButton("Audio", () => BuildAudioCategory(), categoryStartY,
                    ref categoryX, categoryWidth, categoryHeight, categorySpacing, categoriesPerRow, ref categoryIndex);
                AddCategoryButton("Display", () => BuildDisplayCategory(), categoryStartY,
                    ref categoryX, categoryWidth, categoryHeight, categorySpacing, categoriesPerRow, ref categoryIndex);
                AddCategoryButton("Quality Preset", () => BuildQualityPresetCategory(), categoryStartY,
                    ref categoryX, categoryWidth, categoryHeight, categorySpacing, categoriesPerRow, ref categoryIndex);
                AddCategoryButton("World & Visibility", () => BuildWorldCategory(), categoryStartY,
                    ref categoryX, categoryWidth, categoryHeight, categorySpacing, categoriesPerRow, ref categoryIndex);
                AddCategoryButton("Render Scale", () => BuildRenderScaleCategory(), categoryStartY,
                    ref categoryX, categoryWidth, categoryHeight, categorySpacing, categoriesPerRow, ref categoryIndex);
                AddCategoryButton("Graphics", () => BuildGraphicsCategory(), categoryStartY,
                    ref categoryX, categoryWidth, categoryHeight, categorySpacing, categoriesPerRow, ref categoryIndex);
                AddCategoryButton("Lighting", () => BuildLightingCategory(), categoryStartY,
                    ref categoryX, categoryWidth, categoryHeight, categorySpacing, categoriesPerRow, ref categoryIndex);
                AddCategoryButton("Shadow Quality", () => BuildShadowQualityCategory(), categoryStartY,
                    ref categoryX, categoryWidth, categoryHeight, categorySpacing, categoriesPerRow, ref categoryIndex);
                AddCategoryButton("Performance", () => BuildPerformanceCategory(), categoryStartY,
                    ref categoryX, categoryWidth, categoryHeight, categorySpacing, categoriesPerRow, ref categoryIndex);

                _closeButton = new ButtonControl
                {
                    Text = "Back",
                    ControlSize = new Point(140, 32),
                    ViewSize = new Point(140, 32),
                    X = (ControlSize.X - 140) / 2,
                    Y = ContentStartY,
                    AutoViewSize = false,
                    BackgroundColor = new Color(50, 50, 80, 200),
                    HoverBackgroundColor = new Color(70, 70, 110, 220),
                    PressedBackgroundColor = new Color(40, 40, 70, 220),
                    FontSize = 14f,
                    TextColor = Color.White
                };
                _closeButton.Click += (s, e) => _owner.ToggleOptionsPanel();
                Controls.Add(_closeButton);

                BuildAudioCategory(); // default category
            }

            private delegate void CategoryBuilder(ref int currentY);

            private void ClearDynamicControls()
            {
                foreach (var ctrl in _dynamicControls)
                {
                    Controls.Remove(ctrl);
                }
                _dynamicControls.Clear();
                _options.Clear();
            }

            private void BuildCategory(string categoryName, CategoryBuilder builder)
            {
                ClearDynamicControls();

                int currentY = ContentStartY;
                AddHeading(categoryName, ref currentY);
                builder(ref currentY);

                _closeButton.Y = currentY + 10;
                _closeButton.BringToFront();
            }

            private void BuildAudioCategory()
            {
                BuildCategory("Audio", (ref int currentY) =>
                {
                    AddOption("Background Music", () => Constants.BACKGROUND_MUSIC, value =>
                    {
                        Constants.BACKGROUND_MUSIC = value;
                        _owner.ApplyBackgroundMusicSetting(value);
                    }, ref currentY, OptionRowHeight);

                    AddOption("Sound Effects", () => Constants.SOUND_EFFECTS, value =>
                    {
                        Constants.SOUND_EFFECTS = value;
                        _owner.ApplySoundEffectsVolume();
                    }, ref currentY, OptionRowHeight);
                    AddVolumeControl("Music Volume", () => Constants.BACKGROUND_MUSIC_VOLUME, value =>
                    {
                        Constants.BACKGROUND_MUSIC_VOLUME = value;
                        _owner.ApplyBackgroundMusicVolume();
                    }, ref currentY, OptionRowHeight);
                    AddVolumeControl("Effects Volume", () => Constants.SOUND_EFFECTS_VOLUME, value =>
                    {
                        Constants.SOUND_EFFECTS_VOLUME = value;
                        _owner.ApplySoundEffectsVolume();
                    }, ref currentY, OptionRowHeight);
                });
            }

            private void BuildWorldCategory()
            {
                BuildCategory("World & Visibility", (ref int currentY) =>
                {
                    AddOption("Draw Bounding Boxes", () => Constants.DRAW_BOUNDING_BOXES, value => Constants.DRAW_BOUNDING_BOXES = value, ref currentY, OptionRowHeight);
                    AddOption("Draw Bounding Boxes (Interactives)", () => Constants.DRAW_BOUNDING_BOXES_INTERACTIVES, value => Constants.DRAW_BOUNDING_BOXES_INTERACTIVES = value, ref currentY, OptionRowHeight);
                    AddOption("Draw Grass", () => Constants.DRAW_GRASS, value =>
                    {
                        Constants.DRAW_GRASS = value;
                        if (value)
                        {
                            // When enabling grass, ensure textures are loaded
                            var scene = MuGame.Instance?.ActiveScene as BaseScene;
                            scene?.World?.Terrain?.ReloadGrassIfNeeded();
                        }
                    }, ref currentY, OptionRowHeight);
                    AddOption("Low Quality Switch", () => Constants.ENABLE_LOW_QUALITY_SWITCH, value => Constants.ENABLE_LOW_QUALITY_SWITCH = value, ref currentY, OptionRowHeight);
                    AddOption("Low Quality in Login", () => Constants.ENABLE_LOW_QUALITY_IN_LOGIN_SCENE, value => Constants.ENABLE_LOW_QUALITY_IN_LOGIN_SCENE = value, ref currentY, OptionRowHeight);
                });
            }

            private void BuildQualityPresetCategory()
            {
                BuildCategory("Quality Preset", (ref int currentY) =>
                {
                    AddOption("Auto (Detect)", () => GraphicsQualityManager.UserPreset == GraphicsQualityPreset.Auto, value =>
                    {
                        if (value) _owner.ApplyQualityPreset(GraphicsQualityPreset.Auto, RefreshOptions);
                    }, ref currentY, OptionRowHeight);
                    AddOption("Low (0.75x)", () => GraphicsQualityManager.UserPreset == GraphicsQualityPreset.Low, value =>
                    {
                        if (value) _owner.ApplyQualityPreset(GraphicsQualityPreset.Low, RefreshOptions);
                    }, ref currentY, OptionRowHeight);
                    AddOption("Medium (1.0x)", () => GraphicsQualityManager.UserPreset == GraphicsQualityPreset.Medium, value =>
                    {
                        if (value) _owner.ApplyQualityPreset(GraphicsQualityPreset.Medium, RefreshOptions);
                    }, ref currentY, OptionRowHeight);
                    AddOption("High (2.0x)", () => GraphicsQualityManager.UserPreset == GraphicsQualityPreset.High, value =>
                    {
                        if (value) _owner.ApplyQualityPreset(GraphicsQualityPreset.High, RefreshOptions);
                    }, ref currentY, OptionRowHeight);
                });
            }

            private void BuildRenderScaleCategory()
            {
                BuildCategory("Render Scale", (ref int currentY) =>
                {
                    AddOption("Render Scale: 300%", () => Math.Abs(Constants.RENDER_SCALE - 3.0f) < 0.01f, value =>
                    {
                        if (value) { SetRenderScale(3.0f); }
                    }, ref currentY, OptionRowHeight);
                    AddOption("Render Scale: 200%", () => Math.Abs(Constants.RENDER_SCALE - 2.0f) < 0.01f, value =>
                    {
                        if (value) { SetRenderScale(2.0f); }
                    }, ref currentY, OptionRowHeight);
                    AddOption("Render Scale: 150%", () => Math.Abs(Constants.RENDER_SCALE - 1.5f) < 0.01f, value =>
                    {
                        if (value) { SetRenderScale(1.5f); }
                    }, ref currentY, OptionRowHeight);
                    AddOption("Render Scale: 125%", () => Math.Abs(Constants.RENDER_SCALE - 1.25f) < 0.01f, value =>
                    {
                        if (value) { SetRenderScale(1.25f); }
                    }, ref currentY, OptionRowHeight);
                    AddOption("Render Scale: 100%", () => Math.Abs(Constants.RENDER_SCALE - 1.0f) < 0.01f, value =>
                    {
                        if (value) { SetRenderScale(1.0f); }
                    }, ref currentY, OptionRowHeight);
                    AddOption("Render Scale: 75%", () => Math.Abs(Constants.RENDER_SCALE - 0.75f) < 0.01f, value =>
                    {
                        if (value) { SetRenderScale(0.75f); }
                    }, ref currentY, OptionRowHeight);
                    AddOption("Render Scale: 60%", () => Math.Abs(Constants.RENDER_SCALE - 0.6f) < 0.01f, value =>
                    {
                        if (value) { SetRenderScale(0.6f); }
                    }, ref currentY, OptionRowHeight);
                    AddOption("Render Scale: 50%", () => Math.Abs(Constants.RENDER_SCALE - 0.5f) < 0.01f, value =>
                    {
                        if (value) { SetRenderScale(0.5f); }
                    }, ref currentY, OptionRowHeight);
                    AddOption("Render Scale: 37.5%", () => Math.Abs(Constants.RENDER_SCALE - 0.375f) < 0.01f, value =>
                    {
                        if (value) { SetRenderScale(0.375f); }
                    }, ref currentY, OptionRowHeight);
                });
            }

            private void BuildGraphicsCategory()
            {
                BuildCategory("Graphics", (ref int currentY) =>
                {
                    AddOption("High Quality Textures", () => Constants.HIGH_QUALITY_TEXTURES, value => Constants.HIGH_QUALITY_TEXTURES = value, ref currentY, OptionRowHeight);
                    AddOption("V-Sync", () => !Constants.DISABLE_VSYNC, value =>
                    {
                        _owner.SetVSync(value);
                    }, ref currentY, OptionRowHeight, RefreshOptions);
                });
            }

            private void BuildLightingCategory()
            {
                BuildCategory("Lighting & Materials", (ref int currentY) =>
                {
                    AddOption("Sun Light", () => Constants.SUN_ENABLED, value => Constants.SUN_ENABLED = value, ref currentY, OptionRowHeight);
                    AddOption("Day-Night Cycle (Real Time)", () => Constants.ENABLE_DAY_NIGHT_CYCLE, value =>
                    {
                        Constants.ENABLE_DAY_NIGHT_CYCLE = value;
                        if (!value)
                            SunCycleManager.ResetToDefault();
                    }, ref currentY, OptionRowHeight);
                    AddOption("Sun From +X", () => Constants.SUN_DIRECTION.X >= 0f, value =>
                    {
                        var dir = Constants.SUN_DIRECTION;
                        if (dir.LengthSquared() < 0.0001f)
                            dir = new Vector3(1f, 0f, -0.6f);
                        dir.X = Math.Abs(dir.X) * (value ? 1f : -1f);
                        Constants.SUN_DIRECTION = dir;
                    }, ref currentY, OptionRowHeight);
                    AddVolumeControl("Sun Strength (%)", () => Constants.SUN_STRENGTH * 100f, value =>
                    {
                        Constants.SUN_STRENGTH = MathHelper.Clamp(value, 0f, 200f) / 100f;
                    }, ref currentY, OptionRowHeight, 0f, 200f, 5f);
                    AddVolumeControl("Sun Shadow (%)", () => Constants.SUN_SHADOW_STRENGTH * 100f, value =>
                    {
                        Constants.SUN_SHADOW_STRENGTH = MathHelper.Clamp(value, 0f, 100f) / 100f;
                    }, ref currentY, OptionRowHeight, 0f, 100f, 5f);
                    AddOption("Terrain GPU Lighting", () => Constants.ENABLE_TERRAIN_GPU_LIGHTING, value => Constants.ENABLE_TERRAIN_GPU_LIGHTING = value, ref currentY, OptionRowHeight);
                    AddOption("Dynamic Lights", () => Constants.ENABLE_DYNAMIC_LIGHTS, value =>
                    {
                        Constants.ENABLE_DYNAMIC_LIGHTS = value;
                    }, ref currentY, OptionRowHeight, RefreshOptions);
                    AddOption("Dynamic Lighting Shader (GPU)", () => Constants.ENABLE_DYNAMIC_LIGHTING_SHADER, value =>
                    {
                        Constants.ENABLE_DYNAMIC_LIGHTING_SHADER = value;
                        if (!value)
                            Constants.ENABLE_TERRAIN_GPU_LIGHTING = false;
                    }, ref currentY, OptionRowHeight, RefreshOptions);
                    AddOption("Optimize for Integrated GPU", () => Constants.OPTIMIZE_FOR_INTEGRATED_GPU, value => Constants.OPTIMIZE_FOR_INTEGRATED_GPU = value, ref currentY, OptionRowHeight);
                    AddOption("Debug Lighting Areas", () => Constants.DEBUG_LIGHTING_AREAS, value => Constants.DEBUG_LIGHTING_AREAS = value, ref currentY, OptionRowHeight);
                    AddOption("Item Material Shader", () => Constants.ENABLE_ITEM_MATERIAL_SHADER, value => Constants.ENABLE_ITEM_MATERIAL_SHADER = value, ref currentY, OptionRowHeight);
                    AddOption("Monster Material Shader", () => Constants.ENABLE_MONSTER_MATERIAL_SHADER, value => Constants.ENABLE_MONSTER_MATERIAL_SHADER = value, ref currentY, OptionRowHeight);
                });
            }

            private void BuildShadowQualityCategory()
            {
                BuildCategory("Shadow Quality", (ref int currentY) =>
                {
                    AddOption("Shadow Mapping", () => Constants.ENABLE_SHADOW_MAPPING, value =>
                    {
                        Constants.ENABLE_SHADOW_MAPPING = value;
                        if (value && Constants.GetCurrentShadowQuality() == Constants.ShadowQuality.Off)
                        {
                            Constants.ApplyShadowQualityPreset(Constants.ShadowQuality.Medium);
                        }
                        OnShadowSettingChanged();
                    }, ref currentY, OptionRowHeight);

                    currentY += 8;
                    AddHeading("Quality Presets", ref currentY);

                    AddOption("Off (Disabled)", () => Constants.GetCurrentShadowQuality() == Constants.ShadowQuality.Off, value =>
                    {
                        if (value) { Constants.ApplyShadowQualityPreset(Constants.ShadowQuality.Off); OnShadowSettingChanged(); }
                    }, ref currentY, OptionRowHeight);

                    AddOption("Low (512px, 800 units)", () => Constants.GetCurrentShadowQuality() == Constants.ShadowQuality.Low, value =>
                    {
                        if (value) { Constants.ApplyShadowQualityPreset(Constants.ShadowQuality.Low); OnShadowSettingChanged(); }
                    }, ref currentY, OptionRowHeight);

                    AddOption("Medium (1024px, 1200 units)", () => Constants.GetCurrentShadowQuality() == Constants.ShadowQuality.Medium, value =>
                    {
                        if (value) { Constants.ApplyShadowQualityPreset(Constants.ShadowQuality.Medium); OnShadowSettingChanged(); }
                    }, ref currentY, OptionRowHeight);

                    AddOption("High (1024px, 1500 units)", () => Constants.GetCurrentShadowQuality() == Constants.ShadowQuality.High, value =>
                    {
                        if (value) { Constants.ApplyShadowQualityPreset(Constants.ShadowQuality.High); OnShadowSettingChanged(); }
                    }, ref currentY, OptionRowHeight);

                    AddOption("Ultra (2048px, 2000 units)", () => Constants.GetCurrentShadowQuality() == Constants.ShadowQuality.Ultra, value =>
                    {
                        if (value) { Constants.ApplyShadowQualityPreset(Constants.ShadowQuality.Ultra); OnShadowSettingChanged(); }
                    }, ref currentY, OptionRowHeight);
                });
            }

            private void OnShadowSettingChanged()
            {
                // Force shadow map renderer to recreate render targets with new settings
                var shadowRenderer = GraphicsManager.Instance?.ShadowMapRenderer;
                if (shadowRenderer != null)
                {
                    shadowRenderer.EnsureRenderTarget();
                }
                RefreshOptions();
            }

            private void BuildPerformanceCategory()
            {
                BuildCategory("Performance & Debug", (ref int currentY) =>
                {
                    AddOption("Unlimited FPS", () => Constants.UNLIMITED_FPS, value => _owner.SetUnlimitedFps(value), ref currentY, OptionRowHeight, RefreshOptions);
                    AddOption("Dynamic Buffer Pool", () => Constants.ENABLE_DYNAMIC_BUFFER_POOL, value =>
                    {
                        DynamicBufferPool.SetEnabled(value);
                    }, ref currentY, OptionRowHeight);
                    AddOption("Item Material Animation", () => Constants.ENABLE_ITEM_MATERIAL_ANIMATION, value => Constants.ENABLE_ITEM_MATERIAL_ANIMATION = value, ref currentY, OptionRowHeight);
                    AddOption("Debug Panel", () => Constants.SHOW_DEBUG_PANEL, value =>
                    {
                        Constants.SHOW_DEBUG_PANEL = value;
                        _owner.ApplyDebugPanelSetting();
                    }, ref currentY, OptionRowHeight);
                });
            }

            private void BuildDisplayCategory()
            {
                BuildCategory("Display", (ref int currentY) =>
                {
                    var settings = MuGame.AppSettings?.Graphics;
                    if (settings == null) return;

                    // Get supported display modes from adapter
                    var adapter = GraphicsManager.Instance?.GraphicsDevice?.Adapter ?? GraphicsAdapter.DefaultAdapter;
                    var maxDisplayMode = adapter.CurrentDisplayMode;
                    int maxWidth = maxDisplayMode.Width;
                    int maxHeight = maxDisplayMode.Height;

                    // Helper to check if resolution is supported by adapter for fullscreen
                    bool IsResolutionSupported(int w, int h)
                    {
                        // Always allow resolutions up to max for windowed mode
                        if (!settings.IsFullScreen) return w <= maxWidth && h <= maxHeight;

                        // For fullscreen, check if adapter supports this mode
                        foreach (var mode in adapter.SupportedDisplayModes)
                        {
                            if (mode.Width == w && mode.Height == h)
                                return true;
                        }
                        return false;
                    }

                    AddHeading("Resolution", ref currentY);

                    // Standard 16:9 resolutions only - to maintain UI aspect ratio
                    if (IsResolutionSupported(1280, 720))
                    {
                        AddOption("1280x720", () => settings.Width == 1280 && settings.Height == 720, value =>
                        {
                            if (value) SetResolution(1280, 720);
                        }, ref currentY, OptionRowHeight);
                    }

                    if (IsResolutionSupported(1920, 1080))
                    {
                        AddOption("1920x1080", () => settings.Width == 1920 && settings.Height == 1080, value =>
                        {
                            if (value) SetResolution(1920, 1080);
                        }, ref currentY, OptionRowHeight);
                    }

                    if (IsResolutionSupported(2560, 1440))
                    {
                        AddOption("2560x1440", () => settings.Width == 2560 && settings.Height == 1440, value =>
                        {
                            if (value) SetResolution(2560, 1440);
                        }, ref currentY, OptionRowHeight);
                    }

                    if (IsResolutionSupported(3840, 2160))
                    {
                        AddOption("3840x2160", () => settings.Width == 3840 && settings.Height == 2160, value =>
                        {
                            if (value) SetResolution(3840, 2160);
                        }, ref currentY, OptionRowHeight);
                    }

                    currentY += 8;
                    AddHeading("Window Mode", ref currentY);

                    AddOption("Fullscreen", () => settings.IsFullScreen, value =>
                    {
                        SetFullscreen(value);
                    }, ref currentY, OptionRowHeight);
                });
            }

            private void SetResolution(int width, int height)
            {
                var settings = MuGame.AppSettings?.Graphics;
                if (settings == null) return;

                settings.Width = width;
                settings.Height = height;

                MuGame.ScheduleOnMainThread(() =>
                {
                    MuGame.Instance.ApplyGraphicsConfiguration(settings);
                    GraphicsManager.Instance.UpdateRenderScale();
                });

                MuGame.PersistDisplaySettings(width, height, settings.IsFullScreen);
                RefreshOptions();
            }

            private void SetFullscreen(bool enabled)
            {
                var settings = MuGame.AppSettings?.Graphics;
                if (settings == null) return;

                settings.IsFullScreen = enabled;

                MuGame.ScheduleOnMainThread(() =>
                {
                    MuGame.Instance.ApplyGraphicsConfiguration(settings);
                });

                MuGame.PersistDisplaySettings(settings.Width, settings.Height, enabled);
                RefreshOptions();
            }

            private void AddCategoryButton(string label, Action onClick, int startY,
                ref int currentX, int width, int height, int spacing, int perRow, ref int index)
            {
                int row = index / perRow;
                int col = index % perRow;
                int x = 20 + col * (width + spacing);
                int y = startY + row * (height + spacing);

                var button = new ButtonControl
                {
                    Text = label,
                    X = x,
                    Y = y,
                    ControlSize = new Point(width, height),
                    ViewSize = new Point(width, height),
                    AutoViewSize = false,
                    BackgroundColor = new Color(50, 50, 80, 200),
                    HoverBackgroundColor = new Color(70, 70, 110, 220),
                    PressedBackgroundColor = new Color(40, 40, 70, 220),
                    FontSize = 12f,
                    TextColor = Color.White
                };
                button.Click += (s, e) => onClick();
                Controls.Add(button);

                currentX += width + spacing;
                index++;
            }

            private void SetRenderScale(float scale)
            {
                float clampedScale = MathHelper.Clamp(scale, 0.3f, 3.0f);

                if (Math.Abs(Constants.RENDER_SCALE - clampedScale) < 0.0001f)
                {
                    RefreshOptions();
                    return;
                }

                Constants.RENDER_SCALE = clampedScale;
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
                option.CollectControls(_dynamicControls);
                _options.Add(option);
                currentY += rowHeight;
            }

            private void AddHeading(string label, ref int currentY)
            {
                var heading = new LabelControl
                {
                    Text = label,
                    X = 14,
                    Y = currentY,
                    FontSize = 13f,
                    TextColor = new Color(180, 200, 255)
                };
                Controls.Add(heading);
                _dynamicControls.Add(heading);
                currentY += 18;
            }

            public void Refresh()
            {
                foreach (var option in _options)
                {
                    option.Refresh();
                }
            }

            private void AddVolumeControl(string label, Func<float> getter, Action<float> setter, ref int currentY, int rowHeight, float minValue = 0f, float maxValue = 100f, float step = 5f)
            {
                var option = new OptionVolume(label, getter, setter, currentY, _panelWidth, minValue, maxValue, step);
                option.AddTo(Controls);
                option.CollectControls(_dynamicControls);
                _options.Add(option);
                currentY += rowHeight;
            }

            private interface IOptionRow
            {
                void AddTo(ChildrenCollection<GameControl> controls);
                void Refresh();
                void CollectControls(List<GameControl> controls);
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

                public void CollectControls(List<GameControl> controls)
                {
                    controls.Add(_label);
                    controls.Add(_button);
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
                private readonly float _minValue;
                private readonly float _maxValue;
                private readonly float _step;

                public OptionVolume(string label, Func<float> getter, Action<float> setter, int y, int panelWidth, float minValue = 0f, float maxValue = 100f, float step = 5f)
                {
                    _getter = getter;
                    _setter = setter;
                    _minValue = minValue;
                    _maxValue = maxValue;
                    _step = step;

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

                    _minusButton.Click += (s, e) => AdjustVolume(-_step);
                    _plusButton.Click += (s, e) => AdjustVolume(_step);

                    Refresh();
                }

                private void AdjustVolume(float delta)
                {
                    float value = MathHelper.Clamp(_getter() + delta, _minValue, _maxValue);
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
                    float value = MathHelper.Clamp(_getter(), _minValue, _maxValue);
                    _valueLabel.Text = $"{Math.Round(value)}%";
                    _minusButton.Enabled = value > _minValue;
                    _plusButton.Enabled = value < _maxValue;
                }

                public void CollectControls(List<GameControl> controls)
                {
                    controls.Add(_label);
                    controls.Add(_valueLabel);
                    controls.Add(_minusButton);
                    controls.Add(_plusButton);
                }
            }
        }

    }
}
