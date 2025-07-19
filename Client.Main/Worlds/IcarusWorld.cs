using Client.Main.Controls;
using Client.Main.Core.Utilities;
using Client.Main.Objects.Worlds.Icarus;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    [WorldInfo(10, "Icarus")]
    public class IcarusWorld : WalkableWorldControl
    {
        private static readonly Color CLEAR_COLOR = new Color(3f / 256f, 25f / 256f, 44f / 256f, 1f);
        // private CloudLightEffect _cloudLight;
        // private JointThunderEffect _jointThunder;
        private SkyCloudSystem _skyCloudSystem;

        public IcarusWorld() : base(worldIndex: 11)
        {
            EnableShadows = false;
            Terrain.TextureMappingFiles = new Dictionary<int, string>
            {
                { 10, "TileRock04.OZJ" }
            };
            ExtraHeight = 0f;
            BackgroundMusicPath = "Music/icarus.mp3";
        }

        public override async Task Load()
        {
            // Objects.Add(_cloudLight = new CloudLightEffect());
            // Objects.Add(_jointThunder = new JointThunderEffect());
            
            // Create sky-wide cloud system instead of individual objects
            _skyCloudSystem = new SkyCloudSystem();
            Objects.Add(_skyCloudSystem);
            
            await base.Load();
        }

        public override void AfterLoad()
        {
            Vector2 defaultSpawn = new Vector2(14, 12);
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
            base.AfterLoad();
        }

        protected override void CreateMapTileObjects()
        {
            base.CreateMapTileObjects();

            // Remove cloud objects from tiles - now handled by sky system
            MapTileObjects[10] = typeof(WallObject);
        }

        public override void Update(GameTime time)
        {
            base.Update(time);

            if (World is WalkableWorldControl walkableWorld)
            {
                // _cloudLight.Position = walkableWorld.Walker.Position;
                // _jointThunder.Position = walkableWorld.Walker.Position;
            }
        }

        public override void Draw(GameTime time)
        {
            GraphicsDevice.Clear(CLEAR_COLOR);
            base.Draw(time);
        }
    }
}
