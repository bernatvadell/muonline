using System.Buffers;
using System.Text;
using MUnique.OpenMU.Network.Packets.ClientToServer;
using MUnique.OpenMU.Network.Packets.ConnectServer;
using Client.Main.Client;
using System; // For TargetProtocolVersion

namespace Client.Main.Networking.PacketHandling
{
    /// <summary>
    ///  Static class responsible for building outgoing network packets.
    ///  Provides methods to construct packets for various client-server communications, including login, character actions, and Connect Server requests.
    /// </summary>
    public static class PacketBuilder
    {
        // --- Game Server Packet Builders ---

        /// <summary>
        /// Builds a login packet with long password support.
        /// </summary>
        /// <param name="writer">The buffer writer to which the packet will be written.</param>
        /// <param name="username">The username for login.</param>
        /// <param name="password">The password for login.</param>
        /// <param name="clientVersion">Byte array representing the client version.</param>
        /// <param name="clientSerial">Byte array representing the client serial.</param>
        /// <param name="xor3Keys">XOR3 encryption keys for encrypting username and password.</param>
        /// <returns>The size of the built packet in bytes.</returns>
        public static int BuildLoginPacket(IBufferWriter<byte> writer, string username, string password, byte[] clientVersion, byte[] clientSerial, byte[] xor3Keys)
        {
            int packetLength = LoginLongPassword.Length; // Assuming long password format for broader compatibility
            var memory = writer.GetMemory(packetLength).Slice(0, packetLength);
            var loginPacket = new LoginLongPassword(memory); // Using ref struct for efficiency and to avoid allocations

            // Prepare spans for username and password to avoid heap allocations
            Span<byte> userBytes = stackalloc byte[loginPacket.Username.Length];
            Span<byte> passBytes = stackalloc byte[loginPacket.Password.Length];
            userBytes.Clear(); // Clear any potential garbage data
            passBytes.Clear(); // Clear any potential garbage data

            // Encode username and password into byte spans
            Encoding.ASCII.GetBytes(username, userBytes);
            Encoding.ASCII.GetBytes(password, passBytes);
            userBytes.CopyTo(loginPacket.Username); // Copy username bytes to packet buffer
            passBytes.CopyTo(loginPacket.Password); // Copy password bytes to packet buffer

            // Encrypt credentials using XOR3 *after* they are safely copied into the packet buffer
            EncryptXor3(loginPacket.Username, xor3Keys); // Encrypt username
            EncryptXor3(loginPacket.Password, xor3Keys); // Encrypt password

            loginPacket.TickCount = (uint)Environment.TickCount; // Set current tick count
            clientVersion.CopyTo(loginPacket.ClientVersion); // Copy client version
            clientSerial.CopyTo(loginPacket.ClientSerial); // Copy client serial

            // Struct constructor should handle setting packet header (Type, Code, Length, SubCode)
            return packetLength; // Return the length of the packet
        }

        /// <summary>
        /// Builds a packet to request the character list from the Game Server.
        /// </summary>
        /// <param name="writer">The buffer writer to which the packet will be written.</param>
        /// <returns>The size of the built packet in bytes.</returns>
        public static int BuildRequestCharacterListPacket(IBufferWriter<byte> writer)
        {
            int packetSize = RequestCharacterList.Length;
            var memory = writer.GetMemory(packetSize).Slice(0, packetSize);
            var packet = new RequestCharacterList(memory);
            packet.Language = 0; // Assuming English language or default language setting

            return packetSize; // Return the size of the packet
        }

        /// <summary>
        /// Builds a public chat message packet.
        /// </summary>
        /// <param name="writer">The buffer writer to which the packet will be written.</param>
        /// <param name="characterName">The name of the sending character.</param>
        /// <param name="message">The chat message content.</param>
        /// <returns>The size of the built packet in bytes.</returns>
        public static int BuildPublicChatMessagePacket(IBufferWriter<byte> writer, string characterName, string message)
        {
            int messageByteLength = Encoding.UTF8.GetByteCount(message);
            int packetSize = PublicChatMessage.GetRequiredSize(messageByteLength);
            var memory = writer.GetMemory(packetSize).Slice(0, packetSize);
            var packet = new PublicChatMessage(memory); // Uses C1 header

            packet.Character = characterName;
            packet.Message = message;

            return packetSize;
        }

