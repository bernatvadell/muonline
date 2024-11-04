using System.Numerics;

namespace Client.Data.CAP
{
    public class CameraAnglePosition
    {
        public Vector3 CameraAngle { get; set; }
        public Vector3 HeroPosition { get; set; }
        public Vector3 CameraPosition { get; set; }
        public float CameraDistance { get; set; }
        public float CameraZDistance { get; set; }
        public float CameraRatio { get; set; }
        public float CameraFOV { get; set; }
    }
}
