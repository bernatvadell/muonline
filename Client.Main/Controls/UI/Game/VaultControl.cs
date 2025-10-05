using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Content;
using Client.Main.Helpers;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using Client.Main.Controllers;
using MUnique.OpenMU.Network.Packets;

namespace Client.Main.Controls.UI.Game
{
    public class VaultControl : DynamicLayoutControl
    {
        protected override string LayoutJsonResource => "Client.Main.Controls.UI.Game.Layouts.NpcShopLayout.json";
        protected override string TextureRectJsonResource => "Client.Main.Controls.UI.Game.Layouts.NpcShopRect.json";
        protected override string DefaultTexturePath => "Interface/GFx/NpcShop_I3.ozd";
        private static VaultControl _instance;

        private const int VAULT_COLUMNS = 8;
        private const int VAULT_ROWS = 14; // mimic shop layout for now
        private const int CELL_W = 25;
        private const int CELL_H = 25;

        private readonly List<InventoryItem> _items = new();
        private Point _gridTopLeft = new Point(170, 180);
        private SpriteFont _font;
        private Texture2D _slotTexture;
        private GameTime _currentGameTime;
        private InventoryItem _hoveredItem;
        private InventoryItem[,] _grid;
        private InventoryItem _dragItem;
        private Point _dragOriginal;
        private readonly PickedItemRenderer _pickedRenderer = new();
        public InventoryItem GetDraggedItem() => _dragItem;

        public VaultControl()
        {
            Visible = false;
            Interactive = true;
            _grid = new InventoryItem[VAULT_COLUMNS, VAULT_ROWS]; // ensure non-null occupancy grid

            // grid backdrop
            var rows = VAULT_ROWS;
            var cols = VAULT_COLUMNS;
            var ScreenX = _gridTopLeft.X;
            var ScreenY = _gridTopLeft.Y;
            for (var i = 0; i < rows; i++)
            {
                for (var j = 0; j < cols; j++)
                {
                    var textureCtrl = new TextureControl
                    {
                        AutoViewSize = false,
                        TexturePath = DefaultTexturePath,
                        BlendState = BlendState.AlphaBlend,
                        Name = "Cell-" + i + j,
                    };
                    textureCtrl.TextureRectangle = new Rectangle { X = 545, Y = 217, Width = 29, Height = 31 };
                    textureCtrl.Tag = new LayoutInfo
                    {
                        Name = "Cell",
                        ScreenX = ScreenX,
                        ScreenY = ScreenY,
                        Width = 29,
                        Height = 29,
                        Z = 5
                    };
                    Controls.Add(textureCtrl);
                    ScreenX += CELL_W;
                }
                ScreenY += CELL_H;
                ScreenX = _gridTopLeft.X;
            }

            var state = MuGame.Network?.GetCharacterState();
            if (state != null)
            {
                state.VaultItemsChanged += RefreshVaultContent;
            }
        }

