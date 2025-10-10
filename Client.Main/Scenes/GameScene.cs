// GameScene.cs
using Client.Main.Controls;
using Client.Main.Controls.UI;
using Client.Main.Controls.UI.Game;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Client.Main.Worlds;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MUnique.OpenMU.Network.Packets;
using MUnique.OpenMU.Network.Packets.ServerToClient;
using Client.Main.Objects;
using Client.Main.Core.Utilities;
using Client.Main.Networking.PacketHandling.Handlers; // For CharacterClassNumber
using Client.Main.Controllers;
using Microsoft.Extensions.Logging;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Helpers;
using Microsoft.Xna.Framework.Graphics;
using Client.Main.Networking;
using Client.Main.Core.Models;
using System.Reflection;
using Client.Main.Content;
using Client.Data.ATT;
using Client.Main.Controls.UI.Game.Buffs;

namespace Client.Main.Scenes
{
    public class GameScene : BaseScene
    {
        // ──────────────────────────── Fields ────────────────────────────
        private readonly PlayerObject _hero;
        private readonly MainControl _main;
        private WorldControl _nextWorld; // Used for map changes
        private LoadingScreenControl _loadingScreen; // For initial load and map changes
        private MapListControl _mapListControl;
        private ChatLogWindow _chatLog;
        private MoveCommandWindow _moveCommandWindow;
        private ChatInputBoxControl _chatInput;
        private InventoryControl _inventoryControl;
        private Client.Main.Controls.UI.NotificationManager _notificationManager;
        private PartyPanelControl _partyPanel;
        private readonly (string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance) _characterInfo;
        private KeyboardState _previousKeyboardState;
        private bool _isChangingWorld = false;
        private readonly List<(ServerMessage.MessageType Type, string Message)> _pendingNotifications = new();
        private CharacterInfoWindowControl _characterInfoWindow;
        private ILogger _logger = MuGame.AppLoggerFactory?.CreateLogger<GameScene>();
        private MapNameControl _currentMapNameControl; // Track active map name display
        private LabelControl _pingLabel; // Displays current ping
        private double _pingTimer = 0;
        private PauseMenuControl _pauseMenu; // ESC menu
        private Controls.UI.Game.Skills.SkillQuickSlot _skillQuickSlot; // Skill quick slot
        private Controls.UI.Game.Skills.SkillSelectionPanel _skillSelectionPanel; // Skill selection panel (independent)
        private ActiveBuffsPanel _activeBuffsPanel; // Active buffs display (top-left corner)

        // Cache expensive enum values to avoid allocations
        private static readonly Keys[] _allKeys = (Keys[])System.Enum.GetValues(typeof(Keys));

        // Performance optimization fields - track object IDs for O(1) lookups
        private readonly HashSet<ushort> _activePlayerIds = new();
        private readonly HashSet<ushort> _activeMonsterIds = new();
        private readonly HashSet<ushort> _activeNpcIds = new();
        private readonly HashSet<ushort> _activeItemIds = new();

        // Cache for movement-related keys to reduce keyboard processing overhead
        private static readonly Keys[] _moveCommandKeys = { Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.Enter, Keys.Escape };

        // ───────────────────────── Properties ─────────────────────────
        public PlayerObject Hero => _hero;

        public static readonly IReadOnlyDictionary<byte, Type> MapWorldRegistry = DiscoverWorlds();

        private static IReadOnlyDictionary<byte, Type> DiscoverWorlds()
        {
            var registry = new Dictionary<byte, Type>();
            var worldTypes = Assembly.GetExecutingAssembly().GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(WalkableWorldControl).IsAssignableFrom(t));

            foreach (var type in worldTypes)
            {
                var attr = type.GetCustomAttribute<WorldInfoAttribute>();
                if (attr != null)
                {
                    if (!registry.TryAdd((byte)attr.MapId, type))
                    {
                        // Optionally log a warning about duplicate MapId
                    }
                }
            }
            return registry;
        }

        private static readonly Dictionary<ServerMessage.MessageType, Color> NotificationColors = new Dictionary<ServerMessage.MessageType, Color>
        {
            { ServerMessage.MessageType.GoldenCenter, Color.Goldenrod },
            { ServerMessage.MessageType.BlueNormal,   new Color(100, 150, 255) },
            { ServerMessage.MessageType.GuildNotice,  new Color(144, 238, 144) },
        };

