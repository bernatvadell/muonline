using Client.Main.Controls;
using Client.Main.Objects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class SelectCharacterWorld : WorldControl
    {
        public SelectCharacterWorld() : base(worldIndex: 75)
        {
            Camera.Instance.ViewFar = 5000f;
            Camera.Instance.Position = new Vector3(9858, 18813, 700);
            Camera.Instance.Target = new Vector3(7200, 19550, 550);
        }

        public override void Update(GameTime time)
        {
            base.Update(time);
        }
    }
}
