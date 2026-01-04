using Client.Main.Controllers;              // GraphicsManager
using Client.Main.Core.Models;              // ScopeObject
using Client.Main.Graphics;
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
using Client.Main.Scenes;
using Client.Main.Objects.Effects;

namespace Client.Main.Objects
{
    /// <summary>
    /// Dropped item or Zen; the label disappears only when the server
    /// removes the object from scope.
    /// </summary>
    public class DroppedItemObject : WorldObject
    {
        // ─────────────────── constants
        private const float HeightOffset = 55f; // Item exactly at terrain level - model will be positioned above
        private const float PickupRange = 300f;
        private const float LabelOffsetZ = 10f;
        private const int LabelPixelGap = 20;
        private const float BoundingPadding = 2f; // Small padding for interaction

        // ─────────────────── deps / state
        private ScopeObject _scope;
        private ushort _mainPlayerId;
        private CharacterService _charSvc;
        private ILogger<DroppedItemObject> _log;

        private SpriteFont _font;
        private bool _pickedUp;
        private ModelObject _modelObj; // Optional 3D model when available
        private ItemDefinition _definition;
        private bool _isMoney;
        private float _yawRadians;   // Static orientation in world (does not follow camera)
        private readonly List<ModelObject> _coinModels = new List<ModelObject>(); // Multiple coins for money piles
        private DroppedItemShineEffect _shineEffect;

        // ─────────────────── public helpers
        public ushort RawId => _scope?.RawId ?? 0;
        public new string DisplayName { get; private set; }

        // Pool
        private static readonly System.Collections.Concurrent.ConcurrentBag<DroppedItemObject> _pool = new();

        public static DroppedItemObject Rent(
              ScopeObject scope,
              ushort mainPlayerId,
              CharacterService charSvc,
              ILogger<DroppedItemObject> logger = null)
        {
            if (_pool.TryTake(out var obj))
            {
                obj.ResetFromScope(scope, mainPlayerId, charSvc, logger);
                return obj;
            }
            return new DroppedItemObject(scope, mainPlayerId, charSvc, logger);
        }

        public void Recycle()
        {
            try
            {
                Dispose();
            }
            finally
            {
                _pool.Add(this);
            }
        }

        // =====================================================================
        public DroppedItemObject(
              ScopeObject scope,
              ushort mainPlayerId,
              CharacterService charSvc,
              ILogger<DroppedItemObject> logger = null)
        {
            ResetFromScope(scope, mainPlayerId, charSvc, logger);
        }

        private void ResetFromScope(
            ScopeObject scope,
            ushort mainPlayerId,
            CharacterService charSvc,
            ILogger<DroppedItemObject> logger)
        {
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _mainPlayerId = mainPlayerId;
            _charSvc = charSvc ?? throw new ArgumentNullException(nameof(charSvc));
            _log = logger ?? ModelObject.AppLoggerFactory?.CreateLogger<DroppedItemObject>() ?? NullLogger<DroppedItemObject>.Instance;

            NetworkId = scope.Id;
            Interactive = true;
            Hidden = false;
            Status = GameControlStatus.NonInitialized;
            _pickedUp = false;
            _modelObj = null;
            _definition = null;
            _isMoney = false;
            _coinModels.Clear();
            _shineEffect = null;

            // Initialize position at ground level (will be adjusted in Load() after terrain height is known)
            Position = new(
                scope.PositionX * Constants.TERRAIN_SCALE + Constants.TERRAIN_SCALE / 2f,
                scope.PositionY * Constants.TERRAIN_SCALE + Constants.TERRAIN_SCALE / 2f,
                0f); // Ground level, bottom of bounding box

            string baseName = "Unknown Drop";
            ItemDatabase.ItemDetails itemDetails = default;

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
                        float radius = (float)Math.Sqrt(i) * 8f; // Spiral outward
                        float angle = i * 2.4f; // Golden angle for even distribution
                        float offsetX = (float)Math.Cos(angle) * radius;
                        float offsetY = (float)Math.Sin(angle) * radius;
                        float offsetZ = (i / 3) * 2f + 1f; // Stack coins, start slightly above ground

                        // Add small random variation to prevent perfect alignment
                        offsetX += (float)(random.NextDouble() - 0.5) * 4f;
                        offsetY += (float)(random.NextDouble() - 0.5) * 4f;
                        offsetZ += (float)(random.NextDouble() - 0.5) * 1f;

                        model.Position = new Vector3(offsetX, offsetY, offsetZ);

                        // Coins lie flat with slight Z rotation for variety
                        float rotZ = (float)(random.NextDouble() * Math.PI * 2);
                        model.Angle = new Vector3(0, 0, rotZ);

                        model.Scale = 0.8f;
                        model.LightEnabled = true;

                        Children.Add(model);
                        await model.Load();
                        _coinModels.Add(model);
                    }

                    RecenterCoinsAndFitBoundingBox();
                    _log.LogDebug("Gold coin pile loaded with {Count} coins at position {Pos}", coinCount, Position);
                    AttachShineEffect();
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
                        model.ItemDefinition = _definition;

