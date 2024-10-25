using Client.Main.Content;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    public class IceQueen : MonsterObject
    {
        public IceQueen()
        {
            RenderShadow = false; 
            BlendMesh = 2;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster19.bmd");
            await base.Load();
        }
    }
}
