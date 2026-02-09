using Microsoft.Xna.Framework;
using System;
using System.Diagnostics;

namespace Client.Main.Graphics
{
    public class Camera
    {
        // Static Properties
        public static Camera Instance { get; } = new Camera();

        // Fields
        private float _aspectRatio = 1.4f;
        private float _fov = 35f;
        private float _viewNear = 10f;  // Increased from 1f to improve depth precision
        private float _viewFar = 1800f;
        private Vector3 _position = Vector3.Zero;
        private Vector3 _target = Vector3.Zero;
        private readonly BoundingFrustum _frustum = new(Matrix.Identity);

        // Throttle CameraMoved events to avoid flooding subscribers during rapid camera updates
        private long _lastCameraMovedTimeMs = 0;
        private const int CAMERA_MOVED_COOLDOWN_MS = 300; // milliseconds
        private readonly object _cameraMovedLock = new object();

        // Screen shake state
        private float _shakeIntensity;
        private float _shakeTimeLeft;
        private float _shakeFrequency = 25f;
        private float _shakeElapsed;
        private readonly Random _shakeRng = new();

        // Public Properties
        public float AspectRatio
        {
            get => _aspectRatio;
            set { if (_aspectRatio != value) { _aspectRatio = value; UpdateProjection(); } }
        }

        public float FOV
        {
            get => _fov;
            set { if (_fov != value) { _fov = value; UpdateProjection(); } }
        }

        public float ViewNear
        {
            get => _viewNear;
            set { if (_viewNear != value) { _viewNear = value; UpdateProjection(); } }
        }

        public float ViewFar
        {
            get => _viewFar;
            set { if (_viewFar != value) { _viewFar = value; UpdateProjection(); } }
        }

        public Vector3 Position
        {
            get => _position;
            set { if (_position != value) { _position = value; UpdateView(); } }
        }

        public Vector3 Target
        {
            get => _target;
            set { if (_target != value) { _target = value; UpdateView(); } }
        }

        public Matrix View { get; private set; }
        public Matrix Projection { get; private set; }
        public BoundingFrustum Frustum => _frustum;

        // Events
        public event EventHandler CameraMoved;

        // Constructors
        private Camera()
        {
            UpdateProjection();
            UpdateView();
        }

        // Public Methods
        public void ForceUpdate()
        {
            UpdateProjection();
            UpdateView();
            UpdateFrustum();
        }

        /// <summary>
        /// Starts a screen shake. Multiple calls pick the stronger intensity.
        /// </summary>
        public void Shake(float intensity, float duration, float frequency = 25f)
        {
            if (intensity > _shakeIntensity || _shakeTimeLeft <= 0f)
            {
                _shakeIntensity = intensity;
                _shakeFrequency = frequency;
            }
            if (duration > _shakeTimeLeft)
                _shakeTimeLeft = duration;
        }

        /// <summary>
        /// Call once per frame from the main update loop to advance shake timer.
        /// </summary>
        public void UpdateShake(float dt)
        {
            if (_shakeTimeLeft <= 0f)
                return;

            _shakeElapsed += dt;
            _shakeTimeLeft -= dt;

            if (_shakeTimeLeft <= 0f)
            {
                _shakeIntensity = 0f;
                _shakeTimeLeft = 0f;
            }

            UpdateView();
        }

        // Private Methods
        private void UpdateProjection()
        {
            Projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(_fov),
                _aspectRatio,
                _viewNear,
                _viewFar + Constants.MAX_CAMERA_DISTANCE
            );

            UpdateFrustum();
            RaiseCameraMovedThrottled();
        }

        private void UpdateView()
        {
            Vector3 pos = Position;
            Vector3 tgt = Target;

            if (_shakeTimeLeft > 0f && _shakeIntensity > 0f)
            {
                float decay = MathHelper.Clamp(_shakeTimeLeft / 0.5f, 0f, 1f);
                float strength = _shakeIntensity * decay;
                float t = _shakeElapsed * _shakeFrequency;
                float ox = MathF.Sin(t * 1.0f + 0.0f) * strength;
                float oy = MathF.Sin(t * 1.3f + 1.7f) * strength;
                float oz = MathF.Sin(t * 0.9f + 3.1f) * strength * 0.5f;
                var offset = new Vector3(ox, oy, oz);
                pos += offset;
                tgt += offset;
            }

            Vector3 cameraDirection = Vector3.Normalize(tgt - pos);
            Vector3 cameraRight = Vector3.Cross(cameraDirection, Vector3.UnitZ);
            var cameraUp = Vector3.Cross(cameraRight, cameraDirection);

            View = Matrix.CreateLookAt(pos, tgt, cameraUp);

            UpdateFrustum();
            RaiseCameraMovedThrottled();
        }

        private void UpdateFrustum()
        {
            _frustum.Matrix = View * Projection;
        }

        /// <summary>
        /// Raises the CameraMoved event but limits firing to once every CAMERA_MOVED_COOLDOWN_MS milliseconds.
        /// </summary>
        private void RaiseCameraMovedThrottled()
        {
            long nowMs = Stopwatch.GetTimestamp() * 1000 / Stopwatch.Frequency;
            lock (_cameraMovedLock)
            {
                if (nowMs - _lastCameraMovedTimeMs >= CAMERA_MOVED_COOLDOWN_MS)
                {
                    _lastCameraMovedTimeMs = nowMs;
                    CameraMoved?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }
}
