using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    public class ScorchedWizardElite : MonsterObject
    {
        public ScorchedWizardElite()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster357.bmd"); // Scorched Wizard (Elite)
            await base.Load();
        }
    }
}
