using Client.Main.Controls;
using Client.Main.Objects.Monsters;
using Client.Main.Objects.NPCS;
using Client.Main.Objects.Worlds.Noria;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class NoriaWorld : WalkableWorldControl
    {
        public NoriaWorld() : base(worldIndex: 4)
        {
            BackgroundMusicPath = "Music/Noria.mp3";
            Name = "Noria";
        }

        public override async Task Load()
        {
            await base.Load();

            // Objects.Add(new ElfLala() { Location = new Vector2(173, 125), Direction = Models.Direction.SouthWest });
            // Objects.Add(new EoTheCraftsman() { Location = new Vector2(195, 124), Direction = Models.Direction.South });
            // Objects.Add(new Charon() { Location = new Vector2(171, 104), Direction = Models.Direction.SouthEast });
            // Objects.Add(new IceQueen() { Location = new Vector2(175, 117), Direction = Models.Direction.South });
        }

        public override void AfterLoad()
        {
            Vector2 defaultSpawn = new Vector2(174, 123);

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

            Walker.MoveTargetPosition = Walker.TargetPosition;
            Walker.Position = Walker.TargetPosition;

            // water animation parameters for Noria
            Terrain.WaterSpeed = 0.05f;             // Example: faster water movement
            Terrain.DistortionAmplitude = 0.1f;      // Example: stronger distortion
            Terrain.DistortionFrequency = 2.0f;      // Example: lower frequency for distortion

            base.AfterLoad();
        }

        protected override void CreateMapTileObjects()
        {
            base.CreateMapTileObjects();

            MapTileObjects[39] = typeof(ChaosMachineObject);

            MapTileObjects[38] = typeof(RestPlaceObject);

            MapTileObjects[8] = typeof(SitPlaceObject);

            MapTileObjects[6] = typeof(ClimberObject);

            MapTileObjects[37] = typeof(LightBeamObject);

            MapTileObjects[18] = typeof(EoTheCraftsmanPlaceObject);
        }
    }
}
