using Client.Main.Controls;
using Client.Main.Objects.Worlds.Atlans;
using Microsoft.Xna.Framework;

namespace Client.Main.Worlds
{
    public class AtlansWorld : WalkableWorldControl
    {
        public AtlansWorld() : base(worldIndex: 8)
        {
            BackgroundMusicPath = "Music/atlans.mp3";
        }
        public override void AfterLoad()
        {
            base.AfterLoad();
            Walker.Location = new Vector2(20, 20);
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

            MapTileObjects[22] = typeof(BubblesObject); // TODO more bubbles and add some bubble move
            MapTileObjects[23] = typeof(WaterPortalObject);
            MapTileObjects[38] = typeof(LightBeamObject);
            MapTileObjects[40] = typeof(PortalObject);
        }
    }
}
