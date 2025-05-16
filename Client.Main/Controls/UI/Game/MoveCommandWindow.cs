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
        // Stałe dla wyglądu - można je dostosować
        private const int WINDOW_TARGET_WIDTH = 250; // Docelowa szerokość okna
        private const int WINDOW_TARGET_HEIGHT = 550; // Docelowa wysokość okna
        private const int WINDOW_X_OFFSET = 10;    // Odstęp od lewej krawędzi ekranu
        private const int WINDOW_Y_OFFSET_FROM_TOP = 20; // Odstęp od góry ekranu (lub dynamicznie od chatu)

        private const int TITLE_AREA_HEIGHT = 30;
        private const int CLOSE_BUTTON_AREA_HEIGHT = 30;
        private const int ITEM_HEIGHT = 16;
        private const int SCROLLBAR_WIDTH = 17; // Szerokość tekstury paska przewijania

        private const int PADDING_GENERAL = 5; // Ogólny padding wewnątrz okna
        private const int PADDING_TITLE_TOP = 6;
        private const int PADDING_LIST_TOP_FROM_TITLE = 2; // Mały odstęp między tytułem a listą
        private const int PADDING_LIST_BOTTOM_TO_CLOSE = 5; // Odstęp od listy do przycisku Close
        private const int PADDING_SCROLLBAR_RIGHT = 5; // Odstęp scrollbara od prawej krawędzi
        private const int SCROLLBAR_VISUAL_WIDTH = 7; // Wizualna szerokość tła scrollbara
        private const int SCROLLBAR_THUMB_WIDTH = 15; // Szerokość suwaka

        // Kolumny - wartości X względem lewej krawędzi obszaru listy
        private const int COL_MAP_NAME_X = 0;
        private const int COL_LEVEL_X = 105; // Zwiększony odstęp
        private const int COL_ZEN_X = 145;   // Zwiększony odstęp


        private TextureControl _backgroundTextureControl;
        private LabelControl _titleLabel;
        private List<LabelControl> _mapNameLabels;     // Zamiast ButtonControl dla wyglądu listy
        private List<LabelControl> _mapLevelLabels;
        private List<LabelControl> _mapZenLabels;
        private List<Rectangle> _mapClickAreas;      // Obszary klikalne dla każdej mapy
        private int _hoveredMapIndex = -1;           // Indeks mapy pod kursorem

        private ScrollBarControl _scrollBar;
        private ButtonControl _closeButton; // Zmienimy go na TextureButtonControl później, jeśli masz taką klasę

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
            X = WINDOW_X_OFFSET; // Pozostaje po lewej
            Y = WINDOW_Y_OFFSET_FROM_TOP;
            // Y = (MuGame.Instance.Height - ControlSize.Y) / 2; // Wyśrodkowanie w pionie
            BackgroundColor = new Color(15, 15, 25, 220); // Ciemniejsze, bardziej stonowane
        }

        public override async Task Load()
        {
            // Tło nie jest już teksturą, używamy BackgroundColor z UIControl
            // _backgroundTextureControl = new TextureControl ... Controls.Add(_backgroundTextureControl);

            _titleLabel = new LabelControl
            {
                Text = "Warp Command Window",
                Align = ControlAlign.Top | ControlAlign.HorizontalCenter,
                X = 0,
                Y = PADDING_TITLE_TOP,
                FontSize = 13f, // Dostosuj
                TextColor = new Color(255, 230, 190), // Bardziej złoty/pomarańczowy
                ControlSize = new Point(ControlSize.X - (PADDING_GENERAL * 2), TITLE_AREA_HEIGHT - PADDING_TITLE_TOP)
            };
            Controls.Add(_titleLabel);

            int listAreaY = PADDING_TITLE_TOP + (TITLE_AREA_HEIGHT - PADDING_TITLE_TOP) + PADDING_LIST_TOP_FROM_TITLE;
            int listAvailableHeight = ControlSize.Y - listAreaY - CLOSE_BUTTON_AREA_HEIGHT - PADDING_BOTTOM;
            _maxVisibleItems = Math.Max(1, listAvailableHeight / ITEM_HEIGHT);

            _scrollBar = new ScrollBarControl
            {
                // Pozycja X scrollbara: od prawej krawędzi okna, minus jego szerokość i padding
                X = ControlSize.X - SCROLLBAR_THUMB_WIDTH - PADDING_SCROLLBAR_RIGHT, // Użyj SCROLLBAR_THUMB_WIDTH dla X
                Y = listAreaY,
                Height = listAvailableHeight,
                Width = SCROLLBAR_THUMB_WIDTH, // Ustaw szerokość na szerokość suwaka
                Visible = false,
                ThumbHeightAdjustmentFactor = 0.2f
            };
            _scrollBar.ValueChanged += (s, e) => OnScrollChanged();
            Controls.Add(_scrollBar);

            _closeButton = new ButtonControl // Bardziej stylowy przycisk
            {
                Text = "Close",
                X = (ControlSize.X - 60) / 2, // Mniejszy przycisk
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
            RefreshMapListUI(); // Zmieniono nazwę z RefreshMapButtons
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
            // Sortowanie jak w oryginalnym kliencie (po indeksie, który jest tam używany do kolejności)
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

            // TODO: Dalsze warunki (Icarus, Gens)
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
            // Szerokość dostępna dla treści mapy (nazwa + poziom + zen)
            int mapContentWidth = ControlSize.X - PADDING_LEFT - PADDING_RIGHT - (_scrollBar.Visible ? SCROLLBAR_THUMB_WIDTH + PADDING_SCROLLBAR_RIGHT : 0);


            for (int i = 0; i < _maxVisibleItems; i++)
            {
                int currentMapDataIndex = _currentScrollOffset + i; // Indeks w _currentlyDisplayableMaps
                if (currentMapDataIndex >= _currentlyDisplayableMaps.Count) break;

                var mapInfo = _currentlyDisplayableMaps[currentMapDataIndex];

                int currentY = listAreaY + (i * ITEM_HEIGHT);
                // Obszar klikalny dla całego wiersza
                var clickArea = new Rectangle(
                    DisplayPosition.X + PADDING_LEFT, // Ważne: DisplayPosition.X okna
                    DisplayPosition.Y + currentY,     // Ważne: DisplayPosition.Y okna
                    mapContentWidth,
                    ITEM_HEIGHT - 1);
                _mapClickAreas.Add(clickArea);

                Color nameColor = mapInfo.CanMove ? (mapInfo.IsSelected ? Color.Gold : Color.WhiteSmoke) : new Color(100, 100, 100);
                Color levelColor = (playerState != null && playerState.Level >= mapInfo.RequiredLevel) ? (mapInfo.CanMove ? new Color(180, 220, 255) : new Color(90, 90, 90)) : Color.Red;
                Color zenColor = (playerState != null && playerState.InventoryZen >= mapInfo.RequiredZen) ? (mapInfo.CanMove ? new Color(180, 255, 180) : new Color(90, 90, 90)) : Color.Red;
                if (mapInfo.IsStrifeMap && !mapInfo.CanMove) nameColor = new Color(120, 60, 60);


                // Nazwa Mapy
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

                // Wymagany Zen
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
                    SetVisible(false);
                }
                else
                {
                    _logger.LogInformation($"Cannot warp to {mapInfo.DisplayName}, requirements not met.");
                    // Można dodać dźwięk błędu
                }
            }
        }


        private void UpdateScrollbarState()
        {
            bool needsScrollbar = _currentlyDisplayableMaps.Count > _maxVisibleItems;
            // _logger.LogDebug($"UpdateScrollbarState: NeedsScrollbar: {needsScrollbar}, DisplayableMaps: {_currentlyDisplayableMaps.Count}, MaxVisibleItems: {_maxVisibleItems}");

            if (_scrollBar.Visible != needsScrollbar)
            {
                _scrollBar.Visible = needsScrollbar;
                RefreshMapListUI(); // Odśwież listę, ponieważ szerokość kontentu mogła się zmienić
            }

            if (needsScrollbar)
            {
                _scrollBar.Min = 0;
                _scrollBar.Max = Math.Max(0, _currentlyDisplayableMaps.Count - _maxVisibleItems);
                _scrollBar.Value = _currentScrollOffset;
                _scrollBar.SetListMetrics(_maxVisibleItems, _currentlyDisplayableMaps.Count); // Przekaż aktualne metryki
                                                                                              // _logger.LogDebug($"Scrollbar updated: Min: {_scrollBar.Min}, Max: {_scrollBar.Max}, Value: {_scrollBar.Value}");
            }
            // else // Już obsługiwane w UpdateThumbPosition w ScrollBarControl
            // {
            //     // _logger.LogDebug("Scrollbar not needed or Max <= Min.");
            // }
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
            SetVisible(!Visible);
            if (Visible)
            {
                _logger.LogDebug("MoveCommandWindow toggled ON. Refreshing map data.");
                LoadMapData();
                _currentScrollOffset = 0;
                if (_scrollBar.Visible) _scrollBar.Value = 0;
                // RefreshMapListUI(); // Już wywołane w LoadMapData
                // UpdateScrollbarState(); // Już wywołane w LoadMapData
                Scene.FocusControl = this;
            }
            else
            {
                if (Scene?.FocusControl == this) Scene.FocusControl = null;
            }
        }

        private CharacterState playerState => _networkManager.GetCharacterState(); // Skrót

        public int PADDING_BOTTOM = 30;
        public int PADDING_LEFT = 30;
        public int PADDING_RIGHT = 30;
        public int PADDING_TOP_CONTENT = 30;

        public override void Update(GameTime gameTime)
        {
            if (!Visible) return;
            base.Update(gameTime); // To powinno zaktualizować IsMouseOver dla MoveCommandWindow

            var playerState = _networkManager.GetCharacterState();
            bool listNeedsVisualRefresh = false; // Tylko do odświeżenia kolorów/stanu IsSelected
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
                // Ważne: _mapClickAreas przechowuje już absolutne koordynaty na ekranie
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

            // Aktualizacja IsSelected i odświeżenie wizualne etykiet
            if (oldHoveredMapIndex != _hoveredMapIndex || listNeedsVisualRefresh)
            {
                // Odznacz poprzednio zaznaczony
                if (oldHoveredMapIndex != -1 && oldHoveredMapIndex >= _currentScrollOffset && (oldHoveredMapIndex - _currentScrollOffset) < _mapNameLabels.Count)
                {
                    int visualIdx = oldHoveredMapIndex - _currentScrollOffset;
                    if (visualIdx >= 0 && visualIdx < _currentlyDisplayableMaps.Count)
                    {
                        var mapInfo = _currentlyDisplayableMaps[oldHoveredMapIndex];
                        mapInfo.IsSelected = false;
                        _currentlyDisplayableMaps[oldHoveredMapIndex] = mapInfo;
                        // Aktualizuj tylko tę jedną etykietę
                        _mapNameLabels[visualIdx].TextColor = mapInfo.CanMove ? Color.WhiteSmoke : (mapInfo.IsStrifeMap ? new Color(120, 60, 60) : new Color(100, 100, 100));

                    }
                }
                // Zaznacz nowy
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
                // Jeśli tylko zmienił się stan CanMove, a nie hover, odśwież wszystkie widoczne
                else if (listNeedsVisualRefresh)
                {
                    RefreshMapListUI(); // To jest mniej wydajne, ale prostsze jeśli tylko CanMove się zmienia
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
            // Rysowanie tła i tytułu jest teraz obsługiwane przez UIControl.Draw -> GameControl.Draw
            // i następnie przez poszczególne kontrolki w kolekcji Controls.
            // Upewnij się, że BackgroundColor jest ustawione dla MoveCommandWindow.
            base.Draw(gameTime);
        }

        public void ProcessKeyInput(Keys key, bool isRepeated)
        {
            if (!Visible) return;

            if (key == Keys.Escape)
            {
                SetVisible(false);
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
// --- END OF FILE MoveCommandWindow.cs ---