using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Client.Data.LANG
{
    public class LangSkillReader : BaseLangReader<Dictionary<int, Skill>>
    {
        readonly Encoding euckr = CodePagesEncodingProvider.Instance.GetEncoding(51949)!;
        protected override Dictionary<int, Skill> Read(byte[] buffer)
        {
            Dictionary<int, Skill> list = [];
            var str = euckr.GetString(buffer);
            var lines = str.Split("\r\n").Where(line => line.Length > 0 && char.IsDigit(line[0])).ToArray();
            foreach (var line in lines)
            {
                var parts = line.Split('\t');
                Skill entry = new()
                {
                    Id = int.Parse(parts[0]),
                    Name = parts[1].Trim('"'),
                    Level = int.Parse(parts[2]),
                    Damage = int.Parse(parts[3]),
                    ManaCost = int.Parse(parts[4]),
                    AGCost = int.Parse(parts[5]),
                    Distance = int.Parse(parts[6]),
                    Delay = int.Parse(parts[7]),
                    ReqEne = int.Parse(parts[8]),
                    ReqStr = int.Parse(parts[9]),
                    ReqDex = int.Parse(parts[10]),
                    ReqVit = int.Parse(parts[11]),
                    ReqCmd = int.Parse(parts[12]),
                    ElementalType = int.Parse(parts[13]),
                    UseType = int.Parse(parts[14]),
                    Value1 = int.Parse(parts[15]),
                    BaseSkill = int.Parse(parts[16]),
                    ReqKillCount = int.Parse(parts[17]),
                    Status1 = int.Parse(parts[18]),
                    Status2 = int.Parse(parts[19]),
                    Status3 = int.Parse(parts[20]),
                    DW = int.Parse(parts[21]),
                    DK = int.Parse(parts[22]),
                    FE = int.Parse(parts[23]),
                    MG = int.Parse(parts[24]),
                    DL = int.Parse(parts[25]),
                    SU = int.Parse(parts[26]),
                    RF = int.Parse(parts[27]),
                    GL = int.Parse(parts[28]),
                    RW = int.Parse(parts[29]),
                    SL = int.Parse(parts[30]),
                    GC = int.Parse(parts[31]),
                    KM = int.Parse(parts[32]),
                    LM = int.Parse(parts[33]),
                    IK = int.Parse(parts[34]),
                    AL = int.Parse(parts[35]),
                    Value2 = int.Parse(parts[36]),
                    Value3 = int.Parse(parts[37]),
                    Value4 = int.Parse(parts[38]),
                    Value5 = int.Parse(parts[39]),
                    Value39 = int.Parse(parts[40]),
                    Value40 = int.Parse(parts[41]),
                    Value41 = int.Parse(parts[42]),
                    Value42 = int.Parse(parts[43]),
                    Value43 = int.Parse(parts[44]),
                    Value44 = int.Parse(parts[45]),
                    BuffIcon = int.Parse(parts[46]),
                    Animation = int.Parse(parts[47]),
                    Value47 = int.Parse(parts[48]),
                    Value48 = int.Parse(parts[49]),
                    Value49 = int.Parse(parts[50]),
                    Value50 = int.Parse(parts[51]),
                    Value51 = int.Parse(parts[52]),
                    ScalingStat = int.Parse(parts[53]),
                    ScalingStatValue = int.Parse(parts[54]),
                    ScalingStat2 = int.Parse(parts[55]),
                    ScalingStat2Value = int.Parse(parts[56]),
                    ImprintDMG = int.Parse(parts[57]),
                    Value57 = int.Parse(parts[58]),
                    Value58 = int.Parse(parts[59]),
                };
                list.Add(entry.Id, entry);
            }
            return list;
        }
    }
}
