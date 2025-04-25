using Client.Main.Controls;
using Microsoft.Xna.Framework;

namespace Client.Main.Worlds
{
    public class ElvelandWorld : WalkableWorldControl
    {
        public ElvelandWorld() : base(worldIndex: 52)
        {
            BackgroundMusicPath = "Music/elbeland.mp3";
            Name = "Elbeland";
        }

        public override void AfterLoad()
        {
            base.AfterLoad();
            Walker.Location = new Vector2(61, 201);
        }
    }
}
