using System;
using System.Text;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Client.Main.Helpers; // For SpriteBatchScope
using Client.Main.Networking;
using Client.Main.Core.Utilities;
using Microsoft.Extensions.Logging;
using MUnique.OpenMU.Network.Packets;
using Client.Main.Controls.UI;

namespace Client.Main.Controls.UI.Game.Inventory
{
    public class InventoryControl : DynamicLayoutControl
    {
        protected override string LayoutJsonResource => "Client.Main.Controls.UI.Game.Layouts.InventoryLayout.json";
        protected override string TextureRectJsonResource => "Client.Main.Controls.UI.Game.Layouts.InventoryRect.json";
        protected override string DefaultTexturePath => "Interface/GFx/NpcShop_I3.ozd";
        private static readonly string[] s_inventoryTexturePaths =
        {
            "Interface/newui_item_box.tga",
            "Interface/newui_item_table01(L).tga",
            "Interface/newui_item_table01(R).tga",
            "Interface/newui_item_table02(L).tga",
            "Interface/newui_item_table02(R).tga",
            "Interface/newui_item_table03(Up).tga",
            "Interface/newui_item_table03(Dw).tga",
            "Interface/newui_item_table03(L).tga",
            "Interface/newui_item_table03(R).tga",
            "Interface/newui_msgbox_back.jpg"
        };

        private static InventoryControl _instance;

        public const int INVENTORY_SQUARE_WIDTH = 35;
        public const int INVENTORY_SQUARE_HEIGHT = 35;
        private const int WND_TOP_EDGE = 3;
        private const int WND_LEFT_EDGE = 4;
        private const int WND_BOTTOM_EDGE = 8;
        private const int WND_RIGHT_EDGE = 9;

        // Fixed inventory dimensions (8x8)
        public const int Columns = 8;
        public const int Rows = 8;

        private LabelControl _zenLabel;
        private long _zenAmount = 0; // Player's ZEN amount

        public long ZenAmount
        {
            get => _zenAmount;
            set
            {
                if (_zenAmount != value)
                {
                    _zenAmount = value;
                    UpdateZenLabel();
                }
            }
        }

        private Texture2D _texSquare;
        private Texture2D _slotTexture; // Texture for slot background (same as NpcShop)
        private Texture2D _texTableTopLeft, _texTableTopRight, _texTableBottomLeft, _texTableBottomRight;
        private Texture2D _texTableTopPixel, _texTableBottomPixel, _texTableLeftPixel, _texTableRightPixel;
        private Texture2D _texBackground; // For msgbox_back

        private List<InventoryItem> _items;
        private Dictionary<byte, InventoryItem> _equippedItems; // visualize equipped items by slot index
        private InventoryItem[,] _itemGrid; // For quick slot occupancy checks

        private Point _gridOffset = new Point(50, 80); // Grid offset with 40px margins and centered

        // Grid properties use DynamicLayoutControl coordinate system (no UiScaler scaling)
        private Point GridOffset => _gridOffset;
        private int CellWidth => INVENTORY_SQUARE_WIDTH;
        private int CellHeight => INVENTORY_SQUARE_HEIGHT;

        public PickedItemRenderer _pickedItemRenderer;
        private readonly NetworkManager _networkManager;
        private InventoryItem _hoveredItem = null;
        private Point _hoveredSlot = new Point(-1, -1);
        private int _hoveredEquipSlot = -1;
        private int _pickedFromEquipSlot = -1;
        private Point _pickedItemOriginalGrid = new Point(-1, -1);

        private SpriteFont _font; // Font for tooltips

        private readonly ILogger<InventoryControl> _logger;

        // Window dragging variables
        private bool _isDragging = false;
        private Point _dragOffset;
        private DateTime _lastClickTime = DateTime.MinValue;

        // GameTime for animated previews
        private GameTime _currentGameTime;

        private void InitializeGrid()
        {
            _itemGrid = new InventoryItem[Columns, Rows];
            Interactive = true;
            // DynamicLayoutControl will handle sizing automatically
        }

        public InventoryControl(NetworkManager networkManager = null, ILoggerFactory loggerFactory = null) // Zmieniony konstruktor
        {
            _logger = loggerFactory?.CreateLogger<InventoryControl>();
            _items = new List<InventoryItem>();
            _equippedItems = new Dictionary<byte, InventoryItem>();
            _networkManager = networkManager;

            Visible = false;

            InitializeGrid();
            Align = ControlAlign.VerticalCenter | ControlAlign.Right;
            _pickedItemRenderer = new PickedItemRenderer();
            InitializeZenLabel();
        }

