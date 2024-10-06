using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Client.Main
{
    public class Camera
    {
        public static Camera Instance { get; } = new Camera();

        private float _aspectRatio = 1.4f;
        private float _fov = 35f;
        private float _viewNear = 1f;
        private float _viewFar = 2000f;
        private Vector3 _position = Vector3.Zero;
        private Vector3 _target = Vector3.Zero;

        public float AspectRatio { get => _aspectRatio; set { _aspectRatio = value; UpdateProjection(); } }
        public float FOV { get => _fov; set { _fov = value; UpdateProjection(); } }
        public float ViewNear { get => _viewNear; set { _viewNear = value; UpdateProjection(); } }
        public float ViewFar { get => _viewFar; set { _viewFar = value; UpdateProjection(); } }

        public Vector3 Position { get => _position; set { _position = value; UpdateView(); } }
        public Vector3 Target { get => _target; set { _target = value; UpdateView(); } }

        public Matrix View { get; private set; }
        public Matrix Projection { get; private set; }

        public BoundingFrustum Frustum { get; private set; }

        public event EventHandler CameraMoved;

        private Camera()
        {
            UpdateProjection();
            UpdateView();
        }

        public void ForceUpdate()
        {
            UpdateProjection();
            UpdateView();
            UpdateFrustum();
        }

        private void UpdateProjection()
        {
            Projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(_fov),
                _aspectRatio,
                _viewNear,
                _viewFar
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
