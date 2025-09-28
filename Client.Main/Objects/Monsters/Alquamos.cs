using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(69, "Alquamos")]
    public class Alquamos : MonsterObject
    {
        public Alquamos()
        {
            Scale = 1.0f;
            BlendMesh = -2; // Use full blending like other semi-transparent monsters
            BlendMeshLight = 0.7f; // Reduced light for more subtle blending
            Alpha = 0.85f; // Slightly transparent for better blending with environment
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster51.bmd");
            await base.Load();
        }
    }
}
