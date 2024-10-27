using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

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
