// DroppedItemObject.cs
using Client.Main.Controllers;              // GraphicsManager
using Client.Main.Core.Models;              // ScopeObject
using Client.Main.Models;                   // MessageType
using Client.Main.Networking.Services;      // CharacterService
using Client.Main.Controls.UI;              // ChatLogWindow + LabelControl
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;
using System.Threading.Tasks;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using System.Text;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Helpers;
using Client.Main.Content;

namespace Client.Main.Objects
{
    /// <summary>
    /// Dropped item or Zen; the label disappears only when the server
    /// removes the object from scope.
    /// </summary>
    public class DroppedItemObject : WorldObject
    {
        // ─────────────────── constants
        private const float HeightOffset = 90f;
        private const float PickupRange = 300f;
        private const float LabelScale = 0.6f;
        private const float LabelOffsetZ = 10f;
        private const int LabelPixelGap = 20;

        // ─────────────────── deps / state
        private readonly ScopeObject _scope;
        private readonly ushort _mainPlayerId;
        private readonly CharacterService _charSvc;
        private readonly ILogger<DroppedItemObject> _log;

        private SpriteFont _font;
        private LabelControl _label;
        private bool _pickedUp;
        private Texture2D _itemTexture;
        private readonly ItemDefinition _definition;
        private const float SpriteScale = 0.5f;
        private const float SpriteOffsetZ = 0f;

        // ─────────────────── public helpers
        public ushort RawId => _scope.RawId;
        public new string DisplayName { get; }

        // =====================================================================
        public DroppedItemObject(
              ScopeObject scope,
              ushort mainPlayerId,
              CharacterService charSvc,
              ILogger<DroppedItemObject> logger = null)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _mainPlayerId = mainPlayerId;
            _charSvc = charSvc ?? throw new ArgumentNullException(nameof(charSvc));
            _log = logger ?? ModelObject.AppLoggerFactory?.CreateLogger<DroppedItemObject>() ?? NullLogger<DroppedItemObject>.Instance;

            NetworkId = scope.Id;
            Interactive = true;

            Position = new(
                scope.PositionX * Constants.TERRAIN_SCALE + Constants.TERRAIN_SCALE / 2f,
                scope.PositionY * Constants.TERRAIN_SCALE + Constants.TERRAIN_SCALE / 2f,
                0f);

            string baseName = "Unknown Drop";
            ItemDatabase.ItemDetails itemDetails = default;
            _ = ReadOnlySpan<byte>.Empty;

            if (scope is ItemScopeObject itemScope)
            {
                ReadOnlySpan<byte> itemData = itemScope.ItemData.Span;
                baseName = itemScope.ItemDescription;
                itemDetails = ItemDatabase.ParseItemDetails(itemData);
                _definition = ItemDatabase.GetItemDefinition(itemData);
            }
            else if (scope is MoneyScopeObject moneyScope)
            {
                baseName = $"{moneyScope.Amount} Zen";
            }

            DisplayName = FormatItemDisplayName(baseName, itemDetails);

            _label = new LabelControl
            {
                Text = DisplayName,
                FontSize = 10f,
                TextColor = GetLabelColor(scope, itemDetails),
                HasShadow = true,
                ShadowColor = Color.Black,
                ShadowOpacity = 0.8f,
                UseManualPosition = true,
                Visible = false,
                Interactive = true,
                BackgroundColor = new Color(0, 0, 0, 160),
                Alpha = 1.0f,
                Padding = new Margin { Left = 4, Right = 4, Top = 2, Bottom = 2 }
            };

            _label.Tag = this;
        }

        // =====================================================================
        public override async Task Load()
        {
            await base.Load();

            if (World != null)
            {
                float z = World.Terrain.RequestTerrainHeight(Position.X, Position.Y);
                Position = new(Position.X, Position.Y, z + HeightOffset);
            }

            _font = GraphicsManager.Instance.Font;

            if (World?.Scene != null)
            {
                World.Scene.Controls.Add(_label);
                _label.Click += OnLabelClicked;
                await _label.Load();
            }
            else
            {
                _log.LogWarning("World.Scene == null - label will not be visible.");
            }

            if (_definition != null && !string.IsNullOrEmpty(_definition.TexturePath))
            {
                await TextureLoader.Instance.Prepare(_definition.TexturePath);
                _itemTexture = TextureLoader.Instance.GetTexture2D(_definition.TexturePath);
                if (_itemTexture == null && _definition.TexturePath.EndsWith(".bmd", StringComparison.OrdinalIgnoreCase))
                {
                    int w = Math.Max(60, _definition.Width * 60);
                    int h = Math.Max(60, _definition.Height * 60);
                    _itemTexture = BmdPreviewRenderer.GetPreview(_definition, w, h);
                }
            }
        }