        // ──────────────────────── Constructors ────────────────────────
        public GameScene((string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance) characterInfo)
        {
            _characterInfo = characterInfo;
            _logger?.LogDebug($"GameScene constructor called for Character: {_characterInfo.Name} ({_characterInfo.Class})");

            // Create the hero with the appearance data from the character list
            _hero = new PlayerObject(new AppearanceData(characterInfo.Appearance));

            _main = new MainControl(MuGame.Network.GetCharacterState());
            Controls.Add(_main);
            Controls.Add(NpcShopControl.Instance);
            Controls.Add(VaultControl.Instance);

            _mapListControl = new MapListControl { Visible = false };

            _chatLog = new ChatLogWindow
            {
                X = 5,
                Y = UiScaler.VirtualSize.Y - 160 - ChatInputBoxControl.CHATBOX_HEIGHT
            };
            Controls.Add(_chatLog);

            _chatInput = new ChatInputBoxControl(_chatLog, MuGame.AppLoggerFactory)
            {
                X = 5,
                Y = UiScaler.VirtualSize.Y - 65 - ChatInputBoxControl.CHATBOX_HEIGHT
            };
            _chatInput.MessageSendRequested += OnChatMessageSendRequested;
            Controls.Add(_chatInput);

            _pendingNotifications.AddRange(ChatMessageHandler.TakePendingServerMessages());
            _notificationManager = new Client.Main.Controls.UI.NotificationManager();
            Controls.Add(_notificationManager);
            _notificationManager.BringToFront();

            _inventoryControl = new InventoryControl(MuGame.Network, MuGame.AppLoggerFactory);
            Controls.Add(_inventoryControl);
            _inventoryControl.HookEvents();

            _loadingScreen = new LoadingScreenControl { Visible = true, Message = "Loading Game..." };
            Controls.Add(_loadingScreen);
            _loadingScreen.BringToFront();

            _moveCommandWindow = new MoveCommandWindow(MuGame.AppLoggerFactory, MuGame.Network);
            Controls.Add(_moveCommandWindow);
            _moveCommandWindow.MapWarpRequested += OnMapWarpRequested;

            _characterInfoWindow = new CharacterInfoWindowControl { X = 20, Y = 50, Visible = false };
            Controls.Add(_characterInfoWindow);

            _partyPanel = new PartyPanelControl();
            Controls.Add(_partyPanel);

            _pingLabel = new LabelControl
            {
                Text = "Ping: --",
                Align = ControlAlign.Bottom | ControlAlign.Right,
                Margin = new Margin { Bottom = 5, Right = 5 },
                FontSize = 10,
                TextColor = Color.White
            };
            Controls.Add(_pingLabel);
            _pingLabel.BringToFront();

            _chatInput.BringToFront();
            DebugPanel.BringToFront();
            Cursor.BringToFront();

            // Pause/ESC menu
            _pauseMenu = new PauseMenuControl();
            Controls.Add(_pauseMenu);
            _pauseMenu.BringToFront();

            // Skill selection panel (independent, not child of quick slot)
            _skillSelectionPanel = new Controls.UI.Game.Skills.SkillSelectionPanel();
            Controls.Add(_skillSelectionPanel);

            // Skill quick slot
            _skillQuickSlot = new Controls.UI.Game.Skills.SkillQuickSlot(MuGame.Network.GetCharacterState());
            _skillQuickSlot.SetSelectionPanel(_skillSelectionPanel); // Connect panel
            Controls.Add(_skillQuickSlot);
            _skillQuickSlot.BringToFront();

            // Experience bar
            var experienceBar = new Controls.UI.Game.ExperienceBarControl(MuGame.Network.GetCharacterState());
            Controls.Add(experienceBar);

            // Active buffs panel (top-left corner, no border)
            _activeBuffsPanel = new ActiveBuffsPanel(MuGame.Network.GetCharacterState());
            Controls.Add(_activeBuffsPanel);
            _activeBuffsPanel.BringToFront();

            // Start pre-loading common UI assets in background to prevent freezes
            // This runs async and won't block scene initialization
            _ = PreloadCommonUIAssetsAsync();
        }

        public GameScene() : this(GetCharacterInfoFromState())
        {
        }

        public GameScene((string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance) characterInfo, NetworkManager networkManager)
            : this(characterInfo)
        {
            // Optionally store networkManager if needed in the future
        }

        private static (string Name, CharacterClassNumber Class, ushort Level, byte[] Appearance) GetCharacterInfoFromState()
        {
            var state = MuGame.Network?.GetCharacterState();
            if (state != null)
            {
                return (state.Name ?? "Unknown", state.Class, state.Level, Array.Empty<byte>());
            }
            return ("Unknown", CharacterClassNumber.DarkKnight, 1, Array.Empty<byte>());
        }

        // ───────────────────── Content Loading (Progressive) ─────────────────────
        private void UpdateLoadProgress(string message, float progress)
        {
            MuGame.ScheduleOnMainThread(() => // Ensure UI updates are on the main thread
            {
                if (_loadingScreen != null)
                {
                    _loadingScreen.Message = message;
                    _loadingScreen.Progress = progress;
                }
            });
        }