        public static InventoryControl Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new InventoryControl();
                }
                return _instance;
            }
        }

        private void InitializeZenLabel()
        {
            _zenLabel = new LabelControl
            {
                Text = "ZEN: 0",
                TextColor = Color.Gold,
                FontSize = 14f,
                HasShadow = true,
                ShadowColor = Color.Black,
                ShadowOpacity = 0.8f,
                UseManualPosition = false,
                Visible = false
            };

            Controls.Add(_zenLabel);
        }

        private void UpdateZenLabel()
        {
            if (_networkManager == null) return;

            var playerState = _networkManager.GetCharacterState();
            if (_zenLabel != null)
            {
                _zenLabel.Text = $"ZEN: {playerState.InventoryZen}"; // Format with thousand separators
                UpdateZenLabelPosition();
            }
        }

        private void UpdateZenLabelPosition()
        {
            if (_zenLabel != null && _zenLabel.Status == GameControlStatus.Ready)
            {
                // Position ZEN label below equipment area
                const int equipRows = 2;
                int gridBottomY = GridOffset.Y + (Rows * CellHeight);
                int equipAreaBottomY = gridBottomY + 20 + (equipRows * CellHeight);

                _zenLabel.X = GridOffset.X; // Align with left side of grid
                _zenLabel.Y = equipAreaBottomY + 10; // 10 pixels below equipment area
            }
        }

        protected override IEnumerable<string> EnumeratePreloadTextures()
        {
            foreach (var texture in base.EnumeratePreloadTextures())
            {
                yield return texture;
            }

            foreach (var texture in s_inventoryTexturePaths)
            {
                yield return texture;
            }
        }

        public override async Task Load()
        {
            // First load the base DynamicLayoutControl
            await base.Load();

            var tl = TextureLoader.Instance;

            // Load all textures in parallel to avoid blocking main thread
            var textureLoadTasks = new List<Task<Texture2D>>(s_inventoryTexturePaths.Length);
            foreach (var texturePath in s_inventoryTexturePaths)
            {
                textureLoadTasks.Add(tl.PrepareAndGetTexture(texturePath));
            }

            var loadedTextures = await Task.WhenAll(textureLoadTasks);
            _slotTexture = await tl.PrepareAndGetTexture(DefaultTexturePath); // Load slot texture (same as NpcShop)

            _texSquare = loadedTextures[0];
            _texTableTopLeft = loadedTextures[1];
            _texTableTopRight = loadedTextures[2];
            _texTableBottomLeft = loadedTextures[3];
            _texTableBottomRight = loadedTextures[4];
            _texTableTopPixel = loadedTextures[5];
            _texTableBottomPixel = loadedTextures[6];
            _texTableLeftPixel = loadedTextures[7];
            _texTableRightPixel = loadedTextures[8];
            _texBackground = loadedTextures[9];

            _font = GraphicsManager.Instance.Font;

            if (_zenLabel != null)
            {
                await _zenLabel.Load();
                UpdateZenLabelPosition();
            }
        }

        public void Show()
        {
            Console.WriteLine("[DEBUG] InventoryControl.Show() called - reloading layout");

            // Re-add ZEN label after layout reload
            if (_zenLabel != null && !Controls.Contains(_zenLabel))
            {
                Controls.Add(_zenLabel);
            }

            UpdateZenLabel();
            RefreshInventoryContent();
            // Force a UI rebuild from current CharacterState to clear any stale visuals
            _networkManager?.GetCharacterState()?.RaiseInventoryChanged();
            Visible = true;
            BringToFront();
            Scene.FocusControl = this;
            if (_zenLabel != null)
            {
                _zenLabel.Visible = true;
                UpdateZenLabelPosition();
            }

            Console.WriteLine("[DEBUG] InventoryControl.Show() completed");
        }

        public void Hide()
        {
            // If we have a picked item, return it to its original position before hiding
            if (_pickedItemRenderer.Item != null)
            {
                InventoryItem itemToReturn = _pickedItemRenderer.Item;
                AddItem(itemToReturn); // Put it back in the inventory
                _pickedItemRenderer.ReleaseItem(); // Clear the picked item
            }

            Visible = false;
            if (Scene.FocusControl == this)
                Scene.FocusControl = null;
            if (_zenLabel != null)
                _zenLabel.Visible = false;
        }

        /// <summary>
        /// Preloads inventory data and item textures without showing the window.
        /// </summary>
        public void Preload()
        {
            RefreshInventoryContent();
        }

        private void RefreshInventoryContent()
        {
            if (_networkManager == null) return;

            _items.Clear();
            _itemGrid = new InventoryItem[Columns, Rows];
            _equippedItems.Clear();

            var characterItems = _networkManager.GetCharacterState().GetInventoryItems();

            string defaultItemIconTexturePath = "Interface/newui_item_box.tga";

            const int InventorySlotOffset = 12; // first inv slot

            // 1) Equipped items (slots 0..11)
            foreach (var entry in characterItems.Where(e => e.Key <= 11))
            {
                byte slotIndex = entry.Key;
                byte[] itemData = entry.Value;

                ItemDefinition itemDef = ItemDatabase.GetItemDefinition(itemData) ?? new ItemDefinition(0, ItemDatabase.GetItemName(itemData) ?? "Unknown Item", 1, 1, defaultItemIconTexturePath);
                var invItem = new InventoryItem(itemDef, Point.Zero, itemData);
                if (itemData.Length > 2) invItem.Durability = itemData[2];
                _equippedItems[slotIndex] = invItem;
            }

            // 2) Inventory grid (slots 12+)
            foreach (var entry in characterItems.Where(e => e.Key >= 12))
            {
                byte slotIndex = entry.Key;
                byte[] itemData = entry.Value;

                int adjustedIndex = slotIndex - InventorySlotOffset;
                if (adjustedIndex < 0)
                {
                    _logger?.LogWarning($"SlotIndex {slotIndex} is below inventory offset. Skipping.");
                    continue;
                }

                int gridX = adjustedIndex % Columns;
                int gridY = adjustedIndex / Columns;

                if (gridX >= Columns || gridY >= Rows)
                {
                    string itemName = ItemDatabase.GetItemName(itemData) ?? "Unknown Item";
                    _logger?.LogWarning($"Item at slot {slotIndex} ({itemName}) has invalid grid position ({gridX},{gridY}). Skipping.");
                    continue;
                }

                string itemNameFinal = ItemDatabase.GetItemName(itemData) ?? "Unknown Item";
                // Try to obtain a full definition with stats and size
                ItemDefinition itemDef = ItemDatabase.GetItemDefinition(itemData);

                if (itemDef == null)
                {
                    // Fallback definition if the item is unknown in the database
                    itemDef = new ItemDefinition(0, itemNameFinal, 1, 1, defaultItemIconTexturePath);
                }

                InventoryItem newItem = new InventoryItem(itemDef,
                                          new Point(gridX, gridY),
                                          itemData);

                // Use the durability byte from the data if available
                if (itemData.Length > 2)
                    newItem.Durability = itemData[2];

                if (!AddItem(newItem))
                {
                    _logger?.LogWarning($"Failed to add item '{itemNameFinal}' to inventory UI at slot {slotIndex}. Slot might be occupied unexpectedly.");
                }
            }

            // Preload all item textures in parallel to avoid blocking
            var itemTexturePreloadTasks = new List<Task>();
            foreach (var item in _items)
            {
                if (!string.IsNullOrEmpty(item.Definition.TexturePath))
                {
                    itemTexturePreloadTasks.Add(TextureLoader.Instance.Prepare(item.Definition.TexturePath));
                }
            }

            // Don't await here - let them load in background
            if (itemTexturePreloadTasks.Count > 0)
            {
                _ = Task.WhenAll(itemTexturePreloadTasks);
            }
        }

        public bool AddItem(InventoryItem item)
        {
            if (CanPlaceItem(item, item.GridPosition))
            {
                _items.Add(item);
                PlaceItemOnGrid(item);
                return true;
            }
            return false;
        }

        private void PlaceItemOnGrid(InventoryItem item)
        {
            if (item?.Definition == null) return;

            for (int y = 0; y < item.Definition.Height; y++)
            {
                for (int x = 0; x < item.Definition.Width; x++)
                {
                    int gridX = item.GridPosition.X + x;
                    int gridY = item.GridPosition.Y + y;

                    if (gridX < Columns && gridY < Rows)
                    {
                        _itemGrid[gridX, gridY] = item;
                    }
                }
            }
        }

        private void RemoveItemFromGrid(InventoryItem item)
        {
            if (item?.Definition == null) return;

            for (int y = 0; y < item.Definition.Height; y++)
            {
                for (int x = 0; x < item.Definition.Width; x++)
                {
                    int gridX = item.GridPosition.X + x;
                    int gridY = item.GridPosition.Y + y;

                    if (gridX < Columns && gridY < Rows)
                    {
                        _itemGrid[gridX, gridY] = null;
                    }
                }
            }
        }

        private bool CanPlaceItem(InventoryItem itemToPlace, Point targetSlot)
        {
            if (itemToPlace == null || itemToPlace.Definition == null)
                return false;

            // Check inventory boundaries using fixed constants
            if (targetSlot.X < 0 || targetSlot.Y < 0 ||
                targetSlot.X + itemToPlace.Definition.Width > Columns ||
                targetSlot.Y + itemToPlace.Definition.Height > Rows)
            {
                return false;
            }

            // Check if slots are free
            for (int y = 0; y < itemToPlace.Definition.Height; y++)
            {
                for (int x = 0; x < itemToPlace.Definition.Width; x++)
                {
                    int checkX = targetSlot.X + x;
                    int checkY = targetSlot.Y + y;

                    if (checkX >= Columns || checkY >= Rows)
                        return false;

                    if (_itemGrid[checkX, checkY] != null)
                    {
                        return false; // Slot is occupied
                    }
                }
            }
            return true;
        }

        public override void Update(GameTime gameTime)
        {
            _currentGameTime = gameTime;
            // Check for ESC key press (not hold)
            if (MuGame.Instance.Keyboard.IsKeyDown(Keys.Escape) && MuGame.Instance.PrevKeyboard.IsKeyUp(Keys.Escape))
            {
                // If we have a picked item, return it to its original position before closing
                if (_pickedItemRenderer.Item != null)
                {
                    InventoryItem itemToReturn = _pickedItemRenderer.Item;
                    AddItem(itemToReturn); // Put it back in the inventory
                    _pickedItemRenderer.ReleaseItem(); // Clear the picked item
                }
                Visible = false;
            }

            if (!Visible)
            {
                _pickedItemRenderer.Visible = false;
                return;
            }

            // Update label position if size changed
            if (_zenLabel != null && _zenLabel.Visible)
            {
                UpdateZenLabelPosition();
            }

            base.Update(gameTime);

            Point mousePos = MuGame.Instance.UiMouseState.Position;
            _hoveredItem = null;
            _hoveredSlot = new Point(-1, -1);
            _hoveredEquipSlot = GetEquipSlotAtScreenPosition(mousePos);

            // Window dragging logic
            bool leftPressed = MuGame.Instance.UiMouseState.LeftButton == ButtonState.Pressed;
            bool leftJustPressed = leftPressed && MuGame.Instance.PrevUiMouseState.LeftButton == ButtonState.Released;
            bool leftJustReleased = !leftPressed && MuGame.Instance.PrevUiMouseState.LeftButton == ButtonState.Pressed;

            if (leftJustPressed && IsMouseOverDragArea() && !_isDragging)
            {
                // Check for double click (within 500ms)
                DateTime now = DateTime.Now;
                if ((now - _lastClickTime).TotalMilliseconds < 500)
                {
                    // Double click - restore center alignment
                    Align = ControlAlign.VerticalCenter | ControlAlign.Right;
                    _lastClickTime = DateTime.MinValue; // Reset to prevent triple clicks
                }
                else
                {
                    // Single click - start dragging
                    _isDragging = true;
                    _dragOffset = new Point(mousePos.X - X, mousePos.Y - Y);
                    // Disable auto-alignment when starting to drag
                    Align = ControlAlign.None;
                    _lastClickTime = now;
                }
            }
            else if (leftJustReleased && _isDragging)
            {
                _isDragging = false;
                // Keep Align = None to preserve manual position
            }
            else if (_isDragging && leftPressed)
            {
                X = mousePos.X - _dragOffset.X;
                Y = mousePos.Y - _dragOffset.Y;
            }

            // Picking up/dropping logic within inventory (only if not dragging window)
            if (IsMouseOver && !_isDragging) // Checks if mouse is over the entire InventoryControl
            {
                Point gridSlot = GetSlotAtScreenPosition(mousePos);
                _hoveredSlot = gridSlot; // Always update, even if outside the grid (-1,-1)

                if (gridSlot.X != -1) // Mouse is over the grid
                {
                    _hoveredItem = _itemGrid[gridSlot.X, gridSlot.Y];

                    if (leftJustPressed)
                    {
                        if (_pickedItemRenderer.Item != null) // We have a picked up item -> trying to drop
                        {
                            if (CanPlaceItem(_pickedItemRenderer.Item, gridSlot))
                            {
                                InventoryItem itemToPlace = _pickedItemRenderer.Item;
                                // If dropping on the same slot as originally picked, treat as no-op (no server call)
                                if (_pickedItemOriginalGrid.X >= 0 && gridSlot == _pickedItemOriginalGrid)
                                {
                                    itemToPlace.GridPosition = gridSlot;
                                    AddItem(itemToPlace);
                                    _pickedItemRenderer.ReleaseItem();
                                    _pickedItemOriginalGrid = new Point(-1, -1);
                                    return; // No further processing
                                }

                                // Compute from/to slot indices for server
                                const int inventorySlotOffset = 12;
                                byte fromSlot = 0;
                                if (_pickedItemOriginalGrid.X >= 0)
                                {
                                    fromSlot = (byte)(inventorySlotOffset + (_pickedItemOriginalGrid.Y * Columns) + _pickedItemOriginalGrid.X);
                                }
                                else if (_pickedFromEquipSlot >= 0)
                                {
                                    fromSlot = (byte)_pickedFromEquipSlot;
                                }
                                byte toSlot = (byte)(inventorySlotOffset + (gridSlot.Y * Columns) + gridSlot.X);

                                itemToPlace.GridPosition = gridSlot;
                                AddItem(itemToPlace); // Adds to _items and _itemGrid
                                // Send move request to server (inventory -> inventory)
                                if (_networkManager != null)
                                {
                                    var svc = _networkManager.GetCharacterService();
                                    var version = _networkManager.TargetVersion;
                                    var raw = itemToPlace.RawData ?? Array.Empty<byte>();
                                    // Stash the pending move so the handler can update CharacterState precisely on server response
                                    var state = _networkManager.GetCharacterState();
                                    state.StashPendingInventoryMove(fromSlot, toSlot);
                                    _ = System.Threading.Tasks.Task.Run(async () =>
                                    {
                                        await svc.SendItemMoveRequestAsync(fromSlot, toSlot, version, raw);
                                        // Optimistic UI fallback: if no server response within 1200ms, refresh UI back to state
                                        await System.Threading.Tasks.Task.Delay(1200);
                                        if (_networkManager != null && state.IsInventoryMovePending(fromSlot, toSlot))
                                        {
                                            MuGame.ScheduleOnMainThread(() => state.RaiseInventoryChanged());
                                        }
                                    });
                                }
                                _pickedItemRenderer.ReleaseItem();
                                _pickedItemOriginalGrid = new Point(-1, -1);
                                _pickedFromEquipSlot = -1;
                            }
                            // If cannot drop, the item remains picked up
                        }
                        else if (_hoveredItem != null) // We don't have a picked up item -> trying to pick up
                        {
                            _pickedItemRenderer.PickUpItem(_hoveredItem);
                            _pickedItemOriginalGrid = _hoveredItem.GridPosition;
                            RemoveItemFromGrid(_hoveredItem);
                            _items.Remove(_hoveredItem);
                            _hoveredItem = null; // No longer hovering over it, because it's picked up
                            _pickedFromEquipSlot = -1;
                        }
                    }
                }
                else if (_hoveredEquipSlot >= 0)
                {
                    if (leftJustPressed)
                    {
                        if (_pickedItemRenderer.Item != null)
                        {
                            // Drop to equipment
                            var itemToPlace = _pickedItemRenderer.Item;
                            const int inventorySlotOffset = 12;
                            byte fromSlot = 0;
                            if (_pickedItemOriginalGrid.X >= 0)
                                fromSlot = (byte)(inventorySlotOffset + (_pickedItemOriginalGrid.Y * Columns) + _pickedItemOriginalGrid.X);
                            else if (_pickedFromEquipSlot >= 0)
                                fromSlot = (byte)_pickedFromEquipSlot;
                            byte toSlot = (byte)_hoveredEquipSlot;

                            _equippedItems[toSlot] = itemToPlace; // optimistic

                            if (_networkManager != null)
                            {
                                var svc = _networkManager.GetCharacterService();
                                var version = _networkManager.TargetVersion;
                                var raw = itemToPlace.RawData ?? Array.Empty<byte>();
                                var state = _networkManager.GetCharacterState();
                                state.StashPendingInventoryMove(fromSlot, toSlot);
                                _ = System.Threading.Tasks.Task.Run(async () =>
                                {
                                    await svc.SendItemMoveRequestAsync(fromSlot, toSlot, version, raw);
                                    await System.Threading.Tasks.Task.Delay(1200);
                                    if (_networkManager != null && state.IsInventoryMovePending(fromSlot, toSlot))
                                    {
                                        MuGame.ScheduleOnMainThread(() => state.RaiseInventoryChanged());
                                    }
                                });
                            }

                            _pickedItemRenderer.ReleaseItem();
                            _pickedItemOriginalGrid = new Point(-1, -1);
                            _pickedFromEquipSlot = -1;
                        }
                        else
                        {
                            // Pick from equipment
                            if (_equippedItems.TryGetValue((byte)_hoveredEquipSlot, out var eqItem))
                            {
                                _pickedItemRenderer.PickUpItem(eqItem);
                                _equippedItems.Remove((byte)_hoveredEquipSlot);
                                _pickedFromEquipSlot = _hoveredEquipSlot;
                                _pickedItemOriginalGrid = new Point(-1, -1);
                            }
                        }
                    }
                }
            }

            // If we released the mouse while dragging an item OUTSIDE of the inventory grid,
            // drop to vault or terrain.
            if (leftJustReleased && _pickedItemRenderer.Item != null && !_isDragging && !IsMouseOverGrid())
            {
                var item = _pickedItemRenderer.Item;
                // Compute the original inventory slot index (server indexing starts at 12)
                const int inventorySlotOffset = 12;
                byte slotIndex = (byte)(inventorySlotOffset + (item.GridPosition.Y * Columns) + item.GridPosition.X);

                // Check if the cursor is over an open NPC shop -> SELL
                var shop = Client.Main.Controls.UI.Game.NpcShopControl.Instance;
                if (shop != null && shop.Visible && shop.DisplayRectangle.Contains(MuGame.Instance.UiMouseState.Position))
                {
                    var itemToSell = _pickedItemRenderer.Item;
                    var originalGrid = _pickedItemOriginalGrid;
                    int fromEquipSlot = _pickedFromEquipSlot;

                    _pickedItemRenderer.ReleaseItem();
                    _pickedItemOriginalGrid = new Point(-1, -1);
                    _pickedFromEquipSlot = -1;

                    ShowSellConfirmation(itemToSell, slotIndex, originalGrid, fromEquipSlot);
                }
                // Otherwise, check if cursor is over Vault -> move Inventory -> Vault
                else if (Client.Main.Controls.UI.Game.VaultControl.Instance is { } vault && vault.Visible && vault.DisplayRectangle.Contains(MuGame.Instance.UiMouseState.Position) && _networkManager != null)
                {
                    var drop = vault.GetSlotAtScreenPosition(MuGame.Instance.UiMouseState.Position);
                    if (drop.X >= 0 && vault.CanPlaceAt(drop, item))
                    {
                        byte toSlot = (byte)(drop.Y * 8 + drop.X); // vault is 8 columns
                        var svc = _networkManager.GetCharacterService();
                        var raw = item.RawData ?? Array.Empty<byte>();
                        // Stash a self-referencing inventory move so we can restore on failure (server 0x24 fail)
                        var state = _networkManager.GetCharacterState();
                        state.StashPendingInventoryMove(slotIndex, slotIndex);
                        _ = System.Threading.Tasks.Task.Run(async () =>
                        {
                            await svc.SendStorageItemMoveAsync(ItemStorageKind.Inventory, slotIndex, ItemStorageKind.Vault, toSlot, _networkManager.TargetVersion, raw);
                            // Optimistic fallback: if no server response in time, force refresh
                            await System.Threading.Tasks.Task.Delay(1200);
                            if (_networkManager != null && state.IsInventoryMovePending(slotIndex, slotIndex))
                            {
                                MuGame.ScheduleOnMainThread(() =>
                                {
                                    state.RaiseInventoryChanged();
                                    state.RaiseVaultItemsChanged();
                                });
                            }
                        });
                        _pickedItemRenderer.ReleaseItem();
                        _pickedItemOriginalGrid = new Point(-1, -1);
                    }
                    else
                    {
                        // invalid target, restore item back to inventory UI
                        AddItem(item);
                        // Force a small resync to clear any stale visuals
                        _networkManager?.GetCharacterState()?.RaiseInventoryChanged();
                        _pickedItemRenderer.ReleaseItem();
                        _pickedItemOriginalGrid = new Point(-1, -1);
                    }
                }
                // Otherwise, drop to terrain
                else if (Scene?.World is Controls.WalkableWorldControl world && _networkManager != null)
                {
                    byte tileX = world.MouseTileX;
                    byte tileY = world.MouseTileY;
                    // Fire-and-forget send; server will handle success/failure and update inventory accordingly.
                    // Add optimistic UI fallback to refresh inventory if no ACK arrives.
                    _ = System.Threading.Tasks.Task.Run(async () =>
                    {
                        var svc = _networkManager.GetCharacterService();
                        await svc.SendDropItemRequestAsync(tileX, tileY, slotIndex);
                        await System.Threading.Tasks.Task.Delay(1200);
                        // If item still present at slot, force refresh to show it back
                        var state = _networkManager.GetCharacterState();
                        if (state.HasInventoryItem(slotIndex))
                        {
                            MuGame.ScheduleOnMainThread(() => state.RaiseInventoryChanged());
                        }
                    });

                    // We already removed the item from the UI grid when it was picked up; just clear the picked state
                    _pickedItemRenderer.ReleaseItem();
                    _pickedItemOriginalGrid = new Point(-1, -1);
                }
                else
                {
                    // No world or network available - revert the item back into the inventory UI
                    AddItem(item);
                    _pickedItemRenderer.ReleaseItem();
                    _pickedItemOriginalGrid = new Point(-1, -1);
                }
            }

            _pickedItemRenderer.Update(gameTime);
        }

        private void ShowSellConfirmation(InventoryItem item, byte slotIndex, Point originalGrid, int fromEquipSlot)
        {
            if (item == null)
            {
                return;
            }

            if (_networkManager == null)
            {
                MessageWindow.Show("No connection to server. Sale is not possible.");
                RestoreItemAfterCancelledSell(item, originalGrid, fromEquipSlot);
                return;
            }

            var definition = item.Definition;
            if (definition == null)
            {
                MessageWindow.Show("Cannot identify the selected item.");
                RestoreItemAfterCancelledSell(item, originalGrid, fromEquipSlot);
                return;
            }

            string displayName = BuildItemDisplayName(item);

            if (!definition.CanSellToNpc)
            {
                MessageWindow.Show($"Item '{displayName}' cannot be sold to NPC shop.");
                RestoreItemAfterCancelledSell(item, originalGrid, fromEquipSlot);
                return;
            }

            var builder = new StringBuilder();
            builder.AppendLine($"Sell {displayName}?");
            if (definition.IsExpensive)
            {
                builder.AppendLine();
                builder.AppendLine("WARNING: This item is marked as expensive.");
            }

            RequestDialog.Show(
                builder.ToString(),
                onAccept: () => ExecuteSellToNpc(slotIndex),
                onReject: () => RestoreItemAfterCancelledSell(item, originalGrid, fromEquipSlot),
                acceptText: "Sell",
                rejectText: "Cancel");
        }

        private void ExecuteSellToNpc(byte slotIndex)
        {
            if (_networkManager == null)
            {
                MessageWindow.Show("No connection to server. Sale is not possible.");
                return;
            }

            var svc = _networkManager.GetCharacterService();
            if (svc == null)
            {
                MessageWindow.Show("Failed to connect to NPC shop server.");
                return;
            }

            var state = _networkManager.GetCharacterState();
            state.StashPendingSellSlot(slotIndex);
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await svc.SendSellItemToNpcRequestAsync(slotIndex);
                    await System.Threading.Tasks.Task.Delay(1200);
                    var refreshedState = _networkManager?.GetCharacterState();
                    if (refreshedState != null && refreshedState.HasInventoryItem(slotIndex))
                    {
                        MuGame.ScheduleOnMainThread(() => refreshedState.RaiseInventoryChanged());
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error while sending item sale request from slot {Slot}.", slotIndex);
                    var refreshedState = _networkManager?.GetCharacterState();
                    if (refreshedState != null)
                    {
                        MuGame.ScheduleOnMainThread(() => refreshedState.RaiseInventoryChanged());
                    }

                    MuGame.ScheduleOnMainThread(() => MessageWindow.Show("Failed to sell item. Please try again."));
                }
            });
        }

        private void RestoreItemAfterCancelledSell(InventoryItem item, Point originalGrid, int fromEquipSlot)
        {
            if (item == null)
            {
                return;
            }

            if (fromEquipSlot >= 0)
            {
                _equippedItems[(byte)fromEquipSlot] = item;
                _networkManager?.GetCharacterState()?.RaiseEquipmentChanged();
                return;
            }

            Point targetSlot = originalGrid;
            if (targetSlot.X < 0 || targetSlot.Y < 0 || !CanPlaceItem(item, targetSlot))
            {
                if (!TryFindFirstFreeSlot(item, out targetSlot))
                {
                    _logger?.LogWarning("No free space to restore item '{Name}' in inventory.", item.Definition?.Name ?? "Unknown");
                    MessageWindow.Show("No space in inventory to restore item.");
                    return;
                }
            }

            item.GridPosition = targetSlot;
            if (!AddItem(item))
            {
                _logger?.LogWarning("Failed to restore item '{Name}' to inventory.", item.Definition?.Name ?? "Unknown");
                MessageWindow.Show("Restoring item to inventory failed.");
            }
        }



        private bool TryFindFirstFreeSlot(InventoryItem item, out Point slot)
        {
            slot = new Point(-1, -1);
            if (item?.Definition == null)
            {
                return false;
            }

            for (int y = 0; y <= Rows - item.Definition.Height; y++)
            {
                for (int x = 0; x <= Columns - item.Definition.Width; x++)
                {
                    var candidate = new Point(x, y);
                    if (CanPlaceItem(item, candidate))
                    {
                        slot = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        private static string BuildItemDisplayName(InventoryItem item)
        {
            if (item == null)
            {
                return "item";
            }

            string name = item.Definition?.Name ?? ItemDatabase.GetItemName(item.RawData) ?? "item";
            if (item.Details.Level > 0)
            {
                name += $" +{item.Details.Level}";
            }

            if (item.Definition?.BaseDurability == 0 && item.Durability > 1)
            {
                name += $" x{item.Durability}";
            }

            return name;
        }

        public void HookEvents()
        {
            if (_networkManager != null)
            {
                var state = _networkManager.GetCharacterState();
                state.InventoryChanged += RefreshInventoryContent;
                state.MoneyChanged += () =>
                {
                    ZenAmount = state.InventoryZen; // triggers label update
                };
            }
        }

        private Point GetSlotAtScreenPosition(Point screenPos)
        {
            if (DisplayRectangle.Width <= 0 || DisplayRectangle.Height <= 0)
                return new Point(-1, -1);

            Point localPos = new Point(screenPos.X - DisplayRectangle.X - GridOffset.X,
                                       screenPos.Y - DisplayRectangle.Y - GridOffset.Y);

            if (localPos.X < 0 || localPos.Y < 0 ||
                localPos.X >= Columns * CellWidth ||
                localPos.Y >= Rows * CellHeight)
            {
                return new Point(-1, -1); // Outside grid
            }

            return new Point(
                Math.Min(Columns - 1, localPos.X / CellWidth),
                Math.Min(Rows - 1, localPos.Y / CellHeight)
            );
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible) return;

            // First draw the base DynamicLayoutControl (background textures)
            base.Draw(gameTime);

            Rectangle displayRect = DisplayRectangle;

            using (new SpriteBatchScope(GraphicsManager.Instance.Sprite, SpriteSortMode.Deferred, BlendState.AlphaBlend, transform: UiScaler.SpriteTransform))
            {
                // Draw custom inventory elements on top
                DrawFrame(GraphicsManager.Instance.Sprite, displayRect);
                DrawEquippedArea(GraphicsManager.Instance.Sprite, displayRect);
                DrawGrid(GraphicsManager.Instance.Sprite, displayRect);
                DrawItems(GraphicsManager.Instance.Sprite, displayRect);
                // Drag preview is drawn globally in GameScene to ensure top-most z-order
                DrawDragArea(GraphicsManager.Instance.Sprite, displayRect);
                DrawTooltip(GraphicsManager.Instance.Sprite, displayRect);
            }
        }

        private static readonly Dictionary<byte, Point> _equipLayout = new()
        {
            { 0, new Point(0,0) }, // Left hand
            { 2, new Point(1,0) }, // Helm
            { 3, new Point(2,0) }, // Armor
            { 1, new Point(3,0) }, // Right hand
            { 7, new Point(4,0) }, // Wings
            { 9, new Point(5,0) }, // Pendant

            { 8, new Point(0,1) }, // Pet
            { 5, new Point(1,1) }, // Gloves
            { 4, new Point(2,1) }, // Pants
            { 6, new Point(3,1) }, // Boots
            { 10,new Point(4,1) }, // Ring 1
            { 11,new Point(5,1) }, // Ring 2
        };

        private Point GetEquipAreaTopLeft()
        {
            const int equipRows = 2;
            // Move equipment area to bottom of inventory grid with some padding
            int gridBottomY = DisplayRectangle.Y + GridOffset.Y + (Rows * CellHeight);
            return new Point(DisplayRectangle.X + GridOffset.X,
                             gridBottomY + 20); // 20px padding below the grid
        }

        private int GetEquipSlotAtScreenPosition(Point screenPos)
        {
            var equipTopLeft = GetEquipAreaTopLeft();
            foreach (var kv in _equipLayout)
            {
                var cell = kv.Value;
                var slotRect = new Rectangle(
                    equipTopLeft.X + cell.X * CellWidth,
                    equipTopLeft.Y + cell.Y * CellHeight,
                    CellWidth,
                    CellHeight);
                if (slotRect.Contains(screenPos))
                    return kv.Key;
            }
            return -1;
        }

        private void DrawEquippedArea(SpriteBatch spriteBatch, Rectangle frameRect)
        {
            const int equipCols = 6;
            const int equipRows = 2;
            Point equipTopLeft = GetEquipAreaTopLeft();

            // Background panel
            var panelRect = new Rectangle(
                equipTopLeft.X - 8,
                equipTopLeft.Y - 14,
                equipCols * CellWidth + 16,
                equipRows * CellHeight + 24);
            spriteBatch.Draw(GraphicsManager.Instance.Pixel, panelRect, new Color(8, 8, 8, 180));
            // Border
            spriteBatch.Draw(GraphicsManager.Instance.Pixel, new Rectangle(panelRect.X, panelRect.Y, panelRect.Width, 1), Color.Black * 0.6f);
            spriteBatch.Draw(GraphicsManager.Instance.Pixel, new Rectangle(panelRect.X, panelRect.Bottom - 1, panelRect.Width, 1), Color.Black * 0.6f);
            spriteBatch.Draw(GraphicsManager.Instance.Pixel, new Rectangle(panelRect.X, panelRect.Y, 1, panelRect.Height), Color.Black * 0.6f);
            spriteBatch.Draw(GraphicsManager.Instance.Pixel, new Rectangle(panelRect.Right - 1, panelRect.Y, 1, panelRect.Height), Color.Black * 0.6f);

            // Draw backgrounds for equipment slots
            foreach (var kv in _equipLayout)
            {
                var cell = kv.Value;
                Rectangle slotRect = new Rectangle(
                    equipTopLeft.X + cell.X * CellWidth,
                    equipTopLeft.Y + cell.Y * CellHeight,
                    CellWidth,
                    CellHeight);

                if (_slotTexture != null)
                {
                    Rectangle sourceRect = new Rectangle(546, 220, 29, 29);
                    spriteBatch.Draw(_slotTexture, slotRect, sourceRect, Color.White);
                }
                else
                {
                    spriteBatch.Draw(GraphicsManager.Instance.Pixel, slotRect, Color.DarkSlateGray * 0.3f);
                    spriteBatch.Draw(GraphicsManager.Instance.Pixel, new Rectangle(slotRect.X, slotRect.Y, slotRect.Width, 1), Color.Gray * 0.8f);
                    spriteBatch.Draw(GraphicsManager.Instance.Pixel, new Rectangle(slotRect.X, slotRect.Bottom - 1, slotRect.Width, 1), Color.Gray * 0.8f);
                    spriteBatch.Draw(GraphicsManager.Instance.Pixel, new Rectangle(slotRect.X, slotRect.Y, 1, slotRect.Height), Color.Gray * 0.8f);
                    spriteBatch.Draw(GraphicsManager.Instance.Pixel, new Rectangle(slotRect.Right - 1, slotRect.Y, 1, slotRect.Height), Color.Gray * 0.8f);
                }
            }

            // Draw equipped item icons
            foreach (var kv in _equippedItems)
            {
                if (!_equipLayout.TryGetValue(kv.Key, out var cell)) continue;
                var item = kv.Value;
                Rectangle itemRect = new Rectangle(
                    equipTopLeft.X + cell.X * CellWidth,
                    equipTopLeft.Y + cell.Y * CellHeight,
                    CellWidth,
                    CellHeight);

                Texture2D itemTexture = null;
                if (!string.IsNullOrEmpty(item.Definition.TexturePath))
                {
                    itemTexture = TextureLoader.Instance.GetTexture2D(item.Definition.TexturePath);
                    if (itemTexture == null && item.Definition.TexturePath.EndsWith(".bmd", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            itemTexture = BmdPreviewRenderer.GetPreview(item.Definition, itemRect.Width, itemRect.Height);
                        }
                        catch { }
                    }
                }

                if (itemTexture != null)
                {
                    spriteBatch.Draw(itemTexture, itemRect, Color.White);
                }
                else
                {
                    spriteBatch.Draw(GraphicsManager.Instance.Pixel, itemRect, new Color(40, 40, 40, 200));
                }
            }
        }

        private void DrawDragArea(SpriteBatch spriteBatch, Rectangle frameRect)
        {
            // Only show drag area when mouse is over it or when dragging
            // if (IsMouseOverDragArea() || _isDragging)
            // {
            //     int dragAreaHeight = frameRect.Height / 10;
            //     Rectangle dragRect = new Rectangle(
            //         frameRect.X,
            //         frameRect.Y,
            //         frameRect.Width,
            //         dragAreaHeight);

            //     Color dragColor = _isDragging ? Color.Yellow * 0.3f : Color.White * 0.2f;
            //     spriteBatch.Draw(GraphicsManager.Instance.Pixel, dragRect, dragColor);
            // }
        }

        private void DrawFrame(SpriteBatch spriteBatch, Rectangle frameRect)
        {
            var font = GraphicsManager.Instance.Font;
            if (font != null)
            {
                string title = "Inventory"; // Can be made a property
                Vector2 titleSize = font.MeasureString(title);
                float titleScale = 0.5f; // Adjust scale
                Vector2 scaledTitleSize = titleSize * titleScale;
                Vector2 titlePos = new Vector2(
                    frameRect.X + (frameRect.Width - scaledTitleSize.X) / 2,
                    frameRect.Y + 36 // Adjust Y
                );
                spriteBatch.DrawString(font, title, titlePos, Color.Orange, 0, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
            }
        }

        private void DrawGrid(SpriteBatch spriteBatch, Rectangle frameRect)
        {
            Point gridTopLeft = new Point(DisplayRectangle.X + GridOffset.X, DisplayRectangle.Y + GridOffset.Y);

            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    Rectangle slotRect = new Rectangle(
                        gridTopLeft.X + x * CellWidth,
                        gridTopLeft.Y + y * CellHeight,
                        CellWidth,
                        CellHeight);

                    if (_slotTexture != null)
                    {
                        Rectangle sourceRect = new Rectangle(546, 220, 29, 29);
                        spriteBatch.Draw(_slotTexture, slotRect, sourceRect, Color.White);
                    }
                    else
                    {
                        spriteBatch.Draw(GraphicsManager.Instance.Pixel, slotRect, Color.DarkSlateGray * 0.3f);

                        // Slot border
                        spriteBatch.Draw(GraphicsManager.Instance.Pixel, new Rectangle(slotRect.X, slotRect.Y, slotRect.Width, 1), Color.Gray * 0.8f);
                        spriteBatch.Draw(GraphicsManager.Instance.Pixel, new Rectangle(slotRect.X, slotRect.Bottom - 1, slotRect.Width, 1), Color.Gray * 0.8f);
                        spriteBatch.Draw(GraphicsManager.Instance.Pixel, new Rectangle(slotRect.X, slotRect.Y, 1, slotRect.Height), Color.Gray * 0.8f);
                        spriteBatch.Draw(GraphicsManager.Instance.Pixel, new Rectangle(slotRect.Right - 1, slotRect.Y, 1, slotRect.Height), Color.Gray * 0.8f);
                    }

                    // Highlight for drag & drop - uses original slotRect
                    var dragged = _pickedItemRenderer.Item ?? Client.Main.Controls.UI.Game.VaultControl.Instance?.GetDraggedItem();
                    if (dragged != null && IsMouseOverGrid())
                    {
                        Point currentSlot = new Point(x, y);
                        Color? highlightColor = GetSlotHighlightColor(currentSlot, dragged);

                        if (highlightColor.HasValue)
                        {
                            spriteBatch.Draw(GraphicsManager.Instance.Pixel, slotRect, highlightColor.Value);
                        }
                    }
                    else if (IsMouseOverGrid() && dragged == null)
                    {
                        // Highlight hovered slot
                        if (_hoveredSlot.X == x && _hoveredSlot.Y == y)
                        {
                            spriteBatch.Draw(GraphicsManager.Instance.Pixel, slotRect, Color.Yellow * 0.3f);
                        }
                        // Highlight all slots occupied by the hovered multi-slot item
                        else if (_hoveredItem != null && IsSlotOccupiedByItem(new Point(x, y), _hoveredItem))
                        {
                            spriteBatch.Draw(GraphicsManager.Instance.Pixel, slotRect, Color.Blue * 0.3f);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Determines the highlight color for a given slot during drag & drop
        /// </summary>
        /// <param name="slot">Coordinates of the slot to check</param>
        /// <param name="draggedItem">The item being dragged</param>
        /// <returns>Highlight color or null if the slot should not be highlighted</returns>
        private Color? GetSlotHighlightColor(Point slot, InventoryItem draggedItem)
        {
            if (draggedItem == null || _hoveredSlot.X == -1 || _hoveredSlot.Y == -1)
                return null;

            // Check if the given slot belongs to the area that the dragged item would occupy
            if (!IsSlotInDropArea(slot, _hoveredSlot, draggedItem))
                return null;

            // Check if the item can be placed here
            bool canPlace = CanPlaceItem(draggedItem, _hoveredSlot);

            if (canPlace)
            {
                return Color.GreenYellow * 0.5f; // Green - can drop
            }
            else
            {
                return Color.Red * 0.6f; // Red - cannot drop
            }
        }

        /// <summary>
        /// Checks if the given slot belongs to the area that would be occupied by an item dropped at hoveredSlot
        /// </summary>
        private bool IsSlotInDropArea(Point slot, Point dropPosition, InventoryItem item)
        {
            return slot.X >= dropPosition.X &&
                   slot.X < dropPosition.X + item.Definition.Width &&
                   slot.Y >= dropPosition.Y &&
                   slot.Y < dropPosition.Y + item.Definition.Height;
        }

        /// <summary>
        /// Checks if the given slot is occupied by any item
        /// </summary>
        private bool IsSlotOccupied(Point slot)
        {
            if (slot.X < 0 || slot.Y < 0 || slot.X >= Columns || slot.Y >= Rows)
                return false;

            return _itemGrid[slot.X, slot.Y] != null;
        }

        /// <summary>
        /// Checks if the given slot is occupied by a specific item
        /// </summary>
        private bool IsSlotOccupiedByItem(Point slot, InventoryItem item)
        {
            if (slot.X < 0 || slot.Y < 0 || slot.X >= Columns || slot.Y >= Rows || item?.Definition == null)
                return false;

            // Check if the slot falls within the item's occupied area
            return slot.X >= item.GridPosition.X &&
                   slot.X < item.GridPosition.X + item.Definition.Width &&
                   slot.Y >= item.GridPosition.Y &&
                   slot.Y < item.GridPosition.Y + item.Definition.Height;
        }

        private bool IsMouseOverGrid()
        {
            Point mousePos = MuGame.Instance.UiMouseState.Position;
            Rectangle gridScreenRect = new Rectangle(
                DisplayRectangle.X + GridOffset.X,
                DisplayRectangle.Y + GridOffset.Y,
                Columns * CellWidth,
                Rows * CellHeight);
            return gridScreenRect.Contains(mousePos);
        }

        // Public helpers for other controls (e.g., Vault)
        public Point GetSlotAtScreenPositionPublic(Point screenPos) => GetSlotAtScreenPosition(screenPos);
        public bool CanPlaceAt(Point gridSlot, InventoryItem item) => CanPlaceItem(item, gridSlot);

        private bool IsMouseOverDragArea()
        {
            Point mousePos = MuGame.Instance.UiMouseState.Position;
            int dragAreaHeight = DisplayRectangle.Height / 10; // Top 1/10 of window
            Rectangle dragRect = new Rectangle(
                DisplayRectangle.X,
                DisplayRectangle.Y,
                DisplayRectangle.Width,
                dragAreaHeight);
            return dragRect.Contains(mousePos);
        }

        private bool IsPartOfPotentialDropArea(Point slot, InventoryItem itemBeingDragged)
        {
            if (itemBeingDragged == null) return false;

            // _hoveredSlot is the top-left corner of the potential drop location
            return slot.X >= _hoveredSlot.X && slot.X < _hoveredSlot.X + itemBeingDragged.Definition.Width &&
                   slot.Y >= _hoveredSlot.Y && slot.Y < _hoveredSlot.Y + itemBeingDragged.Definition.Height;
        }

        private void DrawItems(SpriteBatch spriteBatch, Rectangle frameRect)
        {
            Point gridTopLeft = new Point(DisplayRectangle.X + GridOffset.X, DisplayRectangle.Y + GridOffset.Y);
            var font = GraphicsManager.Instance.Font;

            foreach (var item in _items)
            {
                if (item == _pickedItemRenderer.Item) continue;

                Rectangle itemRect = new Rectangle(
                    gridTopLeft.X + item.GridPosition.X * CellWidth,
                    gridTopLeft.Y + item.GridPosition.Y * CellHeight,
                    item.Definition.Width * CellWidth,
                    item.Definition.Height * CellHeight);

                Texture2D itemTexture = null;
                if (!string.IsNullOrEmpty(item.Definition.TexturePath))
                {
                    itemTexture = TextureLoader.Instance.GetTexture2D(item.Definition.TexturePath);

                    if (itemTexture == null && item.Definition.TexturePath.EndsWith(".bmd", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            int w = item.Definition.Width * CellWidth;
                            int h = item.Definition.Height * CellHeight;

                            // Use animated preview for hovered items
                            if (item == _hoveredItem)
                            {
                                itemTexture = BmdPreviewRenderer.GetAnimatedPreview(item.Definition, w, h, _currentGameTime);
                            }
                            else
                            {
                                itemTexture = BmdPreviewRenderer.GetPreview(item.Definition, w, h);
                            }
                        }
                        catch (InvalidOperationException ex) when (ex.Message.Contains("UI thread"))
                        {
                            // Skip BMD preview if UI thread check fails
                            // Will fall back to dark gray rectangle
                        }
                        catch (Exception)
                        {
                            // Other errors, also skip
                        }
                    }
                }

                if (itemTexture != null)
                {
                    spriteBatch.Draw(itemTexture, itemRect, Color.White);
                }
                else
                {
                    spriteBatch.Draw(GraphicsManager.Instance.Pixel, itemRect, Color.DarkSlateGray);
                }

                // Draw quantity for stackable items (BaseDurability = 0 means it's stackable)
                if (item.Definition.BaseDurability == 0 && item.Durability > 1)
                {
                    string quantityText = item.Durability.ToString();
                    Vector2 textSize = font.MeasureString(quantityText);
                    float textScale = 0.4f; // Small text
                    Vector2 scaledTextSize = textSize * textScale;

                    // Position in upper right corner of the item (not just first slot)
                    Vector2 textPosition = new Vector2(
                        itemRect.Right - scaledTextSize.X - 2, // 2px margin from right edge
                        itemRect.Y + 2 // 2px margin from top edge
                    );

                    // Draw black outline for readability
                    Color outlineColor = Color.Black;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx != 0 || dy != 0) // Skip center
                            {
                                spriteBatch.DrawString(font, quantityText,
                                    textPosition + new Vector2(dx, dy),
                                    outlineColor, 0f, Vector2.Zero, textScale,
                                    SpriteEffects.None, 0f);
                            }
                        }
                    }

                    // Draw main text in pale yellow
                    Color quantityColor = new Color(255, 255, 180); // Pale yellow
                    spriteBatch.DrawString(font, quantityText, textPosition,
                        quantityColor, 0f, Vector2.Zero, textScale,
                        SpriteEffects.None, 0f);
                }
            }
        }

        private static List<(string txt, Color col)> BuildTooltipLines(InventoryItem it)
        {
            var d = it.Details;
            var li = new List<(string, Color)>();

            // ── name ─────────────────────────────────────────────────────────────
            string name = d.IsExcellent ? $"Excellent {it.Definition.Name}"
                       : d.IsAncient ? $"Ancient {it.Definition.Name}"
                       : it.Definition.Name;
            if (d.Level > 0)
                name += $" +{d.Level}";
            li.Add((name, Color.White));

            // ── stats from ItemDefinition ─────────────────────────────────────
            var def = it.Definition;
            if (def.DamageMin > 0 || def.DamageMax > 0)
            {
                string dmgType = def.TwoHanded ? "Two-hand" : "One-hand";
                li.Add(($"{dmgType} Damage : {def.DamageMin} ~ {def.DamageMax}", Color.Orange));
            }
            if (def.Defense > 0)
                li.Add(($"Defense     : {def.Defense}", Color.Orange));
            if (def.DefenseRate > 0)
                li.Add(($"Defense Rate: {def.DefenseRate}", Color.Orange));
            if (def.AttackSpeed > 0)
                li.Add(($"Attack Speed: {def.AttackSpeed}", Color.Orange));

            // ── durability ────────────────────────────────────────────────
            li.Add(($"Durability : {it.Durability}/{def.BaseDurability}", Color.Silver));

            // ── requirements ────────────────────────────────────────────────────
            if (def.RequiredLevel > 0)
                li.Add(($"Required Level   : {def.RequiredLevel}", Color.LightGray));
            if (def.RequiredStrength > 0)
                li.Add(($"Required Strength: {def.RequiredStrength}", Color.LightGray));
            if (def.RequiredDexterity > 0)
                li.Add(($"Required Agility : {def.RequiredDexterity}", Color.LightGray));
            if (def.RequiredEnergy > 0)
                li.Add(($"Required Energy  : {def.RequiredEnergy}", Color.LightGray));

            if (def.AllowedClasses != null && def.AllowedClasses.Count > 0)
            {
                foreach (string cls in def.AllowedClasses)
                    li.Add(($"Can be equipped by {cls}", Color.LightGray));
            }

            // ── opt (+4/+8/+12) ────────────────────────────────────────
            if (d.OptionLevel > 0)
                li.Add(($"Additional Option : +{d.OptionLevel * 4}", new Color(80, 255, 80)));

            // ── luck / skill ─────────────────────────────────────────────────────
            if (d.HasLuck) li.Add(("+Luck  (Crit +5 %, Jewel +25 %)", Color.CornflowerBlue));
            if (d.HasSkill) li.Add(("+Skill (Right mouse click - skill)", Color.CornflowerBlue));

            // ── excellent ──────────────────────────────────────────────────
            if (d.IsExcellent)
            {
                byte excByte = it.RawData.Length > 3 ? it.RawData[3] : (byte)0;
                foreach (var s in ItemDatabase.ParseExcellentOptions(excByte))
                    li.Add(($"+{s}", new Color(128, 255, 128)));
            }

            // ── ancient flag ────────────────────────
            if (d.IsAncient)
                li.Add(("Ancient Option", new Color(0, 255, 128)));

            return li;
        }

        private void DrawTooltip(SpriteBatch sb, Rectangle frameRect)
        {
            // Don't show tooltip when dragging an item
            if (_pickedItemRenderer.Item != null || _hoveredItem == null || _font == null)
                return;

            var lines = BuildTooltipLines(_hoveredItem);
            const float scale = 0.5f;

            int w = 0, h = 0;
            foreach (var (t, _) in lines)
            {
                Vector2 sz = _font.MeasureString(t) * scale;
                w = Math.Max(w, (int)sz.X);
                h += (int)sz.Y + 2;
            }
            w += 12; h += 8;

            Point m = MuGame.Instance.UiMouseState.Position;

            // Get screen bounds
            Rectangle screenBounds = new Rectangle(0, 0, UiScaler.VirtualSize.X, UiScaler.VirtualSize.Y);

            // Calculate hovered item screen position to avoid covering it
            Point gridTopLeft = new Point(DisplayRectangle.X + GridOffset.X, DisplayRectangle.Y + GridOffset.Y);
            Rectangle hoveredItemRect = new Rectangle(
                gridTopLeft.X + _hoveredItem.GridPosition.X * CellWidth,
                gridTopLeft.Y + _hoveredItem.GridPosition.Y * CellHeight,
                _hoveredItem.Definition.Width * CellWidth,
                _hoveredItem.Definition.Height * CellHeight);

            // Start with tooltip to the right of the cursor
            Rectangle r = new(m.X + 15, m.Y + 15, w, h);

            // If tooltip would cover the hovered item, try different positions
            if (r.Intersects(hoveredItemRect))
            {
                // Try positioning tooltip to the left of the item
                r.X = hoveredItemRect.X - w - 10;
                r.Y = hoveredItemRect.Y;

                // If still intersecting or goes off screen, try above the item
                if (r.Intersects(hoveredItemRect) || r.X < screenBounds.X + 10)
                {
                    r.X = hoveredItemRect.X;
                    r.Y = hoveredItemRect.Y - h - 10;

                    // If still intersecting or goes off screen, try below the item
                    if (r.Intersects(hoveredItemRect) || r.Y < screenBounds.Y + 10)
                    {
                        r.X = hoveredItemRect.X;
                        r.Y = hoveredItemRect.Bottom + 10;
                    }
                }
            }

            // Ensure tooltip stays within screen bounds
            if (r.Right > screenBounds.Right - 10) r.X = screenBounds.Right - 10 - r.Width;
            if (r.Bottom > screenBounds.Bottom - 10) r.Y = screenBounds.Bottom - 10 - r.Height;
            if (r.X < screenBounds.X + 10) r.X = screenBounds.X + 10;
            if (r.Y < screenBounds.Y + 10) r.Y = screenBounds.Y + 10;

            sb.Draw(GraphicsManager.Instance.Pixel, r, Color.Black * 0.85f);
            sb.Draw(GraphicsManager.Instance.Pixel, new Rectangle(r.X, r.Y, r.Width, 1), Color.White);
            sb.Draw(GraphicsManager.Instance.Pixel, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), Color.White);
            sb.Draw(GraphicsManager.Instance.Pixel, new Rectangle(r.X, r.Y, 1, r.Height), Color.White);
            sb.Draw(GraphicsManager.Instance.Pixel, new Rectangle(r.Right - 1, r.Y, 1, r.Height), Color.White);

            int y = r.Y + 4;
            foreach (var (t, col) in lines)
            {
                Vector2 size = _font.MeasureString(t) * scale;
                sb.DrawString(_font, t,
                              new Vector2(r.X + (r.Width - size.X) / 2, y),
                              col, 0f, Vector2.Zero, scale,
                              SpriteEffects.None, 0f);
                y += (int)size.Y + 2;
            }
        }
    }
}
