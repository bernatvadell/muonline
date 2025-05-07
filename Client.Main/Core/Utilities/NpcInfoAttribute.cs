using System;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public class NpcInfoAttribute : Attribute
{
    public new ushort TypeId { get; }
    public string DisplayName { get; }

    public NpcInfoAttribute(ushort typeId, string displayName)
    {
        TypeId = typeId;
        DisplayName = displayName;
    }
}
