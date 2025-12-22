using Client.Main.Content;
using Microsoft.Xna.Framework;
using System;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(105, "Canon Trap")]
    public class CanonTrap : MonsterObject
    {
        public CanonTrap()
        {
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/c_mon.bmd");
            await base.Load();
        }
    }
}