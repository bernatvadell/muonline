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

namespace Client.Main.Objects
{
    /// <summary>
    /// Dropped item or Zen; the label disappears only when the server
    /// removes the object from scope.
    /// </summary>
    public class DroppedItemObject : WorldObject
    {
        // ─────────────────── constants
        private const float HeightOffset = 50f;
        private const float PickupRange  = 300f;
        private const float LabelScale   = 0.6f;
        private const float LabelOffsetZ = 10f;

        // ─────────────────── deps / state
        private readonly ScopeObject _scope;
        private readonly ushort      _mainPlayerId;
        private readonly CharacterService _charSvc;
        private readonly ILogger<DroppedItemObject> _log;

        private SpriteFont   _font;
        private LabelControl _label;
        private bool         _pickedUp;

        // ─────────────────── public helpers
        public ushort RawId       => _scope.RawId;
        public string DisplayName { get; }

        // =====================================================================
        public DroppedItemObject(
            ScopeObject scope,
            ushort mainPlayerId,
            CharacterService charSvc,
            ILogger<DroppedItemObject>? logger = null)
        {
            _scope        = scope  ?? throw new ArgumentNullException(nameof(scope));
            _mainPlayerId = mainPlayerId;
            _charSvc      = charSvc ?? throw new ArgumentNullException(nameof(charSvc));
            _log          = logger
                            ?? ModelObject.AppLoggerFactory?.CreateLogger<DroppedItemObject>()
                            ?? NullLogger<DroppedItemObject>.Instance;

            NetworkId   = scope.Id;
            Interactive = true;

            Position = new(
                scope.PositionX * Constants.TERRAIN_SCALE + Constants.TERRAIN_SCALE / 2f,
                scope.PositionY * Constants.TERRAIN_SCALE + Constants.TERRAIN_SCALE / 2f,
                0f);

            DisplayName = scope switch
            {
                ItemScopeObject   it  => it.ItemDescription,
                MoneyScopeObject  mn  => $"{mn.Amount} Zen",
                _                     => "Unknown Drop"
            };

            _label = new LabelControl
            {
                Text            = DisplayName,
                FontSize        = 12f,
                TextColor       = GetLabelColor(scope),
                HasShadow       = true,
                ShadowColor     = Color.Black,
                ShadowOpacity   = 0.7f,
                UseManualPosition = true,
                Visible         = false,
                Interactive     = true
            };
            // ‼  NIE dodajemy do Children – to nie WorldObject
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

            // Dołącz etykietę do UI sceny
            if (World?.Scene != null)
            {
                World.Scene.Controls.Add(_label);
                _label.Click += OnLabelClicked;
                await _label.Load();
            }
            else
            {
                _log.LogWarning("World.Scene == null – label will not be visible.");
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
        public override void OnClick()
        {
            base.OnClick();
            if (_pickedUp) return;

            if (World is not Controls.WalkableWorldControl w || w.Walker == null) return;
            if (w.Walker.NetworkId != _mainPlayerId)                              return;

            float d = Vector3.Distance(w.Walker.Position, Position);
            if (d > PickupRange)
            {
                World.Scene?.Controls.OfType<ChatLogWindow>()
                    .FirstOrDefault()?.AddMessage("System", "Item is too far away.", MessageType.System);
                return;
            }

            _pickedUp          = true;
            _label.Interactive = false;

            Task.Run(() => _charSvc.SendPickupItemRequestAsync(RawId, MuGame.Network.TargetVersion));
            _log.LogDebug("Pickup request sent for {Id:X4}", RawId);
            // Czekamy na pakiet serwera, obiektu na razie nie usuwamy.
        }

        // ─────────────────── label helpers
        private void UpdateLabelVisibility()
        {
            bool ready = !Hidden && Status == GameControlStatus.Ready;
            bool near  = false;

            if (World is Controls.WalkableWorldControl w && w.Walker != null)
                near = Vector3.Distance(w.Walker.Position, Position) <= 2000f;

            _label.Visible     = ready && near && !OutOfView
                              && World?.Scene?.Status == GameControlStatus.Ready;
            _label.Interactive = _label.Visible && !_pickedUp;
        }

        private void UpdateLabelPosition()
        {
            if (_font == null || GraphicsDevice == null || Camera.Instance == null) return;

            Vector3 anchor = new(Position.X, Position.Y, Position.Z + LabelOffsetZ);
            Vector3 screen = GraphicsDevice.Viewport.Project(anchor,
                                Camera.Instance.Projection,
                                Camera.Instance.View,
                                Matrix.Identity);

            if (screen.Z is < 0f or > 1f) { _label.Visible = false; return; }

            Vector2 size = _font.MeasureString(_label.Text) * LabelScale;
            _label.X = (int)(screen.X - size.X / 2);
            _label.Y = (int)(screen.Y - size.Y - 5);
            _label.ControlSize = new((int)size.X, (int)size.Y);
        }

        private static Color GetLabelColor(ScopeObject s) =>
            s switch
            {
                ItemScopeObject item when item.ItemDescription.StartsWith("Jewel", StringComparison.OrdinalIgnoreCase) => Color.Yellow,
                MoneyScopeObject _ => Color.Gold,
                _                  => Color.White
            };

        // =====================================================================
        private void OnLabelClicked(object? sender, EventArgs e) => OnClick(); // proxy dla LabelControl

        // =====================================================================
        public override void Dispose()
        {
            // Zdejmij etykietę z UI
            if (_label != null && World?.Scene != null)
            {
                _label.Click -= OnLabelClicked;
                World.Scene.Controls.Remove(_label);
                _label.Dispose();
            }
            base.Dispose();
        }
    }
}
