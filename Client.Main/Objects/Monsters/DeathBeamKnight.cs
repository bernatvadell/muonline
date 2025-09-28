using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(63, "Death Beam Knight")]
    public class DeathBeamKnight : MonsterObject
    {
        public DeathBeamKnight()
        {
            Scale = 1.9f;
            BlendMesh = -2; // Makes the entire monster semi-transparent like in original
            BlendMeshLight = 1.0f;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster45.bmd");
            await base.Load();
        }
    }
}
