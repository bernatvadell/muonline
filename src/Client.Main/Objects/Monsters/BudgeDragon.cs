using Client.Main.Content;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Objects.Monsters
{
    public class BudgeDragon : MonsterObject
    {
        public BudgeDragon()
        {
            RenderShadow = true;
            Scale = 0.7f;
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"Monster/Monster03.bmd");
            Position = new Vector3(Position.X, Position.Y, Position.Z - 40f);
            await base.Load();
        }
    }
}
