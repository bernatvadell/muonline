using Client.Main.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using System;

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

        public RenderTarget2D MainRenderTarget { get; private set; }
        public RenderTarget2D TempTarget1 { get; private set; }
        public RenderTarget2D TempTarget2 { get; private set; }

        public Effect ShadowEffect { get; private set; }

        public void Init(GraphicsDevice graphicsDevice, ContentManager content)
        {
            _graphicsDevice = graphicsDevice;
            _contentManager = content;

            // Initialize resources needed for the game
            BMDLoader.Instance.SetGraphicsDevice(_graphicsDevice);
            TextureLoader.Instance.SetGraphicsDevice(_graphicsDevice);

            Pixel = new Texture2D(_graphicsDevice, 1, 1);
            Pixel.SetData(new[] { Color.White });

            InitializeRenderTargets();

            AlphaRGBEffect = LoadEffect("AlphaRGB");
            FXAAEffect = LoadEffect("FXAA");
            ShadowEffect = LoadEffect("Shadow");

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

        private void InitializeRenderTargets()
        {
            PresentationParameters pp = _graphicsDevice.PresentationParameters;

            int targetWidth = MuGame.Instance.Width;
            int targetHeight = MuGame.Instance.Height;

            //#if ANDROID || IOS
            //        targetWidth = (int)(targetWidth * 0.5f); //TODO: adjust the controls 
            //        targetHeight = (int)(targetHeight * 0.5f);
            //#endif

            MainRenderTarget = new RenderTarget2D(_graphicsDevice, targetWidth, targetHeight, false, pp.BackBufferFormat, DepthFormat.Depth24);
            TempTarget1 = new RenderTarget2D(_graphicsDevice, targetWidth, targetHeight);
            TempTarget2 = new RenderTarget2D(_graphicsDevice, targetWidth, targetHeight);

            EffectRenderTarget = new RenderTarget2D(_graphicsDevice, targetWidth, targetHeight, false, pp.BackBufferFormat, DepthFormat.Depth24);
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

            AlphaRGBEffect?.Dispose();
            FXAAEffect?.Dispose();
            ShadowEffect?.Dispose();
            AlphaTestEffect3D?.Dispose();
            BoundingBoxEffect3D?.Dispose();
            BasicEffect3D?.Dispose();
        }
    }
}