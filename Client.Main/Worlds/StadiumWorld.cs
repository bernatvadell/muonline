using Client.Main.Controls;
using Microsoft.Xna.Framework;

namespace Client.Main.Worlds
{
    public class StadiumWorld : WalkableWorldControl
    {
        public StadiumWorld() : base(worldIndex: 7) // ARENA
        {
            Name = "Arena";
        }

        protected override void CreateMapTileObjects()
        {
            base.CreateMapTileObjects();
        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(56, 85);
            base.AfterLoad();
        }
    }
}
