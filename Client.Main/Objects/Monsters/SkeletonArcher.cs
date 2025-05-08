using Client.Main.Content;
using Client.Main.Models;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(15, "Skeleton Archer")]
    public class SkeletonArcher : MonsterObject
    {
        public SkeletonArcher()
        {
            Scale = 1.1f; // Set according to C++ Setting_Monster
            RenderShadow = true;
        }

        public override async Task Load()
        {
            // Uses player model with a specific appearance subtype
            // Model = await BMDLoader.Instance.Prepare($"Player/Player.bmd");
            // TODO: Implement SubType handling in C# to apply MODEL_SKELETON2 appearance
            await base.Load();
        }
        // No specific sounds assigned in C++ for this SubType
    }
}