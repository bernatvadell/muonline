using Client.Main.Controls;
using Microsoft.Xna.Framework;

namespace Client.Main.Worlds
{
    public class World034World : WalkableWorldControl
    {
        public World034World() : base(worldIndex: 34) // AIDA
        {
            Name = "Aida";
            BackgroundMusicPath = "Music/Aida.mp3";
        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(85, 10);
            base.AfterLoad();
        }
    }
}
