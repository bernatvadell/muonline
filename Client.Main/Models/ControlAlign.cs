using System;

namespace Client.Main.Models
{
    public enum HorizontalAlign
    {
        Left = 0,
        Center = 1,
        Right = 2,
    }

    public enum VerticalAlign
    {
        Top = 0,
        Center = 1,
        Bottom = 2,
    }

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
