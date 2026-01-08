using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Client.Data.BMD
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
    public struct ItemBMD
    {
        public int ItemIndex;
        public ushort ItemSubGroup;        
        public ushort ItemSubIndex;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szModelFolder;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szModelName;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
        public byte[] szItemName;
        public byte KindA;
        public byte KindB;
        public byte Type;
        public byte TwoHands;
        public ushort DropLevel;
        public ushort Slot;
        public ushort SkillIndex;
        public byte Width;
        public byte Height;
        public ushort DamageMin;
        public ushort DamageMax;
        public ushort DefenseRate;
        //public byte GAP_1;
        public ushort Defense;
        public ushort MagicResistance;
        public byte AttackSpeed;
        public byte WalkSpeed;
        public byte Durability;
        public byte MagicDur;
        public ushort GAP_2;
        public int MagicPower;
        public int CombatPower;
        public ushort ReqStr;
        public ushort ReqDex;
        public ushort ReqEne;
        public ushort ReqVit;
        public ushort ReqCmd;
        public ushort ReqLvl;
        public int ItemValue;
        public int Money;
        public byte SetAttr;
        public byte DW;
        public byte DK;
        public byte FE;
        public byte MG;
        public byte DL;
        public byte SU;
        public byte RF;
        public byte GL;
        public byte RW;
        public byte SL;
        public byte GC;
        public byte KM;
        public byte LM;
        public byte IK;
        public byte AL; //alchemist
        public byte Resist_0;
        public byte Resist_1;
        public byte Resist_2;
        public byte Resist_3;
        public byte Resist_4;
        public byte Resist_5;
        public byte Resist_6;
        public byte Dump;
        public byte Transaction;
        public byte PersonalStore;
        public byte Warehouse;
        public byte SellNpc;
        public byte Expensive;
        public byte Repair;
        public byte Overlap;
        public byte PcFlag;
        public byte MuunFlag;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 19)]
        public byte[] leftover;
    }
}
