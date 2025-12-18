using System;
using System.Text;
using MUnique.OpenMU.Network.Packets;

namespace Client.Main.Networking.Packets
{
    /// <summary>
    /// Packet structure for creating a new character.
    /// C1 F3 01 - CreateCharacter (by client)
    /// </summary>
    public readonly ref struct CreateCharacterRequest
    {
        private readonly Span<byte> _data;

        /// <summary>
        /// Fixed length of the CreateCharacter packet.
        /// </summary>
        public const int Length = 15;

        /// <summary>
        /// Initializes a new instance of the CreateCharacterRequest packet.
        /// </summary>
        /// <param name="data">The data span to write the packet to.</param>
        public CreateCharacterRequest(Span<byte> data)
        {
            if (data.Length < Length)
                throw new ArgumentException($"Data span must be at least {Length} bytes", nameof(data));

            _data = data;
            
            // Initialize packet header
            _data[0] = 0xC1; // Packet type
            _data[1] = (byte)Length; // Packet length
            _data[2] = 0xF3; // Main code for character management
            _data[3] = 0x01; // Sub code for character creation
        }

        /// <summary>
        /// Gets or sets the character name (10 bytes, ASCII encoded).
        /// </summary>
        public string Name
        {
            get
            {
                var nameSpan = _data.Slice(4, 10);
                int length = 0;
                for (int i = 0; i < nameSpan.Length; i++)
                {
                    if (nameSpan[i] == 0) break;
                    length++;
                }
                return length > 0 ? Encoding.ASCII.GetString(nameSpan.Slice(0, length)) : string.Empty;
            }
            set
            {
                var nameSpan = _data.Slice(4, 10);
                nameSpan.Clear(); // Zero out the name field
                if (!string.IsNullOrEmpty(value))
                {
                    var bytes = Encoding.ASCII.GetBytes(value);
                    bytes.AsSpan(0, Math.Min(bytes.Length, 10)).CopyTo(nameSpan);
                }
            }
        }

        /// <summary>
        /// Gets or sets the character class.
        /// The class value is stored left-shifted by 2 bits (6-bit field).
        /// </summary>
        public CharacterClassNumber CharacterClass
        {
            get => (CharacterClassNumber)(_data[14] >> 2);
            set => _data[14] = (byte)((byte)value << 2);
        }
    }
}
