using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(358, "Persona")]
    public class Persona : MonsterObject
    {
        public Persona()
        {
            Scale = 1.0f;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster115.bmd");
            await base.Load();
        }
    }
}
