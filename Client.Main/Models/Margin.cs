namespace Client.Main.Models
{
    public struct Margin
    {
        private static Margin _empty = default;

        public int Top;
        public int Right;
        public int Bottom;
        public int Left;

        public static Margin Empty => _empty;
    }
}
