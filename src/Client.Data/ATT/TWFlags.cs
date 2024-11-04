namespace Client.Data.ATT
{
    [Flags]
    public enum TWFlags : ushort
    {
        None = 0x0000,
        SafeZone = 0x0001,  // 1
        Character = 0x0002,  // 2
        NoMove = 0x0004,  // 4
        NoGround = 0x0008,  // 8
        Water = 0x0010,  // 16
        Action = 0x0020,  // 32
        Height = 0x0040,  // 64
        CameraUp = 0x0080,  // 128
        NoAttackZone = 0x0100,  // 256
        Att1 = 0x0200,  // 512
        Att2 = 0x0400,  // 1024
        Att3 = 0x0800,  // 2048
        Att4 = 0x1000,  // 4096
        Att5 = 0x2000,  // 8192
        Att6 = 0x4000,  // 16384
        Att7 = 0x8000   // 32768
    }
}
