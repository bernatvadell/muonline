using Client.Data.BMD;
using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Objects;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class LoginWorld : WorldControl
    {
        public LoginWorld() : base(worldIndex: 74, tourMode: true)
        {
            Camera.Instance.ViewFar = 7000f;
            Camera.Instance.FOV = 65f;
        }
    }
}
