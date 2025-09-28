using Client.Main.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(75, "Great Drakan")]
    public class GreatDrakan : MonsterObject
    {
        public GreatDrakan()
        {
            Scale = 1.0f;
            // Set meshes that should NOT use blending (equivalent to NoneBlendMesh = true)
            NoneBlendMeshes.Add(0); // Mesh 0: no blending
            NoneBlendMeshes.Add(3); // Mesh 3: no blending
            NoneBlendMeshes.Add(4); // Mesh 4: no blending
            // Mesh 1 and 2 will use blending (not in NoneBlendMeshes set)
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster55.bmd"); // TODO
            await base.Load();
        }
    }
}
