using Client.Main.Content;
using Client.Main.Models;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(16, "Elite Skeleton")]
    public class EliteSkeleton : MonsterObject
    {
        public EliteSkeleton()
        {
            Scale = 1.2f; // Set according to C++ Setting_Monster
            RenderShadow = true;
        }

        public override async Task Load()
        {
            // Uses player model with a specific appearance subtype
            Model = await BMDLoader.Instance.Prepare($"Skill/Skeleton03.bmd");
            await base.Load();
        }
        // No specific sounds assigned in C++ for this SubType
    }
}