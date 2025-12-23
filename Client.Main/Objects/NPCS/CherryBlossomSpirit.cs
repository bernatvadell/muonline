using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    /// <summary>
    /// Cherry Blossom Spirit NPC.
    /// </summary>
    [NpcInfo(450, "Cherry Blossom Spirit")]
    public class CherryBlossomSpirit : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/cherryblossom/cherry_blossom.bmd");
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
