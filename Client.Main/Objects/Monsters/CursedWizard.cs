using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    [NpcInfo(34, "Cursed Wizard")]
    public class CursedWizard : MonsterObject
    {
        public CursedWizard()
        {
            RenderShadow = true;
            Scale = 1.0f; // Default, adjust if needed
        }

        public override async Task Load()
        {
            // C++ uses CreateCharacter with MODEL_PLAYER and sets equipment
            // Model = await BMDLoader.Instance.Prepare($"Player/Player.bmd");
            // TODO: C# needs a mechanism to apply specific equipment (Legendary Set)
            // based on the MonsterType/SubType. This might involve a separate
            // Equipment system or logic within the MonsterObject or a derived class.
            await base.Load();
        }
        // No specific sounds assigned in C++ for this case
    }
}