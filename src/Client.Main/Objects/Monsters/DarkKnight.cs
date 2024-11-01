using Client.Main.Content;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    public class DarkKnight : MonsterObject
    {
        public DarkKnight() 
        {
            RenderShadow = false;
        }

    public override async Task Load()
    {
        Model = await BMDLoader.Instance.Prepare($"Monster/Monster04.bmd");
        await base.Load();
    }
}
}
