using System;
using System.Buffers;
using System.Text;
using Client.Main.Core.Client;
using MUnique.OpenMU.Network.Packets;
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.Network.Packets.ConnectServer;

namespace Client.Main.Networking.PacketHandling
{
    /// <summary>
    /// Builds outgoing network packets for game and connect server communication.
    /// </summary>
    public static class PacketBuilder
    {
        // ──────────────────────── Game Server Packets ─────────────────────────

        /// <summary>
        /// Builds a login packet using the long-password format.
        /// </summary>
        public static int BuildLoginPacket(
            IBufferWriter<byte> writer,
            string username,
            string password,
            byte[] clientVersion,
            byte[] clientSerial,
            byte[] xor3Keys)
        {
            int length = LoginLongPassword.Length;
            var memory = writer.GetMemory(length).Slice(0, length);
            var packet = new LoginLongPassword(memory);

            // Prepare spans to avoid allocations
            Span<byte> userSpan = stackalloc byte[packet.Username.Length];
            Span<byte> passSpan = stackalloc byte[packet.Password.Length];

            Encoding.ASCII.GetBytes(username, userSpan);
            Encoding.ASCII.GetBytes(password, passSpan);

            userSpan.CopyTo(packet.Username);
            passSpan.CopyTo(packet.Password);

            // XOR3 encryption in-place
            EncryptXor3(packet.Username, xor3Keys);
            EncryptXor3(packet.Password, xor3Keys);

            packet.TickCount = (uint)Environment.TickCount;
            clientVersion.CopyTo(packet.ClientVersion);
            clientSerial.CopyTo(packet.ClientSerial);

            return length;
        }

        /// <summary>
        /// Builds a packet requesting the character list.
        /// </summary>
        public static int BuildRequestCharacterListPacket(IBufferWriter<byte> writer)
        {
            int length = RequestCharacterList.Length;
            var memory = writer.GetMemory(length).Slice(0, length);
            _ = new RequestCharacterList(memory);
            return length;
        }

        /// <summary>
        /// Builds a packet for sending a public chat message.
        /// </summary>
        public static int BuildPublicChatMessagePacket(
            IBufferWriter<byte> writer,
            string characterName,
            string message)
        {
            int messageBytes = Encoding.UTF8.GetByteCount(message);
            int length = PublicChatMessage.GetRequiredSize(messageBytes);
            var memory = writer.GetMemory(length).Slice(0, length);
            var packet = new PublicChatMessage(memory);

            packet.Character = characterName;
            packet.Message = message;
            return length;
        }

        /// <summary>
        /// Builds a whisper (private chat) packet.
        /// </summary>
        public static int BuildWhisperMessagePacket(
            IBufferWriter<byte> writer,
            string receiverName,
            string message)
        {
            int messageBytes = Encoding.UTF8.GetByteCount(message);
            int length = WhisperMessage.GetRequiredSize(messageBytes);
            var memory = writer.GetMemory(length).Slice(0, length);
            var packet = new WhisperMessage(memory);

            packet.ReceiverName = receiverName;
            packet.Message = message;
            return length;
        }

        /// <summary>
        /// Builds a packet to select a character by name.
        /// </summary>
        public static int BuildSelectCharacterPacket(
            IBufferWriter<byte> writer,
            string characterName)
        {
            int length = SelectCharacter.Length;
            var memory = writer.GetMemory(length).Slice(0, length);
            var packet = new SelectCharacter(memory);

            packet.Name = characterName;
            return length;
        }

        public static int BuildClientReadyAfterMapChangePacket(IBufferWriter<byte> writer)
        {
            int length = ClientReadyAfterMapChange.Length;
            var memory = writer.GetMemory(length).Slice(0, length);
            _ = new ClientReadyAfterMapChange(memory);
            return length;
        }

        /// <summary>
        /// Builds a packet for an instant move (teleport) request.
        /// </summary>
        public static int BuildInstantMoveRequestPacket(
            IBufferWriter<byte> writer,
            byte x,
            byte y)
        {
            int length = InstantMoveRequest.Length;
            var memory = writer.GetMemory(length).Slice(0, length);
            var packet = new InstantMoveRequest(memory);

            packet.TargetX = x;
            packet.TargetY = y;
            return length;
        }

