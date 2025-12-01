using Client.Main.Controllers;
using Client.Main.Models;
using Client.Main.Objects;
using Client.Main.Objects.Monsters;
using Client.Main.Objects.Player;
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

        // --- Mouse tile caching for performance ---
        private Point _lastMousePosition = new Point(-1, -1);
        private Vector3 _lastCameraPosition;
        private bool _mouseTileDirty = true;

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

            MonsterObject hoveredMonster = Scene.MouseHoverObject as MonsterObject;

            // Handle click‐to‐move with a simple cooldown
            if (!Scene.IsMouseInputConsumedThisFrame && // check if UI already handled the click
                (Scene.MouseControl == this || Scene.MouseControl == World) && // ensure this world or its base is the target
                MuGame.Instance.Mouse.LeftButton == ButtonState.Pressed &&
                _cursorNextMoveTime <= 0f)
            {
                // If an NPC is under the cursor, consume click and don't trigger move
                if (Scene.MouseHoverObject is NPCObject)
                {
                    if (Scene is Client.Main.Scenes.BaseScene bs)
                        bs.SetMouseInputConsumed();
                    _cursorNextMoveTime = 250f;
                    return;
                }
                if (Walker is PlayerObject player)
                {
                    MonsterObject monster = hoveredMonster ?? FindMonsterAtTile(MouseTileX, MouseTileY);
                    if (monster != null)
                    {
                        float attackRange = player.GetAttackRangeTiles();
                        if (Vector2.Distance(player.Location, monster.Location) <= attackRange)
                        {
                            player.Attack(monster);
                            if (Scene is Client.Main.Scenes.BaseScene bs)
                                bs.SetMouseInputConsumed();
                            _cursorNextMoveTime = 250f;
                            return;
                        }
                    }
                }

                _cursorNextMoveTime = 250f;
                var newTile = new Vector2(MouseTileX, MouseTileY);

                if (!IsWalkable(newTile))
                    return;

                // Don't allow movement if player is dead
                if (!Walker.IsAlive())
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
                    100f, // Assuming 100f as the minimum camera distance
                    500f); // Assuming 500f as the maximum camera distance
            }
            _previousScrollValue = currentScroll;

            base.Update(time);
        }

        // --- Helper Methods ---

        /// <summary>
        /// Calculates which terrain tile is under the mouse cursor by raycasting.
        /// Uses caching to avoid recalculating when mouse hasn't moved.
        /// </summary>
        private void CalculateMouseTilePos()
        {
            var currentMousePos = MuGame.Instance.Mouse.Position;
            var currentCamPos = Camera.Instance.Position;

            // Check if we need to recalculate
            if (currentMousePos == _lastMousePosition && 
                currentCamPos == _lastCameraPosition && 
                !_mouseTileDirty)
            {
                return; // Use cached values
            }

            _lastMousePosition = currentMousePos;
            _lastCameraPosition = currentCamPos;
            _mouseTileDirty = false;

            // Use pre-calculated MouseRay from MuGame (already updated only when mouse moves)
            var ray = MuGame.Instance.MouseRay;
            
            // Optimized ray march with larger steps
            const float maxDistance = 5000f; // Reduced from 10000f - camera rarely needs more
            float coarseStep = Constants.TERRAIN_SCALE; // 100f instead of 40f
            float fineStep = Constants.TERRAIN_SCALE / 10f; // 10f for refinement
            float traveled = 0f;

            var lastPos = ray.Position;
            var lastDiff = lastPos.Z - Terrain.RequestTerrainHeight(lastPos.X, lastPos.Y) + ExtraHeight;
            bool hit = false;
            Vector3 hitPos = Vector3.Zero;

            while (traveled < maxDistance)
            {
                traveled += coarseStep;
                var pos = ray.Position + ray.Direction * traveled;
                float terrainZ = Terrain.RequestTerrainHeight(pos.X, pos.Y) + ExtraHeight;
                float diff = pos.Z - terrainZ;

                if (lastDiff > 0f && diff <= 0f)
                {
                    // Refine within the overshoot segment using smaller steps
                    float backtrackStart = traveled - coarseStep;
                    float refineTraveled = backtrackStart;
                    var refineLastPos = ray.Position + ray.Direction * backtrackStart;
                    float refineLastDiff = refineLastPos.Z - Terrain.RequestTerrainHeight(refineLastPos.X, refineLastPos.Y) + ExtraHeight;

                    while (refineTraveled <= traveled)
                    {
                        refineTraveled += fineStep;
                        var refinePos = ray.Position + ray.Direction * refineTraveled;
                        float refineTerrainZ = Terrain.RequestTerrainHeight(refinePos.X, refinePos.Y) + ExtraHeight;
                        float refineDiff = refinePos.Z - refineTerrainZ;

                        if (refineLastDiff > 0f && refineDiff <= 0f)
                        {
                            float t = refineLastDiff / (refineLastDiff - refineDiff);
                            hitPos = Vector3.Lerp(refineLastPos, refinePos, t);
                            hit = true;
                            break;
                        }

                        refineLastPos = refinePos;
                        refineLastDiff = refineDiff;
                    }

                    if (hit)
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

        /// <summary>
        /// Returns the first <see cref="MonsterObject"/> occupying the given tile, or <c>null</c>.
        /// </summary>
        private MonsterObject FindMonsterAtTile(byte tileX, byte tileY)
        {
            var monsters = Monsters;
            for (int i = 0; i < monsters.Count; i++)
            {
                var m = monsters[i];
                if (m != null &&
                    m.Location.X == tileX &&
                    m.Location.Y == tileY)
                {
                    return m;
                }
            }
            return null;
        }
    }
}
