using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Objects.Worlds.Lorencia;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace Client.Main.Worlds
{
    public class LorenciaWorld : WalkableWorldControl
    {

        // Define the pub area as a rectangle
        private Rectangle pubArea = new Rectangle(12000, 12000, 900, 1500);
        // Flag to track if the player is already in the pub area
        private bool isInPubArea = false;
        private string pubMusicPath = "Music/Pub.mp3";

        public LorenciaWorld() : base(worldIndex: 1)
        {
            BackgroundMusicPath = "Music/MuTheme.mp3";
        }

        public override void AfterLoad()
        {
            Walker.Location = new Vector2(138, 124);

            // water animation parameters for LorenciaWorld
            Terrain.WaterSpeed = 0.05f;             // Example: faster water movement
            Terrain.DistortionAmplitude = 0.2f;      // Example: stronger distortion
            Terrain.DistortionFrequency = 1.0f;      // Example: lower frequency for distortion

            base.AfterLoad();
        }

        protected override void CreateMapTileObjects()
        {
            base.CreateMapTileObjects();

            for (var i = 0; i < 13; i++)
                MapTileObjects[i] = typeof(TreeObject);

            for (var i = 0; i < 8; i++)
                MapTileObjects[20 + i] = typeof(GrassObject);

            for (var i = 0; i < 5; i++)
                MapTileObjects[30 + i] = typeof(StoneObject);

            for (var i = 0; i < 3; i++)
                MapTileObjects[40 + i] = typeof(StoneStatueObject);

            MapTileObjects[43] = typeof(SteelStatueObject);

            for (var i = 0; i < 3; i++)
                MapTileObjects[44 + i] = typeof(TombObject);

            for (var i = 0; i < 2; i++)
                MapTileObjects[50 + i] = typeof(FireLightObject);

            MapTileObjects[52] = typeof(BonfireObject);
            MapTileObjects[55] = typeof(DungeonGateObject);

            for (var i = 0; i < 2; i++)
                MapTileObjects[56 + i] = typeof(MerchantAnimalObject);

            MapTileObjects[58] = typeof(TreasureDrumObject);
            MapTileObjects[59] = typeof(TreasureChestObject);
            MapTileObjects[60] = typeof(ShipObject);

            for (var i = 0; i < 3; i++)
                MapTileObjects[65 + i] = typeof(SteelWallObject);

            MapTileObjects[68] = typeof(SteelDoorObject);

            for (var i = 0; i < 6; i++)
                MapTileObjects[69 + i] = typeof(StoneWallObject);

            for (var i = 0; i < 4; i++)
                MapTileObjects[75 + i] = typeof(MuWallObject);

            MapTileObjects[80] = typeof(BridgeObject);

            for (var i = 0; i < 4; i++)
                MapTileObjects[81 + i] = typeof(FenceObject);

            MapTileObjects[85] = typeof(BridgeStoneObject);

            MapTileObjects[90] = typeof(StreetLightObject);

            for (var i = 0; i < 3; i++)
                MapTileObjects[91 + i] = typeof(CannonObject);

            MapTileObjects[95] = typeof(CurtainObject);

            for (var i = 0; i < 2; i++)
                MapTileObjects[96 + i] = typeof(SignObject);

            for (var i = 0; i < 4; i++)
                MapTileObjects[98 + i] = typeof(CarriageObject);

            for (var i = 0; i < 2; i++)
                MapTileObjects[102 + i] = typeof(StrawObject);

            MapTileObjects[105] = typeof(WaterSpoutObject);

            for (var i = 0; i < 4; i++)
                MapTileObjects[106 + i] = typeof(WellObject);

            MapTileObjects[110] = typeof(HangingObject);
            MapTileObjects[111] = typeof(StairObject);

            for (var i = 0; i < 5; i++)
                MapTileObjects[115 + i] = typeof(HouseObject);

            MapTileObjects[120] = typeof(TentObject);

            for (var i = 0; i < 6; i++)
                MapTileObjects[121 + i] = typeof(HouseWallObject);

            for (var i = 0; i < 3; i++)
                MapTileObjects[127 + i] = typeof(HouseEtcObject);

            for (var i = 0; i < 3; i++)
                MapTileObjects[130 + i] = typeof(LightObject);

            MapTileObjects[133] = typeof(PoseBoxObject);

            for (var i = 0; i < 7; i++)
                MapTileObjects[140 + i] = typeof(FurnitureObject);

            MapTileObjects[150] = typeof(CandleObject);

            for (var i = 0; i < 3; i++)
                MapTileObjects[151 + i] = typeof(BeerObject);
        }

        public override void Update(GameTime time)
        {
            base.Update(time);

            // Check player's position (only X and Y are relevant)
            Vector2 playerPos = new Vector2(Walker.Position.X, Walker.Position.Y);
            // Create a Point from player's position to use with Rectangle.Contains
            Point playerPoint = new Point((int)playerPos.X, (int)playerPos.Y);

            if (pubArea.Contains(playerPoint))
            {
                // If player enters the pub area and wasn't there before
                if (!isInPubArea)
                {
                    isInPubArea = true;
                    // Stop the current background music and play the pub music
                    SoundController.Instance.StopBackgroundMusic();
                    SoundController.Instance.PlayBackgroundMusic(pubMusicPath);
                }
            }
            else
            {
                // If player leaves the pub area and was previously inside
                if (isInPubArea)
                {
                    isInPubArea = false;
                    // Stop the pub music and resume the default background music
                    SoundController.Instance.StopBackgroundMusic();
                    SoundController.Instance.PlayBackgroundMusic(BackgroundMusicPath);
                }
            }
        }

        public override async Task Load()
        {
            await base.Load();
        }
    }
}
