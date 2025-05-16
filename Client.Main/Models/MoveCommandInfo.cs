namespace Client.Main.Models
{
    public struct MoveCommandInfo
    {
        public int Index;
        public string ServerMapName;
        public string DisplayName;
        public int RequiredLevel;
        public int RequiredZen;
        public bool CanMove;
        public bool IsSelected;
        public bool IsStrifeMap;
        public bool IsEventMap;
    }
}