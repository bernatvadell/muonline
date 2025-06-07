using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(233, "Messenger of Archangel")]
    public class MessengerOfArchangel : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Archangel_Messenger.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
        }
    }
}
