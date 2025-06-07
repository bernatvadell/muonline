using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client.Main.Helpers; // For SpriteBatchScope
using System;
using Client.Main.Networking;
using Client.Main.Core.Utilities;
using Microsoft.Extensions.Logging;

namespace Client.Main.Controls.UI.Game.Inventory
{
    public class InventoryControl : UIControl
    {
        public const int INVENTORY_SQUARE_WIDTH = 30;
        public const int INVENTORY_SQUARE_HEIGHT = 30;
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
        private Texture2D _texTableTopLeft, _texTableTopRight, _texTableBottomLeft, _texTableBottomRight;
        private Texture2D _texTableTopPixel, _texTableBottomPixel, _texTableLeftPixel, _texTableRightPixel;
        private Texture2D _texBackground; // For msgbox_back

        private List<InventoryItem> _items;
        private InventoryItem[,] _itemGrid; // For quick slot occupancy checks

        private Point _gridOffset = new Point(WND_LEFT_EDGE + 2, WND_TOP_EDGE + 38); // Grid offset inside the frame (adjust as needed)

        public PickedItemRenderer _pickedItemRenderer;
        private readonly NetworkManager _networkManager;
        private InventoryItem _hoveredItem = null;
        private Point _hoveredSlot = new Point(-1, -1);

        private SpriteFont _font; // Font for tooltips

        private readonly ILogger<InventoryControl> _logger;

        private void InitializeGrid()
        {
            _itemGrid = new InventoryItem[Columns, Rows];

            // Dynamic size based on dimensions
            int gridWidth = Columns * INVENTORY_SQUARE_WIDTH;
            int gridHeight = Rows * INVENTORY_SQUARE_HEIGHT;

            // Add an additional 25 pixels height for the ZEN label
            int zenLabelSpace = 25;

            ControlSize = new Point(gridWidth + WND_LEFT_EDGE + WND_RIGHT_EDGE + 4,
                                  gridHeight + WND_TOP_EDGE + WND_BOTTOM_EDGE + 38 + 8 + zenLabelSpace);
            ViewSize = ControlSize;
            AutoViewSize = false;
            Interactive = true;
            Visible = false; // Hidden by default

            // Update label position after resizing
            if (_zenLabel != null && Visible)
                UpdateZenLabelPosition();
        }

        public InventoryControl(NetworkManager networkManager, ILoggerFactory loggerFactory) // Zmieniony konstruktor
        {
            _logger = loggerFactory.CreateLogger<InventoryControl>();
            _items = new List<InventoryItem>();
            _networkManager = networkManager ?? throw new ArgumentNullException(nameof(networkManager));

            InitializeGrid();
            Align = ControlAlign.VerticalCenter | ControlAlign.HorizontalCenter;
            _pickedItemRenderer = new PickedItemRenderer();
            InitializeZenLabel();
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
                int gridBottomY = _gridOffset.Y + (Rows * INVENTORY_SQUARE_HEIGHT);

                _zenLabel.X = ViewSize.X - _zenLabel.ViewSize.X - 15;
                _zenLabel.Y = gridBottomY + 5; // 5 pixels below the grid
            }
        }

        public override async Task Load()
        {
            var tl = TextureLoader.Instance;
            _texSquare = await tl.PrepareAndGetTexture("Interface/newui_item_box.tga");
            _texTableTopLeft = await tl.PrepareAndGetTexture("Interface/newui_item_table01(L).tga");
            _texTableTopRight = await tl.PrepareAndGetTexture("Interface/newui_item_table01(R).tga");
            _texTableBottomLeft = await tl.PrepareAndGetTexture("Interface/newui_item_table02(L).tga");
            _texTableBottomRight = await tl.PrepareAndGetTexture("Interface/newui_item_table02(R).tga");
            _texTableTopPixel = await tl.PrepareAndGetTexture("Interface/newui_item_table03(Up).tga");
            _texTableBottomPixel = await tl.PrepareAndGetTexture("Interface/newui_item_table03(Dw).tga");
            _texTableLeftPixel = await tl.PrepareAndGetTexture("Interface/newui_item_table03(L).tga");
            _texTableRightPixel = await tl.PrepareAndGetTexture("Interface/newui_item_table03(R).tga");

            _texBackground = await tl.PrepareAndGetTexture("Interface/newui_msgbox_back.jpg");

            _font = GraphicsManager.Instance.Font;

            await base.Load();

            if (_zenLabel != null)
            {
                await _zenLabel.Load();
                UpdateZenLabelPosition();
            }
        }

