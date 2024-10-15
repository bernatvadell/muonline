using Client.Main.Controls;
using Microsoft.Xna.Framework;

namespace Client.Main.Worlds
{
    public class AtlansWorld : WalkableWorldControl
    {
        public AtlansWorld() : base(worldIndex: 8)
        {

        }

        public override void AfterLoad()
        {
            base.AfterLoad();
            Walker.Location = new Vector2(20, 20);
        }

        public override void Draw(GameTime time)
        {
            base.Draw(time);
        }
    }
}
