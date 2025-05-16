using Client.Main.Controllers;
using Client.Main.Models;
using Client.Main.Objects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Threading.Tasks;

namespace Client.Main.Controls
{
    /// <summary>
    /// Extends WorldControl to support click‐to‐move gameplay.
    /// </summary>
    public abstract class WalkableWorldControl : WorldControl
    {
        // --- Fields ---

        private CursorObject _cursor;
        private float _cursorNextMoveTime;
        private int _previousScrollValue;
        private float _targetCameraDistance;
        private float _minCameraDistance;
        private float _maxCameraDistance;

        // --- Properties ---

        /// <summary>
        /// The player's walker object.
        /// </summary>
        public WalkerObject Walker { get; set; }

        /// <summary>
        /// The X coordinate of the tile currently under the mouse.
        /// </summary>
        public byte MouseTileX { get; set; } = 0;

        /// <summary>
        /// The Y coordinate of the tile currently under the mouse.
        /// </summary>
        public byte MouseTileY { get; set; } = 0;

        /// <summary>
        /// Height offset applied when placing the cursor above terrain.
        /// </summary>
        public float ExtraHeight { get; set; }

        // --- Constructors ---

        /// <summary>
        /// Initializes a walkable world with default walker.
        /// </summary>
        public WalkableWorldControl(short worldIndex)
            : base(worldIndex)
        {
            Interactive = true;
        }

        /// <summary>
        /// Initializes a walkable world with a specified walker.
        /// </summary>
        public WalkableWorldControl(short worldIndex, WalkerObject walker)
            : this(worldIndex)
        {
            Walker = walker;
        }

        // --- Lifecycle Methods ---

        public override async Task Load()
        {
            Objects.Add(_cursor = new CursorObject());
            await base.Load();
        }

        public override void Update(GameTime time)
        {
            if (Status != GameControlStatus.Ready || !Visible)
                return;

            // some UI overlay has the mouse, skip click-to-move this frame.
            if (Scene != null && Scene.MouseHoverControl != null && Scene.MouseHoverControl != Scene.World)
            {
                // a UI element has focus or mouse over, and it's not the world itself,
                // then the game world shouldn't process its specific click or scroll.
                // The IsMouseInputConsumedThisFrame flag further reinforces this for other inputs.
                base.Update(time);
                return;
            }

            CalculateMouseTilePos();

            // Handle click‐to‐move with a simple cooldown
            if (!Scene.IsMouseInputConsumedThisFrame && // check if UI already handled the click
                (Scene.MouseControl == this || Scene.MouseControl == World) && // ensure this world or its base is the target
                MuGame.Instance.Mouse.LeftButton == ButtonState.Pressed &&
                _cursorNextMoveTime <= 0f)
            {
                _cursorNextMoveTime = 250f;
                var newTile = new Vector2(MouseTileX, MouseTileY);

                if (!IsWalkable(newTile))
                    return;

                float worldX = newTile.X * Constants.TERRAIN_SCALE;
                float worldY = newTile.Y * Constants.TERRAIN_SCALE;
                float height = Terrain.RequestTerrainHeight(worldX, worldY) + ExtraHeight;
                _cursor.Position = new Vector3(worldX, worldY, height) + new Vector3(50f, 40f, 0);
                Walker.MoveTo(newTile);
            }
            else if (_cursorNextMoveTime > 0f)
            {
                _cursorNextMoveTime -= (float)time.ElapsedGameTime.TotalMilliseconds;
            }

            var mouseState = MuGame.Instance.Mouse;
            int currentScroll = mouseState.ScrollWheelValue;
            int scrollDiff = currentScroll - _previousScrollValue;
            if (scrollDiff != 0 && !Scene.IsMouseInputConsumedThisFrame) // check if UI already handled scroll
            {
                float zoomChange = scrollDiff / 100f * 100f;
                _targetCameraDistance = MathHelper.Clamp(
                    _targetCameraDistance - zoomChange,
                    _minCameraDistance,
                    _maxCameraDistance);
            }
            _previousScrollValue = currentScroll;

            base.Update(time);
        }

        // --- Helper Methods ---

        /// <summary>
        /// Calculates which terrain tile is under the mouse cursor by raycasting.
        /// </summary>
        private void CalculateMouseTilePos()
        {
            var mousePos = Mouse.GetState().Position.ToVector2();
            var viewport = GraphicsManager.Instance.GraphicsDevice.Viewport;

            var near = viewport.Unproject(new Vector3(mousePos, 0f),
                                          Camera.Instance.Projection,
                                          Camera.Instance.View,
                                          Matrix.Identity);
            var far = viewport.Unproject(new Vector3(mousePos, 1f),
                                          Camera.Instance.Projection,
                                          Camera.Instance.View,
                                          Matrix.Identity);

            var ray = new Ray(near, Vector3.Normalize(far - near));
            const float maxDistance = 10000f;
            float step = Constants.TERRAIN_SCALE / 10f;
            float traveled = 0f;

            var lastPos = ray.Position;
            var lastDiff = lastPos.Z - Terrain.RequestTerrainHeight(lastPos.X, lastPos.Y) + ExtraHeight;
            bool hit = false;
            Vector3 hitPos = Vector3.Zero;

            while (traveled < maxDistance)
            {
                traveled += step;
                var pos = ray.Position + ray.Direction * traveled;
                float terrainZ = Terrain.RequestTerrainHeight(pos.X, pos.Y) + ExtraHeight;
                float diff = pos.Z - terrainZ;

                if (lastDiff > 0f && diff <= 0f)
                {
                    float t = lastDiff / (lastDiff - diff);
                    hitPos = Vector3.Lerp(lastPos, pos, t);
                    hit = true;
                    break;
                }

                lastPos = pos;
                lastDiff = diff;
            }

            if (hit)
            {
                int gx = (int)(hitPos.X / Constants.TERRAIN_SCALE);
                int gy = (int)(hitPos.Y / Constants.TERRAIN_SCALE);

                MouseTileX = (byte)Math.Clamp(gx, 0, Constants.TERRAIN_SIZE - 1);
                MouseTileY = (byte)Math.Clamp(gy, 0, Constants.TERRAIN_SIZE - 1);
            }
            else
            {
                MouseTileX = 0;
                MouseTileY = 0;
            }
        }
    }
}