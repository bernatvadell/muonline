using Client.Main.Controls.UI.Common;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Client.Main.Core.Client;
using Client.Main.Networking;
using MUnique.OpenMU.Network.Packets;
using Client.Main.Scenes;

namespace Client.Main.Controls.UI.Game
{
    public class MoveCommandWindow : UIControl
    {
        private const int WINDOW_TARGET_WIDTH = 250;
        private const int WINDOW_TARGET_HEIGHT = 550;
        private const int WINDOW_X_OFFSET = 10;
        private const int WINDOW_Y_OFFSET_FROM_TOP = 20;

        private const int TITLE_AREA_HEIGHT = 30;
        private const int CLOSE_BUTTON_AREA_HEIGHT = 30;
        private const int ITEM_HEIGHT = 16;
        private const int SCROLLBAR_WIDTH = 17;

        private const int PADDING_GENERAL = 5;
        private const int PADDING_TITLE_TOP = 6;
        private const int PADDING_LIST_TOP_FROM_TITLE = 2;
        private const int PADDING_LIST_BOTTOM_TO_CLOSE = 5;
        private const int PADDING_SCROLLBAR_RIGHT = 5;
        private const int SCROLLBAR_VISUAL_WIDTH = 7;
        private const int SCROLLBAR_THUMB_WIDTH = 15;

        private const int COL_MAP_NAME_X = 0;
        private const int COL_LEVEL_X = 105;
        private const int COL_ZEN_X = 145; 


        private LabelControl _titleLabel;
        private List<LabelControl> _mapNameLabels;
        private List<LabelControl> _mapLevelLabels;
        private List<LabelControl> _mapZenLabels;
        private List<Rectangle> _mapClickAreas;
        private int _hoveredMapIndex = -1;

        private ScrollBarControl _scrollBar;
        private ButtonControl _closeButton;

        private List<MoveCommandInfo> _availableMapsFullList;
        private List<MoveCommandInfo> _currentlyDisplayableMaps;

        private int _currentScrollOffset = 0;
        private int _maxVisibleItems;

        private readonly ILogger<MoveCommandWindow> _logger;
        private readonly NetworkManager _networkManager;

        public event Action<int> MapWarpRequested;

        public MoveCommandWindow(ILoggerFactory loggerFactory, NetworkManager networkManager)
        {
            _logger = loggerFactory.CreateLogger<MoveCommandWindow>();
            _networkManager = networkManager ?? throw new ArgumentNullException(nameof(networkManager));

            _mapNameLabels = new List<LabelControl>();
            _mapLevelLabels = new List<LabelControl>();
            _mapZenLabels = new List<LabelControl>();
            _mapClickAreas = new List<Rectangle>();

            _availableMapsFullList = new List<MoveCommandInfo>();
            _currentlyDisplayableMaps = new List<MoveCommandInfo>();

            Visible = false;
            Interactive = true;

            ControlSize = new Point(WINDOW_TARGET_WIDTH, WINDOW_TARGET_HEIGHT);
            ViewSize = ControlSize;
            X = WINDOW_X_OFFSET;
            Y = WINDOW_Y_OFFSET_FROM_TOP;
            BackgroundColor = new Color(15, 15, 25, 220);
        }

