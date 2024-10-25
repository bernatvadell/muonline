using Client.Data;
using Client.Data.BMD;
using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Objects.Effects;
using Client.Main.Objects.Worlds.Login;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class LoginWorld : TourWorldControl
    {
        public LoginWorld() : base(worldIndex: 74)
        {
            Camera.Instance.ViewFar = 2500f;
            Camera.Instance.ViewNear = 1;
            Camera.Instance.FOV = 65f;
        }

        protected override void CreateMapTileObjects()
        {
            base.CreateMapTileObjects();
            MapTileObjects[37] = typeof(TorchObject);
            MapTileObjects[79] = typeof(StatueTorchObject);
        }
    }
}