        protected override async Task LoadSceneContentWithProgress(Action<string, float> progressCallback)
        {
            UpdateLoadProgress("Initializing Game Scene...", 0.0f);

            // 1. Hero Setup
            UpdateLoadProgress("Setting up hero info...", 0.05f);

            var charState = MuGame.Network?.GetCharacterState();
            if (charState == null)
            {
                UpdateLoadProgress("Error: CharacterState is null.", 1.0f);
                _logger?.LogDebug("CharacterState is null in GameScene.Load, cannot proceed.");
                if (_loadingScreen != null) { Controls.Remove(_loadingScreen); _loadingScreen.Dispose(); _loadingScreen = null; }
                _main.Visible = false;
                return;
            }

            _hero.CharacterClass = _characterInfo.Class;
            _hero.Name = _characterInfo.Name;

            charState.UpdateCoreCharacterInfo(
                charState.Id,
                _characterInfo.Name,
                _characterInfo.Class,
                _characterInfo.Level,
                charState.PositionX,
                charState.PositionY,
                charState.MapId
            );

            _hero.NetworkId = charState.Id;
            _hero.Location = new Vector2(charState.PositionX, charState.PositionY);
            _logger?.LogDebug($"GameScene.LoadSceneContentWithProgress: _hero.NetworkId set to {charState.Id:X4}, Location set to ({charState.PositionX}, {charState.PositionY}).");

            UpdateLoadProgress("Hero info applied.", 0.1f);
            _hero.PlayerMoved += OnHeroMoved;
            _hero.PlayerTookDamage += OnHeroTookDamage;

            // 2. Determine Initial World (Quick)
            UpdateLoadProgress("Determining initial world...", 0.15f);
            Type initialWorldType = typeof(LorenciaWorld);
            if (charState != null && MapWorldRegistry.TryGetValue((byte)charState.MapId, out var mappedType))
            {
                initialWorldType = mappedType;
            }
            else
            {
                _logger?.LogDebug($"GameScene.Load: Unknown MapId: {charState?.MapId}. Defaulting to Lorencia.");
            }
            UpdateLoadProgress($"Initial world: {initialWorldType.Name}.", 0.2f);

            // 3. Instantiate and Initialize World
            UpdateLoadProgress($"Loading world: {initialWorldType.Name}...", 0.25f);

            if (World != null)
            {
                Controls.Remove(World);
                World.Dispose();
                World = null;
            }

            var worldInstance = (WorldControl)Activator.CreateInstance(initialWorldType);
            if (worldInstance is WalkableWorldControl walkable)
            {
                _logger?.LogDebug($"GameScene.LoadSceneContentWithProgress: About to assign _hero to walkable.Walker. _hero.NetworkId: {_hero.NetworkId:X4}");

                walkable.Walker = _hero;
                _logger?.LogDebug($"GameScene.LoadSceneContentWithProgress: Assigned _hero ({_hero.NetworkId:X4}) to walkableWorld.Walker.");

                EnsureWalkerNetworkId(walkable, charState.Id, "after assignment and verification");

                _logger?.LogDebug($"GameScene.LoadSceneContentWithProgress: Walker.NetworkId after assignment and verification: {walkable.Walker?.NetworkId:X4}");
            }

            Controls.Add(worldInstance);
            World = worldInstance;

            World.Objects.Add(_hero);
            _logger?.LogDebug($"GameScene.LoadSceneContentWithProgress: Added _hero to World.Objects.");

            await worldInstance.Initialize();
            UpdateLoadProgress($"World {initialWorldType.Name} initialized.", 0.6f);

            if (worldInstance is WalkableWorldControl walkableAfterInit)
            {
                _logger?.LogDebug($"GameScene.LoadSceneContentWithProgress: Walker.NetworkId after world initialization: {walkableAfterInit.Walker?.NetworkId:X4}");

                EnsureWalkerNetworkId(walkableAfterInit, charState.Id, "after world initialization");
                _logger?.LogDebug($"GameScene.LoadSceneContentWithProgress: Walker.NetworkId after fix: {walkableAfterInit.Walker?.NetworkId:X4}");
            }

            // 4. Load Hero Assets
            UpdateLoadProgress("Loading hero assets...", 0.65f);
            if (_hero.Status == GameControlStatus.NonInitialized || _hero.Status == GameControlStatus.Initializing)
            {
                ushort expectedNetworkId = charState.Id;
                _logger?.LogDebug($"GameScene.LoadSceneContentWithProgress: _hero.NetworkId before Load(): {_hero.NetworkId:X4}");

                await _hero.Load();

                _logger?.LogDebug($"GameScene.LoadSceneContentWithProgress: _hero.NetworkId after Load(): {_hero.NetworkId:X4}");
                EnsureHeroNetworkId(expectedNetworkId, "after hero Load()");
            }
            UpdateLoadProgress("Hero assets loaded.", 0.80f);

            // 5. Import Pending Objects
            UpdateLoadProgress("Importing nearby entities...", 0.85f);
            if (World.Status == GameControlStatus.Ready)
            {
                await ImportPendingRemotePlayers();
                await ImportPendingNpcsMonsters();
                await ImportPendingDroppedItems();
            }
            else
            {
                _logger?.LogDebug($"GameScene.Load: World not ready after Initialize (Status: {World.Status}). Pending objects may not import correctly.");
            }
            UpdateLoadProgress("Entities imported.", 0.95f);

            // Preload sounds for dropped items
            UpdateLoadProgress("Preloading sounds...", 0.96f);
            PreloadSounds();
            UpdateLoadProgress("Sounds preloaded.", 0.965f);

            // Preload NPC and monster textures
            UpdateLoadProgress("Preloading NPC textures...", 0.97f);
            // Skip preloading to avoid blocking
            UpdateLoadProgress("NPC textures preloaded.", 0.975f);

            // Preload UI textures so opening windows doesn't cause stalls
            UpdateLoadProgress("Preloading UI textures...", 0.98f);
            // Skip preloading to avoid blocking
            UpdateLoadProgress("UI textures preloaded.", 0.99f);

            if (World is WalkableWorldControl finalWalkable)
            {
                _logger?.LogDebug($"GameScene.LoadSceneContentWithProgress: FINAL CHECK - Walker.NetworkId: {finalWalkable.Walker?.NetworkId:X4}, CharState.Id: {charState.Id:X4}");

                // One last check and fix if needed
                EnsureWalkerNetworkId(finalWalkable, charState.Id, "final verification");
                _logger?.LogDebug($"GameScene.LoadSceneContentWithProgress: After final fix - Walker.NetworkId: {finalWalkable.Walker?.NetworkId:X4}");
            }

            // Finalize
            if (_loadingScreen != null)
            {
                Controls.Remove(_loadingScreen);
                _loadingScreen.Dispose();
                _loadingScreen = null;
            }
            _main.Visible = true;
            UpdateLoadProgress("Game ready!", 1.0f);
        }

        public override async Task Load()
        {
            // This method is called by BaseScene.Initialize() if LoadSceneContentWithProgress is not overridden,
            // OR if the overridden method calls base.Load().
            // For GameScene, we want the progressive loading, so we'll call it from here if this Load is hit.
            // However, with the new structure, InitializeWithProgressReporting should call LoadSceneContentWithProgress directly.
            // This is a fallback / ensures old paths might still work or for clarity.
            if (Status == GameControlStatus.Initializing) // Check if we are already in the new init flow
            {
                await LoadSceneContentWithProgress(UpdateLoadProgress);
            }
            else
            {
                // Fallback to old behavior or log a warning
                _logger?.LogDebug("GameScene.Load() called outside of InitializeWithProgressReporting flow. Consider refactoring.");
                await base.Load(); // Which is empty in BaseScene, then calls derived GameScene's old Load logic
            }
        }

