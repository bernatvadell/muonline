using Client.Main.Controls;
using Microsoft.Xna.Framework;

namespace Client.Main.Worlds
{
    public class LostTowerWorld : WalkableWorldControl
    {
        public LostTowerWorld() : base(worldIndex: 5)
        {
            BackgroundMusicPath = "Music/lost_tower_b.mp3";
            Name = "Lost Tower";
        }

        public override void AfterLoad()
        {
            base.AfterLoad();
            Walker.Location = new Vector2(208, 81);
        }
    }
}
