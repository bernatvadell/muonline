using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World132World : WalkableWorldControl
    {
        public World132World() : base(worldIndex: 132)
        {

        }

        public override void AfterLoad()
        {            
            Walker.Location = new Vector2(240, 157);
            base.AfterLoad();
        }
    }
}
