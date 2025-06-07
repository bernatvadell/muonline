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
    public class ElfSoldier : CompositeNPCObject
    {
        private readonly ILogger<ElfSoldier> _logger;

        public ElfSoldier()
        {
            _logger = AppLoggerFactory?.CreateLogger<ElfSoldier>();
        }

        public override async Task Load()
        {
            // 1. Ładujemy model animacji (szkielet) do właściwości Model w klasie bazowej
            Model = await BMDLoader.Instance.Prepare("Player/Player.bmd");
            if (Model == null)
            {
                _logger.LogError("CRITICAL: Could not load base player model 'Player/Player.bmd'. NPC cannot be animated.");
                Status = GameControlStatus.Error;
                return;
            }

            await SetBodyPartsAsync("Player/", "HelmElf", "ArmorElf", "PantElf", "GloveElf", "BootElf", 5);

            if (this.Wings != null)
            {
                this.Wings.Type = 403;
                this.Wings.Hidden = false;
                this.Wings.LinkParentAnimation = false;
            }
            else
            {
                _logger?.LogWarning("ElfSoldier: obiekt 'Wings' jest null i nie można go ustawić.");
            }

            await base.Load();

            CurrentAction = (int)PlayerAction.StopFlying;
            Scale = 1.0f;

            var currentBBox = BoundingBoxLocal;
            BoundingBoxLocal = new BoundingBox(currentBBox.Min,
                new Vector3(currentBBox.Max.X, currentBBox.Max.Y, currentBBox.Max.Z + 70f));
        }

        protected override void HandleClick() { }
    }
}