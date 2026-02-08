using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace Client.Data.BMD
{
    public class BMDGateReader : BaseReader<BMDGate[]>
    {
        protected override BMDGate[] Read(byte[] buffer)
        {
            int sizeGate = Marshal.SizeOf<BMDGate>();

            var decrypted = (byte[])buffer.Clone();

            BuxCryptor.ConvertPerRecordInPlace(decrypted, sizeGate);

            var span = MemoryMarshal.Cast<byte, BMDGate>(decrypted.AsSpan());

            return span.ToArray();
        }
    }
}