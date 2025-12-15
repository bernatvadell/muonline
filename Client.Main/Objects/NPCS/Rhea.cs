using Client.Main.Content;
using Client.Main.Networking;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(416, "Rhea")]
    public class Rhea : NPCObject
    {
        public override bool CanRepair => true;

        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare("NPC/rhea.bmd");
            await base.Load();

            // Rhea has slower animation speed
            AnimationSpeed = 3f;
        }

        protected override void HandleClick()
        {
            var svc = MuGame.Network?.GetCharacterService();
            if (svc != null)
                _ = svc.SendTalkToNpcRequestAsync(NetworkId);
        }
    }
}
