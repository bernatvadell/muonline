using Client.Main.Controls;
using Microsoft.Xna.Framework;

namespace Client.Main.Worlds
{
    public class TarkanWorld : WalkableWorldControl
    {
        public TarkanWorld() : base(worldIndex: 9) // TARKAN
        {
            Name = "Tarkan";
            BackgroundMusicPath = "Music/tarkan.mp3";
        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(200, 58);
            base.AfterLoad();
        }
    }
}