        public static VaultControl Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new VaultControl();
                }
                return _instance;
            }
        }

        public override async System.Threading.Tasks.Task Load()
        {
            await base.Load();
            _font = GraphicsManager.Instance.Font;
            _slotTexture = await TextureLoader.Instance.PrepareAndGetTexture(DefaultTexturePath);
        }

        // Removed earlier simple Update; merged into the full Update below

        private void RefreshVaultContent()
        {
            _items.Clear();
            var state = MuGame.Network?.GetCharacterState();
            if (state == null) return;

            var dict = state.GetVaultItems();
            foreach (var kv in dict)
            {
                byte slot = kv.Key;
                byte[] data = kv.Value;
                int gx = slot % VAULT_COLUMNS;
                int gy = slot / VAULT_COLUMNS;
                var def = ItemDatabase.GetItemDefinition(data) ?? new ItemDefinition(0, ItemDatabase.GetItemName(data) ?? "Unknown Item", 1, 1, "Interface/newui_item_box.tga");
                var it = new InventoryItem(def, new Point(gx, gy), data);
                if (data.Length > 2) it.Durability = data[2];
                _items.Add(it);
            }

            foreach (var it in _items)
            {
                if (!string.IsNullOrEmpty(it.Definition.TexturePath))
                    _ = TextureLoader.Instance.Prepare(it.Definition.TexturePath);
            }

            if (_items.Count > 0)
            {
                Visible = true;
                BringToFront();
                SoundController.Instance.PlayBuffer("Sound/iCreateWindow.wav");
            }

            // rebuild occupancy grid
            _grid = new InventoryItem[VAULT_COLUMNS, VAULT_ROWS];
            foreach (var it in _items)
            {
                for (int y = 0; y < it.Definition.Height; y++)
                    for (int x = 0; x < it.Definition.Width; x++)
                    {
                        int gx = it.GridPosition.X + x;
                        int gy = it.GridPosition.Y + y;
                        if (gx >= 0 && gx < VAULT_COLUMNS && gy >= 0 && gy < VAULT_ROWS)
                            _grid[gx, gy] = it;
                    }
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible) return;
            base.Draw(gameTime);
            var sprite = GraphicsManager.Instance.Sprite;
            using (new SpriteBatchScope(sprite, SpriteSortMode.Deferred, BlendState.AlphaBlend, transform: UiScaler.SpriteTransform))
            {
                var font = _font ?? GraphicsManager.Instance.Font;
                Point origin = _gridTopLeft;
                _hoveredItem = null;
                var mouse = MuGame.Instance.UiMouseState.Position;
                var hoveredSlot = GetSlotAtScreenPosition(mouse);

                var snapshot = _items.ToArray();
                foreach (var item in snapshot)
                {
                    var rect = new Rectangle(
                        origin.X + item.GridPosition.X * CELL_W,
                        origin.Y + item.GridPosition.Y * CELL_H,
                        item.Definition.Width * CELL_W,
                        item.Definition.Height * CELL_H);

                    bool isHovered = rect.Contains(mouse);
                    if (isHovered) _hoveredItem = item;

                    Texture2D tex = null;
                    if (!string.IsNullOrEmpty(item.Definition.TexturePath))
                    {
                        tex = TextureLoader.Instance.GetTexture2D(item.Definition.TexturePath);
                        if (tex == null && item.Definition.TexturePath.EndsWith(".bmd", System.StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                int w = rect.Width; int h = rect.Height;
                                tex = isHovered && _currentGameTime != null
                                    ? BmdPreviewRenderer.GetAnimatedPreview(item, w, h, _currentGameTime)
                                    : BmdPreviewRenderer.GetPreview(item, w, h);
                            }
                            catch { }
                        }
                    }

                    if (tex != null) sprite.Draw(tex, rect, Color.White);
                    else sprite.Draw(GraphicsManager.Instance.Pixel, rect, Color.DarkSlateGray * 0.8f);

                    if (item.Definition.BaseDurability == 0 && item.Durability > 1)
                    {
                        string qty = item.Durability.ToString();
                        var size = font.MeasureString(qty) * 0.4f;
                        var pos = new Vector2(rect.Right - size.X - 2, rect.Y + 2);
                        for (int dx = -1; dx <= 1; dx++)
                            for (int dy = -1; dy <= 1; dy++)
                                if (dx != 0 || dy != 0)
                                    sprite.DrawString(font, qty, pos + new Vector2(dx, dy), Color.Black, 0, Vector2.Zero, 0.4f, SpriteEffects.None, 0);
                        sprite.DrawString(font, qty, pos, new Color(255, 255, 180), 0, Vector2.Zero, 0.4f, SpriteEffects.None, 0);
                    }
                }
                // Hover highlights (when not dragging)
                if (_dragItem == null && hoveredSlot.X >= 0)
                {
                    // Highlight hovered slot
                    var hoveredRect = new Rectangle(
                        origin.X + hoveredSlot.X * CELL_W,
                        origin.Y + hoveredSlot.Y * CELL_H,
                        CELL_W, CELL_H);
                    sprite.Draw(GraphicsManager.Instance.Pixel, hoveredRect, Color.Yellow * 0.3f);

                    // Highlight all slots occupied by hovered item
                    if (_hoveredItem != null)
                    {
                        for (int y = 0; y < _hoveredItem.Definition.Height; y++)
                        {
                            for (int x = 0; x < _hoveredItem.Definition.Width; x++)
                            {
                                int gx = _hoveredItem.GridPosition.X + x;
                                int gy = _hoveredItem.GridPosition.Y + y;
                                if (gx == hoveredSlot.X && gy == hoveredSlot.Y) continue; // keep yellow for hovered cell
                                var r = new Rectangle(
                                    origin.X + gx * CELL_W,
                                    origin.Y + gy * CELL_H,
                                    CELL_W, CELL_H);
                                sprite.Draw(GraphicsManager.Instance.Pixel, r, Color.Blue * 0.3f);
                            }
                        }
                    }
                }
                // Drag preview is drawn globally in GameScene to ensure top-most z-order
                // Highlight slots for drop when dragging (from vault or from inventory)
                var scene = MuGame.Instance.ActiveScene;
                var invCtrl = scene?.Controls?.OfType<Inventory.InventoryControl>()?.FirstOrDefault();
                var dragged = _dragItem ?? invCtrl?._pickedItemRenderer?.Item;
                if (dragged != null)
                {
                    var slot = GetSlotAtScreenPosition(mouse);
                    if (slot.X >= 0)
                    {
                        Color color = CanPlace(dragged, slot) ? Color.GreenYellow * 0.5f : Color.Red * 0.6f;
                        for (int y = 0; y < dragged.Definition.Height; y++)
                        {
                            for (int x = 0; x < dragged.Definition.Width; x++)
                            {
                                var r = new Rectangle(
                                    origin.X + (slot.X + x) * CELL_W,
                                    origin.Y + (slot.Y + y) * CELL_H,
                                    CELL_W, CELL_H);
                                sprite.Draw(GraphicsManager.Instance.Pixel, r, color);
                            }
                        }
                    }
                }
            }
        }

        public override void DrawAfter(GameTime gameTime)
        {
            if (!Visible) return;
            base.DrawAfter(gameTime);
            var sprite = GraphicsManager.Instance.Sprite;
            using (new SpriteBatchScope(sprite, SpriteSortMode.Deferred, BlendState.AlphaBlend, transform: UiScaler.SpriteTransform))
            {
                DrawTooltip(sprite, DisplayRectangle);
            }
        }

        public override void Update(GameTime gameTime)
        {
            // ESC to close
            var k = Keyboard.GetState();
            if (k.IsKeyDown(Keys.Escape))
            {
                CloseWindow();
                var svc = MuGame.Network?.GetCharacterService();
                if (svc != null)
                {
                    _ = svc.SendCloseNpcRequestAsync();
                }
            }

            _currentGameTime = gameTime;
            var mouse = MuGame.Instance.UiMouseState;
            var prev = MuGame.Instance.PrevUiMouseState;
            bool leftJustPressed = mouse.LeftButton == ButtonState.Pressed && prev.LeftButton == ButtonState.Released;
            bool leftJustReleased = mouse.LeftButton == ButtonState.Released && prev.LeftButton == ButtonState.Pressed;

            // Click-pick / click-drop behavior (like Inventory):
            if (leftJustPressed && _dragItem == null && _hoveredItem != null)
            {
                _dragItem = _hoveredItem;
                _dragOriginal = _dragItem.GridPosition;
                RemoveFromGrid(_dragItem);
                _items.Remove(_dragItem);
                _pickedRenderer.PickUpItem(_dragItem);
            }
            else if (leftJustPressed && _dragItem != null)
            {
                // compute drop target in vault grid
                var drop = GetSlotAtScreenPosition(mouse.Position);
                bool placed = false;
                if (drop.X >= 0 && CanPlace(_dragItem, drop))
                {
                    // Same slot? No-op
                    if (drop == _dragOriginal)
                    {
                        _dragItem.GridPosition = _dragOriginal;
                        PlaceOnGrid(_dragItem);
                        _items.Add(_dragItem);
                        _pickedRenderer.ReleaseItem();
                        _dragItem = null;
                        base.Update(gameTime);
                        return;
                    }
                    placed = true;
                    // send move vault->vault
                    byte fromSlot = (byte)(_dragOriginal.Y * VAULT_COLUMNS + _dragOriginal.X);
                    byte toSlot = (byte)(drop.Y * VAULT_COLUMNS + drop.X);
                    _dragItem.GridPosition = drop;
                    PlaceOnGrid(_dragItem);
                    _items.Add(_dragItem);
                    var svc = MuGame.Network?.GetCharacterService();
                    var state = MuGame.Network?.GetCharacterState();
                    var raw = _dragItem.RawData ?? System.Array.Empty<byte>();
                    if (svc != null)
                    {
                        state?.StashPendingVaultMove(fromSlot, toSlot);
                        _ = System.Threading.Tasks.Task.Run(async () =>
                        {
                            await svc.SendStorageItemMoveAsync(ItemStorageKind.Vault, fromSlot, ItemStorageKind.Vault, toSlot, MuGame.Network.TargetVersion, raw);
                            // Fallback refresh if no ACK in time (do NOT consume the pending state)
                            await System.Threading.Tasks.Task.Delay(1200);
                            if (MuGame.Network != null && MuGame.Network.GetCharacterState().IsVaultMovePending(fromSlot, toSlot))
                            {
                                MuGame.ScheduleOnMainThread(() => MuGame.Network.GetCharacterState().RaiseVaultItemsChanged());
                            }
                        });
                    }
                }
                else
                {
                    // try drop into inventory
                    var scene = MuGame.Instance.ActiveScene;
                    var invCtrl = scene?.Controls?.OfType<Inventory.InventoryControl>()?.FirstOrDefault();
                    if (invCtrl != null && invCtrl.Visible && invCtrl.DisplayRectangle.Contains(mouse.Position))
                    {
                        // calculate inventory slot similar to InventoryControl
                        const int invCols = Inventory.InventoryControl.Columns;
                        const int invRows = Inventory.InventoryControl.Rows;
                        int invCellW = Inventory.InventoryControl.INVENTORY_SQUARE_WIDTH;
                        int invCellH = Inventory.InventoryControl.INVENTORY_SQUARE_HEIGHT;
                        // hardcoded offsets consistent with InventoryControl
                        Point invOffset = new Point(50, 80);
                        Point local = new Point(mouse.X - invCtrl.DisplayRectangle.X - invOffset.X, mouse.Y - invCtrl.DisplayRectangle.Y - invOffset.Y);
                        if (local.X >= 0 && local.Y >= 0)
                        {
                            int sx = local.X / invCellW; int sy = local.Y / invCellH;
                            if (sx >= 0 && sx < invCols && sy >= 0 && sy < invRows)
                            {
                                byte fromSlot = (byte)(_dragOriginal.Y * VAULT_COLUMNS + _dragOriginal.X);
                                byte toSlot = (byte)(12 + sy * invCols + sx); // inventory starts at 12
                                var svc = MuGame.Network?.GetCharacterService();
                                if (svc != null && invCtrl.CanPlaceAt(new Point(sx, sy), _dragItem))
                                {
                                    var raw = _dragItem.RawData ?? System.Array.Empty<byte>();
                                    _ = System.Threading.Tasks.Task.Run(async () =>
                                    {
                                        var state2 = MuGame.Network?.GetCharacterState();
                                        state2?.StashPendingVaultMove(fromSlot, 0xFF);
                                        await svc.SendStorageItemMoveAsync(ItemStorageKind.Vault, fromSlot, ItemStorageKind.Inventory, toSlot, MuGame.Network.TargetVersion, raw);
                                        // Fallback refresh if no ACK in time (do NOT consume the pending state)
                                        await System.Threading.Tasks.Task.Delay(1200);
                                        if (MuGame.Network != null && state2 != null && state2.IsVaultMovePending(fromSlot, 0xFF))
                                        {
                                            MuGame.ScheduleOnMainThread(() =>
                                            {
                                                state2.RaiseVaultItemsChanged();
                                                state2.RaiseInventoryChanged();
                                            });
                                        }
                                    });
                                }
                                else
                                {
                                    // invalid placement on inventory -> not placed
                                    placed = false;
                                }
                                // optimistic: don't put back into vault grid
                                if (svc != null && invCtrl.CanPlaceAt(new Point(sx, sy), _dragItem))
                                    placed = true;
                            }
                        }
                    }
                }

                if (!placed)
                {
                    // restore original
                    _dragItem.GridPosition = _dragOriginal;
                    PlaceOnGrid(_dragItem);
                    _items.Add(_dragItem);
                }

                _pickedRenderer.ReleaseItem();
                _dragItem = null;
            }

            _pickedRenderer.Update(gameTime);
            base.Update(gameTime);
        }

        /// <summary>
        /// Closes the vault window safely, restoring any dragged item back to its original slot.
        /// </summary>
        public void CloseWindow()
        {
            // If a vault item is being dragged, restore it to original slot before closing
            if (_dragItem != null)
            {
                _dragItem.GridPosition = _dragOriginal;
                PlaceOnGrid(_dragItem);
                if (!_items.Contains(_dragItem))
                    _items.Add(_dragItem);
                _pickedRenderer.ReleaseItem();
                _dragItem = null;
            }
            Visible = false;
        }

        public Point GetSlotAtScreenPosition(Point screenPos)
        {
            Point local = new Point(screenPos.X - DisplayRectangle.X - _gridTopLeft.X, screenPos.Y - DisplayRectangle.Y - _gridTopLeft.Y);
            if (local.X < 0 || local.Y < 0) return new Point(-1, -1);
            int sx = local.X / CELL_W; int sy = local.Y / CELL_H;
            if (sx < 0 || sx >= VAULT_COLUMNS || sy < 0 || sy >= VAULT_ROWS) return new Point(-1, -1);
            return new Point(sx, sy);
        }

        private bool CanPlace(InventoryItem it, Point topLeft)
        {
            if (it == null) return false;
            if (topLeft.X < 0 || topLeft.Y < 0 || topLeft.X + it.Definition.Width > VAULT_COLUMNS || topLeft.Y + it.Definition.Height > VAULT_ROWS) return false;
            for (int y = 0; y < it.Definition.Height; y++)
                for (int x = 0; x < it.Definition.Width; x++)
                {
                    if (_grid[topLeft.X + x, topLeft.Y + y] != null) return false;
                }
            return true;
        }

        private void PlaceOnGrid(InventoryItem it)
        {
            for (int y = 0; y < it.Definition.Height; y++)
                for (int x = 0; x < it.Definition.Width; x++)
                {
                    int gx = it.GridPosition.X + x; int gy = it.GridPosition.Y + y;
                    if (gx >= 0 && gx < VAULT_COLUMNS && gy >= 0 && gy < VAULT_ROWS)
                        _grid[gx, gy] = it;
                }
        }

        private void RemoveFromGrid(InventoryItem it)
        {
            for (int y = 0; y < it.Definition.Height; y++)
                for (int x = 0; x < it.Definition.Width; x++)
                {
                    int gx = it.GridPosition.X + x; int gy = it.GridPosition.Y + y;
                    if (_grid != null && gx >= 0 && gx < VAULT_COLUMNS && gy >= 0 && gy < VAULT_ROWS)
                        _grid[gx, gy] = null;
                }
        }

        private static List<(string txt, Color col)> BuildTooltipLines(InventoryItem it)
        {
            var d = it.Details;
            var li = new List<(string, Color)>();
            string name = d.IsExcellent ? $"Excellent {it.Definition.Name}" : d.IsAncient ? $"Ancient {it.Definition.Name}" : it.Definition.Name;
            if (d.Level > 0) name += $" +{d.Level}";
            li.Add((name, Color.White));
            var def = it.Definition;
            if (def.DamageMin > 0 || def.DamageMax > 0)
            {
                string dmgType = def.TwoHanded ? "Two-hand" : "One-hand";
                li.Add(($"{dmgType} Damage : {def.DamageMin} ~ {def.DamageMax}", Color.Orange));
            }
            if (def.Defense > 0) li.Add(($"Defense     : {def.Defense}", Color.Orange));
            if (def.DefenseRate > 0) li.Add(($"Defense Rate: {def.DefenseRate}", Color.Orange));
            if (def.AttackSpeed > 0) li.Add(($"Attack Speed: {def.AttackSpeed}", Color.Orange));
            li.Add(($"Durability : {it.Durability}/{def.BaseDurability}", Color.Silver));
            if (def.RequiredLevel > 0) li.Add(($"Required Level   : {def.RequiredLevel}", Color.LightGray));
            if (def.RequiredStrength > 0) li.Add(($"Required Strength: {def.RequiredStrength}", Color.LightGray));
            if (def.RequiredDexterity > 0) li.Add(($"Required Agility : {def.RequiredDexterity}", Color.LightGray));
            if (def.RequiredEnergy > 0) li.Add(($"Required Energy  : {def.RequiredEnergy}", Color.LightGray));
            if (def.AllowedClasses != null && def.AllowedClasses.Count > 0)
            {
                foreach (string cls in def.AllowedClasses)
                    li.Add(($"Can be equipped by {cls}", Color.LightGray));
            }
            if (d.OptionLevel > 0) li.Add(($"Additional Option : +{d.OptionLevel * 4}", new Color(80, 255, 80)));
            if (d.HasLuck) li.Add(("+Luck  (Crit +5 %, Jewel +25 %)", Color.CornflowerBlue));
            if (d.HasSkill) li.Add(("+Skill (Right mouse click - skill)", Color.CornflowerBlue));
            if (d.IsExcellent)
            {
                byte excByte = it.RawData.Length > 3 ? it.RawData[3] : (byte)0;
                foreach (var s in ItemDatabase.ParseExcellentOptions(excByte)) li.Add(($"+{s}", new Color(128, 255, 128)));
            }
            if (d.IsAncient) li.Add(("Ancient Option", new Color(0, 255, 128)));
            return li;
        }

        private void DrawTooltip(SpriteBatch sb, Rectangle frameRect)
        {
            if (_hoveredItem == null || _font == null) return;
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
            Rectangle screenBounds = new Rectangle(0, 0, UiScaler.VirtualSize.X, UiScaler.VirtualSize.Y);
            Point gridTopLeft = _gridTopLeft + new Point(DisplayRectangle.X, DisplayRectangle.Y);
            Rectangle hoveredItemRect = new Rectangle(
                gridTopLeft.X + _hoveredItem.GridPosition.X * CELL_W,
                gridTopLeft.Y + _hoveredItem.GridPosition.Y * CELL_H,
                _hoveredItem.Definition.Width * CELL_W,
                _hoveredItem.Definition.Height * CELL_H);
            Rectangle r = new(m.X + 15, m.Y + 15, w, h);
            if (r.Intersects(hoveredItemRect))
            {
                r.X = hoveredItemRect.X - w - 10; r.Y = hoveredItemRect.Y;
                if (r.Intersects(hoveredItemRect) || r.X < screenBounds.X + 10)
                { r.X = hoveredItemRect.X; r.Y = hoveredItemRect.Y - h - 10;
                  if (r.Intersects(hoveredItemRect) || r.Y < screenBounds.Y + 10)
                  { r.X = hoveredItemRect.X; r.Y = hoveredItemRect.Bottom + 10; } }
            }
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
            { Vector2 size = _font.MeasureString(t) * scale;
              sb.DrawString(_font, t, new Vector2(r.X + (r.Width - size.X) / 2, y), col, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
              y += (int)size.Y + 2; }
        }

        // Public helper for other controls to validate placement against vault grid
        public bool CanPlaceAt(Point topLeft, InventoryItem item) => CanPlace(item, topLeft);

        // Draws the picked item preview (called from GameScene at top-most layer)
        public void DrawPickedPreview(SpriteBatch sprite, GameTime gameTime) => _pickedRenderer.Draw(sprite, gameTime);
    }
}