        /// <summary>
        /// Builds a walk request packet with a sequence of direction steps.
        /// </summary>
        public static int BuildWalkRequestPacket(
            IBufferWriter<byte> writer,
            byte startX,
            byte startY,
            byte[] path)
        {
            if (path == null || path.Length == 0)
                return 0;

            int stepsBytes = (path.Length + 1) / 2;
            int length = WalkRequest.GetRequiredSize(stepsBytes);
            var memory = writer.GetMemory(length).Slice(0, length);
            var packet = new WalkRequest(memory);

            packet.SourceX = startX;
            packet.SourceY = startY;
            packet.StepCount = (byte)path.Length;
            packet.TargetRotation = path[0];

            var directions = packet.Directions;
            int idx = 0;
            for (int i = 0; i < stepsBytes; i++)
            {
                byte high = idx < path.Length ? path[idx++] : (byte)0x0F;
                byte low = idx < path.Length ? path[idx++] : (byte)0x0F;
                directions[i] = (byte)((high << 4) | (low & 0x0F));
            }

            return length;
        }

        /// <summary>
        /// Builds a packet requesting to pick up an item by ID.
        /// </summary>
        public static int BuildPickupItemRequestPacket(
            IBufferWriter<byte> writer,
            ushort itemId,
            TargetProtocolVersion version)
        {
            if (version == TargetProtocolVersion.Version097 ||
                version == TargetProtocolVersion.Season6)
            {
                int length = PickupItemRequest.Length;
                var memory = writer.GetMemory(length).Slice(0, length);
                var packet = new PickupItemRequest(memory);
                packet.ItemId = itemId;
                return length;
            }
            else
            {
                int length = PickupItemRequest075.Length;
                var memory = writer.GetMemory(length).Slice(0, length);
                var packet = new PickupItemRequest075(memory);
                packet.ItemId = itemId;
                return length;
            }
        }

        /// <summary>
        /// Builds a packet to request an animation or rotation.
        /// </summary>
        public static int BuildAnimationRequestPacket(
            IBufferWriter<byte> writer,
            byte rotation,
            byte animationNumber)
        {
            int length = AnimationRequest.Length;
            var memory = writer.GetMemory(length).Slice(0, length);
            var packet = new AnimationRequest(memory);

            packet.Rotation = rotation;
            packet.AnimationNumber = animationNumber;
            return length;
        }

        /// <summary>
        /// Builds a hit request packet for a basic attack.
        /// </summary>
        public static int BuildHitRequestPacket(
            IBufferWriter<byte> writer,
            ushort targetId,
            byte attackAnimation,
            byte lookingDirection)
        {
            int length = HitRequest.Length;
            var memory = writer.GetMemory(length).Slice(0, length);
            var packet = new HitRequest(memory);

            packet.TargetId = targetId;
            packet.AttackAnimation = attackAnimation;
            packet.LookingDirection = lookingDirection;

            return length;
        }

        /// <summary>
        /// Builds a packet to request increasing a character's stat point.
        /// </summary>
        /// <param name="writer">The buffer writer to write the packet to.</param>
        /// <param name="attribute">The attribute to increase.</param>
        /// <returns>The length of the built packet.</returns>
        public static int BuildIncreaseCharacterStatPointPacket(
            IBufferWriter<byte> writer,
            CharacterStatAttribute attribute)
        {
            int length = IncreaseCharacterStatPoint.Length;
            var memory = writer.GetMemory(length).Slice(0, length);
            var packet = new IncreaseCharacterStatPoint(memory);

            packet.StatType = attribute;

            return length;
        }

        // ───────────────────── Connect Server Packets ──────────────────────

        /// <summary>
        /// Builds a request packet for the game server list from the connect server.
        /// </summary>
        public static int BuildServerListRequestPacket(IBufferWriter<byte> writer)
        {
            int length = ServerListRequest.Length;
            var memory = writer.GetMemory(length).Slice(0, length);
            _ = new ServerListRequest(memory);
            return length;
        }

        /// <summary>
        /// Builds a request packet for connection info of a specific game server.
        /// </summary>
        public static int BuildServerInfoRequestPacket(
            IBufferWriter<byte> writer,
            ushort serverId)
        {
            int length = ConnectionInfoRequest.Length;
            var memory = writer.GetMemory(length).Slice(0, length);
            var packet = new ConnectionInfoRequest(memory);

            packet.ServerId = serverId;
            return length;
        }

        // ──────────────────────────── Helpers ─────────────────────────────

        /// <summary>
        /// Applies XOR-3 encryption to the provided span in place.
        /// </summary>
        private static void EncryptXor3(Span<byte> data, byte[] xor3Keys)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] ^= xor3Keys[i % 3];
            }
        }
    }
}
