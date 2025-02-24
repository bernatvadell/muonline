using System.Runtime.InteropServices;
using System.Text;

namespace Client.Data
{
    public static class BinaryReaderExtensions
    {
        public static string ReadString(this System.IO.BinaryReader br, int length, Encoding? encoding = null)
        {
            encoding = encoding ?? Encoding.ASCII;
            var buff = br.ReadBytes(length);
            var idx = Array.IndexOf(buff, (byte)0);
            return Encoding.ASCII.GetString(buff, 0, idx > 0 ? idx : buff.Length);
        }

        public static T ReadStruct<T>(this BinaryReader br) where T : struct
        {
            int size = Marshal.SizeOf<T>();
            byte[] buffer = br.ReadBytes(size);
            GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            try
            {
                IntPtr ptr = handle.AddrOfPinnedObject();
                return Marshal.PtrToStructure<T>(ptr);
            }
            finally
            {
                handle.Free();
            }
        }

        public static T[] ReadStructArray<T>(this BinaryReader br, int length) where T : struct
        {
            var structs = new T[length];

            for (int i = 0; i < length; i++)
            {
                structs[i] = br.ReadStruct<T>();
            }

            return structs;
        }
    }
}
