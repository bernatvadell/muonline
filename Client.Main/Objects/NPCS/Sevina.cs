using Client.Main.Content;
using Client.Main.Networking;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(235, "Priestess Sevina")]
    public class Sevina : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Sevina01.bmd");
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
