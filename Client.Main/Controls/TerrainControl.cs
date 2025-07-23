using Client.Data.ATT;
using Client.Main.Controls.Terrain;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Client.Main.Controls
{
    /// <summary>
    /// Manages and coordinates all terrain-related systems, acting as a facade
    /// for loading, rendering, and querying terrain information.
    /// </summary>
    public class TerrainControl : GameControl
    {
        // --- Sub-systems ---
        private TerrainData _data;
        private TerrainLoader _loader;
        private TerrainPhysics _physics;
        private TerrainLightManager _lightManager;
        private WindSimulator _wind;
        private TerrainVisibilityManager _visibility;
        private TerrainRenderer _renderer;
        private GrassRenderer _grassRenderer;

        // --- Public Properties (Facades) ---
        public short WorldIndex { get; set; }
        public Vector3 LightDirection { get; set; } = new Vector3(0.5f, -0.5f, 0.5f);
        public IReadOnlyList<DynamicLight> DynamicLights => _lightManager.DynamicLights;
        public IReadOnlyList<DynamicLight> ActiveLights => _lightManager.ActiveLights;
        public Texture2D HeightMapTexture => _data?.HeightMapTexture;
        private Dictionary<int, string> _pendingTextureMap = new();

        public Dictionary<int, string> TextureMappingFiles
        {
            get => _data != null ? _data.TextureMappingFiles : _pendingTextureMap;
            set
            {
                if (_data != null)
                    _data.TextureMappingFiles = value;
                else
                    _pendingTextureMap = value ?? new Dictionary<int, string>();
            }
        }

        public float WaterSpeed { get => _renderer.WaterSpeed; set => _renderer.WaterSpeed = value; }
        public float DistortionAmplitude { get => _renderer.DistortionAmplitude; set => _renderer.DistortionAmplitude = value; }
        public float DistortionFrequency { get => _renderer.DistortionFrequency; set => _renderer.DistortionFrequency = value; }
        public float AmbientLight { get => _renderer.AmbientLight; set => _renderer.AmbientLight = value; }
        public float GrassBrightness { get => _grassRenderer.GrassBrightness; set => _grassRenderer.GrassBrightness = value; }
        public Vector2 WaterFlowDirection { get => _renderer.WaterFlowDirection; set => _renderer.WaterFlowDirection = value; }
        public HashSet<byte> GrassTextureIndices => _grassRenderer.GrassTextureIndices;
        
        /// <summary>
        /// Configures grass settings for specific world requirements.
        /// This should be called in world's AfterLoad() method.
        /// </summary>
        /// <param name="brightness">Grass brightness multiplier (default: 1.0f)</param>
        /// <param name="textureIndices">Valid texture indices for grass rendering (default: {0})</param>
        public void ConfigureGrass(float brightness = 1f, params byte[] textureIndices)
        {
            var oldBrightness = _grassRenderer.GrassBrightness;
            _grassRenderer.GrassBrightness = brightness;
            
            if (textureIndices != null && textureIndices.Length > 0)
            {
                _grassRenderer.GrassTextureIndices.Clear();
                foreach (var index in textureIndices)
                {
                    _grassRenderer.GrassTextureIndices.Add(index);
                }
            }
            
            Console.WriteLine($"[TerrainControl] ConfigureGrass for World{WorldIndex} - Brightness: {oldBrightness:F2} → {brightness:F2}, TextureIndices: [{string.Join(", ", textureIndices ?? new byte[] { 0 })}]");
        }

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
        public TerrainFrameMetrics FrameMetrics { get; } = new TerrainFrameMetrics();

        public TerrainControl()
        {
            AutoViewSize = false;
            ViewSize = new Point(MuGame.Instance.Width, MuGame.Instance.Height);
        }

        public override async Task Load()
        {
            _loader = new TerrainLoader(WorldIndex);
            
            if (_pendingTextureMap.Count > 0)
            {
                _loader.SetTextureMapping(_pendingTextureMap);
            }
            
            _data = await _loader.LoadAsync();
            _pendingTextureMap.Clear();

            if (_data == null)
            {
                Status = Models.GameControlStatus.Error;
                return;
            }

            // Initialize sub-systems in order of dependency
            _lightManager = new TerrainLightManager(_data, this);
            _physics = new TerrainPhysics(_data, _lightManager);
            _wind = new WindSimulator(_data);
            _visibility = new TerrainVisibilityManager(_data);
            _grassRenderer = new GrassRenderer(GraphicsDevice, _data, _physics, _wind);
            _renderer = new TerrainRenderer(GraphicsDevice, _data, _visibility, _lightManager, _grassRenderer)
            {
                WorldIndex = this.WorldIndex
            };

            // Post-load processing
            _lightManager.CreateTerrainNormals();
            _lightManager.CreateFinalLightmap(LightDirection);
            _renderer.CreateHeightMapTexture();
            
            // Reset grass to defaults before loading world-specific content
            _grassRenderer.LoadContent(WorldIndex);

            Camera.Instance.AspectRatio = GraphicsDevice.Viewport.AspectRatio;

            await base.Load();
        }

        public override void Update(GameTime time)
        {
            base.Update(time);
            if (Status != Models.GameControlStatus.Ready || _visibility == null)
                return;

            var camPos2D = new Vector2(Camera.Instance.Position.X, Camera.Instance.Position.Y);

            _visibility.Update(camPos2D);
            _lightManager.UpdateActiveLights((float)time.ElapsedGameTime.TotalSeconds);
            _wind.Update(time);
            _renderer.Update(time);
        }

        public override void Draw(GameTime time)
        {
            if (!Visible || Status != Models.GameControlStatus.Ready || _renderer == null || _grassRenderer == null)
                return;

            FrameMetrics.Reset();
            _renderer.Draw(after: false);

            // Aggregate metrics from renderers
            FrameMetrics.DrawCalls = _renderer.DrawCalls + _grassRenderer.Flushes; // Grass flush is a draw call
            FrameMetrics.DrawnTriangles = _renderer.DrawnTriangles + _grassRenderer.DrawnTriangles;
            FrameMetrics.DrawnBlocks = _renderer.DrawnBlocks;
            FrameMetrics.DrawnCells = _renderer.DrawnCells;
            FrameMetrics.GrassFlushes = _grassRenderer.Flushes;

            base.Draw(time);
        }

        public override void DrawAfter(GameTime gameTime)
        {
            if (!Visible || Status != Models.GameControlStatus.Ready || _renderer == null || _grassRenderer == null)
                return;

            _renderer.Draw(after: true);
            base.DrawAfter(gameTime);
        }

        // --- Public Query Methods (Facade) ---
        public TWFlags RequestTerrainFlag(int x, int y) => _physics.RequestTerrainFlag(x, y);
        public float RequestTerrainHeight(float xf, float yf) => _physics.RequestTerrainHeight(xf, yf);
        public Vector3 EvaluateTerrainLight(float xf, float yf) => _physics.RequestTerrainLight(xf, yf, AmbientLight);
        public Vector3 EvaluateDynamicLight(Vector2 position) => _lightManager.EvaluateDynamicLight(position);
        public byte GetBaseTextureIndexAt(int x, int y) => _physics.GetBaseTextureIndexAt(x, y);
        public float GetWindValue(int x, int y) => _wind.GetWindValue(x, y);

        // --- Light Management (Facade) ---
        public void AddDynamicLight(DynamicLight light) => _lightManager.AddDynamicLight(light);
        public void RemoveDynamicLight(DynamicLight light) => _lightManager.RemoveDynamicLight(light);

        public override void Dispose()
        {
            base.Dispose();
            _data = null; // Allow GC to collect all data
            GC.SuppressFinalize(this);
        }
    }
}