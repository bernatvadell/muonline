using Client.Main.Content;
using Client.Main.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Controllers
{
    public class GraphicsManager : IDisposable
    {
        private GraphicsDevice _graphicsDevice;
        private ContentManager _contentManager;

        private Texture2D _pixelTexture;

        public static GraphicsManager Instance { get; private set; } = new GraphicsManager();

        public GraphicsDevice GraphicsDevice => _graphicsDevice;

        public bool IsFXAAEnabled { get; set; } = false;
        public bool IsAlphaRGBEnabled { get; set; } = true;

        public SpriteBatch Sprite { get; private set; }
        public SpriteFont Font { get; private set; }
        public RenderTarget2D EffectRenderTarget { get; private set; }
        public Texture2D Pixel { get; private set; }
        public AlphaTestEffect AlphaTestEffectUI { get; private set; }
        public AlphaTestEffect AlphaTestEffect3D { get; private set; }
        public BasicEffect BasicEffect3D { get; private set; }
        public BasicEffect BoundingBoxEffect3D { get; private set; }
        public Effect AlphaRGBEffect { get; set; }
        public Effect FXAAEffect { get; private set; }
        public Effect GammaCorrectionEffect { get; private set; }

        public RenderTarget2D MainRenderTarget { get; private set; }
        public RenderTarget2D TempTarget1 { get; private set; }
        public RenderTarget2D TempTarget2 { get; private set; }

        public Effect ShadowEffect { get; private set; }
        public Effect ItemMaterialEffect { get; private set; }
        public Effect MonsterMaterialEffect { get; private set; }
        public Effect DynamicLightingEffect { get; private set; }
        public ShadowMapRenderer ShadowMapRenderer { get; private set; }
        
        // RasterizerState cache to avoid per-mesh allocations
        private static readonly Dictionary<(float bias, CullMode cull), RasterizerState> _rasterizerCache = new();
        
        // Cached DepthStencilState for highlight rendering to avoid allocations
        public static readonly DepthStencilState ReadOnlyDepth = new DepthStencilState
        {
            DepthBufferEnable = true,
            DepthBufferWriteEnable = false
        };

        public void Init(GraphicsDevice graphicsDevice, ContentManager content)
        {
            _graphicsDevice = graphicsDevice;
            _contentManager = content;

            // Initialize resources needed for the game
            BMDLoader.Instance.SetGraphicsDevice(_graphicsDevice);
            TextureLoader.Instance.SetGraphicsDevice(_graphicsDevice);
            DynamicBufferPool.SetGraphicsDevice(_graphicsDevice);

            Pixel = new Texture2D(_graphicsDevice, 1, 1);
            Pixel.SetData(new[] { Color.White });

            InitializeRenderTargets();

            AlphaRGBEffect = LoadEffect("AlphaRGB");
            FXAAEffect = LoadEffect("FXAA");
            ShadowEffect = LoadEffect("Shadow");
            GammaCorrectionEffect = LoadEffect("GammaCorrection");
            ItemMaterialEffect = LoadEffect("ItemMaterial");
            MonsterMaterialEffect = LoadEffect("MonsterMaterial");
            DynamicLightingEffect = LoadEffect("DynamicLighting");
            ShadowMapRenderer = new ShadowMapRenderer(_graphicsDevice);

            InitializeFXAAEffect();

            AlphaTestEffectUI = new AlphaTestEffect(_graphicsDevice)
            {
                VertexColorEnabled = true,
                Projection = Matrix.CreateOrthographicOffCenter(0, _graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height, 0, 0, 1),
                View = Matrix.Identity,
                World = Matrix.Identity,
                ReferenceAlpha = (int)(255 * 0.25f)
            };

            AlphaTestEffect3D = new AlphaTestEffect(_graphicsDevice)
            {
                VertexColorEnabled = true,
                World = Matrix.Identity,
                AlphaFunction = CompareFunction.Greater,
                ReferenceAlpha = (int)(255 * 0.01f)
            };

            BasicEffect3D = new BasicEffect(_graphicsDevice)
            {
                TextureEnabled = true,
                VertexColorEnabled = true,
                World = Matrix.Identity
            };

            BoundingBoxEffect3D = new BasicEffect(_graphicsDevice)
            {
                VertexColorEnabled = true,
                View = Camera.Instance.View,
                Projection = Camera.Instance.Projection,
                World = Matrix.Identity
            };

            Sprite = new SpriteBatch(_graphicsDevice);
            Font = _contentManager.Load<SpriteFont>("Arial");
        }

        private void InitializeFXAAEffect()
        {
            FXAAEffect?.Parameters["Resolution"]?.SetValue(new Vector2(_graphicsDevice.Viewport.Width, _graphicsDevice.Viewport.Height));
        }


        public void UpdateRenderScale()
        {
            // Dispose old render targets
            MainRenderTarget?.Dispose();
            TempTarget1?.Dispose();
            TempTarget2?.Dispose();
            EffectRenderTarget?.Dispose();

            // Recreate with new scale
            InitializeRenderTargets();

            // Update UiScaler with new render scale
            var settings = MuGame.AppSettings?.Graphics;
            if (settings != null)
            {
                int actualWidth = Math.Max(1, _graphicsDevice.PresentationParameters.BackBufferWidth);
                int actualHeight = Math.Max(1, _graphicsDevice.PresentationParameters.BackBufferHeight);
                UiScaler.Configure(
                    actualWidth,
                    actualHeight,
                    settings.UiVirtualWidth,
                    settings.UiVirtualHeight);
            }
        }

        /// <summary>
        /// Gets the appropriate SamplerState based on quality settings.
        /// </summary>
        public static SamplerState GetQualitySamplerState()
        {
            if (Constants.HIGH_QUALITY_TEXTURES)
            {
                return SamplerState.AnisotropicClamp;
            }
            return SamplerState.PointClamp;
        }

        /// <summary>
        /// Gets the appropriate SamplerState for linear sampling based on quality settings.
        /// </summary>
        public static SamplerState GetQualityLinearSamplerState()
        {
            if (Constants.HIGH_QUALITY_TEXTURES)
            {
                return SamplerState.AnisotropicWrap;
            }
            return SamplerState.LinearClamp;
        }

        private void InitializeRenderTargets()
        {
            PresentationParameters pp = _graphicsDevice.PresentationParameters;

            int backBufferWidth = Math.Max(1, _graphicsDevice.PresentationParameters.BackBufferWidth);
            int backBufferHeight = Math.Max(1, _graphicsDevice.PresentationParameters.BackBufferHeight);

            int targetWidth = Math.Max(1, (int)MathF.Round(backBufferWidth * Constants.RENDER_SCALE));
            int targetHeight = Math.Max(1, (int)MathF.Round(backBufferHeight * Constants.RENDER_SCALE));

            // Apply render scale for internal resolution, guard against zero-sized targets on mobile

            // POPRAWKA: Używamy SurfaceFormat.Color zamiast pp.BackBufferFormat dla MSAA
            // to pomaga z problemem gamma
            SurfaceFormat renderTargetFormat = Constants.MSAA_ENABLED ? SurfaceFormat.Color : pp.BackBufferFormat;

            MainRenderTarget = new RenderTarget2D(_graphicsDevice, targetWidth, targetHeight, false,
                renderTargetFormat, DepthFormat.Depth24,
                Constants.MSAA_ENABLED ? pp.MultiSampleCount : 0,
                RenderTargetUsage.DiscardContents);

            // Temp targets don't need MSAA
            TempTarget1 = new RenderTarget2D(_graphicsDevice, targetWidth, targetHeight, false,
                SurfaceFormat.Color, DepthFormat.None);
            TempTarget2 = new RenderTarget2D(_graphicsDevice, targetWidth, targetHeight, false,
                SurfaceFormat.Color, DepthFormat.None);

            EffectRenderTarget = new RenderTarget2D(_graphicsDevice, targetWidth, targetHeight, false,
                renderTargetFormat, DepthFormat.Depth24,
                Constants.MSAA_ENABLED ? pp.MultiSampleCount : 0,
                RenderTargetUsage.DiscardContents);
        }

        private Effect LoadEffect(string effectName)
        {
            try
            {
                return _contentManager.Load<Effect>(effectName);
            }
            catch (Exception)
            {
                Console.WriteLine($"{effectName} could not be loaded!");
                return null;
            }
        }

        public void SwapTargets(ref RenderTarget2D source, ref RenderTarget2D destination)
        {
            source = destination;
            destination = (destination == TempTarget1) ? TempTarget2 : TempTarget1;
        }

        public Texture2D GetPixelTexture()
        {
            if (_pixelTexture == null)
            {
                _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
                _pixelTexture.SetData(new[] { Color.White });
            }

            return _pixelTexture;
        }

        public void Dispose()
        {
            MainRenderTarget?.Dispose();
            TempTarget1?.Dispose();
            TempTarget2?.Dispose();
            Pixel?.Dispose();
            ShadowMapRenderer?.Dispose();

            AlphaRGBEffect?.Dispose();
            FXAAEffect?.Dispose();
            ShadowEffect?.Dispose();
            DynamicLightingEffect?.Dispose();
            ItemMaterialEffect?.Dispose();
            MonsterMaterialEffect?.Dispose();
            AlphaTestEffect3D?.Dispose();
            BoundingBoxEffect3D?.Dispose();
            BasicEffect3D?.Dispose();
            
            // Dispose cached rasterizer states
            foreach (var state in _rasterizerCache.Values)
                state.Dispose();
            _rasterizerCache.Clear();
        }
        
        /// <summary>
        /// Gets a cached RasterizerState with the specified depth bias and cull mode to avoid per-mesh allocations.
        /// PERFORMANCE: This eliminates expensive RasterizerState creation during rendering.
        /// </summary>
        public static RasterizerState GetCachedRasterizerState(float depthBias, CullMode cullMode, RasterizerState template = null)
        {
            // Normalize depth bias to common values to improve cache hit rate
            float normalizedBias = depthBias == 0f ? 0f : 
                                 Math.Abs(depthBias) < 0.00001f ? -0.00002f : depthBias;
            
            var key = (normalizedBias, cullMode);
            
            if (_rasterizerCache.TryGetValue(key, out var cachedState))
                return cachedState;

            // Create new state and cache it
            var newState = new RasterizerState
            {
                CullMode = cullMode,
                FillMode = template?.FillMode ?? FillMode.Solid,
                DepthBias = normalizedBias,
                SlopeScaleDepthBias = normalizedBias * 0.1f
            };
            
            _rasterizerCache[key] = newState;
            return newState;
        }
    }
}