        public void Show()
        {
            UpdateZenLabel();
            RefreshInventoryContent();
            Visible = true;
            BringToFront();
            Scene.FocusControl = this;
            if (_zenLabel != null)
            {
                _zenLabel.Visible = true;
                UpdateZenLabelPosition();
            }
        }

        public void Hide()
        {
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
            _items.Clear();
            _itemGrid = new InventoryItem[Columns, Rows];

            var characterItems = _networkManager.GetCharacterState().GetInventoryItems();

            string defaultItemIconTexturePath = "Interface/newui_item_box.tga";

            const int InventorySlotOffset = 12; // first inv slot

            foreach (var entry in characterItems)
            {
                byte slotIndex = entry.Key;
                byte[] itemData = entry.Value;

                int adjustedIndex = slotIndex - InventorySlotOffset;
                if (adjustedIndex < 0)
                {
                    _logger.LogWarning($"SlotIndex {slotIndex} is below inventory offset. Skipping.");
                    continue;
                }

                int gridX = adjustedIndex % Columns;
                int gridY = adjustedIndex / Columns;

                if (gridX >= Columns || gridY >= Rows)
                {
                    string itemName = ItemDatabase.GetItemName(itemData) ?? "Unknown Item";
                    _logger.LogWarning($"Item at slot {slotIndex} ({itemName}) has invalid grid position ({gridX},{gridY}). Skipping.");
                    continue;
                }

                string itemNameFinal = ItemDatabase.GetItemName(itemData) ?? "Unknown Item";
                int itemVisualWidth = 1;
                int itemVisualHeight = 1;

                ItemDefinition itemDef = new ItemDefinition(0, itemNameFinal, itemVisualWidth, itemVisualHeight, defaultItemIconTexturePath);
                InventoryItem newItem = new InventoryItem(itemDef, new Point(gridX, gridY));

                if (!AddItem(newItem))
                {
                    _logger.LogWarning($"Failed to add item '{itemNameFinal}' to inventory UI at slot {slotIndex}. Slot might be occupied unexpectedly.");
                }
            }

            foreach (var item in _items)
            {
                if (!string.IsNullOrEmpty(item.Definition.TexturePath))
                {
                    TextureLoader.Instance.Prepare(item.Definition.TexturePath);
                }
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

            Point mousePos = MuGame.Instance.Mouse.Position;
            _hoveredItem = null;
            _hoveredSlot = new Point(-1, -1);

            // Picking up/dropping logic
            if (IsMouseOver) // Checks if mouse is over the entire InventoryControl
            {
                Point gridSlot = GetSlotAtScreenPosition(mousePos);
                _hoveredSlot = gridSlot; // Always update, even if outside the grid (-1,-1)

                if (gridSlot.X != -1) // Mouse is over the grid
                {
                    _hoveredItem = _itemGrid[gridSlot.X, gridSlot.Y];

                    if (MuGame.Instance.Mouse.LeftButton == ButtonState.Pressed && MuGame.Instance.PrevMouseState.LeftButton == ButtonState.Released)
                    {
                        if (_pickedItemRenderer.Item != null) // We have a picked up item -> trying to drop
                        {
                            if (CanPlaceItem(_pickedItemRenderer.Item, gridSlot))
                            {
                                InventoryItem itemToPlace = _pickedItemRenderer.Item;
                                itemToPlace.GridPosition = gridSlot;
                                AddItem(itemToPlace); // Adds to _items and _itemGrid
                                _pickedItemRenderer.ReleaseItem();
                            }
                            // If cannot drop, the item remains picked up
                        }
                        else if (_hoveredItem != null) // We don't have a picked up item -> trying to pick up
                        {
                            _pickedItemRenderer.PickUpItem(_hoveredItem);
                            RemoveItemFromGrid(_hoveredItem);
                            _items.Remove(_hoveredItem);
                            _hoveredItem = null; // No longer hovering over it, because it's picked up
                        }
                    }
                }
            }

            _pickedItemRenderer.Update(gameTime);
        }

        public void HookEvents()
        {
            _networkManager.GetCharacterState().InventoryChanged += RefreshInventoryContent;
        }

        private Point GetSlotAtScreenPosition(Point screenPos)
        {
            if (DisplayRectangle.Width <= 0 || DisplayRectangle.Height <= 0)
                return new Point(-1, -1);

            Point localPos = new Point(screenPos.X - DisplayRectangle.X - _gridOffset.X,
                                       screenPos.Y - DisplayRectangle.Y - _gridOffset.Y);

            if (localPos.X < 0 || localPos.Y < 0 ||
                localPos.X >= Columns * INVENTORY_SQUARE_WIDTH ||
                localPos.Y >= Rows * INVENTORY_SQUARE_HEIGHT)
            {
                return new Point(-1, -1); // Outside grid
            }

            return new Point(
                Math.Min(Columns - 1, localPos.X / INVENTORY_SQUARE_WIDTH),
                Math.Min(Rows - 1, localPos.Y / INVENTORY_SQUARE_HEIGHT)
            );
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible) return;

            using (new SpriteBatchScope(GraphicsManager.Instance.Sprite, SpriteSortMode.Deferred, BlendState.AlphaBlend))
            {
                Rectangle displayRect = DisplayRectangle;
                if (_texBackground != null)
                    GraphicsManager.Instance.Sprite.Draw(_texBackground, displayRect, Color.White);

                DrawFrame(GraphicsManager.Instance.Sprite, displayRect);
                DrawGrid(GraphicsManager.Instance.Sprite, displayRect);
                DrawItems(GraphicsManager.Instance.Sprite, displayRect);
                _pickedItemRenderer.Draw(gameTime);
                DrawTooltip(GraphicsManager.Instance.Sprite, displayRect);
            }

            base.Draw(gameTime); // This will draw all children, including the ZEN label
        }

