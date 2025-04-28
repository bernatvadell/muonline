namespace Client.Main.Models
{
    public enum MessageType
    {
        All = 0,
        Chat,
        Whisper,
        System,
        Error,
        Party,
        Guild,
        Union,
        Gens,
        GM,

        Info,

        Unknown = -1
    }
}