using Client.Main.Controls;
using Client.Main.Core.Utilities;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    [WorldInfo(11, "Blood Castle 1")]
    public class BloodCastle1World : WalkableWorldControl
    {
        public BloodCastle1World() : base(worldIndex: 12) // All BC1-7 use World12
        {
            BackgroundMusicPath = "Sound/iBloodCastle.wav";
            Name = "Blood Castle 1";
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