        /// <summary>
        /// Builds a whisper message packet.
        /// </summary>
        /// <param name="writer">The buffer writer to which the packet will be written.</param>
        /// <param name="receiverName">The name of the receiving character.</param>
        /// <param name="message">The whisper message content.</param>
        /// <returns>The size of the built packet in bytes.</returns>
        public static int BuildWhisperMessagePacket(IBufferWriter<byte> writer, string receiverName, string message)
        {
            int messageByteLength = Encoding.UTF8.GetByteCount(message);
            int packetSize = WhisperMessage.GetRequiredSize(messageByteLength);
            var memory = writer.GetMemory(packetSize).Slice(0, packetSize);
            var packet = new WhisperMessage(memory); // Uses C1 header

            packet.ReceiverName = receiverName;
            packet.Message = message;

            return packetSize;
        }

        /// <summary>
        ///  Encrypts a span of bytes using XOR3 encryption with the provided keys.
        ///  This method modifies the input span directly, applying the XOR3 encryption in-place.
        /// </summary>
        /// <param name="data">The span of bytes to encrypt.</param>
        /// <param name="xor3Keys">The XOR3 encryption keys (3 bytes).</param>
        private static void EncryptXor3(Span<byte> data, byte[] xor3Keys)
        {
            for (int i = 0; i < data.Length; i++)
            {
                data[i] ^= xor3Keys[i % 3]; // Apply XOR with key, cycling through the 3 keys
            }
        }

        /// <summary>
        /// Builds a packet to select a character with the given name.
        /// </summary>
        /// <param name="writer">The buffer writer to which the packet will be written.</param>
        /// <param name="characterName">The name of the character to select.</param>
        /// <returns>The size of the built packet in bytes.</returns>
        public static int BuildSelectCharacterPacket(IBufferWriter<byte> writer, string characterName)
        {
            int packetSize = SelectCharacter.Length;
            var memory = writer.GetMemory(packetSize).Slice(0, packetSize);
            var packet = new SelectCharacter(memory);
            packet.Name = characterName; // Set the character name in the packet

            return packetSize; // Return the size of the packet
        }

        /// <summary>
        /// Builds a packet to request an instant move to the specified coordinates.
        /// </summary>
        /// <param name="writer">The buffer writer to which the packet will be written.</param>
        /// <param name="x">The target X-coordinate.</param>
        /// <param name="y">The target Y-coordinate.</param>
        /// <returns>The size of the built packet in bytes.</returns>
        public static int BuildInstantMoveRequestPacket(IBufferWriter<byte> writer, byte x, byte y)
        {
            int packetSize = InstantMoveRequest.Length;
            var memory = writer.GetMemory(packetSize).Slice(0, packetSize);
            var packet = new InstantMoveRequest(memory);
            packet.TargetX = x; // Set target X-coordinate
            packet.TargetY = y; // Set target Y-coordinate
            return packetSize; // Return the size of the packet
        }

        /// <summary>
        /// Builds a walk request packet with a sequence of directions.
        /// </summary>
        /// <param name="writer">The buffer writer to which the packet will be written.</param>
        /// <param name="startX">The starting X-coordinate of the walk.</param>
        /// <param name="startY">The starting Y-coordinate of the walk.</param>
        /// <param name="path">An array of bytes representing the path directions.</param>
        /// <returns>The size of the built packet in bytes, or 0 if the path is null or empty.</returns>
        public static int BuildWalkRequestPacket(IBufferWriter<byte> writer, byte startX, byte startY, byte[] path)
        {
            if (path == null || path.Length == 0) return 0; // Return 0 if no path is provided

            // Calculate the number of bytes needed to pack direction steps (2 steps per byte)
            int stepsDataLength = (path.Length + 1) / 2;
            int packetSize = WalkRequest.GetRequiredSize(stepsDataLength); // Get size based on path length

            var memory = writer.GetMemory(packetSize).Slice(0, packetSize);
            var packet = new WalkRequest(memory);

            packet.SourceX = startX; // Set starting X-coordinate
            packet.SourceY = startY; // Set starting Y-coordinate
            packet.StepCount = (byte)path.Length; // Set total steps in the path
            // Set initial rotation/direction based on the first step (if path is not empty)
            packet.TargetRotation = (path.Length > 0) ? path[0] : (byte)0;

            var directionsSpan = packet.Directions; // Span to write direction bytes into
            int pathIndex = 0;
            for (int i = 0; i < stepsDataLength; i++)
            {
                // Get the high nibble (first direction step for this byte)
                byte highNibble = (pathIndex < path.Length) ? path[pathIndex++] : (byte)0x0F; // Use 0x0F for padding if path ends

                // Get the low nibble (second direction step for this byte)
                byte lowNibble = (pathIndex < path.Length) ? path[pathIndex++] : (byte)0x0F; // Use 0x0F for padding if path ends

                // Combine two 4-bit direction steps into one byte
                directionsSpan[i] = (byte)((highNibble << 4) | (lowNibble & 0x0F));
            }
            return packetSize; // Return the size of the packet
        }

