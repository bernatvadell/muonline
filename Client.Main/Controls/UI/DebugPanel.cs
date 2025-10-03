using System.Text;
using Client.Main.Controllers;
using Client.Main.Content;
using Client.Main.Models;
using Microsoft.Xna.Framework;

namespace Client.Main.Controls.UI
{
    public class DebugPanel : UIControl
    {
        private LabelControl _fpsLabel;
        private LabelControl _mousePosLabel;
        private LabelControl _playerCordsLabel;
        private LabelControl _mapTileLabel;
        private LabelControl _effectsStatusLabel;
        private LabelControl _objectCursorLabel;
        private LabelControl _tileFlagsLabel;
        private LabelControl _performanceMetricsLabel;
        private LabelControl _objectMetricsLabel; // New label for object metrics
        private LabelControl _bmdMetricsLabel;    // New label for BMD buffer metrics
        private LabelControl _poolingMetricsLabel; // NEW: Matrix pooling stats
        private LabelControl _batchSortingLabel;   // NEW: Batch sorting status
        private double _updateTimer = 0;
        private const double UPDATE_INTERVAL_MS = 100; // 100ms
        private StringBuilder _sb = new StringBuilder(350); // Increased capacity for new metrics
        private int _lastFrameIndex = -1; // Track frame changes for per-frame stats

        public DebugPanel()
        {
            Align = ControlAlign.Top | ControlAlign.Right;
            Margin = new Margin { Top = 10, Right = 10 };
            Padding = new Margin { Top = 15, Left = 15 };
            ControlSize = new Point(380, 280); // Increased size for pooling + batch metrics
            BackgroundColor = Color.Black * 0.6f;
            BorderColor = Color.White * 0.3f;
            BorderThickness = 2;

            var posX = Padding.Left;
            var posY = Padding.Top;
            var labelHeight = 20;

            Controls.Add(_fpsLabel = new LabelControl { Text = "FPS: {0}", TextColor = Color.LightGreen, X = posX, Y = posY });
            Controls.Add(_mousePosLabel = new LabelControl { Text = "Mouse Position - X: {0}, Y:{1}", TextColor = Color.LightBlue, X = posX, Y = posY += labelHeight });
            Controls.Add(_playerCordsLabel = new LabelControl { Text = "Player Cords - X: {0}, Y:{1}", TextColor = Color.LightCoral, X = posX, Y = posY += labelHeight });
            Controls.Add(_mapTileLabel = new LabelControl { Text = "MAP Tile - X: {0}, Y:{1}", TextColor = Color.LightYellow, X = posX, Y = posY += labelHeight });
            Controls.Add(_effectsStatusLabel = new LabelControl { Text = "FXAA: {0} - AlphaRGB:{1}", TextColor = Color.Yellow, X = posX, Y = posY += labelHeight });
            Controls.Add(_objectCursorLabel = new LabelControl { Text = "Cursor Object: {0}", TextColor = Color.CadetBlue, X = posX, Y = posY += labelHeight });
            Controls.Add(_tileFlagsLabel = new LabelControl { Text = "Tile Flags: {0}", TextColor = Color.Lime, X = posX, Y = posY += labelHeight });
            Controls.Add(_performanceMetricsLabel = new LabelControl { Text = "Perf: {0}", TextColor = Color.OrangeRed, X = posX, Y = posY += labelHeight });
            Controls.Add(_objectMetricsLabel = new LabelControl { Text = "Objects: {0}", TextColor = Color.LightCyan, X = posX, Y = posY += labelHeight });
            Controls.Add(_bmdMetricsLabel = new LabelControl { Text = "BMD: {0}", TextColor = Color.LightSkyBlue, X = posX, Y = posY += labelHeight });
            Controls.Add(_poolingMetricsLabel = new LabelControl { Text = "Pool: {0}", TextColor = Color.Cyan, X = posX, Y = posY += labelHeight });
            Controls.Add(_batchSortingLabel = new LabelControl { Text = "Batch: {0}", TextColor = Color.Magenta, X = posX, Y = posY += labelHeight });
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!Visible) return;

#if DEBUG
            // Capture pooling stats once per frame (double-buffered to avoid race conditions)
            int currentFrame = MuGame.FrameIndex;
            if (currentFrame != _lastFrameIndex)
            {
                _lastFrameIndex = currentFrame;
                Client.Main.Objects.ModelObject.CaptureFrameStats();
            }
#endif

            _updateTimer += gameTime.ElapsedGameTime.TotalMilliseconds;