        private async void OnMapWarpRequested(int mapIndex, string mapDisplayName)
        {
            _logger?.LogDebug($"Player requested warp to map index: {mapIndex}");
            var mapName = mapDisplayName;
            _chatLog.AddMessage("System", $"Warping to {mapName} (ID {mapIndex})...", MessageType.System);

            try
            {
                await MuGame.Network.SendWarpRequestAsync((ushort)mapIndex);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, $"Error sending warp request for map index {mapIndex}.");
                _chatLog.AddMessage("System", $"Error warping: {ex.Message}", MessageType.Error);
            }
        }

        private void OnHeroMoved(object sender, EventArgs e)
        {
            if (NpcShopControl.Instance.Visible)
            {
                _logger?.LogInformation("Hero moved, closing NPC shop window.");
                NpcShopControl.Instance.Visible = false;
                // Also close inventory when NPC shop closes (similar to vault behavior)
                if (_inventoryControl?.Visible == true)
                {
                    _inventoryControl.Hide();
                }
                var svc = MuGame.Network?.GetCharacterService();
                if (svc != null)
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await svc.SendCloseNpcRequestAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Failed to send close NPC request");
                        }
                    });
                }
            }

            // Close Vault (storage) similarly and hide Inventory if it was open together with it
            if (VaultControl.Instance.Visible)
            {
                _logger?.LogInformation("Hero moved, closing Vault (storage) window.");
                VaultControl.Instance.CloseWindow();
                if (_inventoryControl?.Visible == true)
                {
                    _inventoryControl.Hide();
                }
                var svc = MuGame.Network?.GetCharacterService();
                if (svc != null)
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await svc.SendCloseNpcRequestAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Failed to send close NPC request");
                        }
                    });
                }
            }
        }

        private void OnHeroTookDamage(object sender, EventArgs e)
        {
            // Do not close NPC shop on damage unless explicitly desired.
            // But close Vault (storage) and related Inventory when taking damage
            if (VaultControl.Instance.Visible)
            {
                _logger?.LogInformation("Hero took damage, closing Vault (storage) window.");
                VaultControl.Instance.CloseWindow();
                if (_inventoryControl?.Visible == true)
                {
                    _inventoryControl.Hide();
                }
                var svc = MuGame.Network?.GetCharacterService();
                if (svc != null)
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            await svc.SendCloseNpcRequestAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Failed to send close NPC request");
                        }
                    });
                }
            }
        }

        // ─────────────────── Map Change Logic (Remains largely the same) ───────────────────
        public async Task ChangeMap(Type worldType)
        {
            _isChangingWorld = true;

            // Clear object tracking for new map
            ClearObjectTracking();

            if (_loadingScreen == null) // Recreate if disposed, or handle if just hidden
            {
                _loadingScreen = new LoadingScreenControl { Visible = true };
                Controls.Add(_loadingScreen);
                _loadingScreen.BringToFront();
            }
            else
            {
                _loadingScreen.Visible = true;
            }
            _loadingScreen.Message = $"Loading {worldType.Name}...";
            _loadingScreen.Progress = 0f; // Reset progress for map change
            _main.Visible = false;

            var previousWorld = World;

            if (previousWorld is { Objects: { } objects })
            {
                objects.Detach(_hero);
            }

            _hero.Reset();

            _nextWorld = (WorldControl)Activator.CreateInstance(worldType);
            if (_nextWorld is WalkableWorldControl walkable)
                walkable.Walker = _hero;

            // Add to controls and set World property BEFORE Initialize.
            Controls.Add(_nextWorld);
            World = _nextWorld; // Temporarily set to new world for its Initialize
            World.Objects.Add(_hero); // Add hero to new world

            _loadingScreen.Progress = 0.1f;
            _logger?.LogDebug($"GameScene.ChangeMap<{worldType.Name}>: Initializing new world...");
            await _nextWorld.Initialize();
            _logger?.LogDebug($"GameScene.ChangeMap<{worldType.Name}>: New world initialized. Status: {_nextWorld.Status}");
            _loadingScreen.Progress = 0.7f;

            if (previousWorld != null)
            {
                Controls.Remove(previousWorld);
                previousWorld.Dispose();
                _logger?.LogDebug("GameScene.ChangeMap: Disposed previous world.");
            }

            _nextWorld = null;

            if (World.Status == GameControlStatus.Ready)
            {
                _loadingScreen.Progress = 0.8f;
                _logger?.LogDebug("GameScene.ChangeMap: World is Ready. Importing pending objects...");
                await ImportPendingRemotePlayers();
                await ImportPendingNpcsMonsters();
                await ImportPendingDroppedItems();
            }
            else
            {
                _logger?.LogDebug($"GameScene.ChangeMap: World not ready after Initialize (Status: {World.Status}). Pending objects may not import correctly.");
            }
            _loadingScreen.Progress = 0.95f;

            await MuGame.Network.SendClientReadyAfterMapChangeAsync();

            Controls.Remove(_loadingScreen);
            _loadingScreen.Dispose();
            _loadingScreen = null;
            _main.Visible = true;
            _isChangingWorld = false;

            if (!string.IsNullOrEmpty(World.Name))
            {
                // Hide previous map name control if exists
                if (_currentMapNameControl != null)
                {
                    Controls.Remove(_currentMapNameControl);
                    _currentMapNameControl = null;
                }

                // Create and show new map name control
                _currentMapNameControl = new MapNameControl { LabelText = World.Name };
                Controls.Add(_currentMapNameControl);
                _currentMapNameControl.BringToFront();
                _chatLog.BringToFront();
                _chatInput.BringToFront();
                _mapListControl?.BringToFront();
                DebugPanel.BringToFront();
                Cursor.BringToFront();
            }
            _logger?.LogDebug($"GameScene.ChangeMap<{worldType.Name}>: ChangeMap completed.");
        }

        public async Task ChangeMap<T>() where T : WalkableWorldControl, new()
        {
            await ChangeMap(typeof(T));
        }

        // ──────────────────── Debug & Test Methods ────────────────────
        private void AddChatTestData()
        {
            if (_chatLog == null) return;
            _chatLog.AddMessage(string.Empty, "Welcome to the world of Mu!", MessageType.System);
            _chatLog.AddMessage("Player1", "Hello everyone!", MessageType.Chat);
            _chatLog.AddMessage("System", "Server will restart in 5 minutes.", MessageType.Info);
            _chatLog.AddMessage("GM_Tester", "Please report for an interview.", MessageType.GM);
            _chatLog.AddMessage("AllyMember", "Shall we go for the boss?", MessageType.Guild);
            _chatLog.AddMessage("PartyDude", "I've got a spot!", MessageType.Party);
            _chatLog.AddMessage("Whisperer", "Shall we meet in Lorencia?", MessageType.Whisper);
            for (int i = 0; i < 10; i++) _chatLog.AddMessage("Spammer", $"Test message number {i + 1}.", MessageType.Chat);
            _chatLog.AddMessage(string.Empty, "An unexpected error has occurred.", MessageType.Error);
        }

        // ─────────────────── Import NPCs & Monsters ───────────────────
        private async Task ImportPendingNpcsMonsters()
        {
            if (World is not WalkableWorldControl w) return;
            var list = ScopeHandler.TakePendingNpcsMonsters();
            if (list.Count == 0) return;

            foreach (var s in list)
            {
                // Use HashSet for O(1) lookup instead of expensive LINQ
                if (_activeNpcIds.Contains(s.Id) || _activeMonsterIds.Contains(s.Id)) continue;

                if (!NpcDatabase.TryGetNpcType(s.TypeNumber, out Type objectType)) continue;
                if (Activator.CreateInstance(objectType) is WalkerObject npcMonster)
                {
                    npcMonster.NetworkId = s.Id;
                    npcMonster.Location = new Vector2(s.PositionX, s.PositionY);
                    npcMonster.Direction = (Models.Direction)s.Direction;
                    npcMonster.World = w;

                    try
                    {
                        await npcMonster.Load();
                        w.Objects.Add(npcMonster);

                        // Track the added object
                        if (npcMonster is MonsterObject)
                            _activeMonsterIds.Add(s.Id);
                        else
                            _activeNpcIds.Add(s.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"Error loading pending NPC/Monster {s.Id:X4}");
                        npcMonster.Dispose();
                    }
                }
            }
        }

        // ─────────────────── Import Remote Players ───────────────────
        private async Task ImportPendingRemotePlayers()
        {
            if (World is not WalkableWorldControl w) return;
            var list = ScopeHandler.TakePendingPlayers();

            var heroId = MuGame.Network.GetCharacterState().Id;
            foreach (var s in list)
            {
                if (s.Id == heroId) continue;

                // Use HashSet for O(1) lookup instead of expensive LINQ
                if (_activePlayerIds.Contains(s.Id)) continue;

                // Preserve appearance data so remote players show correct equipment
                var remote = new PlayerObject(new AppearanceData(s.AppearanceData))
                {
                    NetworkId = s.Id,
                    Name = s.Name,
                    CharacterClass = s.Class,
                    Location = new Vector2(s.PositionX, s.PositionY),
                    World = w
                };

                try
                {
                    await remote.Load();
                    w.Objects.Add(remote);

                    // Track the added player
                    _activePlayerIds.Add(s.Id);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"Error loading pending remote player {s.Name} ({s.Id:X4})");
                    remote.Dispose();
                }
            }
        }

        // ─────────────────── Import Dropped Items ───────────────────
        private Task ImportPendingDroppedItems()
        {
            if (World is not WalkableWorldControl w) return Task.CompletedTask;

            var scopeManager = MuGame.Network?.GetScopeManager();
            if (scopeManager == null) return Task.CompletedTask;

            var allDrops = scopeManager.GetScopeItems(ScopeObjectType.Item)
                                       .Concat(scopeManager.GetScopeItems(ScopeObjectType.Money))
                                       .Cast<ScopeObject>();

            foreach (var s in allDrops)
            {
                // Use HashSet for O(1) lookup instead of expensive LINQ
                if (_activeItemIds.Contains(s.Id))
                    continue;

                // Load and add dropped items on main thread to ensure World.Scene is available
                MuGame.ScheduleOnMainThread(async () =>
                {
                    if (w.Status != GameControlStatus.Ready ||
                        _activeItemIds.Contains(s.Id))
                        return;

                    var obj = new DroppedItemObject(
                        s,
                        MuGame.Network.GetCharacterState().Id,
                        MuGame.Network.GetCharacterService(),
                        MuGame.AppLoggerFactory.CreateLogger<DroppedItemObject>());

                    // Set World property before loading
                    obj.World = w;

                    // Add to world so World.Scene is available
                    w.Objects.Add(obj);

                    // Track the added item
                    _activeItemIds.Add(s.Id);

                    try
                    {
                        await obj.Load();
                        // Don't set Hidden immediately - let WorldObject.Update handle visibility checks
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"Error loading pending dropped item {s.Id:X4}");
                        w.Objects.Remove(obj);
                        _activeItemIds.Remove(s.Id);
                        obj.Dispose();
                    }
                });
            }

            return Task.CompletedTask;
        }

        // ─────────────────── NetworkId Fix Helper ───────────────────
        private void EnsureHeroNetworkId(ushort expectedId, string context = "")
        {
            if (_hero.NetworkId != expectedId)
            {
                _logger?.LogWarning($"NetworkId mismatch in {context}. Fixing: {_hero.NetworkId:X4} -> {expectedId:X4}");
                _hero.NetworkId = expectedId;
            }
        }

        private void EnsureWalkerNetworkId(WalkableWorldControl walkable, ushort expectedId, string context = "")
        {
            if (walkable?.Walker?.NetworkId != expectedId)
            {
                _logger?.LogWarning($"Walker NetworkId mismatch in {context}. Fixing: {walkable.Walker?.NetworkId:X4} -> {expectedId:X4}");
                if (walkable.Walker != null)
                {
                    walkable.Walker.NetworkId = expectedId;
                }
            }
        }

        // ─────────────────── Generic Object Import Helper ───────────────────
        private async Task ImportObjects<T>(
            ICollection<ScopeObject> objects,
            HashSet<ushort> trackingSet,
            Func<ScopeObject, T> createFunc,
            string objectTypeName) where T : WorldObject
        {
            if (World is not WalkableWorldControl w) return;
            if (objects.Count == 0) return;

            foreach (var s in objects)
            {
                if (trackingSet.Contains(s.Id)) continue;

                try
                {
                    var gameObject = createFunc(s);
                    if (gameObject == null) continue;

                    gameObject.World = w;
                    await gameObject.Load();
                    w.Objects.Add(gameObject);
                    trackingSet.Add(s.Id);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"Error loading {objectTypeName} {s.Id:X4}");
                }
            }
        }

        // ─────────────────── Object Tracking Helpers ───────────────────
        private void ClearObjectTracking()
        {
            if (World?.Objects != null)
            {
                // Remove all visual objects from current world (they belong to previous map)
                var objectsToRemove = new List<WorldObject>();

                foreach (var obj in World.Objects.ToList())
                {
                    // Keep the hero (main player)
                    if (obj == _hero) continue;

                    objectsToRemove.Add(obj);
                }

                foreach (var obj in objectsToRemove)
                {
                    World.Objects.Remove(obj);
                    obj.Dispose();
                }

                _logger?.LogDebug("ClearObjectTracking: Removed {Count} objects from previous map", objectsToRemove.Count);
            }

            // Clear our local tracking collections
            _activePlayerIds.Clear();
            _activeMonsterIds.Clear();
            _activeNpcIds.Clear();
            _activeItemIds.Clear();

            // Clear dropped items from ScopeManager manually
            // Server may not send OutOfScope packets for items during warp, causing old items to persist
            var scopeManager = MuGame.Network?.GetScopeManager();
            if (scopeManager != null)
            {
                scopeManager.ClearDroppedItemsFromScope();
                _logger?.LogDebug("ClearObjectTracking: Manually cleared dropped items from ScopeManager");
            }
        }

        private void RemoveObjectFromTracking(ushort networkId)
        {
            _activePlayerIds.Remove(networkId);
            _activeMonsterIds.Remove(networkId);
            _activeNpcIds.Remove(networkId);
            _activeItemIds.Remove(networkId);
        }

        // ─────────────────── Notification Handling ───────────────────
        public void ShowNotificationMessage(ServerMessage.MessageType messageType, string message)
        {
            MuGame.ScheduleOnMainThread(() =>
            {
                lock (_pendingNotifications) { _pendingNotifications.Add((messageType, message)); }
            });
        }

        private void ProcessPendingNotifications()
        {
            if (_notificationManager == null) return;
            List<(ServerMessage.MessageType Type, string Message)> currentBatch;
            lock (_pendingNotifications)
            {
                if (_pendingNotifications.Count == 0) return;
                currentBatch = new List<(ServerMessage.MessageType Type, string Message)>(_pendingNotifications);
                _pendingNotifications.Clear();
            }
            foreach (var pending in currentBatch)
            {
                if (NotificationColors.TryGetValue(pending.Type, out Color notificationColor))
                {
                    if (pending.Type != ServerMessage.MessageType.BlueNormal)
                    {
                        _notificationManager.AddNotification(pending.Message, notificationColor);
                    }
                }
                else
                {
                    _chatLog?.AddMessage(string.Empty, pending.Message, Models.MessageType.System);
                }
                if (pending.Type == ServerMessage.MessageType.BlueNormal)
                {
                    _chatLog?.AddMessage(string.Empty, pending.Message, Models.MessageType.System);
                }
            }
        }

        // ─────────────────────────── Update Loop ───────────────────────────
        public override void Update(GameTime gameTime)
        {
            if (_isChangingWorld)
            {
                _loadingScreen?.Update(gameTime);
                return;
            }

            var currentKeyboardState = Keyboard.GetState();

            // Toggle pause menu on ESC (edge-triggered)
            if (currentKeyboardState.IsKeyDown(Keys.Escape) && _previousKeyboardState.IsKeyUp(Keys.Escape))
            {
                if (_pauseMenu != null)
                {
                    _pauseMenu.Visible = !_pauseMenu.Visible;
                    if (_pauseMenu.Visible)
                        _pauseMenu.BringToFront();
                }
            }

            base.Update(gameTime);

            if (FocusControl == _moveCommandWindow && _moveCommandWindow.Visible)
            {
                // Only check relevant keys for move command window instead of all keys
                foreach (Keys key in _moveCommandKeys)
                {
                    if (currentKeyboardState.IsKeyDown(key) && _previousKeyboardState.IsKeyUp(key))
                    {
                        _moveCommandWindow.ProcessKeyInput(key, false);
                    }
                }
            }

            // Determine if any UI element that captures typing has focus.
            bool isUiInputActive =
                (FocusControl is TextFieldControl)
                || (FocusControl == _moveCommandWindow && _moveCommandWindow.Visible)
                || (_pauseMenu != null && _pauseMenu.Visible);

            // Process global hotkeys ONLY if a UI input element is NOT active.
            if (!isUiInputActive)
            {
                if (currentKeyboardState.IsKeyDown(Keys.I) && !_previousKeyboardState.IsKeyDown(Keys.I))
                {
                    bool wasVisible = _inventoryControl.Visible;
                    if (wasVisible)
                        _inventoryControl.Hide();
                    else
                        _inventoryControl.Show();

                    // Play window open sound only when opening (not closing)
                    if (!wasVisible)
                        SoundController.Instance.PlayBuffer("Sound/iCreateWindow.wav");
                }
                if (currentKeyboardState.IsKeyDown(Keys.C) && !_previousKeyboardState.IsKeyDown(Keys.C))
                {
                    if (_characterInfoWindow != null)
                    {
                        bool wasVisible = _characterInfoWindow.Visible;
                        if (wasVisible)
                            _characterInfoWindow.HideWindow();
                        else
                            _characterInfoWindow.ShowWindow();

                        // Play window open sound only when opening (not closing)
                        if (!wasVisible)
                            SoundController.Instance.PlayBuffer("Sound/iCreateWindow.wav");
                    }
                }
                if (currentKeyboardState.IsKeyDown(Keys.M) && !_previousKeyboardState.IsKeyDown(Keys.M))
                {
                    if (!NpcShopControl.Instance.Visible)
                    {
                        _moveCommandWindow.ToggleVisibility();
                    }
                }

                // Handle opening the chat window if it's not focused and Enter is pressed.
                if (!IsKeyboardEnterConsumedThisFrame && !_chatInput.Visible && currentKeyboardState.IsKeyDown(Keys.Enter) && !_previousKeyboardState.IsKeyDown(Keys.Enter))
                {
                    _chatInput.Show();
                }
            }

            _notificationManager?.Update(gameTime);
            ProcessPendingNotifications();

            if (World == null || World.Status != GameControlStatus.Ready)
            {
                _previousKeyboardState = currentKeyboardState;
                return;
            }

            // Handle attack clicks on monsters with proper validation
            if (!IsMouseInputConsumedThisFrame &&
                MouseHoverObject is MonsterObject targetMonster &&
                MuGame.Instance.Mouse.LeftButton == ButtonState.Pressed &&
                MuGame.Instance.PrevMouseState.LeftButton == ButtonState.Released) // Fresh press
            {
                if (Hero != null &&
                    !targetMonster.IsDead &&
                    targetMonster.World == World && // Ensure same world
                    Vector2.Distance(Hero.Location, targetMonster.Location) <= Hero.GetAttackRangeTiles()) // Check range
                {
                    Hero.Attack(targetMonster);
                    SetMouseInputConsumed(); // Consume the click
                }
            }

            // Handle skill usage with right-click
            if (!IsMouseInputConsumedThisFrame &&
                !IsMouseOverUi() && // Don't use skills if mouse is over UI
                MuGame.Instance.Mouse.RightButton == ButtonState.Pressed &&
                MuGame.Instance.PrevMouseState.RightButton == ButtonState.Released && // Fresh press
                _skillQuickSlot?.SelectedSkill != null) // Must have skill selected
            {
                if (Hero != null && World is WalkableWorldControl walkableWorld)
                {
                    // Check if player is in SafeZone
                    var terrainFlags = walkableWorld.Terrain.RequestTerrainFlag((int)Hero.Location.X, (int)Hero.Location.Y);
                    if (terrainFlags.HasFlag(TWFlags.SafeZone))
                    {
                        _logger?.LogDebug("Cannot use skill in SafeZone");
                        SetMouseInputConsumed();
                    }
                    else
                    {
                        var skill = _skillQuickSlot.SelectedSkill;

                        // Check if skill is area/buff type
                        if (IsAreaSkill(skill.SkillId))
                        {
                            // Area/buff skill - can be used with or without target
                            ushort extraTargetId = 0;
                            if (MouseHoverObject is MonsterObject skillTargetMonster &&
                                !skillTargetMonster.IsDead &&
                                skillTargetMonster.World == World)
                            {
                                extraTargetId = skillTargetMonster.NetworkId;
                            }
                            UseAreaSkill(skill, extraTargetId);
                        }
                        else
                        {
                            // Targeted skill - requires target
                            if (MouseHoverObject is MonsterObject skillTargetMonster &&
                                !skillTargetMonster.IsDead &&
                                skillTargetMonster.World == World)
                            {
                                UseSkillOnTarget(skill, skillTargetMonster);
                            }
                        }

                        SetMouseInputConsumed();
                    }
                }
            }

            if (currentKeyboardState.IsKeyDown(Keys.F5) && _previousKeyboardState.IsKeyUp(Keys.F5)) _chatLog?.ToggleFrame();
            if (currentKeyboardState.IsKeyDown(Keys.F4) && _previousKeyboardState.IsKeyUp(Keys.F4)) _chatLog?.CycleSize();
            if (currentKeyboardState.IsKeyDown(Keys.F6) && _previousKeyboardState.IsKeyUp(Keys.F6)) _chatLog?.CycleBackgroundAlpha();
            if (currentKeyboardState.IsKeyDown(Keys.F2) && _previousKeyboardState.IsKeyUp(Keys.F2))
            {
                if (_chatLog != null)
                {
                    var nextType = _chatLog.CurrentViewType + 1;
                    if (!System.Enum.IsDefined(typeof(Models.MessageType), nextType) || nextType == Models.MessageType.Unknown) nextType = Models.MessageType.All;
                    if (nextType == Models.MessageType.Info || nextType == Models.MessageType.Error) nextType++;
                    if (!System.Enum.IsDefined(typeof(Models.MessageType), nextType) || nextType == Models.MessageType.Unknown) nextType = Models.MessageType.All;
                    _chatLog.ChangeViewType(nextType);
                    Console.WriteLine($"[ChatLog] Changed view to: {nextType}");
                }
            }
            if (_chatLog != null && _chatLog.IsFrameVisible)
            {
                int scrollDelta = 0;
                if (currentKeyboardState.IsKeyDown(Keys.PageUp) && _previousKeyboardState.IsKeyUp(Keys.PageUp)) scrollDelta = _chatLog.NumberOfShowingLines;
                if (currentKeyboardState.IsKeyDown(Keys.PageDown) && _previousKeyboardState.IsKeyUp(Keys.PageDown)) scrollDelta = -_chatLog.NumberOfShowingLines;
                if (scrollDelta != 0) _chatLog.ScrollLines(scrollDelta);
            }
            _previousKeyboardState = currentKeyboardState;

            // Update ping every 5 seconds to reduce network overhead
            _pingTimer += gameTime.ElapsedGameTime.TotalSeconds;
            if (_pingTimer >= 5.0)
            {
                _pingTimer = 0;
                _ = UpdatePingAsync();
            }
        }

        // ─────────────────────────── Draw Loop ───────────────────────────
        public override void Draw(GameTime gameTime)
        {
            if (_isChangingWorld || World == null || World.Status != GameControlStatus.Ready)
            {
                _loadingScreen?.Draw(gameTime);
                return;
            }

            using (new SpriteBatchScope(
                       GraphicsManager.Instance.Sprite,
                       SpriteSortMode.Deferred,
                       BlendState.AlphaBlend,
                       SamplerState.PointClamp,
                       DepthStencilState.None,
                       transform: UiScaler.SpriteTransform))
            {
                foreach (var ctrl in Controls.ToArray())
                {
                    if (ctrl == null || ctrl == World || !ctrl.Visible)
                    {
                        continue;
                    }

                    ctrl.Draw(gameTime);
                }

            }

            base.Draw(gameTime);

            // Final top-most pass: draw dragged item previews above all UI windows
            using (new SpriteBatchScope(
                       GraphicsManager.Instance.Sprite,
                       SpriteSortMode.Deferred,
                       BlendState.AlphaBlend,
                       SamplerState.PointClamp,
                       DepthStencilState.None,
                       transform: UiScaler.SpriteTransform))
            {
                var sprite = GraphicsManager.Instance.Sprite;
                _inventoryControl?._pickedItemRenderer?.Draw(sprite, gameTime);
                Client.Main.Controls.UI.Game.VaultControl.Instance?.DrawPickedPreview(sprite, gameTime);
            }
            _characterInfoWindow?.BringToFront();
        }

        private void PreloadSounds()
        {
            SoundController.Instance.PreloadSound("Sound/pDropItem.wav");
            SoundController.Instance.PreloadSound("Sound/pDropMoney.wav");
            SoundController.Instance.PreloadSound("Sound/mGem.wav");
            SoundController.Instance.PreloadSound("Sound/pGetItem.wav");
        }

        /// <summary>
        /// Preloads textures for UI controls to avoid stalls when opening them later.
        /// </summary>
        private async Task PreloadUITextures()
        {
            if (_inventoryControl != null)
            {
                await _inventoryControl.Initialize();
                await _inventoryControl.PreloadAssetsAsync();
                _inventoryControl.Preload();
            }

            if (_characterInfoWindow != null)
            {
                await _characterInfoWindow.Initialize();
                await _characterInfoWindow.PreloadAssetsAsync();
            }
        }

        private void OnChatMessageSendRequested(object sender, ChatMessageEventArgs e)
        {
            if (_isChangingWorld || MuGame.Network == null || !MuGame.Network.IsConnected)
            {
                _chatLog.AddMessage("System", "Cannot send message while disconnected or changing maps.", MessageType.Error);
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

        private async Task UpdatePingAsync()
        {
            if (MuGame.Network == null)
                return;

            int? ping = await MuGame.Network.PingServerAsync();
            MuGame.ScheduleOnMainThread(() =>
            {
                _pingLabel.Text = ping.HasValue ? $"Ping: {ping.Value} ms" : "Ping: --";
            });
        }

        /// <summary>
        /// Pre-loads common UI textures in background to prevent freezes when opening windows.
        /// This runs async with low priority to avoid impacting gameplay FPS.
        /// </summary>
        private async Task PreloadCommonUIAssetsAsync()
        {
            try
            {
                _logger?.LogInformation("Starting UI asset pre-loading...");

                var texturePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var control in EnumerateUiControls())
                {
                    if (control is not IUiTexturePreloadable preloadable)
                    {
                        continue;
                    }

                    var paths = preloadable.GetPreloadTexturePaths();
                    if (paths == null)
                    {
                        continue;
                    }

                    foreach (var path in paths)
                    {
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            texturePaths.Add(path);
                        }
                    }
                }

                if (texturePaths.Count == 0)
                {
                    _logger?.LogInformation("No UI textures registered for pre-loading.");
                    return;
                }

                foreach (var assetPath in texturePaths)
                {
                    var path = assetPath;
                    MuGame.TaskScheduler.QueueTask(async () =>
                    {
                        try
                        {
                            await TextureLoader.Instance.Prepare(path);
                            _ = TextureLoader.Instance.GetTexture2D(path);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogTrace(ex, "Failed to pre-load UI asset: {Asset}", path);
                        }
                    }, Controllers.TaskScheduler.Priority.Low);

                    await Task.Delay(10);
                }

                _logger?.LogInformation("UI asset pre-loading completed. {Count} assets queued for background loading.", texturePaths.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error during UI asset pre-loading (non-critical).");
            }
        }

        private IEnumerable<GameControl> EnumerateUiControls()
        {
            var rootControls = Controls?.ToArray();
            if (rootControls == null || rootControls.Length == 0)
            {
                yield break;
            }

            var stack = new Stack<GameControl>(rootControls);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                yield return current;

                var children = current.Controls?.ToArray();
                if (children == null || children.Length == 0)
                {
                    continue;
                }

                for (int i = 0; i < children.Length; i++)
                {
                    stack.Push(children[i]);
                }
            }
        }

        /// <summary>
        /// Checks if mouse is currently over any UI element (not game world).
        /// </summary>
        private bool IsMouseOverUi()
        {
            // MouseHoverControl is set by BaseScene - if it's not the World, we're over UI
            return MouseHoverControl != null && MouseHoverControl != World;
        }

        /// <summary>
        /// Checks if a skill is area/buff type (doesn't require target).
        /// </summary>
        private bool IsAreaSkill(ushort skillId)
        {
            return Core.Utilities.SkillDatabase.IsAreaSkill(skillId);
        }

        /// <summary>
        /// Uses the selected targeted skill on a monster.
        /// </summary>
        private void UseSkillOnTarget(Core.Client.SkillEntryState skill, MonsterObject target)
        {
            if (skill == null || target == null || Hero == null)
                return;

            _logger?.LogInformation("Using targeted skill {SkillId} (Level {Level}) on target {TargetId}",
                skill.SkillId, skill.SkillLevel, target.NetworkId);

            // Send targeted skill usage packet to server
            // Animation will be played when server confirms with SkillAnimation packet
            _ = MuGame.Network.GetCharacterService().SendSkillRequestAsync(
                skill.SkillId,
                target.NetworkId);
        }

        /// <summary>
        /// Uses the selected skill at the player's position with optional target.
        /// </summary>
        private void UseAreaSkill(Core.Client.SkillEntryState skill, ushort extraTargetId = 0)
        {
            if (skill == null || Hero == null)
                return;

            if (extraTargetId != 0)
            {
                _logger?.LogInformation("Using skill {SkillId} (Level {Level}) at position ({X},{Y}) with target {TargetId}",
                    skill.SkillId, skill.SkillLevel, (byte)Hero.Location.X, (byte)Hero.Location.Y, extraTargetId);
            }
            else
            {
                _logger?.LogInformation("Using area skill {SkillId} (Level {Level}) at position ({X},{Y})",
                    skill.SkillId, skill.SkillLevel, (byte)Hero.Location.X, (byte)Hero.Location.Y);
            }

            // Send area skill usage packet to server
            // Animation will be played when server confirms with AreaSkillAnimation packet
            // Use player's current position and direction
            byte targetX = (byte)Hero.Location.X;
            byte targetY = (byte)Hero.Location.Y;
            byte rotation = (byte)((Hero.Angle.Y / (2 * Math.PI)) * 255); // Convert radians to 0-255 range (use Y component for rotation)

            _ = MuGame.Network.GetCharacterService().SendAreaSkillRequestAsync(
                skill.SkillId,
                targetX,
                targetY,
                rotation,
                extraTargetId);
        }

        public override void Dispose()
        {
            if (_hero != null)
            {
                _hero.PlayerMoved -= OnHeroMoved;
                _hero.PlayerTookDamage -= OnHeroTookDamage;
            }
            base.Dispose();
        }
    }
}
