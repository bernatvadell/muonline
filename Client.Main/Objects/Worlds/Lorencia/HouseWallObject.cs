using Client.Data;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Lorencia
{
    public class HouseWallObject : ModelObject
    {
        // Constants
        private const float TARGET_ALPHA = 0.3f;
        private const float FADE_SPEED = 0.3f;
        private const float Y_PROXIMITY_THRESHOLD = 200f;

        // Flicker effect settings for mesh 4
        private static readonly Random _rand = new Random();

        // State fields
        private float _alpha = 1f;
        private bool _playerInside = false;
        private bool _isTransparent = false;

        private bool _flickerEnabled = false;
        private float _flickerAlpha = 1f;
        private float _flickerStart = 1f;
        private float _flickerTarget = 1f;
        private float _flickerDur = 0f;
        private float _flickerElapsed = 0f;

        public override bool IsTransparent => _isTransparent || (Alpha < 0.99f);

        // Cannot be cached due to flicker animation and player-proximity fading
        public override bool IsStaticForCaching => false;

        public override async Task Load()
        {
            BlendState = BlendState.AlphaBlend;
            LightEnabled = true;

            string idx = (Type - (ushort)ModelType.HouseWall01 + 1)
                         .ToString()
                         .PadLeft(2, '0');

            if (idx == "02")
            {
                // Enable flicker for mesh 4
                _flickerEnabled = true;
                _flickerStart = 1f;
                _flickerTarget = 0.85f + (float)_rand.NextDouble() * 0.15f;  // 0.85–1.0
                _flickerDur = 0.1f + (float)_rand.NextDouble() * 0.1f;       // 0.10–0.20s
                _flickerElapsed = 0f;

                BlendMesh = 4;
                BlendMeshState = BlendState.Additive;
                LightEnabled = true;
            }

            Model = await BMDLoader.Instance.Prepare($"Object1/HouseWall{idx}.bmd");
            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            _isTransparent = false;
            base.Update(gameTime);

            if (World is not WalkableWorldControl world)
                return;

            // Set IsTransparent only when actual transparency is active
            IsTransparent = (Alpha < 0.99f) || _isTransparent;

            // Flicker effect
            if (_flickerEnabled)
            {
                float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
                _flickerElapsed += dt;

                if (_flickerElapsed >= _flickerDur)
                {
                    _flickerStart = _flickerTarget;
                    _flickerTarget = 0.85f + (float)_rand.NextDouble() * 0.15f;
                    _flickerDur = 0.1f + (float)_rand.NextDouble() * 0.1f;
                    _flickerElapsed = 0f;
                }

                float t = MathHelper.Clamp(_flickerElapsed / _flickerDur, 0f, 1f);
                float smoothStep = t * t * (3f - 2f * t);
                _flickerAlpha = MathHelper.Lerp(_flickerStart, _flickerTarget, smoothStep);
            }

            // Fade when player is behind the wall
            if (Type >= 121 && Type <= 124)
            {
                Vector2 pPos = world.Walker.Location;

                bool behind = pPos.X * 100 < Position.X
                              && Math.Abs(pPos.X * 100 - Position.X) < 300f;
                bool withinY = Math.Abs(pPos.Y * 100 - Position.Y) <= Y_PROXIMITY_THRESHOLD + 50f;

                if (behind && withinY)
                    _isTransparent = true;

                float target = (behind && withinY) ? TARGET_ALPHA : 1f;
                _alpha = MathHelper.Lerp(_alpha, target, FADE_SPEED);
                Alpha = _alpha;
            }

            // Hide roof when player is inside
            if (Type == (ushort)ModelType.HouseWall05 ||
                Type == (ushort)ModelType.HouseWall06)
            {
                _playerInside = world.HeroTile == 4;
                float target = _playerInside ? 0f : 1f;
                Alpha = MathHelper.Lerp(Alpha, target, FADE_SPEED);
            }
        }

        public override void DrawMesh(int mesh)
        {
            if (_flickerEnabled && mesh == BlendMesh)
            {
                float originalAlpha = Alpha;
                var device = GraphicsDevice;
                var originalBlend = device.BlendState;

                if (_isTransparent)
                {
                    // Apply minimum alpha with flicker when transparent
                    Alpha = Math.Max(TARGET_ALPHA, originalAlpha) * _flickerAlpha;
                }
                else
                {
                    // Normal flicker when opaque
                    Alpha = originalAlpha * _flickerAlpha;
                }

                base.DrawMesh(mesh);

                Alpha = originalAlpha;
                device.BlendState = originalBlend;
            }
            else
            {
                base.DrawMesh(mesh);
            }
        }
    }
}
