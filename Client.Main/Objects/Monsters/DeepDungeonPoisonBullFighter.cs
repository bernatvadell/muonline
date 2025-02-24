using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    internal class DeepDungeonPoisonBullFighter : MonsterObject
    {
        public DeepDungeonPoisonBullFighter()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster321.bmd");
            await base.Load();
        }
    }
}