        public override async Task Load()
        {

            _titleLabel = new LabelControl
            {
                Text = "Warp Command Window",
                Align = ControlAlign.Top | ControlAlign.HorizontalCenter,
                X = 0,
                Y = PADDING_TITLE_TOP,
                FontSize = 13f,
                TextColor = new Color(255, 230, 190),
                ControlSize = new Point(ControlSize.X - (PADDING_GENERAL * 2), TITLE_AREA_HEIGHT - PADDING_TITLE_TOP)
            };
            Controls.Add(_titleLabel);

            int listAreaY = PADDING_TITLE_TOP + (TITLE_AREA_HEIGHT - PADDING_TITLE_TOP) + PADDING_LIST_TOP_FROM_TITLE;
            int listAvailableHeight = ControlSize.Y - listAreaY - CLOSE_BUTTON_AREA_HEIGHT - PADDING_BOTTOM;
            _maxVisibleItems = Math.Max(1, listAvailableHeight / ITEM_HEIGHT);

            _scrollBar = new ScrollBarControl
            {
                X = ControlSize.X - SCROLLBAR_THUMB_WIDTH - PADDING_SCROLLBAR_RIGHT, // Użyj SCROLLBAR_THUMB_WIDTH dla X
                Y = listAreaY,
                Height = listAvailableHeight,
                Width = SCROLLBAR_THUMB_WIDTH,
                Visible = false,
                ThumbHeightAdjustmentFactor = 0.2f
            };
            _scrollBar.ValueChanged += (s, e) => OnScrollChanged();
            Controls.Add(_scrollBar);

            _closeButton = new ButtonControl
            {
                Text = "Close",
                X = (ControlSize.X - 60) / 2,
                Y = ControlSize.Y - PADDING_BOTTOM - (ITEM_HEIGHT + 2), // Na samym dole
                ViewSize = new Point(60, ITEM_HEIGHT + 2),
                ControlSize = new Point(60, ITEM_HEIGHT + 2),
                FontSize = 9.5f,
                TextColor = Color.WhiteSmoke,
                BackgroundColor = new Color(0.1f, 0.1f, 0.2f, 0.8f), // Ciemne tło
                HoverBackgroundColor = new Color(0.2f, 0.2f, 0.35f, 0.9f),
                PressedBackgroundColor = new Color(0.05f, 0.05f, 0.15f, 0.9f),
                BorderColor = new Color(100, 100, 120),
                BorderThickness = 1,
            };
            _closeButton.Click += (s, e) => ToggleVisibility();
            Controls.Add(_closeButton);

            await base.Load();
            LoadMapData();
        }

        private void LoadMapData()
        {
            _logger.LogDebug("Loading map data...");
            _availableMapsFullList = MoveCommandDataManager.Instance.GetMoveCommandDataList();
            _logger.LogDebug($"Loaded {_availableMapsFullList.Count} maps from data manager.");
            FilterAndSortMaps();
            RefreshMapListUI();
            UpdateScrollbarState();
        }

        private void FilterAndSortMaps()
        {
            var playerState = _networkManager.GetCharacterState();
            _currentlyDisplayableMaps = _availableMapsFullList.Select(map =>
            {
                map.CanMove = CanPlayerMoveTo(map, playerState);
                return map;
            }).ToList();
            _currentlyDisplayableMaps = _currentlyDisplayableMaps.OrderBy(m => m.Index).ToList();
        }

        private bool CanPlayerMoveTo(MoveCommandInfo mapInfo, CharacterState playerState)
        {
            if (playerState == null) return false;
            int playerLevel = playerState.Level;
            uint playerZen = playerState.InventoryZen;
            int requiredLevel = mapInfo.RequiredLevel;
            var charClass = playerState.Class;

            if (charClass == CharacterClassNumber.DarkKnight || charClass == CharacterClassNumber.BladeKnight || charClass == CharacterClassNumber.BladeMaster ||
                charClass == CharacterClassNumber.DarkLord || charClass == CharacterClassNumber.LordEmperor ||
                charClass == CharacterClassNumber.RageFighter || charClass == CharacterClassNumber.FistMaster)
            {
                if (mapInfo.RequiredLevel != 400)
                {
                    requiredLevel = (int)(mapInfo.RequiredLevel * 2.0f / 3.0f);
                }
            }
            if (playerLevel < requiredLevel) return false;
            if (playerZen < mapInfo.RequiredZen) return false;
            if (playerState.HeroState == CharacterHeroState.PlayerKiller1stStage || playerState.HeroState == CharacterHeroState.PlayerKiller2ndStage)
                return false;

            return true;
        }