                        model.Position = Vector3.Zero;

                        // Use original rotation from ItemOrientationHelper
                        var baseAngle = ItemOrientationHelper.GetWorldDropEuler(_definition);
                        model.Angle = new Vector3(
                            baseAngle.X + MathHelper.PiOver2,
                            baseAngle.Y - MathHelper.PiOver2,
                            baseAngle.Z + MathHelper.PiOver2 / 2f
                        );

                        model.Scale = 0.6f;
                        model.LightEnabled = true;

                        Children.Add(model);
                        await model.Load();
                        _modelObj = model;

                        // Position model so its bottom touches the ground
                        PositionModelOnGround(model);

                        AttachShineEffect();
                        return; // 3D model loaded
                    }
                    catch (Exception ex)
                    {
                        _log.LogDebug(ex, "Failed to load BMD model for dropped item: {Path}", _definition.TexturePath);
                    }
                }
            }

            AttachShineEffect();
        }

        // =====================================================================
        /// <summary>
        /// Positions the model so its lowest vertex touches the ground (parent's Z=0 in local space).
        /// Calculates bounding box directly from model geometry.
        /// </summary>
        private void PositionModelOnGround(ModelObject model)
        {
            if (model?.Model?.Meshes == null)
            {
                // Fallback bounding box
                BoundingBoxLocal = new BoundingBox(
                    new Vector3(-20, -20, 0),
                    new Vector3(20, 20, 40));
                return;
            }

            var bmd = model.Model;
            var bones = model.GetBoneTransforms();

            // Find model bounds directly from vertices
            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);
            bool hasVertices = false;

            // Build rotation matrix from model angle
            Matrix rotationMatrix = Matrix.CreateRotationX(model.Angle.X) *
                                   Matrix.CreateRotationY(model.Angle.Y) *
                                   Matrix.CreateRotationZ(model.Angle.Z);

            foreach (var mesh in bmd.Meshes)
            {
                if (mesh.Vertices == null) continue;

                foreach (var vert in mesh.Vertices)
                {
                    // Transform vertex by bone
                    Matrix boneMatrix = Matrix.Identity;
                    if (bones != null && vert.Node >= 0 && vert.Node < bones.Length)
                    {
                        boneMatrix = bones[vert.Node];
                    }

                    // Vertex position in model's local space
                    Vector3 localPos = new Vector3(vert.Position.X, vert.Position.Y, vert.Position.Z);
                    Vector3 transformedPos = Vector3.Transform(localPos, boneMatrix);

                    // Apply model rotation
                    Vector3 rotatedPos = Vector3.Transform(transformedPos, rotationMatrix);

                    // Apply scale
                    rotatedPos *= model.Scale;

                    min = Vector3.Min(min, rotatedPos);
                    max = Vector3.Max(max, rotatedPos);
                    hasVertices = true;
                }
            }

            if (!hasVertices)
            {
                // Fallback bounding box
                BoundingBoxLocal = new BoundingBox(
                    new Vector3(-20, -20, 0),
                    new Vector3(20, 20, 40));
                return;
            }

            // Move model up so its lowest point is at Z=0 (ground level)
            float groundOffset = -min.Z;
            model.Position = new Vector3(
                -(min.X + max.X) * 0.5f,  // Center X
                -(min.Y + max.Y) * 0.5f,  // Center Y
                groundOffset               // Lift to ground level
            );

            // Calculate bounds after repositioning
            float halfWidth = MathF.Max((max.X - min.X) * 0.5f, 10f);
            float halfDepth = MathF.Max((max.Y - min.Y) * 0.5f, 10f);
            float height = MathF.Max(max.Z - min.Z, 15f);

            // Set bounding box with minimal padding
            BoundingBoxLocal = new BoundingBox(
                new Vector3(-halfWidth - BoundingPadding, -halfDepth - BoundingPadding, 0f),
                new Vector3(halfWidth + BoundingPadding, halfDepth + BoundingPadding, height + BoundingPadding));
        }

        // =====================================================================
        /// <summary>
        /// Centers coin pile and fits bounding box for money drops.
        /// </summary>
        private void RecenterCoinsAndFitBoundingBox()
        {
            if (_coinModels.Count == 0)
                return;

            // Calculate bounds from coin positions
            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);

            foreach (var coin in _coinModels)
            {
                Vector3 coinPos = coin.Position;
                float coinRadius = 12f * coin.Scale;
                float coinHeight = 4f * coin.Scale;

                min = Vector3.Min(min, coinPos - new Vector3(coinRadius, coinRadius, 0));
                max = Vector3.Max(max, coinPos + new Vector3(coinRadius, coinRadius, coinHeight));
            }

            // Center in X/Y, keep Z at ground level
            float centerX = (min.X + max.X) * 0.5f;
            float centerY = (min.Y + max.Y) * 0.5f;
            float minZ = MathF.Min(min.Z, 0f);

            foreach (var coin in _coinModels)
            {
                coin.Position = new Vector3(
                    coin.Position.X - centerX,
                    coin.Position.Y - centerY,
                    coin.Position.Z - minZ
                );
            }

            // Recalculate bounds after centering
            float halfWidth = MathF.Max((max.X - min.X) * 0.5f, 15f);
            float halfDepth = MathF.Max((max.Y - min.Y) * 0.5f, 15f);
            float height = MathF.Max(max.Z - min.Z, 10f);

            BoundingBoxLocal = new BoundingBox(
                new Vector3(-halfWidth - BoundingPadding, -halfDepth - BoundingPadding, 0f),
                new Vector3(halfWidth + BoundingPadding, halfDepth + BoundingPadding, height + BoundingPadding));
        }

        // =====================================================================
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
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
                if (World.Scene is GameScene scene)
                {
                    scene.ChatLog?.AddMessage("System", "Item is too far away.", MessageType.System);
                }
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
                _log.LogInformation("OnClick: Pick up initiated for Money. Server will update Zen directly.");
            }
            else
            {
                _log.LogWarning("OnClick: Attempting to pick up unknown scope object type: {ScopeType}", _scope.ObjectType);
                return;
            }

            _pickedUp = true;

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
            var random = new Random(RawId);

            if (zenAmount < 100)
                return random.Next(2, 5);
            if (zenAmount < 1000)
                return random.Next(4, 7);
            if (zenAmount < 10000)
                return random.Next(6, 10);
            if (zenAmount < 100000)
                return random.Next(9, 14);
            if (zenAmount < 1000000)
                return random.Next(12, 17);

            return random.Next(15, 21);
        }

        private Color GetLabelColor(ScopeObject s, ItemDatabase.ItemDetails details)
        {
            if (details.IsAncient) return new Color(0, 255, 128);
            if (details.IsExcellent) return new Color(128, 255, 128);
            if (details.Level >= 7) return Color.Gold;
            if (details.HasBlueOptions) return new Color(130, 180, 255);
            if (details.Level >= 3) return new Color(255, 165, 0);
            if (details.Level >= 1) return Color.White;
            if (s is MoneyScopeObject) return Color.Gold;
            return Color.Gray;
        }

        public override void DrawHoverName()
        {
            if (_pickedUp || Hidden)
                return;

            if (_font == null)
                _font = GraphicsManager.Instance.Font;
            if (_font == null || GraphicsDevice == null || Camera.Instance == null)
                return;

            bool near = false;
            if (World is Controls.WalkableWorldControl w && w.Walker != null)
                near = Vector3.Distance(w.Walker.Position, Position) <= 2000f;
            if (!near || World?.Scene?.Status != GameControlStatus.Ready)
                return;

            var scope = _scope;
            string text = DisplayName;
            float baseScale = 10f / Client.Main.Constants.BASE_FONT_SIZE;
            float scale = baseScale * UiScaler.Scale * Constants.RENDER_SCALE;
            ReadOnlySpan<byte> itemSpan = ReadOnlySpan<byte>.Empty;
            if (scope is ItemScopeObject iso)
            {
                itemSpan = iso.ItemData.Span;
            }
            var color = GetLabelColor(scope, ItemDatabase.ParseItemDetails(itemSpan));

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
                sb.Draw(GraphicsManager.Instance.Pixel, rect, null, new Color(0, 0, 0, 160), 0f, Vector2.Zero, SpriteEffects.None, layer);
                sb.Draw(GraphicsManager.Instance.Pixel, new Rectangle(rect.X, rect.Y, rect.Width, 1), null, Color.White * 0.3f, 0f, Vector2.Zero, SpriteEffects.None, layer);
                sb.Draw(GraphicsManager.Instance.Pixel, new Rectangle(rect.X, rect.Bottom - 1, rect.Width, 1), null, Color.White * 0.3f, 0f, Vector2.Zero, SpriteEffects.None, layer);
                sb.Draw(GraphicsManager.Instance.Pixel, new Rectangle(rect.X, rect.Y, 1, rect.Height), null, Color.White * 0.3f, 0f, Vector2.Zero, SpriteEffects.None, layer);
                sb.Draw(GraphicsManager.Instance.Pixel, new Rectangle(rect.Right - 1, rect.Y, 1, rect.Height), null, Color.White * 0.3f, 0f, Vector2.Zero, SpriteEffects.None, layer);
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
                sb.End();
                sb.Begin(SpriteSortMode.BackToFront, BlendState.NonPremultiplied, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone);
                draw();
                sb.End();
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

        public void ResetPickupState()
        {
            _pickedUp = false;
        }

        private void OnLabelClicked(object sender, EventArgs e) => OnClick();

        public override void Dispose()
        {
            base.Dispose();
        }

        private void AttachShineEffect()
        {
            if (_shineEffect != null)
                return;

            _shineEffect = new DroppedItemShineEffect();
            Children.Add(_shineEffect);
            _ = _shineEffect.Load();
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
