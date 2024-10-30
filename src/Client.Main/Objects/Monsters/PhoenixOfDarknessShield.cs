using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    public class PhoenixOfDarknessShield : MonsterObject
    {
        public PhoenixOfDarknessShield()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster57.bmd");
            await base.Load();
        }
    }
}
