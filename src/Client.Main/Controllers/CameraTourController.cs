using Client.Data.CWS;
using Client.Main.Controls;
using Microsoft.Xna.Framework;

namespace Client.Main.Controllers
{
    public class CameraTourController
    {
        private int _currentWaypointIndex = 0;
        private float _waitTime = 0f;
        private bool _isWaiting = false;

        private Vector3 _startPosition;
        private Vector3 _endPosition;
        private Vector3 _startTarget;
        private Vector3 _endTarget;
        private float _moveProgress = 0f;
        private float _moveDuration;

        public WayPoint[] WayPoints { get; }
        public bool Loop { get; }
        public WorldControl World { get; }

        public CameraTourController(WayPoint[] wayPoints, bool loop, WorldControl worldControl)
        {
            World = worldControl;
            WayPoints = wayPoints;
            Loop = loop;

            if (WayPoints.Length > 0)
            {
                var firstWaypoint = WayPoints[0];
                var terrainHeight = World.Terrain.RequestTerrainHeight(firstWaypoint.CameraX, firstWaypoint.CameraY);
                Camera.Instance.Position = new Vector3(firstWaypoint.CameraX, firstWaypoint.CameraY, firstWaypoint.CameraZ + terrainHeight);

                if (WayPoints.Length > 1)
                {
                    var nextWaypoint = WayPoints[1];
                    var nextTerrainHeight = World.Terrain.RequestTerrainHeight(nextWaypoint.CameraX, nextWaypoint.CameraY);
                    var nextPosition = new Vector3(nextWaypoint.CameraX, nextWaypoint.CameraY, nextWaypoint.CameraZ + nextTerrainHeight);
                    var initialDirection = Vector3.Normalize(nextPosition - Camera.Instance.Position);
                    Camera.Instance.Target = Camera.Instance.Position + initialDirection;
                }
                else
                {
                    Camera.Instance.Target = Camera.Instance.Position + Vector3.Forward;
                }

                PrepareNextMove();
            }
        }

        private void PrepareNextMove()
        {
            var currentWaypoint = WayPoints[_currentWaypointIndex];
            var terrainHeight = World.Terrain.RequestTerrainHeight(currentWaypoint.CameraX, currentWaypoint.CameraY);
            _startPosition = Camera.Instance.Position;
            _endPosition = new Vector3(currentWaypoint.CameraX, currentWaypoint.CameraY, currentWaypoint.CameraZ + terrainHeight);

            float distance = Vector3.Distance(_startPosition, _endPosition);
            float moveSpeed = currentWaypoint.CameraMoveAccel;

            if (moveSpeed <= 0f)
                moveSpeed = 0.1f;

            _moveDuration = distance / moveSpeed;

            if (_moveDuration <= 0f)
                _moveDuration = 1f;

            int nextIndex = _currentWaypointIndex + 1;
            if (nextIndex < WayPoints.Length)
            {
                var nextWaypoint = WayPoints[nextIndex];
                var nextTerrainHeight = World.Terrain.RequestTerrainHeight(nextWaypoint.CameraX, nextWaypoint.CameraY);
                var nextPosition = new Vector3(nextWaypoint.CameraX, nextWaypoint.CameraY, nextWaypoint.CameraZ + nextTerrainHeight);
                _endTarget = nextPosition;
            }
            else
            {
                _endTarget = _endPosition + (Camera.Instance.Target - Camera.Instance.Position);
            }

            _startTarget = Camera.Instance.Target;
            _moveProgress = 0f;
        }

        public void Update(GameTime gameTime)
        {
            if (_currentWaypointIndex >= WayPoints.Length) { 
                return;
            }

            var currentWaypoint = WayPoints[_currentWaypointIndex];

            if (_isWaiting)
            {
                _waitTime += (float)gameTime.ElapsedGameTime.TotalSeconds;

                if (_waitTime >= currentWaypoint.Delay)
                {
                    _isWaiting = false;
                    _waitTime = 0f;
                    _currentWaypointIndex++;

                    if (_currentWaypointIndex >= WayPoints.Length)
                    {
                        if (Loop) _currentWaypointIndex = 0;
                        return;
                    }

                    PrepareNextMove();
                }
            }
            else
            {
                _moveProgress += FPSCounter.Instance.FPS_ANIMATION_FACTOR / _moveDuration;
                _moveProgress = MathHelper.Clamp(_moveProgress, 0f, 1f);

                Camera.Instance.Position = Vector3.Lerp(_startPosition, _endPosition, _moveProgress);

                float targetStartProgress = 0.7f;
                if (_moveProgress < targetStartProgress)
                {
                    Camera.Instance.Target = _startTarget;
                }
                else
                {
                    float adjustedProgress = (_moveProgress - targetStartProgress) / (1f - targetStartProgress);
                    adjustedProgress = MathHelper.Clamp(adjustedProgress, 0f, 1f);
                    Camera.Instance.Target = Vector3.Lerp(_startTarget, _endTarget, adjustedProgress);
                }

                Camera.Instance.ForceUpdate();

                if (_moveProgress >= 1f)
                {
                    if (currentWaypoint.Delay > 0f)
                    {
                        _isWaiting = true;
                        _waitTime = 0f;
                    }
                    else
                    {
                        _currentWaypointIndex++;
                        if (_currentWaypointIndex < WayPoints.Length)
                            PrepareNextMove();
                        else if (Loop)
                        {
                            _currentWaypointIndex = 0;
                            PrepareNextMove();
                        }
                    }
                }
            }
        }
    }
}
