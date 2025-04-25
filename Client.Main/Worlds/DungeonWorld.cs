using Client.Main.Controls;
using Microsoft.Xna.Framework;

namespace Client.Main.Worlds
{
    public class DungeonWorld : WalkableWorldControl
    {
        public DungeonWorld() : base(worldIndex: 2)
        {
            BackgroundMusicPath = "Music/Dungeon.mp3";
            Name = "Dungeon";
        }

        public override void AfterLoad()
        {
            base.AfterLoad();
            Walker.Location = new Vector2(232, 126);
        }
    }
}
