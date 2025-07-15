using Client.Data;
using Client.Main.Content;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Client.Main.Objects.Wings;
using System.Threading.Tasks;
using Client.Main.Objects.Effects;
using Microsoft.Xna.Framework;
using Microsoft.Extensions.Logging;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(257, "Elf Soldier")]
    public class ElfSoldier : NPCObject
    {
        private new readonly ILogger<ElfSoldier> _logger;
        private WingObject _wings;
        public ElfSoldier()
        {
            _logger = AppLoggerFactory?.CreateLogger<ElfSoldier>();

            _wings = new WingObject
            {
                BlendMesh = 0,
                BlendMeshState = Microsoft.Xna.Framework.Graphics.BlendState.Additive
            };
            Children.Add(_wings);
        }

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("Player/Player.bmd");
            if (Model == null)
            {
                _logger.LogError("CRITICAL: Could not load base player model 'Player/Player.bmd'. NPC cannot be animated.");
                Status = GameControlStatus.Error;
                return;
            }

            await SetBodyPartsAsync("Player/", "HelmMale", "ArmorMale", "PantMale", "GloveMale", "BootMale", 25);

            // Set item enhancement level +11 for all equipment parts
            Helm.ItemLevel = 11;
            Armor.ItemLevel = 11;
            Pants.ItemLevel = 11;
            Gloves.ItemLevel = 11;
            Boots.ItemLevel = 11;

            _wings.Model = await BMDLoader.Instance.Prepare("Item/Wing04.bmd");

            await base.Load();

            CurrentAction = (int)PlayerAction.PlayerStopFly;
            Scale = 1.0f;

            var currentBBox = BoundingBoxLocal;
            BoundingBoxLocal = new BoundingBox(currentBBox.Min,
                new Vector3(currentBBox.Max.X, currentBBox.Max.Y, currentBBox.Max.Z + 70f));
        }

        protected override void HandleClick() { }
    }
}