        private void ClearMapListUI()
        {
            foreach (var lbl in _mapNameLabels) { Controls.Remove(lbl); lbl.Dispose(); }
            foreach (var lbl in _mapLevelLabels) { Controls.Remove(lbl); lbl.Dispose(); }
            foreach (var lbl in _mapZenLabels) { Controls.Remove(lbl); lbl.Dispose(); }
            _mapNameLabels.Clear();
            _mapLevelLabels.Clear();
            _mapZenLabels.Clear();
            _mapClickAreas.Clear();
        }

        private void RefreshMapListUI()
        {
            // _logger.LogDebug($"Refreshing map list UI. ScrollOffset: {_currentScrollOffset}, MaxVisible: {_maxVisibleItems}, DisplayableMaps: {_currentlyDisplayableMaps.Count}");
            ClearMapListUI();

            int listAreaY = PADDING_TOP_CONTENT;
            int mapContentWidth = ControlSize.X - PADDING_LEFT - PADDING_RIGHT - (_scrollBar.Visible ? SCROLLBAR_THUMB_WIDTH + PADDING_SCROLLBAR_RIGHT : 0);


            for (int i = 0; i < _maxVisibleItems; i++)
            {
                int currentMapDataIndex = _currentScrollOffset + i;
                if (currentMapDataIndex >= _currentlyDisplayableMaps.Count) break;

                var mapInfo = _currentlyDisplayableMaps[currentMapDataIndex];

                int currentY = listAreaY + (i * ITEM_HEIGHT);
                var clickArea = new Rectangle(
                    DisplayPosition.X + PADDING_LEFT,
                    DisplayPosition.Y + currentY,
                    mapContentWidth,
                    ITEM_HEIGHT - 1);
                _mapClickAreas.Add(clickArea);

                Color nameColor = mapInfo.CanMove ? (mapInfo.IsSelected ? Color.Gold : Color.WhiteSmoke) : new Color(100, 100, 100);
                Color levelColor = (playerState != null && playerState.Level >= mapInfo.RequiredLevel) ? (mapInfo.CanMove ? new Color(180, 220, 255) : new Color(90, 90, 90)) : Color.Red;
                Color zenColor = (playerState != null && playerState.InventoryZen >= mapInfo.RequiredZen) ? (mapInfo.CanMove ? new Color(180, 255, 180) : new Color(90, 90, 90)) : Color.Red;
                if (mapInfo.IsStrifeMap && !mapInfo.CanMove) nameColor = new Color(120, 60, 60);


                var nameLabel = new LabelControl
                {
                    X = PADDING_LEFT + COL_MAP_NAME_X,
                    Y = currentY,
                    Text = mapInfo.DisplayName,
                    FontSize = 9.5f,
                    TextColor = nameColor,
                    ControlSize = new Point(COL_LEVEL_X - COL_MAP_NAME_X - 5, ITEM_HEIGHT - 1)
                };
                Controls.Add(nameLabel); _mapNameLabels.Add(nameLabel);

                // Wymagany Poziom
                var levelLabel = new LabelControl
                {
                    X = PADDING_LEFT + COL_LEVEL_X,
                    Y = currentY,
                    Text = mapInfo.RequiredLevel.ToString(),
                    FontSize = 9.5f,
                    TextColor = levelColor,
                    ControlSize = new Point(COL_ZEN_X - COL_LEVEL_X - 5, ITEM_HEIGHT - 1)
                };
                Controls.Add(levelLabel); _mapLevelLabels.Add(levelLabel);

                var zenLabel = new LabelControl
                {
                    X = PADDING_LEFT + COL_ZEN_X,
                    Y = currentY,
                    Text = mapInfo.RequiredZen.ToString(),
                    FontSize = 9.5f,
                    TextColor = zenColor,
                    ControlSize = new Point(mapContentWidth - COL_ZEN_X, ITEM_HEIGHT - 1)
                };
                Controls.Add(zenLabel); _mapZenLabels.Add(zenLabel);
            }
        }

