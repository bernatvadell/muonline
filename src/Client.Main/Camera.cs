using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main
{
    public class Camera
    {
        public static Camera Instance { get; } = new Camera();

        private float _aspectRatio = 1.4f;
        private float _fov = 35f;
        private float _viewNear = 1f;
        private float _viewFar = 2200f;
        private Vector3 _position = new Vector3(100, 900, 330);
        private Vector3 _target = new Vector3(100, 1200, 110);

        public float AspectRatio { get => _aspectRatio; set { _aspectRatio = value; UpdateProjection(); } }
        public float FOV { get => _fov; set { _fov = value; UpdateProjection(); } }
        public float ViewNear { get => _viewNear; set { _viewNear = value; UpdateProjection(); } }
        public float ViewFar { get => _viewFar; set { _viewFar = value; UpdateProjection(); } }

        public Vector3 Position { get => _position; set { _position = value; UpdateView(); } }
        public Vector3 Target { get => _target; set { _target = value; UpdateView(); } }

        public Matrix View { get; private set; }
        public Matrix Projection { get; private set; }

        public BoundingFrustum Frustum { get; private set; }

        public bool FollowTarget { get; set; } = true;

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
        }

        private void UpdateView()
        {
            Vector3 cameraUp = Vector3.Up;

            if (FollowTarget)
            {
                Vector3 cameraDirection = Vector3.Normalize(Target - Position);
                Vector3 cameraRight = Vector3.Cross(cameraDirection, Vector3.UnitZ);
                cameraUp = Vector3.Cross(cameraRight, cameraDirection);
            }

            View = Matrix.CreateLookAt(Position, Target, cameraUp);

            UpdateFrustum();
        }

        private void UpdateFrustum()
        {
            float adjustedFOV = _fov + 10f;
            float adjustedViewFar = _viewFar + 120f;

            Matrix expandedProjection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(adjustedFOV),
                _aspectRatio,
                _viewNear,
                adjustedViewFar
            );

            Frustum = new BoundingFrustum(View * expandedProjection);
        }
    }
}
