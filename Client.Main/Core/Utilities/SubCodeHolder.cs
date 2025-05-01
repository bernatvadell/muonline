using System.Collections.Generic;
using MUnique.OpenMU.Network.Packets.ConnectServer;
using MUnique.OpenMU.Network.Packets.ServerToClient;

namespace Client.Main.Core.Utilities
{
    /// <summary>
    ///  Static class holding a set of Game Server packet codes that are known to utilize sub-codes.
    ///  This is crucial for the PacketRouter to correctly identify packet handlers, as packets with sub-codes require both main and sub-code for routing.
    /// </summary>
    public static class SubCodeHolder
    {
        /// <summary>
        ///  HashSet containing the main codes of Game Server packets that have sub-codes.
        ///  Initialized with codes from ServerToClientPackets.cs in MUnique.OpenMU.Network and additional codes identified to use sub-codes.
        /// </summary>
        private static readonly HashSet<byte> CodesWithSubCode = new HashSet<byte>
        {
            LoginResponse.Code, // F1 - Login response packet
            CharacterList.Code, // F3 - Character list packet
            NpcItemBuyFailed.Code, // 32 - NPC item buy failed response (Note: ItemBought uses 32 without subcode)
            TradeMoneySetResponse.Code, // 3A - Trade money set response packet
            PlayerShopSetItemPriceResponse.Code, // 3F - Player shop set item price response packet
            CurrentHealthAndShield.Code, // 26 - Current health and shield status packet
            CurrentManaAndAbility.Code, // 27 - Current mana and ability status packet
            LegacyQuestMonsterKillInfo.Code, // A4 - Legacy quest monster kill information packet
            DuelStartResult.Code, // AA - Duel start result packet
            ChaosCastleEnterResult.Code, // AF - Chaos Castle enter result packet
            IllusionTempleEnterResult.Code, // BF - Illusion Temple enter result packet
            QuestEventResponse.Code, // F6 - Quest event response packet
            OpenNpcDialog.Code, // F9 - Open NPC dialog packet
            CharacterClassCreationUnlock.Code, // DE - Character class creation unlock packet
            MasterCharacterLevelUpdate.Code, // F3 - Master character level update packet
            MasterSkillLevelUpdate.Code, // F3 - Master skill level update packet
            MasterSkillList.Code, // F3 - Master skill list packet
            MapChanged.Code, // 1C - Map changed packet
                             // Additional codes from ServerToClientPackets.cs and other identified packets with subcodes:
            0x22, // Handling FF/FE/01 for Item Pickup/Money Update
            0xC1, // Friend Add/Delete Response etc. - Friend list and friend actions response packets
            0xC8, // Letter Delete Response - In-game letter delete response packet
            0xCA, // Chat Room Invite Response - Chat room invitation response packet
            0xCB, // Friend Invite Result - Friend invitation result packet
            0xD0, // Special NPC Action Results - Results from special NPC actions (e.g., crafting)
            0xD1, // Kanturu/Raklion Info/Enter - Kanturu and Raklion event information and entry packets
            0xD2, // Cash Shop Point/Open/Buy/Gift/List/Delete/Consume - Cash shop related packets
            0xE1, // Guild Role Assign Response - Guild role assignment response packet
            0xE2, // Guild Type Change Response - Guild type change response packet
            0xE5, // Guild Relationship Request Response - Guild relationship request response packet
            0xE6, // Guild Relationship Change Response - Guild relationship change response packet
            0xEB, // Alliance Kick Response - Guild alliance kick response packet
            0xF7, // Empire Guardian Enter Response - Empire Guardian event enter response
            0xF8, // Gens Join/Leave/Reward/Ranking Response - Gens system related responses
            0xBD, // Crywolf Info/Contract/Benefit - Crywolf Fortress event information and status packets
        };

        /// <summary>
        ///  Checks if a given Game Server packet code is known to have a sub-code.
        /// </summary>
        /// <param name="code">The main packet code to check.</param>
        /// <returns><c>true</c> if the packet code is in the list of codes with sub-codes; otherwise, <c>false</c>.</returns>
        public static bool ContainsSubCode(byte code) => CodesWithSubCode.Contains(code);
    }

    /// <summary>
    ///  Static class holding a set of Connect Server packet codes that are known to utilize sub-codes.
    ///  Similar to <see cref="SubCodeHolder"/> but specifically for Connect Server packets.
    /// </summary>
    public static class ConnectServerSubCodeHolder
    {
        /// <summary>
        ///  HashSet containing the main codes of Connect Server packets that have sub-codes.
        ///  Initialized with codes from ConnectServerPackets.cs in MUnique.OpenMU.Network.
        /// </summary>
        private static readonly HashSet<byte> CodesWithSubCode = new HashSet<byte>
        {
            Hello.Code, // 0x00 - Hello packet, initial handshake
            ConnectionInfoRequest.Code, // 0xF4 - Connection info request/response packet (Request uses 0x03, Response uses 0x03)
            ServerListRequest.Code, // 0xF4 - Server list request/response packet (Request uses 0x06, Response uses 0x06)
            ClientNeedsPatch.Code, // 0x05 - Client patch requirement notification packet
        };

        /// <summary>
        ///  Checks if a given Connect Server packet code is known to have a sub-code.
        /// </summary>
        /// <param name="code">The main packet code to check.</param>
        /// <returns><c>true</c> if the packet code is in the list of Connect Server codes with sub-codes; otherwise, <c>false</c>.</returns>
        public static bool ContainsSubCode(byte code) => CodesWithSubCode.Contains(code);
    }
}