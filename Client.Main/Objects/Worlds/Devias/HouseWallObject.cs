using Client.Data;
using Client.Main.Content;
using Client.Main.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Threading.Tasks;

namespace Client.Main.Objects.Worlds.Devias
{
    public class HouseWallObject : ModelObject
    {
        // Constants
        private const float TARGET_ALPHA = 0.3f;
        private const float FADE_SPEED = 0.3f;
        private const float Y_PROXIMITY_THRESHOLD = 100f;
        private const float ScaleLocation = 100f; // Conversion factor for player position

        // Flicker effect settings for mesh 3 when Type == 78
        private static readonly Random _rand = new Random();

        // State fields
        private float _alpha = 1f;
        private bool _isTransparent = false;

        private bool _flickerEnabled = false;
        private float _flickerAlpha = 1f;
        private float _flickerStart = 1f;
        private float _flickerTarget = 1f;
        private float _flickerDur = 0f;
        private float _flickerElapsed = 0f;

        public override bool IsTransparent => _isTransparent || (Alpha < 0.99f);

        public override async Task Load()
        {
            BlendState = BlendState.AlphaBlend;
            LightEnabled = true;

            if (Type == 78)
            {
                // Enable flicker effect for mesh 3
                _flickerEnabled = true;
                _flickerStart = 1f;
                _flickerTarget = 0.85f + (float)_rand.NextDouble() * 0.15f;  // Range 0.85–1.0
                _flickerDur = 0.1f + (float)_rand.NextDouble() * 0.1f;      // Range 0.10–0.20 seconds
                _flickerElapsed = 0f;

                BlendMesh = 3;
                BlendMeshState = BlendState.Additive;
            }

            Model = await BMDLoader.Instance.Prepare($"Object3/Object{Type + 1}.bmd");
            await base.Load();
        }

        public override void Update(GameTime gameTime)
        {
            _isTransparent = false;
            base.Update(gameTime);

            if (!(World is WalkableWorldControl walkableWorld))
                return;

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
                float smoothStep = t * t * (3f - 2f * t); // Smooth interpolation
                _flickerAlpha = MathHelper.Lerp(_flickerStart, _flickerTarget, smoothStep);
            }

            Vector2 playerPos = walkableWorld.Walker.Location * ScaleLocation;

            if (Type == 75 || Type == 76 || Type == 77 || Type == 78)
            {
                // Check if player is behind the wall
                bool isBehind = (playerPos.X < Position.X) && (Math.Abs(playerPos.X - Position.X) < 300f);
                bool isWithinY = Math.Abs(playerPos.Y - Position.Y) <= Y_PROXIMITY_THRESHOLD;
                float targetAlpha = (isBehind && isWithinY) ? TARGET_ALPHA : 1f;

                if (isBehind && isWithinY)
                    _isTransparent = true;

                _alpha = MathHelper.Lerp(_alpha, targetAlpha, FADE_SPEED);
                Alpha = _alpha;
            }
            else if (Type == (ushort)ModelType.Fence01 || Type == (ushort)ModelType.Fence02 || Type == 98 || Type == 99)
            {
                // Fade fence objects when player is under the roof
                bool playerInside = IsPlayerUnderRoof(walkableWorld);
                float targetAlpha = playerInside ? 0f : 1f;
                Alpha = MathHelper.Lerp(Alpha, targetAlpha, FADE_SPEED);
            }

            IsTransparent = (Alpha < 0.99f) || _isTransparent;
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);
        }

        public override void DrawMesh(int mesh)
        {
            if (_flickerEnabled && mesh == BlendMesh)
            {
                float originalAlpha = Alpha;
                var device = GraphicsDevice;
                var originalBlendState = device.BlendState;

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
                device.BlendState = originalBlendState;
            }
            else
            {
                base.DrawMesh(mesh);
            }
        }

        /// <summary>
        /// Determines if the player is inside the building (under the roof).
        /// </summary>
        private bool IsPlayerUnderRoof(WalkableWorldControl walkableWorld)
        {
            Vector2 playerPos = walkableWorld.Walker.Location * ScaleLocation;
            Vector3 playerPos3D = new Vector3(playerPos.X, playerPos.Y, Position.Z);

            Vector3 minOffset = new Vector3(500f, 400f, 1000f);
            Vector3 maxOffset = new Vector3(400f, 500f, 1000f);
            BoundingBox buildingBounds = new BoundingBox(Position - minOffset, Position + maxOffset);

            return buildingBounds.Contains(playerPos3D) == ContainmentType.Contains;
        }
    }
}
