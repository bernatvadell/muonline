using Client.Main.Content;
using Client.Main.Controls.UI;
using Client.Main.Core.Client;
using Client.Main.Networking;
using MUnique.OpenMU.Network.Packets;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(407, "Werewolf Quarrel")]
    public class WerewolfQuarrel : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Npc_Quarrel.bmd");
            await base.Load();
        }
        protected override void HandleClick()
        {
            var svc = MuGame.Network?.GetCharacterService();
            var state = MuGame.Network?.GetCharacterState();
            if (svc == null)
            {
                return;
            }

            if (state == null)
            {
                RequestDialog.ShowInfo("Character state is not available.");
                return;
            }

            // Devin Part 2 (quest index 5)
            var questState = state.GetLegacyQuestState(5);
            if (questState != LegacyQuestState.Active)
            {
                string msg = questState == LegacyQuestState.Complete
                    ? "You have already completed this step."
                    : "You are not on the Barracks of Balgass quest step.";
                RequestDialog.ShowInfo(msg);
                return;
            }

            if (state.Level < 400)
            {
                RequestDialog.ShowInfo("Level 400+ is required.");
                return;
            }

            if (state.InventoryZen < 7_000_000)
            {
                RequestDialog.ShowInfo("7,000,000 Zen is required.");
                return;
            }

            RequestDialog.Show(
                "Enter Barracks of Balgass?\n(Requires Devin's 3rd class quest - Part 2 active)",
                onAccept: () => _ = svc.SendEnterOnWerewolfRequestAsync(),
                onReject: () => { },
                acceptText: "Enter",
                rejectText: "Cancel");
        }
    }
}
