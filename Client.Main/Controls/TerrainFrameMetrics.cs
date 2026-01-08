namespace Client.Main.Controls
{
    /// <summary>
    /// Exposes frame-specific rendering metrics for debugging or performance monitoring.
    /// </summary>
    public sealed class TerrainFrameMetrics
    {
        public int DrawCalls { get; internal set; }
        public int DrawnTriangles { get; internal set; }
        public int DrawnBlocks { get; internal set; }
        public int DrawnCells { get; internal set; }
        public int GrassFlushes { get; internal set; }

        public void Reset()
        {
            DrawCalls = 0;
            DrawnTriangles = 0;
            DrawnBlocks = 0;
            DrawnCells = 0;
            GrassFlushes = 0;
        }
    }
}
