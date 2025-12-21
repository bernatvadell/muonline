using Client.Main.Content;
using Client.Main.Controls.UI;
using Client.Main.Core.Client;
using Client.Main.Networking;
using MUnique.OpenMU.Network.Packets;
using System.Threading.Tasks;

namespace Client.Main.Objects.NPCS
{
    [NpcInfo(408, "Gatekeeper")]
    public class Gatekeeper : NPCObject
    {
        public override async Task Load()
        {
            Model = await BMDLoader.Instance.Prepare($"NPC/Npc_Castel_Gate.bmd");
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

            // Devin Part 3 (quest index 6)
            var questState = state.GetLegacyQuestState(6);
            if (questState != LegacyQuestState.Active)
            {
                string msg = questState == LegacyQuestState.Complete
                    ? "You have already completed this step."
                    : "You are not on the Refuge of Balgass quest step.";
                RequestDialog.ShowInfo(msg);
                return;
            }

            if (state.Level < 400)
            {
                RequestDialog.ShowInfo("Level 400+ is required.");
                return;
            }

            if (state.InventoryZen < 10_000_000)
            {
                RequestDialog.ShowInfo("10,000,000 Zen is required.");
                return;
            }

            RequestDialog.Show(
                "Enter Refuge of Balgass?\n(Requires Devin's 3rd class quest - Part 3 active)",
                onAccept: () => _ = svc.SendEnterOnGatekeeperRequestAsync(),
                onReject: () => { },
                acceptText: "Enter",
                rejectText: "Cancel");
        }
    }
}
