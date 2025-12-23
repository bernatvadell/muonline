using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    /// <summary>
    /// Cherry Blossom Tree NPC - decorative or quest NPC.
    /// </summary>
    [NpcInfo(451, "Cherry Blossom Tree")]
    public class CherryBlossomTree : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/cherryblossom/sakuratree.bmd");
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
