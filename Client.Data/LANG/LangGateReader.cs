using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Data.LANG
{
    public class LangGateReader : BaseLangReader<Dictionary<int, Gate>>
    {
        readonly Encoding euckr = CodePagesEncodingProvider.Instance.GetEncoding(51949)!;
        protected override Dictionary<int, Gate> Read(byte[] buffer)
        {
            Dictionary<int, Gate> list = [];
            var str = euckr.GetString(buffer);
            var lines = str.Split("\r\n").Where(line => line.Length > 0 && char.IsDigit(line[0])).ToArray();
            foreach (var line in lines)
            {
                var parts = line.Split('\t');
                if (parts[0] == "0") continue;
                Gate gate = new()
                {
                    Id = int.Parse(parts[0]),
                    Flag = int.Parse(parts[1]),
                    Map = int.Parse(parts[2]),
                    X1 = int.Parse(parts[3]),
                    Y1 = int.Parse(parts[4]),
                    X2 = int.Parse(parts[5]),
                    Y2 = int.Parse(parts[6]),
                    Target = int.Parse(parts[7]),
                    Direction = int.Parse(parts[8]),
                    Level = int.Parse(parts[9]),
                    Unk1 = int.Parse(parts[10]),
                    Unk2 = int.Parse(parts[11]),
                };
                list.Add(gate.Id, gate);
            }
            return list;
        }
    }
}