        private void OnMapClicked(int visualIndex)
        {
            int actualMapIndexInList = _currentScrollOffset + visualIndex;
            if (actualMapIndexInList < _currentlyDisplayableMaps.Count)
            {
                var mapInfo = _currentlyDisplayableMaps[actualMapIndexInList];
                if (mapInfo.CanMove)
                {
                    _logger.LogInformation($"Warp to map index: {mapInfo.Index} ({mapInfo.DisplayName}) requested by player.");
                    MapWarpRequested?.Invoke(mapInfo.Index);
                    Visible = false;
                }
                else
                {
                    _logger.LogInformation($"Cannot warp to {mapInfo.DisplayName}, requirements not met.");
                    // error sound?
                }
            }
        }


        private void UpdateScrollbarState()
        {
            bool needsScrollbar = _currentlyDisplayableMaps.Count > _maxVisibleItems;

            if (_scrollBar.Visible != needsScrollbar)
            {
                _scrollBar.Visible = needsScrollbar;
                RefreshMapListUI();
            }

            if (needsScrollbar)
            {
                _scrollBar.Min = 0;
                _scrollBar.Max = Math.Max(0, _currentlyDisplayableMaps.Count - _maxVisibleItems);
                _scrollBar.Value = _currentScrollOffset;
                _scrollBar.SetListMetrics(_maxVisibleItems, _currentlyDisplayableMaps.Count); // Przekaż aktualne metryki
                                                                                              // _logger.LogDebug($"Scrollbar updated: Min: {_scrollBar.Min}, Max: {_scrollBar.Max}, Value: {_scrollBar.Value}");
            }
        }

        private void OnScrollChanged()
        {
            if (_currentScrollOffset != _scrollBar.Value)
            {
                _currentScrollOffset = _scrollBar.Value;
                RefreshMapListUI();
            }
        }

        public void ToggleVisibility()
        {
            Visible = !Visible;
            if (Visible)
            {
                _logger.LogDebug("MoveCommandWindow toggled ON. Refreshing map data.");
                LoadMapData();
                _currentScrollOffset = 0;
                if (_scrollBar.Visible) _scrollBar.Value = 0;
                Scene.FocusControl = this;
            }
            else
            {
                if (Scene?.FocusControl == this) Scene.FocusControl = null;
            }
        }

        private CharacterState playerState => _networkManager.GetCharacterState();

        public int PADDING_BOTTOM = 30;
        public int PADDING_LEFT = 30;
        public int PADDING_RIGHT = 30;
        public int PADDING_TOP_CONTENT = 30;