        // =====================================================================
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            UpdateLabelVisibility();
            if (_label.Visible) UpdateLabelPosition();
        }

        // =====================================================================
        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            if (!Visible || _itemTexture == null || GraphicsDevice == null)
                return;

            Vector3 anchor = new(Position.X, Position.Y, Position.Z + SpriteOffsetZ);
            Vector3 screen = GraphicsDevice.Viewport.Project(
                anchor,
                Camera.Instance.Projection,
                Camera.Instance.View,
                Matrix.Identity);

            if (screen.Z < 0f || screen.Z > 1f)
                return;

            var sb = GraphicsManager.Instance.Sprite;
            Vector2 origin = new(_itemTexture.Width / 2f, _itemTexture.Height / 2f);

            void draw()
            {
                float pitch = MathHelper.ToRadians(0f);
                sb.Draw(
                    _itemTexture,
                    new Vector2(screen.X, screen.Y),
                    null,
                    Color.White,
                    pitch,
                    origin,
                    SpriteScale,
                    SpriteEffects.None,
                    screen.Z);
            }

            if (!SpriteBatchScope.BatchIsBegun)
            {
                using (new SpriteBatchScope(sb, SpriteSortMode.BackToFront, BlendState.NonPremultiplied, SamplerState.PointClamp, DepthState))
                {
                    draw();
                }
            }
            else
            {
                draw();
            }
        }

        // =====================================================================
        public override void OnClick()
        {
            base.OnClick();
            if (_pickedUp) return;

            if (World is not Controls.WalkableWorldControl w || w.Walker == null) return;
            if (w.Walker.NetworkId != _mainPlayerId) return;

            float d = Vector3.Distance(w.Walker.Position, Position);
            if (d > PickupRange)
            {
                World.Scene?.Controls.OfType<ChatLogWindow>()
                    .FirstOrDefault()?.AddMessage("System", "Item is too far away.", MessageType.System);
                return;
            }

            // Stash the item data BEFORE sending the request
            CharacterState charState = MuGame.Network?.GetCharacterState();
            if (charState == null)
            {
                _log.LogError("OnClick: CharacterState is null, cannot stash item for pickup.");
                return;
            }

            charState.SetPendingPickupRawId(RawId);

            if (_scope is ItemScopeObject itemScope)
            {
                charState.StashPickedItem(itemScope.ItemData.ToArray());
            }
            else if (_scope is MoneyScopeObject moneyScope)
            {
                // For money, the server typically handles amount updates directly.
                // Stashing a representation of money might be complex if the server doesn't expect item data for it.
                // Let's assume for now that if a 0x22 packet comes for money, it might be an error or unhandled by this specific logic.
                // If the server *does* use this packet type for money pickup success with a slot,
                // then a placeholder byte[] representing money would need to be stashed.
                // For simplicity, and based on typical MU, money pickups are handled by C1 22 FE (InventoryMoneyUpdate).
                // So, if this OnClick is for money, we might not need to stash, and the success indication
                // from 0xC3 0x22 <slot> would be for an actual item.
                _log.LogInformation("OnClick: Pick up initiated for Money. Server will update Zen directly.");
                // No stashing needed for money if server sends InventoryMoneyUpdate on success.
                // If server *does* send C3 22 <slot> for money, then stashing a representative byte[] is needed.
                // Example: charState.StashPickedItem(new byte[] { 15, 0, 0, 0, (byte)(moneyScope.Amount & 0xFF), 14 << 4 }); // Dummy data
            }
            else
            {
                _log.LogWarning("OnClick: Attempting to pick up unknown scope object type: {ScopeType}", _scope.ObjectType);
                return; // Don't send request for unknown types
            }

            _pickedUp = true;
            _label.Interactive = false; // Prevent further clicks on label while pickup is in progress

            Task.Run(() => _charSvc.SendPickupItemRequestAsync(RawId, MuGame.Network.TargetVersion));
            _log.LogDebug("Pickup request sent for {RawId:X4} ({DisplayName})", RawId, DisplayName);
        }

        // ─────────────────── label helpers

        private string FormatItemDisplayName(string baseName, ItemDatabase.ItemDetails details)
        {
            var sb = new StringBuilder();

            if (details.IsExcellent) sb.Append("Excellent ");
            sb.Append(baseName);

            if (details.Level > 0) sb.Append($" +{details.Level}");
            if (details.OptionLevel > 0) sb.Append($" +Options{details.OptionLevel * 4}");
            if (details.HasLuck) sb.Append(" +Luck");
            if (details.HasSkill) sb.Append(" +Skill");

            return sb.ToString();
        }

        private Color GetLabelColor(ScopeObject s, ItemDatabase.ItemDetails details)
        {
            // Ancient/Excellent
            if (details.IsAncient) return new Color(0, 255, 128);
            if (details.IsExcellent) return new Color(128, 255, 128);

            // +7 up
            if (details.Level >= 7) return Color.Gold;

            // (Luck, Skill, Add)
            if (details.HasBlueOptions) return new Color(130, 180, 255);

            // +3 +4 +5 +6
            if (details.Level >= 3) return new Color(255, 165, 0);

            //  +1, +2
            if (details.Level >= 1) return Color.White;

            // ZEN
            if (s is MoneyScopeObject) return Color.Gold;

            return Color.Gray; // +0
        }

        private void UpdateLabelVisibility()
        {
            bool ready = !Hidden && Status == GameControlStatus.Ready;
            bool near = false;

            if (World is Controls.WalkableWorldControl w && w.Walker != null)
                near = Vector3.Distance(w.Walker.Position, Position) <= 2000f;

            _label.Visible = ready && near && !OutOfView
                              && World?.Scene?.Status == GameControlStatus.Ready;
            _label.Interactive = _label.Visible && !_pickedUp;
        }

        private void UpdateLabelPosition()
        {
            if (_font == null || GraphicsDevice == null || Camera.Instance == null) return;

            float scale = _label.FontSize / _font.LineSpacing;

            Vector3 anchor = new(Position.X, Position.Y, Position.Z + LabelOffsetZ);
            Vector3 screen = GraphicsDevice.Viewport.Project(
                                anchor,
                                Camera.Instance.Projection,
                                Camera.Instance.View,
                                Matrix.Identity);

            if (screen.Z is < 0f or > 1f) { _label.Visible = false; return; }

            Vector2 textSize = _font.MeasureString(_label.Text) * scale;

            int width = (int)(textSize.X + _label.Padding.Left + _label.Padding.Right);
            int height = (int)(textSize.Y + _label.Padding.Top + _label.Padding.Bottom);

            _label.ControlSize = new(width, height);

            _label.X = (int)(screen.X - width / 2f);
            _label.Y = (int)(screen.Y - height - LabelPixelGap);
        }

        private static Color GetLabelColor(ScopeObject s) =>
            s switch
            {
                ItemScopeObject item when item.ItemDescription.StartsWith("Jewel", StringComparison.OrdinalIgnoreCase) => Color.Yellow,
                MoneyScopeObject _ => Color.Gold,
                _ => Color.White
            };

        /// <summary>
        /// Resets the pickup state so the item can be clicked again.
        /// </summary>
        public void ResetPickupState()
        {
            _pickedUp = false;
            _label.Interactive = _label.Visible;
        }

        // =====================================================================
        private void OnLabelClicked(object sender, EventArgs e) => OnClick();

        // =====================================================================
        public override void Dispose()
        {
            // Remove the label from whichever parent currently holds it
            if (_label != null)
            {
                _label.Click -= OnLabelClicked;
                _label.Parent?.Controls.Remove(_label);
                _label.Dispose();
            }
            base.Dispose();
        }
    }
}
