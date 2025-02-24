using Client.Main.Controls;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class World115World : WalkableWorldControl
    {
        public World115World() : base(worldIndex: 115) // LOREN MARKET & EVENT SQUARE (02)
        {

        }

        public override void AfterLoad()
        {            
            Walker.Location = new Vector2(22, 12);
            base.AfterLoad();
        }
    }
}
