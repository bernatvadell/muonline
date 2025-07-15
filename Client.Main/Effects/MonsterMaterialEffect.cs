using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Client.Main.Controllers;

namespace Client.Main.Effects
{
    public class MonsterMaterialEffect
    {
        private Effect _effect;
        private EffectParameter _worldParam;
        private EffectParameter _viewParam;
        private EffectParameter _projectionParam;
        private EffectParameter _eyePositionParam;
        private EffectParameter _lightDirectionParam;
        private EffectParameter _diffuseTextureParam;
        private EffectParameter _glowColorParam;
        private EffectParameter _glowIntensityParam;
        private EffectParameter _timeParam;
        private EffectParameter _enableGlowParam;

        public Matrix World { get; set; } = Matrix.Identity;
        public Matrix View { get; set; } = Matrix.Identity;
        public Matrix Projection { get; set; } = Matrix.Identity;
        public Vector3 EyePosition { get; set; } = Vector3.Zero;
        public Vector3 LightDirection { get; set; } = new Vector3(0.707f, -0.707f, 0f);
        public Texture2D DiffuseTexture { get; set; }
        public Vector3 GlowColor { get; set; } = new Vector3(1.0f, 0.8f, 0.0f); // Default gold
        public float GlowIntensity { get; set; } = 0.0f;
        public float Time { get; set; } = 0.0f;
        public bool EnableGlow { get; set; } = false;

        public MonsterMaterialEffect(GraphicsDevice graphicsDevice)
        {
            // Try to load from content manager first, fallback to graphics manager
            try
            {
                var graphicsManager = GraphicsManager.Instance;
                if (graphicsManager?.ItemMaterialEffect != null)
                {
                    // Clone the existing effect for monster use
                    _effect = graphicsManager.ItemMaterialEffect.Clone();
                }
                else
                {
                    // Load directly - this should work if MonsterMaterial.fx is in Content
                    var content = MuGame.Instance?.Content;
                    if (content != null)
                    {
                        _effect = content.Load<Effect>("MonsterMaterial");
                    }
                    else
                    {
                        throw new System.Exception("Could not load MonsterMaterial effect");
                    }
                }
            }
            catch
            {
                // Fallback: try to load from GraphicsManager
                var graphicsManager = GraphicsManager.Instance;
                _effect = graphicsManager?.ItemMaterialEffect?.Clone() ?? 
                          throw new System.Exception("Could not initialize MonsterMaterialEffect");
            }

            CacheParameters();
        }

        private void CacheParameters()
        {
            _worldParam = _effect.Parameters["World"];
            _viewParam = _effect.Parameters["View"];
            _projectionParam = _effect.Parameters["Projection"];
            _eyePositionParam = _effect.Parameters["EyePosition"];
            _lightDirectionParam = _effect.Parameters["LightDirection"];
            _diffuseTextureParam = _effect.Parameters["DiffuseTexture"];
            
            // Monster-specific parameters
            _glowColorParam = _effect.Parameters["GlowColor"];
            _glowIntensityParam = _effect.Parameters["GlowIntensity"];
            _timeParam = _effect.Parameters["Time"];
            _enableGlowParam = _effect.Parameters["EnableGlow"];
        }

        public void Apply()
        {
            _worldParam?.SetValue(World);
            _viewParam?.SetValue(View);
            _projectionParam?.SetValue(Projection);
            _eyePositionParam?.SetValue(EyePosition);
            _lightDirectionParam?.SetValue(LightDirection);
            _diffuseTextureParam?.SetValue(DiffuseTexture);
            
            _glowColorParam?.SetValue(GlowColor);
            _glowIntensityParam?.SetValue(GlowIntensity);
            _timeParam?.SetValue(Time);
            _enableGlowParam?.SetValue(EnableGlow);
        }

        public EffectPass CurrentTechnique => _effect.CurrentTechnique.Passes[0];

        public void SetGlow(Vector3 color, float intensity)
        {
            GlowColor = color;
            GlowIntensity = intensity;
            EnableGlow = intensity > 0.0f;
        }

        public void DisableGlow()
        {
            EnableGlow = false;
            GlowIntensity = 0.0f;
        }

        // Predefined glow colors for convenience
        public static class GlowColors
        {
            public static readonly Vector3 Gold = new Vector3(1.0f, 0.8f, 0.0f);
            public static readonly Vector3 Red = new Vector3(1.0f, 0.2f, 0.2f);
            public static readonly Vector3 Blue = new Vector3(0.2f, 0.4f, 1.0f);
            public static readonly Vector3 Green = new Vector3(0.2f, 1.0f, 0.2f);
            public static readonly Vector3 Purple = new Vector3(0.8f, 0.2f, 1.0f);
            public static readonly Vector3 White = new Vector3(1.0f, 1.0f, 1.0f);
            public static readonly Vector3 Orange = new Vector3(1.0f, 0.5f, 0.0f);
        }
    }
}