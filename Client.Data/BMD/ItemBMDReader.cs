using NVorbis.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Data.BMD
{
    public class ItemBMDReader : BaseReader<List<ItemBMD>>
    {
        protected override List<ItemBMD> Read(byte[] buffer)
        {
            List<ItemBMD> items = [];
            var len = buffer.Length;
            using var br = new BinaryReader(new MemoryStream(buffer));
            var itemCount = br.ReadInt32();
            var BytesPerItem = (len - 8) / itemCount; //len minus 4 bytes item count and 4 bytes crc at the end
            while (br.BaseStream.Position != br.BaseStream.Length - 4) //ignore last 4 bytes that is crc
            {
                var itemBytes = br.ReadBytes(BytesPerItem);
                XOR3(ref itemBytes);
                using var itemReader = new BinaryReader(new MemoryStream(itemBytes));
                var item = itemReader.ReadStruct<ItemBMD>();
                items.Add(item);
            }            
            return items;
        }


        private void XOR3(ref byte[] data)
        {
            byte[] XOR_3_KEY = { 0xFC, 0xCF, 0xAB };
            for (int i = 0; i < data.Length; i++)
            {
                data[i] ^= XOR_3_KEY[i % 3];
            }
        }
    }
}