        public override void Update(GameTime gameTime)
        {
            if (!Visible) return;
            base.Update(gameTime);

            var playerState = _networkManager.GetCharacterState();
            bool listNeedsVisualRefresh = false;
            for (int i = 0; i < _currentlyDisplayableMaps.Count; ++i)
            {
                var map = _currentlyDisplayableMaps[i];
                bool canMoveNow = CanPlayerMoveTo(map, playerState);
                if (map.CanMove != canMoveNow)
                {
                    map.CanMove = canMoveNow;
                    _currentlyDisplayableMaps[i] = map;
                    listNeedsVisualRefresh = true;
                }
            }

            var mouse = MuGame.Instance.Mouse;
            int oldHoveredMapIndex = _hoveredMapIndex;
            _hoveredMapIndex = -1;

            for (int i = 0; i < _mapClickAreas.Count; i++)
            {
                if (_mapClickAreas[i].Contains(mouse.Position))
                {
                    _hoveredMapIndex = _currentScrollOffset + i;
                    if (mouse.LeftButton == ButtonState.Pressed && MuGame.Instance.PrevMouseState.LeftButton == ButtonState.Released)
                    {
                        OnMapClicked(i);
                        if (Scene is BaseScene baseScene) baseScene.SetMouseInputConsumed(); // signal consumption
                        return;
                    }
                    break;
                }
            }

            if (oldHoveredMapIndex != _hoveredMapIndex || listNeedsVisualRefresh)
            {
                if (oldHoveredMapIndex != -1 && oldHoveredMapIndex >= _currentScrollOffset && (oldHoveredMapIndex - _currentScrollOffset) < _mapNameLabels.Count)
                {
                    int visualIdx = oldHoveredMapIndex - _currentScrollOffset;
                    if (visualIdx >= 0 && visualIdx < _currentlyDisplayableMaps.Count)
                    {
                        var mapInfo = _currentlyDisplayableMaps[oldHoveredMapIndex];
                        mapInfo.IsSelected = false;
                        _currentlyDisplayableMaps[oldHoveredMapIndex] = mapInfo;
                        _mapNameLabels[visualIdx].TextColor = mapInfo.CanMove ? Color.WhiteSmoke : (mapInfo.IsStrifeMap ? new Color(120, 60, 60) : new Color(100, 100, 100));

                    }
                }
                if (_hoveredMapIndex != -1 && _hoveredMapIndex >= _currentScrollOffset && (_hoveredMapIndex - _currentScrollOffset) < _mapNameLabels.Count)
                {
                    int visualIdx = _hoveredMapIndex - _currentScrollOffset;
                    if (visualIdx >= 0 && visualIdx < _currentlyDisplayableMaps.Count)
                    {
                        var mapInfo = _currentlyDisplayableMaps[_hoveredMapIndex];
                        mapInfo.IsSelected = true;
                        _currentlyDisplayableMaps[_hoveredMapIndex] = mapInfo;
                        _mapNameLabels[visualIdx].TextColor = mapInfo.CanMove ? Color.Gold : _mapNameLabels[visualIdx].TextColor; // Podświetl na złoto jeśli można
                    }
                }
                else if (listNeedsVisualRefresh)
                {
                    RefreshMapListUI();
                }
            }
        }

        public override bool ProcessMouseScroll(int scrollDelta)
        {
            if (!Visible || !IsMouseOver) // only process if visible and mouse is over this control
                return false;

            if (_scrollBar != null && _scrollBar.Visible && _scrollBar.IsMouseOver)
            {
                // if the scrollbar handles it, then the MoveCommandWindow also considers it handled.
                if (_scrollBar.ProcessMouseScroll(scrollDelta))
                {
                    return true;
                }
            }
            // scrollbar didn't handle (or wasn't applicable), the MoveCommandWindow itself doesn't have other scrollable content here.
            // However, if it had its own direct scrollable list without a scrollbar, logic would go here.
            // For now, if mouse is over the window but not its scrollbar, and scroll happens,
            // we could argue it's still "consuming" it for this window context even if no direct action.
            // But return false if no action taken by the window itself.
            // For simplicity and to prevent pass-through if the window is clearly the focus:
            if (_scrollBar != null && _scrollBar.Visible)  // a scrollbar exists, assume scroll should be for it
            {
                _scrollBar.Value -= scrollDelta; // scrollDelta: positive for up, negative for down
                return true;
            }
            
            return false; // window didn't use the scroll (e.g., no scrollbar visible)
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible) return;
            base.Draw(gameTime);
        }

        public void ProcessKeyInput(Keys key, bool isRepeated)
        {
            if (!Visible) return;

            if (key == Keys.Escape)
            {
                Visible = false;
                if (Scene?.FocusControl == this) Scene.FocusControl = null;
            }
            else if (_scrollBar.Visible)
            {
                if (key == Keys.Up) _scrollBar.Value--;
                else if (key == Keys.Down) _scrollBar.Value++;
                else if (key == Keys.PageUp) _scrollBar.Value -= _maxVisibleItems;
                else if (key == Keys.PageDown) _scrollBar.Value += _maxVisibleItems;
            }
        }
    }
}