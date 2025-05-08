using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(40, "Death Knight")]
    public class DeathKnightMonster : MonsterObject
    {
        public DeathKnightMonster()
        {
            Scale = 1.3f; // Set according to C++ Setting_Monster
        }

        public override async Task Load()
        {
            // Model Loading Type: 29 -> File Number: 29 + 1 = 30
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster30.bmd"); // Uses its own model
            await base.Load(); // Calls base Load, might overwrite Model if not handled correctly, check inheritance
                               // C++: Models[MODEL_MONSTER01+Type].BoneHead = 19;
        }
    }
}