            if (_updateTimer >= UPDATE_INTERVAL_MS)
            {
                _updateTimer = 0;

                _sb.Clear().Append("FPS: ").Append((int)FPSCounter.Instance.FPS_AVG);
                _fpsLabel.Text = _sb.ToString();

                Point screenMouse = MuGame.Instance.Mouse.Position;
                Point uiMouse = MuGame.Instance.UiMouseState.Position;
                _sb.Clear().Append("Mouse Screen (X:").Append(screenMouse.X)
                   .Append(", Y:").Append(screenMouse.Y)
                   .Append(") UI (X:").Append(uiMouse.X)
                   .Append(", Y:").Append(uiMouse.Y).Append(')');
                _mousePosLabel.Text = _sb.ToString();

                _sb.Clear().Append("FXAA: ").Append(GraphicsManager.Instance.IsFXAAEnabled ? "ON" : "OFF")
                   .Append(" - AlphaRGB:").Append(GraphicsManager.Instance.IsAlphaRGBEnabled ? "ON" : "OFF");
                _effectsStatusLabel.Text = _sb.ToString();

                _sb.Clear().Append("Cursor Object: ").Append(World?.Scene?.MouseHoverObject != null ? World.Scene.MouseHoverObject.GetType().Name : "N/A");
                _objectCursorLabel.Text = _sb.ToString();

                if (World is WalkableWorldControl walkableWorld && walkableWorld.Walker != null)
                {
                    _playerCordsLabel.Visible = true;
                    _mapTileLabel.Visible = true;
                    _tileFlagsLabel.Visible = true;
                    _performanceMetricsLabel.Visible = true;
                    _objectMetricsLabel.Visible = true; // Show object metrics label
                    _bmdMetricsLabel.Visible = true;

                    _sb.Clear().Append("Player Cords - X: ").Append(walkableWorld.Walker.Location.X)
                       .Append(", Y:").Append(walkableWorld.Walker.Location.Y);
                    _playerCordsLabel.Text = _sb.ToString();

                    _sb.Clear().Append("MAP Tile - X: ").Append(walkableWorld.MouseTileX)
                       .Append(", Y:").Append(walkableWorld.MouseTileY);
                    _mapTileLabel.Text = _sb.ToString();

                    var flags = walkableWorld.Terrain.RequestTerrainFlag((int)walkableWorld.Walker.Location.X,
                                                                         (int)walkableWorld.Walker.Location.Y);
                    _sb.Clear().Append("Tile Flags: ").Append(flags);
                    _tileFlagsLabel.Text = _sb.ToString();

                    // Update terrain performance metrics display
                    var terrainMetrics = walkableWorld.Terrain.FrameMetrics;
                    _sb.Clear()
                       .Append($"Terrain: Drw:{terrainMetrics.DrawCalls} ")
                       .Append($"Tri:{terrainMetrics.DrawnTriangles} ")
                       .Append($"Blk:{terrainMetrics.DrawnBlocks} ")
                       .Append($"Cel:{terrainMetrics.DrawnCells}");
                    _performanceMetricsLabel.Text = _sb.ToString();

                    // Update object performance metrics display
                    var objectMetrics = walkableWorld.ObjectMetrics;
                    _sb.Clear().Append($"Objects: Drw:{objectMetrics.DrawnTotal}/{objectMetrics.TotalObjects} (Culled:{objectMetrics.CulledByFrustum})");
                    _objectMetricsLabel.Text = _sb.ToString();

                    // Update BMD buffer metrics
                    var bmd = BMDLoader.Instance;
                    _sb.Clear()
                      .Append($"BMD: VB:{bmd.LastFrameVBUpdates} IB:{bmd.LastFrameIBUploads} ")
                      .Append($"Vtx:{bmd.LastFrameVerticesTransformed} Mesh:{bmd.LastFrameMeshesProcessed} ")
                      .Append($"Cache:{bmd.LastFrameCacheHits}/{bmd.LastFrameCacheMisses}");
                    _bmdMetricsLabel.Text = _sb.ToString();

#if DEBUG
                    // Update Matrix pooling metrics (DEBUG only) - per frame stats
                    var poolStats = Client.Main.Objects.ModelObject.GetPoolingStats();
                    _sb.Clear()
                      .Append($"Pool/Frame: Rent:{poolStats.Rents} Ret:{poolStats.Returns} ")
                      .Append($"Leak:{poolStats.Rents - poolStats.Returns}");
                    _poolingMetricsLabel.Text = _sb.ToString();
                    _poolingMetricsLabel.Visible = true;
#else
                    _poolingMetricsLabel.Visible = false;
#endif

                    // Update batch sorting status
                    _sb.Clear()
                      .Append($"Batch Sort: {(Constants.ENABLE_BATCH_OPTIMIZED_SORTING ? "ON" : "OFF")} ")
                      .Append($"(Model grouping for state reduction)");
                    _batchSortingLabel.Text = _sb.ToString();
                    _batchSortingLabel.Visible = true;
                }
                else
                {
                    _playerCordsLabel.Visible = false;
                    _mapTileLabel.Visible = false;
                    _tileFlagsLabel.Visible = false;
                    _performanceMetricsLabel.Visible = false;
                    _objectMetricsLabel.Visible = false; // Hide object metrics label
                    _bmdMetricsLabel.Visible = false;
                    _poolingMetricsLabel.Visible = false;
                    _batchSortingLabel.Visible = false;
                }
            }
        }
    }
}