        /// <summary>
        /// Builds a packet to request picking up an item from the ground.
        /// </summary>
        /// <param name="writer">The buffer writer to which the packet will be written.</param>
        /// <param name="itemId">The ID of the item to pick up.</param>
        /// <param name="version">The target protocol version to determine the correct packet structure.</param>
        /// <returns>The size of the built packet in bytes.</returns>
        public static int BuildPickupItemRequestPacket(IBufferWriter<byte> writer, ushort itemId, TargetProtocolVersion version)
        {
            // Choose packet structure based on protocol version (different packet codes and structures in 0.75 vs later versions)
            if (version == TargetProtocolVersion.Version097 || version == TargetProtocolVersion.Season6) // C3 22 is used from 0.97 onwards
            {
                int packetSize = PickupItemRequest.Length;
                var memory = writer.GetMemory(packetSize).Slice(0, packetSize);
                var packet = new PickupItemRequest(memory);
                packet.ItemId = itemId; // Set item ID (BigEndian for 0.97+)
                return packetSize; // Return the size of the packet
            }
            else // Version075 uses C1 22
            {
                int packetSize = PickupItemRequest075.Length;
                var memory = writer.GetMemory(packetSize).Slice(0, packetSize);
                var packet = new PickupItemRequest075(memory);
                packet.ItemId = itemId; // Set item ID (BigEndian for 0.75)
                return packetSize; // Return the size of the packet
            }
        }

        /// <summary>
        /// Builds a packet to request animation (e.g., character rotation or action animation).
        /// </summary>
        /// <param name="writer">The buffer writer to which the packet will be written.</param>
        /// <param name="rotation">The rotation direction.</param>
        /// <param name="animationNumber">The animation number to play.</param>
        /// <returns>The size of the built packet in bytes.</returns>
        public static int BuildAnimationRequestPacket(IBufferWriter<byte> writer, byte rotation, byte animationNumber)
        {
            int packetSize = AnimationRequest.Length;
            var memory = writer.GetMemory(packetSize).Slice(0, packetSize);
            var packet = new AnimationRequest(memory);
            packet.Rotation = rotation; // Set rotation direction
            packet.AnimationNumber = animationNumber; // Set animation number
            return packetSize; // Return the size of the packet
        }

        // --- Connect Server Packet Builders ---

        /// <summary>
        /// Builds a packet to request the list of game servers from the Connect Server.
        /// </summary>
        /// <param name="writer">The buffer writer to which the packet will be written.</param>
        /// <returns>The size of the built packet in bytes.</returns>
        public static int BuildServerListRequestPacket(IBufferWriter<byte> writer)
        {
            int packetSize = ServerListRequest.Length;
            var memory = writer.GetMemory(packetSize).Slice(0, packetSize);
            _ = new ServerListRequest(memory); // Initialize packet header and structure
            return packetSize; // Return the size of the packet
        }

        /// <summary>
        /// Builds a packet to request connection information for a specific game server from the Connect Server.
        /// </summary>
        /// <param name="writer">The buffer writer to which the packet will be written.</param>
        /// <param name="serverId">The ID of the server for which to request connection info.</param>
        /// <returns>The size of the built packet in bytes.</returns>
        public static int BuildServerInfoRequestPacket(IBufferWriter<byte> writer, ushort serverId)
        {
            int packetSize = ConnectionInfoRequest.Length;
            var memory = writer.GetMemory(packetSize).Slice(0, packetSize);
            var packet = new ConnectionInfoRequest(memory);
            packet.ServerId = serverId; // Set the server ID for the connection info request
            return packetSize; // Return the size of the packet
        }
    }
}