using Client.Main.Controls;
using Client.Main.Core.Utilities;
using Client.Main.Objects.Worlds.Atlans;
using Microsoft.Xna.Framework;

namespace Client.Main.Worlds
{
    [WorldInfo(7, "Atlans")]
    public class AtlansWorld : WalkableWorldControl
    {
        private BoidManager _boidManager;

        public AtlansWorld() : base(worldIndex: 8)
        {
            BackgroundMusicPath = "Music/atlans.mp3";
            Name = "Atlans";
        }
        public override void AfterLoad()
        {
            Vector2 defaultSpawn = new Vector2(20, 20);

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

            // Initialize fish boid system for underwater areas
            _boidManager = new BoidManager(this);

            base.AfterLoad();
        }

        protected override void CreateMapTileObjects()
        {
            base.CreateMapTileObjects();

            var waterPlantIndices = new[] { 5, 6, 24, 25, 26, 27, 31, 33 };
            foreach (var index in waterPlantIndices)
            {
                MapTileObjects[index] = typeof(WaterPlantObject);
            }

            var gateIndices = new[] { 32, 34 };
            foreach (var index in gateIndices)
            {
                MapTileObjects[index] = typeof(GateObject);
            }

            MapTileObjects[22] = typeof(BubblesObject);
            MapTileObjects[23] = typeof(WaterPortalObject);
            MapTileObjects[38] = typeof(LightBeamObject);
            MapTileObjects[40] = typeof(PortalObject);
        }

        public override void Update(GameTime time)
        {
            base.Update(time);

            // Update fish boid system
            _boidManager?.Update(time);
        }

        public override void Dispose()
        {
            // Clean up fish before disposing world
            _boidManager?.Clear();
            _boidManager = null;

            base.Dispose();
        }
    }
}
