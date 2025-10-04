using Client.Main.Controllers;              // GraphicsManager
using Client.Main.Core.Models;              // ScopeObject
using Client.Main.Models;                   // MessageType
using Client.Main.Networking.Services;      // CharacterService
using Client.Main.Controls.UI;              // ChatLogWindow + LabelControl
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Client.Main.Core.Client;
using Client.Main.Core.Utilities;
using System;
using System.Collections.Generic;
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
        private const float HeightOffset = 60f;
        private const float PickupRange = 300f;
        private const float LabelOffsetZ = 10f;
        private const int LabelPixelGap = 20;
        private const float BoundingSnapEpsilon = 1f; // Minimum movement threshold when recentring children
        private const float BoundingPadding = 4f; // Extra space so models never clip the box walls
        private const float MinimumHalfExtent = 18f; // Prevent degenerate narrow bounding boxes
        private const float MinimumBoundingHeight = 24f; // Ensure some vertical interaction room

        // ─────────────────── deps / state
        private readonly ScopeObject _scope;
        private readonly ushort _mainPlayerId;
        private readonly CharacterService _charSvc;
        private readonly ILogger<DroppedItemObject> _log;

        private SpriteFont _font;
        private bool _pickedUp;
        private ModelObject _modelObj; // Optional 3D model when available
        private readonly ItemDefinition _definition;
        private readonly bool _isMoney;
        private float _yawRadians;   // Static orientation in world (does not follow camera)
        private readonly List<ModelObject> _coinModels = new List<ModelObject>(); // Multiple coins for money piles
        private readonly List<Vector3> _childBoundsScratch = new(32); // Reuse buffer while fitting bounding boxes

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

            // Initialize position at ground level (will be adjusted in Load() after terrain height is known)
            Position = new(
                scope.PositionX * Constants.TERRAIN_SCALE + Constants.TERRAIN_SCALE / 2f,
                scope.PositionY * Constants.TERRAIN_SCALE + Constants.TERRAIN_SCALE / 2f,
                0f); // Ground level, bottom of bounding box

            string baseName = "Unknown Drop";
            ItemDatabase.ItemDetails itemDetails = default;
            _ = ReadOnlySpan<byte>.Empty;

            if (scope is ItemScopeObject itemScope)
            {
                ReadOnlySpan<byte> itemData = itemScope.ItemData.Span;
                baseName = itemScope.ItemDescription;
                itemDetails = ItemDatabase.ParseItemDetails(itemData);
                _definition = ItemDatabase.GetItemDefinition(itemData);
                _isMoney = false;
            }
            else if (scope is MoneyScopeObject moneyScope)
            {
                baseName = $"{moneyScope.Amount} Zen";
                _isMoney = true;
            }

            DisplayName = FormatItemDisplayName(baseName, itemDetails);

            // LabelControl is not used anymore (it rendered above UI).
            // We draw the item name in the world pass (depth-aware) by overriding DrawHoverName.

            // Initialize a deterministic static yaw based on the raw id to make items look natural but stable
            _yawRadians = ((RawId & 0xFF) / 255f) * MathHelper.TwoPi;
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

            // Handle money (gold coin) model - create a pile of coins
            if (_isMoney)
            {
                try
                {
                    var bmd = await BMDLoader.Instance.Prepare("Item\\Gold01.bmd");
                    if (bmd == null)
                    {
                        _log.LogWarning("Gold coin BMD model is null after loading");
                        return;
                    }

                    // Determine coin count based on amount (more zen = more coins, capped at reasonable number)
                    var moneyScope = _scope as MoneyScopeObject;
                    int coinCount = CalculateCoinCount(moneyScope?.Amount ?? 0);

                    // Use deterministic random based on RawId for consistent results
                    var random = new Random(RawId);

                    // Create multiple coins in a pile with anti-collision positioning
                    for (int i = 0; i < coinCount; i++)
                    {
                        var model = new DroppedItemModel();
                        model.Model = bmd;

                        // Position coins in a circular pile pattern with vertical stacking
                        // All relative to parent position (which is at bottom center of bbox = ground level)
                        float radius = (float)Math.Sqrt(i) * 8f; // Spiral outward
                        float angle = i * 2.4f; // Golden angle for even distribution
                        float offsetX = (float)Math.Cos(angle) * radius;
                        float offsetY = (float)Math.Sin(angle) * radius;
                        float offsetZ = (i / 3) * 3f; // Stack coins vertically from ground level

                        // Add small random variation to prevent perfect alignment
                        offsetX += (float)(random.NextDouble() - 0.5) * 4f;
                        offsetY += (float)(random.NextDouble() - 0.5) * 4f;
                        offsetZ += (float)(random.NextDouble() - 0.5) * 1f;

                        model.Position = new Vector3(offsetX, offsetY, offsetZ);

                        // Coins lie flat (like original code) but with slight Z rotation for variety
                        float rotZ = (float)(random.NextDouble() * Math.PI * 2);
                        model.Angle = new Vector3(0, 0, rotZ);

                        model.Scale = 0.8f;
                        model.LightEnabled = true;

                        Children.Add(model);
                        await model.Load();
                        _coinModels.Add(model);
                    }

                    RecenterChildrenAndFitBoundingBox();
                    _log.LogInformation("Gold coin pile loaded with {Count} coins at position {Pos}", coinCount, Position);
                    return; // 3D model loaded
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Failed to load gold coin BMD model");
                }
            }
            // Handle item models
            else if (_definition != null && !string.IsNullOrEmpty(_definition.TexturePath))
            {
                // Try to load real 3D model
                if (_definition.TexturePath.EndsWith(".bmd", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var bmd = await BMDLoader.Instance.Prepare(_definition.TexturePath);
                        var model = new DroppedItemModel();
                        model.Model = bmd;

                        model.Position = Vector3.Zero;
                        // Use consistent group-specific orientation from ItemOrientationHelper
                        var baseAngle = ItemOrientationHelper.GetWorldDropEuler(_definition);
                        model.Angle = new Vector3(baseAngle.X + MathHelper.PiOver2, baseAngle.Y - MathHelper.PiOver2, baseAngle.Z + MathHelper.PiOver2 / 2); // +90 degrees on X axis
                        model.Scale = 0.6f; // Tuned size; detailed fit happens in RecenterChildrenAndFitBoundingBox

                        Children.Add(model);
                        await model.Load();
                        _modelObj = model;
                        RecenterChildrenAndFitBoundingBox();
                        return; // 3D model loaded, skip texture sprite path
                    }
                    catch (Exception ex)
                    {
                        _log.LogDebug(ex, "Failed to load BMD model for dropped item: {Path}", _definition.TexturePath);
                    }
                }

                // Skip if we already know this texture failed to load\n
            }
        }

        // =====================================================================
        private void RecenterChildrenAndFitBoundingBox()
        {
            if (!TryGetChildBounds(out var localBounds))
            {
                return; // No geometry yet (sprite fallback or model load failure)
            }

            Vector3 correction = new(
                (localBounds.Min.X + localBounds.Max.X) * 0.5f,
                (localBounds.Min.Y + localBounds.Max.Y) * 0.5f,
                localBounds.Min.Z);

            if (MathF.Abs(correction.X) > BoundingSnapEpsilon ||
                MathF.Abs(correction.Y) > BoundingSnapEpsilon ||
                MathF.Abs(correction.Z) > BoundingSnapEpsilon)
            {
                foreach (var child in Children)
                {
                    if (child is ModelObject model && model.Model != null)
                    {
                        model.Position -= correction;
                    }
                }

                if (!TryGetChildBounds(out localBounds))
                {
                    return;
                }
            }

            float halfWidth = MathF.Max(MathF.Abs(localBounds.Min.X), MathF.Abs(localBounds.Max.X));
            float halfDepth = MathF.Max(MathF.Abs(localBounds.Min.Y), MathF.Abs(localBounds.Max.Y));
            float minZ = MathF.Min(localBounds.Min.Z, 0f);
            float height = localBounds.Max.Z - minZ;

            halfWidth = MathF.Max(halfWidth, MinimumHalfExtent);
            halfDepth = MathF.Max(halfDepth, MinimumHalfExtent);
            height = MathF.Max(height, MinimumBoundingHeight);

            BoundingBoxLocal = new BoundingBox(
                new Vector3(-halfWidth - BoundingPadding, -halfDepth - BoundingPadding, 0f),
                new Vector3(halfWidth + BoundingPadding, halfDepth + BoundingPadding, height + BoundingPadding));
        }

        private bool TryGetChildBounds(out BoundingBox localBounds)
        {
            localBounds = default;

            if (Children.Count == 0)
            {
                return false;
            }

            Matrix parentWorld = WorldPosition;
            float determinant = parentWorld.Determinant();
            if (MathF.Abs(determinant) < float.Epsilon)
            {
                return false; // Matrix not invertible -> no reliable local conversion
            }

            Matrix.Invert(ref parentWorld, out Matrix inverseParent);

            _childBoundsScratch.Clear();

            foreach (var child in Children)
            {
                if (child is not ModelObject model || model.Model == null || model.Status != GameControlStatus.Ready)
                {
                    continue;
                }

                var corners = model.BoundingBoxWorld.GetCorners();
                for (int i = 0; i < corners.Length; i++)
                {
                    _childBoundsScratch.Add(Vector3.Transform(corners[i], inverseParent));
                }
            }

            if (_childBoundsScratch.Count == 0)
            {
                return false;
            }

            localBounds = BoundingBox.CreateFromPoints(_childBoundsScratch);
            _childBoundsScratch.Clear();
            return true;
        }

        // =====================================================================
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Label visibility and position are computed in DrawHoverName (depth-aware UI pass).
        }

        // =====================================================================
        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
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

            _pickedUp = true; // Prevent further clicks while pickup is in progress

            Task.Run(() => _charSvc.SendPickupItemRequestAsync(RawId, MuGame.Network.TargetVersion));
            _log.LogDebug("Pickup request sent for {RawId:X4} ({DisplayName})", RawId, DisplayName);
        }

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

        private int CalculateCoinCount(uint zenAmount)
        {
            // Scale coin count based on amount of zen with randomization
            // Use RawId as seed for consistent randomization per drop
            var random = new Random(RawId);

            if (zenAmount < 100)
                return random.Next(2, 5);      // 2-4 coins
            if (zenAmount < 1000)
                return random.Next(4, 7);      // 4-6 coins
            if (zenAmount < 10000)
                return random.Next(6, 10);     // 6-9 coins
            if (zenAmount < 100000)
                return random.Next(9, 14);     // 9-13 coins
            if (zenAmount < 1000000)
                return random.Next(12, 17);    // 12-16 coins

            return random.Next(15, 21);        // 15-20 coins for huge amounts
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

        // We override DrawHoverName to render the dropped item label in the world UI pass (depth-aware),
        // so it never draws above HUD windows.
        public override void DrawHoverName()
        {
            if (_pickedUp || Hidden || OutOfView)
                return;

            if (_font == null)
                _font = GraphicsManager.Instance.Font;
            if (_font == null || GraphicsDevice == null || Camera.Instance == null)
                return;

            // Only show when player is reasonably near and scene is ready
            bool near = false;
            if (World is Controls.WalkableWorldControl w && w.Walker != null)
                near = Vector3.Distance(w.Walker.Position, Position) <= 2000f;
            if (!near || World?.Scene?.Status != GameControlStatus.Ready)
                return;

            var scope = _scope; // local ref
            string text = DisplayName;
            // Scale font based on resolution and render scale
            float baseScale = 10f / Client.Main.Constants.BASE_FONT_SIZE;
            float scale = baseScale * UiScaler.Scale * Constants.RENDER_SCALE;
            ReadOnlySpan<byte> itemSpan = ReadOnlySpan<byte>.Empty;
            if (scope is ItemScopeObject iso)
            {
                itemSpan = iso.ItemData.Span;
            }
            var color = GetLabelColor(scope, ItemDatabase.ParseItemDetails(itemSpan));

            // Label position: slightly above the top of the bounding box
            Vector3 anchor = new(Position.X, Position.Y, BoundingBoxWorld.Max.Z + LabelOffsetZ);
            Vector3 screen = GraphicsDevice.Viewport.Project(
                anchor,
                Camera.Instance.Projection,
                Camera.Instance.View,
                Matrix.Identity);

            if (screen.Z < 0f || screen.Z > 1f)
                return;

            var sb = GraphicsManager.Instance.Sprite;
            Vector2 textSize = _font.MeasureString(text) * scale;
            int padX = 4, padY = 2;
            int width = (int)(textSize.X) + padX * 2;
            int height = (int)(textSize.Y) + padY * 2;
            var rect = new Rectangle(
                (int)(screen.X - width / 2f),
                (int)(screen.Y - height - LabelPixelGap),
                width, height);
            float layer = screen.Z;

            void draw()
            {
                // Background (with same layer depth as text)
                sb.Draw(GraphicsManager.Instance.Pixel, rect, null, new Color(0, 0, 0, 160), 0f, Vector2.Zero, SpriteEffects.None, layer);
                // Border (same depth)
                sb.Draw(GraphicsManager.Instance.Pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), null, Color.White * 0.3f, 0f, Vector2.Zero, SpriteEffects.None, layer);
                sb.Draw(GraphicsManager.Instance.Pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), null, Color.White * 0.3f, 0f, Vector2.Zero, SpriteEffects.None, layer);
                sb.Draw(GraphicsManager.Instance.Pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), null, Color.White * 0.3f, 0f, Vector2.Zero, SpriteEffects.None, layer);
                sb.Draw(GraphicsManager.Instance.Pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), null, Color.White * 0.3f, 0f, Vector2.Zero, SpriteEffects.None, layer);
                // Text (original color via GetLabelColor, same layer)
                sb.DrawString(
                    _font,
                    text,
                    new Vector2(rect.X + padX, rect.Y + padY),
                    color,
                    0f,
                    Vector2.Zero,
                    scale,
                    SpriteEffects.None,
                    layer);
            }

            if (!SpriteBatchScope.BatchIsBegun)
            {
                using (new SpriteBatchScope(sb, SpriteSortMode.BackToFront, BlendState.NonPremultiplied, SamplerState.PointClamp, DepthStencilState.None))
                {
                    draw();
                }
            }
            else
            {
                // Temporarily switch to no-depth state to avoid partial occlusion by world geometry
                sb.End();
                sb.Begin(SpriteSortMode.BackToFront, BlendState.NonPremultiplied, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                draw();
                sb.End();
                // Restore the original DepthRead state for the rest of the world overlays
                sb.Begin(SpriteSortMode.BackToFront, BlendState.NonPremultiplied, SamplerState.PointClamp, DepthStencilState.DepthRead, RasterizerState.CullNone);
            }
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
        }

        // =====================================================================
        private void OnLabelClicked(object sender, EventArgs e) => OnClick();

        // =====================================================================
        public override void Dispose()
        {
            base.Dispose();
        }
    }

    // Minimal model subclass used for dropped items
    internal class DroppedItemModel : ModelObject
    {
        public override async Task Load()
        {
            await base.Load();
        }
    }
}
