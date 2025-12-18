using System;
using System.Text;

namespace Client.Main.Networking.Packets
{
    /// <summary>
    /// Represents a request to delete a character (C1 F3 02).
    /// Packet format: [C1] [0x18] [0xF3] [0x02] [name(10)] [securityCode(10)]
    /// </summary>
    public readonly ref struct DeleteCharacterRequest
    {
        private readonly Span<byte> _data;

        /// <summary>
        /// Fixed length of the DeleteCharacter packet.
        /// </summary>
        public const int Length = 24;

        /// <summary>
        /// Initializes a new instance of the DeleteCharacterRequest packet.
        /// </summary>
        /// <param name="data">The data span to write the packet to.</param>
        public DeleteCharacterRequest(Span<byte> data)
        {
            if (data.Length < Length)
                throw new ArgumentException($"Data span must be at least {Length} bytes", nameof(data));

            _data = data;
            
            // Initialize packet header
            _data[0] = 0xC1; // Packet type
            _data[1] = (byte)Length; // Packet length (24 = 0x18)
            _data[2] = 0xF3; // Main code for character management
            _data[3] = 0x02; // Sub code for character deletion
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
        /// Gets or sets the security code (10 bytes, ASCII encoded).
        /// </summary>
        public string SecurityCode
        {
            get
            {
                var codeSpan = _data.Slice(14, 10);
                int length = 0;
                for (int i = 0; i < codeSpan.Length; i++)
                {
                    if (codeSpan[i] == 0) break;
                    length++;
                }
                return length > 0 ? Encoding.ASCII.GetString(codeSpan.Slice(0, length)) : string.Empty;
            }
            set
            {
                var codeSpan = _data.Slice(14, 10);
                codeSpan.Clear(); // Zero out the security code field
                if (!string.IsNullOrEmpty(value))
                {
                    var bytes = Encoding.ASCII.GetBytes(value);
                    bytes.AsSpan(0, Math.Min(bytes.Length, 10)).CopyTo(codeSpan);
                }
            }
        }
    }
}
