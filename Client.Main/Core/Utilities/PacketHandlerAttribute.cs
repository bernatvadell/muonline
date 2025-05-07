using System;

namespace Client.Main.Core.Utilities
{
    /// <summary>
    ///  Attribute to mark methods as handlers for specific network packets, identified by their main and sub codes.
    ///  This attribute is used to route incoming packets to the appropriate handling methods.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    public sealed class PacketHandlerAttribute : Attribute
    {
        /// <summary>
        /// Gets the main operation code of the packet that this handler is designed to process.
        /// </summary>
        public byte MainCode { get; }

        /// <summary>
        /// Gets the sub-operation code of the packet that this handler is designed to process.
        /// </summary>
        public byte SubCode { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PacketHandlerAttribute"/> class.
        /// </summary>
        /// <param name="mainCode">The main operation code of the packet.</param>
        /// <param name="subCode">The sub-operation code of the packet. Use 0xFF if the packet does not have a sub-code.</param>
        public PacketHandlerAttribute(byte mainCode, byte subCode)
        {
            MainCode = mainCode;
            SubCode = subCode;
        }
    }
}