using System;

namespace Client.Main.Models
{
    [Flags]
    public enum ControlAlign
    {
        None = 0,
        Top = 1 << 0,
        Bottom = 1 << 1,
        Left = 1 << 2,
        Right = 1 << 3,
        HorizontalCenter = 1 << 4,
        VerticalCenter = 1 << 5,
    }
}
