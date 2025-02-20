using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    public abstract class MonsterObject : WalkerObject
    {
        public MonsterObject()
        {
            Interactive = true;
        }
    }
}
