using Client.Main.Controls;
using Microsoft.Xna.Framework;

namespace Client.Main.Worlds
{
    public class DevilSquareWorld : WalkableWorldControl
    {
        public DevilSquareWorld() : base(worldIndex: 10) // DEVIL SQUARE
        {
            Name = "Devil Square";
            BackgroundMusicPath = "Music/devil_square_intro.mp3";
        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(200, 58);
            base.AfterLoad();
        }
    }
}
