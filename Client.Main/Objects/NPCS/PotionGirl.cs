using Client.Main.Content;
using Client.Main.Controls.UI.Game;
using Client.Main.Networking;
using Client.Main.Objects;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(253, "Potion Girl Amy")]
    public class PotionGirl : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("NPC/Girl01.bmd");
            await SetBodyPartsAsync("Npc/",
                "GirlHead", "GirlUpper", "GirlLower", "Glove", "Boot",
                1);
            await base.Load();
        }
        protected override void HandleClick()
        {
            var svc = MuGame.Network?.GetCharacterService();
            if (svc != null)
            {
                _ = svc.SendTalkToNpcRequestAsync(NetworkId);
            }
        }
    }
}
