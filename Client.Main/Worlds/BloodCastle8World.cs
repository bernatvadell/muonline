using Client.Main.Controls;
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    [WorldInfo(52, "Blood Castle 8 (Master Level)")]
    public class BloodCastle8World : WalkableWorldControl
    {
        public BloodCastle8World() : base(worldIndex: 52)
        {
            BackgroundMusicPath = "Sound/iBloodCastle.wav";
            Name = "Blood Castle 8 (Master Level)";
        }

        public override async Task Load()
        {
            await base.Load();
        }

        public override void AfterLoad()
        {
            Vector2 defaultSpawn = new Vector2(13, 9);

            Walker.Reset();

            bool shouldUseDefaultSpawn = false;
            if (MuGame.Network == null ||
                MuGame.Network.CurrentState == Core.Client.ClientConnectionState.Initial ||
                MuGame.Network.CurrentState == Core.Client.ClientConnectionState.Disconnected)
            {
                shouldUseDefaultSpawn = true;
            }
            else if (Walker.Location == Vector2.Zero)
            {
                shouldUseDefaultSpawn = true;
            }

            if (shouldUseDefaultSpawn)
            {
                Walker.Location = defaultSpawn;
            }

            base.AfterLoad();
        }
    }
}
