using Microsoft.Xna.Framework;
using System;

namespace Client.Main
{
    public class Camera
    {
        // Static Properties
        public static Camera Instance { get; } = new Camera();

        // Fields
        private float _aspectRatio = 1.4f;
        private float _fov = 35f;
        private float _viewNear = 1f;
        private float _viewFar = 1800f;
        private Vector3 _position = Vector3.Zero;
        private Vector3 _target = Vector3.Zero;

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
        public BoundingFrustum Frustum { get; private set; }

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
            CameraMoved?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateView()
        {
            Vector3 cameraDirection = Vector3.Normalize(Target - Position);
            Vector3 cameraRight = Vector3.Cross(cameraDirection, Vector3.UnitZ);
            var cameraUp = Vector3.Cross(cameraRight, cameraDirection);

            View = Matrix.CreateLookAt(Position, Target, cameraUp);

            UpdateFrustum();
            CameraMoved?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateFrustum()
        {
            Frustum = new BoundingFrustum(View * Projection);
        }
    }
}