        private void DrawFrame(SpriteBatch spriteBatch, Rectangle frameRect)
        {
            int topEdge = WND_TOP_EDGE;
            int leftEdge = WND_LEFT_EDGE;
            int bottomEdge = WND_BOTTOM_EDGE;
            int rightEdge = WND_RIGHT_EDGE;

            int cornerSize = 14;

            // Corners
            if (_texTableTopLeft != null) spriteBatch.Draw(_texTableTopLeft, new Rectangle(frameRect.X, frameRect.Y, cornerSize, cornerSize), Color.White);
            if (_texTableTopRight != null) spriteBatch.Draw(_texTableTopRight, new Rectangle(frameRect.Right - cornerSize, frameRect.Y, cornerSize, cornerSize), Color.White);
            if (_texTableBottomLeft != null) spriteBatch.Draw(_texTableBottomLeft, new Rectangle(frameRect.X, frameRect.Bottom - cornerSize, cornerSize, cornerSize), Color.White);
            if (_texTableBottomRight != null) spriteBatch.Draw(_texTableBottomRight, new Rectangle(frameRect.Right - cornerSize, frameRect.Bottom - cornerSize, cornerSize, cornerSize), Color.White);

            // Horizontal edges (pixels)
            if (_texTableTopPixel != null)
                spriteBatch.Draw(_texTableTopPixel, new Rectangle(frameRect.X + cornerSize, frameRect.Y, frameRect.Width - 2 * cornerSize, cornerSize), Color.White);
            if (_texTableBottomPixel != null)
                spriteBatch.Draw(_texTableBottomPixel, new Rectangle(frameRect.X + cornerSize, frameRect.Bottom - cornerSize, frameRect.Width - 2 * cornerSize, cornerSize), Color.White);

            // Vertical edges (pixels)
            if (_texTableLeftPixel != null)
                spriteBatch.Draw(_texTableLeftPixel, new Rectangle(frameRect.X, frameRect.Y + cornerSize, cornerSize, frameRect.Height - 2 * cornerSize), Color.White);
            if (_texTableRightPixel != null)
                spriteBatch.Draw(_texTableRightPixel, new Rectangle(frameRect.Right - cornerSize, frameRect.Y + cornerSize, cornerSize, frameRect.Height - 2 * cornerSize), Color.White);

            // Title text (as in CNewUIInventoryExtension)
            var font = GraphicsManager.Instance.Font;
            if (font != null)
            {
                string title = "Inventory"; // Can be made a property
                Vector2 titleSize = font.MeasureString(title);
                float titleScale = 0.7f; // Adjust scale
                Vector2 scaledTitleSize = titleSize * titleScale;
                Vector2 titlePos = new Vector2(
                    frameRect.X + (frameRect.Width - scaledTitleSize.X) / 2,
                    frameRect.Y + 12 // Adjust Y
                );
                spriteBatch.DrawString(font, title, titlePos, Color.WhiteSmoke, 0, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
            }
        }

        private void DrawGrid(SpriteBatch spriteBatch, Rectangle frameRect)
        {
            Point gridTopLeft = new Point(frameRect.X + _gridOffset.X, frameRect.Y + _gridOffset.Y);

            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                {
                    Rectangle slotRect = new Rectangle(
                        gridTopLeft.X + x * INVENTORY_SQUARE_WIDTH,
                        gridTopLeft.Y + y * INVENTORY_SQUARE_HEIGHT,
                        INVENTORY_SQUARE_WIDTH,
                        INVENTORY_SQUARE_HEIGHT);

                    // Draw slot background using _texSquare texture if available
                    if (_texSquare != null)
                    {
                        Rectangle expandedTextureRect = new Rectangle(
                            slotRect.X,
                            slotRect.Y,
                            slotRect.Width + 15,
                            slotRect.Height + 15);

                        spriteBatch.Draw(_texSquare, expandedTextureRect, Color.White);
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
                    if (_pickedItemRenderer.Item != null && IsMouseOverGrid())
                    {
                        Point currentSlot = new Point(x, y);
                        Color? highlightColor = GetSlotHighlightColor(currentSlot, _pickedItemRenderer.Item);

                        if (highlightColor.HasValue)
                        {
                            spriteBatch.Draw(GraphicsManager.Instance.Pixel, slotRect, highlightColor.Value);
                        }
                    }
                    else if (IsMouseOverGrid() && _hoveredSlot.X == x && _hoveredSlot.Y == y && _pickedItemRenderer.Item == null)
                    {
                        spriteBatch.Draw(GraphicsManager.Instance.Pixel, slotRect, Color.Yellow * 0.3f);
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

        private bool IsMouseOverGrid()
        {
            Point mousePos = MuGame.Instance.Mouse.Position;
            Rectangle gridScreenRect = new Rectangle(
                DisplayRectangle.X + _gridOffset.X,
                DisplayRectangle.Y + _gridOffset.Y,
                Columns * INVENTORY_SQUARE_WIDTH,
                Rows * INVENTORY_SQUARE_HEIGHT);
            return gridScreenRect.Contains(mousePos);
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
            Point gridTopLeft = new Point(frameRect.X + _gridOffset.X, frameRect.Y + _gridOffset.Y);
            var font = GraphicsManager.Instance.Font;

            foreach (var item in _items)
            {
                if (item == _pickedItemRenderer.Item) continue;

                Rectangle itemRect = new Rectangle(
                    gridTopLeft.X + item.GridPosition.X * INVENTORY_SQUARE_WIDTH,
                    gridTopLeft.Y + item.GridPosition.Y * INVENTORY_SQUARE_HEIGHT,
                    item.Definition.Width * INVENTORY_SQUARE_WIDTH,
                    item.Definition.Height * INVENTORY_SQUARE_HEIGHT);

                Texture2D itemTexture = null;
                if (!string.IsNullOrEmpty(item.Definition.TexturePath))
                {
                    itemTexture = TextureLoader.Instance.GetTexture2D(item.Definition.TexturePath);
                }

                if (itemTexture != null)
                {
                    spriteBatch.Draw(itemTexture, itemRect, Color.White);
                }
                else
                {
                    // Fallback: rysuj szary kwadrat, jeśli tekstura nie jest dostępna (np. jeszcze się ładuje)
                    spriteBatch.Draw(GraphicsManager.Instance.Pixel, itemRect, Color.DarkSlateGray);
                }

                spriteBatch.Draw(GraphicsManager.Instance.Pixel, new Rectangle(itemRect.X, itemRect.Y, itemRect.Width, 1), Color.White);
                spriteBatch.Draw(GraphicsManager.Instance.Pixel, new Rectangle(itemRect.X, itemRect.Bottom - 1, itemRect.Width, 1), Color.White);
                spriteBatch.Draw(GraphicsManager.Instance.Pixel, new Rectangle(itemRect.X, itemRect.Y, 1, itemRect.Height), Color.White);
                spriteBatch.Draw(GraphicsManager.Instance.Pixel, new Rectangle(itemRect.Right - 1, itemRect.Y, 1, itemRect.Height), Color.White);

                if (font != null && !string.IsNullOrEmpty(item.Definition.Name))
                {
                    float textScale = 0.4f;
                    string text = item.Definition.Name;
                    Vector2 textSize = font.MeasureString(text) * textScale;
                    Vector2 textPos = new Vector2(
                        itemRect.X + (itemRect.Width - textSize.X) / 2,
                        itemRect.Y + (itemRect.Height - textSize.Y) / 2);
                    spriteBatch.DrawString(font, text, textPos, Color.White, 0, Vector2.Zero, textScale, SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawTooltip(SpriteBatch spriteBatch, Rectangle frameRect)
        {
            if (_hoveredItem != null && _pickedItemRenderer.Item == null && _font != null)
            {
                string text = $"{_hoveredItem.Definition.Name}\nLvl: {_hoveredItem.Level} Dur: {_hoveredItem.Durability}";
                Vector2 textSize = _font.MeasureString(text) * 0.6f;
                Point mousePos = MuGame.Instance.Mouse.Position;

                Rectangle tooltipRect = new Rectangle(
                    mousePos.X + 15,
                    mousePos.Y + 15,
                    (int)textSize.X + 10,
                    (int)textSize.Y + 6);

                // Adjust position so the tooltip does not go off-screen
                if (tooltipRect.Right > frameRect.Right - 10)
                    tooltipRect.X = frameRect.Right - 10 - tooltipRect.Width;
                if (tooltipRect.Bottom > frameRect.Bottom - 10)
                    tooltipRect.Y = frameRect.Bottom - 10 - tooltipRect.Height;

                spriteBatch.Draw(GraphicsManager.Instance.Pixel, tooltipRect, Color.Black * 0.85f);
                spriteBatch.DrawString(_font, text, new Vector2(tooltipRect.X + 5, tooltipRect.Y + 3), Color.White, 0, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);
            }
        }
    }
}