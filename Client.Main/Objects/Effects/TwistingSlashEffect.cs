#nullable enable
using System;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controls;
using Client.Main.Controllers;
using Client.Main.Graphics;
using Client.Main.Helpers;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Twisting Slash visual effect: rotating slash arcs, sparks, and dynamic lighting.
    /// Inspired by original MU wheel skill visuals.
    /// </summary>
    public sealed class TwistingSlashEffect : WorldObject
    {
        private const string SlashTexturePath = "Effect/flare01.jpg";
        private const string GlowTexturePath = "Effect/flare.jpg";
        private const string CoreTexturePath = "Effect/Shiny05.jpg";
        private const string SparkTexturePath = "Effect/Spark03.jpg";

        private const int MaxSparks = 80;
        private const float SparkSpawnInterval = 0.008f;
        private const int BurstSparkCount = 24;
        private const float SparkScaleBoost = 1.9f;
        private const float WeaponShadowInnerRadiusScale = 0.78f;
        private const float WeaponShadowSpinSpeed = 18f;
        private const float WeaponShadowBaseAlpha = 0.35f;
        private static readonly float WeaponShadowTilt = MathHelper.PiOver2;

        private readonly WalkerObject _caster;
        private readonly float _duration;
        private float _remaining;
        private float _time;
        private float _spin;
        private float _sparkTimer;

        private Vector3 _center;
        private Vector3 _orbitPosition;
        private float _orbitRadius = 150f;
        private float _orbitHeight = 55f;
        private float _spinSpeed = 14f;

        private SpriteBatch _spriteBatch = null!;
        private Texture2D _slashTexture = null!;
        private Texture2D _glowTexture = null!;
        private Texture2D _coreTexture = null!;
        private Texture2D _sparkTexture = null!;

        private readonly SparkParticle[] _sparks = new SparkParticle[MaxSparks];
        private readonly DynamicLight _centerLight;
        private readonly DynamicLight _orbitLight;
        private bool _lightsAdded;
        private bool _weaponShadowInitialized;

        private readonly TwistingSlashWeaponShadow? _weaponShadow;
        private readonly string? _weaponModelPath;
        private readonly int _weaponItemLevel;
        private readonly bool _weaponExcellent;
        private readonly bool _weaponAncient;

        private readonly Color _slashColor = new Color(1f, 0.62f, 0.32f, 1f);
        private readonly Color _glowColor = new Color(1f, 0.4f, 0.2f, 0.85f);
        private readonly Color _coreColor = new Color(1f, 0.85f, 0.45f, 1f);
        private readonly Color _sparkColor = new Color(1f, 0.85f, 0.25f, 1f);
        private readonly Color _sparkHotColor = new Color(1f, 0.25f, 0.05f, 1f);

        private struct SparkParticle
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Life;
            public float MaxLife;
            public float Rotation;
            public float Scale;
        }

        public TwistingSlashEffect(WalkerObject caster, float durationSeconds = 0.65f)
        {
            _caster = caster ?? throw new ArgumentNullException(nameof(caster));
            _duration = MathHelper.Clamp(durationSeconds, 0.2f, 1.5f);
            _remaining = _duration;

            IsTransparent = true;
            AffectedByTransparency = true;
            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;

            BoundingBoxLocal = new BoundingBox(
                new Vector3(-220f, -220f, -40f),
                new Vector3(220f, 220f, 220f));

            _centerLight = new DynamicLight
            {
                Owner = this,
                Color = new Vector3(1.0f, 0.6f, 0.25f),
                Radius = 260f,
                Intensity = 1.25f
            };

            _orbitLight = new DynamicLight
            {
                Owner = this,
                Color = new Vector3(1.0f, 0.7f, 0.35f),
                Radius = 180f,
                Intensity = 1.4f
            };

            for (int i = 0; i < MaxSparks; i++)
            {
                _sparks[i].Life = 0f;
            }

            for (int i = 0; i < BurstSparkCount; i++)
            {
                SpawnSpark();
            }

            if (_caster is PlayerObject player)
            {
                var weapon = GetWeaponSource(player);
                if (weapon != null && (!string.IsNullOrEmpty(weapon.TexturePath) || weapon.Model != null))
                {
                    _weaponModelPath = weapon.TexturePath;
                    _weaponItemLevel = weapon.ItemLevel;
                    _weaponExcellent = weapon.IsExcellentItem;
                    _weaponAncient = weapon.IsAncientItem;

                    _weaponShadow = new TwistingSlashWeaponShadow();
                    Children.Add(_weaponShadow);
                }
            }
        }

        public override async Task LoadContent()
        {
            await base.LoadContent();

            _ = await TextureLoader.Instance.Prepare(SlashTexturePath);
            _ = await TextureLoader.Instance.Prepare(GlowTexturePath);
            _ = await TextureLoader.Instance.Prepare(CoreTexturePath);
            _ = await TextureLoader.Instance.Prepare(SparkTexturePath);

            _spriteBatch = GraphicsManager.Instance.Sprite;
            _slashTexture = TextureLoader.Instance.GetTexture2D(SlashTexturePath) ?? GraphicsManager.Instance.Pixel;
            _glowTexture = TextureLoader.Instance.GetTexture2D(GlowTexturePath) ?? GraphicsManager.Instance.Pixel;
            _coreTexture = TextureLoader.Instance.GetTexture2D(CoreTexturePath) ?? GraphicsManager.Instance.Pixel;
            _sparkTexture = TextureLoader.Instance.GetTexture2D(SparkTexturePath) ?? GraphicsManager.Instance.Pixel;

            if (_weaponShadow != null && !_weaponShadowInitialized)
            {
                _weaponShadowInitialized = true;

                if (!string.IsNullOrEmpty(_weaponModelPath))
                {
                    _weaponShadow.Model = await BMDLoader.Instance.Prepare(_weaponModelPath);
                    _weaponShadow.ItemLevel = _weaponItemLevel;
                    _weaponShadow.IsExcellentItem = _weaponExcellent;
                    _weaponShadow.IsAncientItem = _weaponAncient;
                }
                else
                {
                    _weaponShadow.Hidden = true;
                }
            }

            if (World?.Terrain != null && !_lightsAdded)
            {
                World.Terrain.AddDynamicLight(_centerLight);
                World.Terrain.AddDynamicLight(_orbitLight);
                _lightsAdded = true;
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (Status != GameControlStatus.Ready)
                return;

            ForceInView();

            if (_caster == null || _caster.Status == GameControlStatus.Disposed || _caster.World == null)
            {
                RemoveSelf();
                return;
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _remaining -= dt;
            _time += dt;

            if (_remaining <= 0f)
            {
                RemoveSelf();
                return;
            }

            _center = _caster.WorldPosition.Translation + new Vector3(0f, 0f, _orbitHeight);

            float facing = _caster.Angle.Z;
            _spin -= dt * _spinSpeed;

            float orbitAngle = facing + _spin;
            float orbitBob = MathF.Sin(_time * 6.0f) * 12f;
            _orbitPosition = _center + new Vector3(MathF.Cos(orbitAngle) * _orbitRadius, MathF.Sin(orbitAngle) * _orbitRadius, orbitBob);

            UpdateWeaponShadow(orbitAngle);
            UpdateSparks(dt);
            UpdateDynamicLights();

            Position = _center;
        }

        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime);

            if (!Visible || _spriteBatch == null || _slashTexture == null)
                return;

            float lifeAlpha = MathHelper.Clamp(_remaining / _duration, 0f, 1f);

            if (!SpriteBatchScope.BatchIsBegun)
            {
                using (new SpriteBatchScope(_spriteBatch, SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthState))
                {
                    DrawLayers(lifeAlpha);
                }
            }
            else
            {
                DrawLayers(lifeAlpha);
            }
        }

        private void DrawLayers(float lifeAlpha)
        {
            float pulse = 0.85f + 0.15f * MathF.Sin(_time * 10f);

            float coreScale = MathHelper.Lerp(0.9f, 1.35f, pulse);
            DrawSprite(_coreTexture, _center, _coreColor * (lifeAlpha * pulse), _time * 2.5f, new Vector2(coreScale, coreScale));

            float glowScale = MathHelper.Lerp(1.2f, 2.0f, 1f - lifeAlpha);
            DrawSprite(_glowTexture, _center, _glowColor * (lifeAlpha * 0.6f), _time * 1.2f, new Vector2(glowScale, glowScale));

            const int arcCount = 3;
            float facing = _caster.Angle.Z;
            for (int i = 0; i < arcCount; i++)
            {
                float arcAngle = facing + _spin + i * (MathHelper.TwoPi / arcCount);
                float radius = _orbitRadius * (0.92f + 0.08f * MathF.Sin(_time * 6f + i));
                float bob = MathF.Sin(_time * 8f + i) * 12f;
                Vector3 pos = _center + new Vector3(MathF.Cos(arcAngle) * radius, MathF.Sin(arcAngle) * radius, bob);
                float rotation = arcAngle + MathHelper.PiOver2;
                float arcAlpha = lifeAlpha * (0.8f - i * 0.15f);
                Vector2 arcScale = new Vector2(2.2f, 0.55f);
                DrawSprite(_slashTexture, pos, _slashColor * arcAlpha, rotation, arcScale);
            }

            DrawSprite(_glowTexture, _orbitPosition, _glowColor * (lifeAlpha * 0.75f), _time * 3.5f, new Vector2(0.7f, 0.7f));

            for (int i = 0; i < MaxSparks; i++)
            {
                if (_sparks[i].Life <= 0f)
                    continue;

                float t = _sparks[i].Life / _sparks[i].MaxLife;
                float alpha = MathHelper.Clamp(t * 1.25f, 0f, 1f);
                Vector2 sparkScale = new Vector2(_sparks[i].Scale, _sparks[i].Scale)
                    * MathHelper.Lerp(0.6f, 1.15f, t)
                    * SparkScaleBoost;
                Color sparkColor = Color.Lerp(_sparkHotColor, _sparkColor, t);
                DrawSprite(_sparkTexture, _sparks[i].Position, sparkColor * (alpha * lifeAlpha), _sparks[i].Rotation, sparkScale);
            }
        }

        private void UpdateSparks(float dt)
        {
            _sparkTimer += dt;
            while (_sparkTimer >= SparkSpawnInterval)
            {
                _sparkTimer -= SparkSpawnInterval;
                SpawnSpark();
            }

            int extraSpawns = (int)MathHelper.Clamp(dt * 120f, 0f, 5f);
            for (int i = 0; i < extraSpawns; i++)
            {
                SpawnSpark();
            }

            for (int i = 0; i < MaxSparks; i++)
            {
                if (_sparks[i].Life <= 0f)
                    continue;

                _sparks[i].Life -= dt;
                if (_sparks[i].Life <= 0f)
                    continue;

                _sparks[i].Position += _sparks[i].Velocity * dt;
                _sparks[i].Velocity.Z -= 160f * dt;
                _sparks[i].Velocity *= 1f - MathHelper.Clamp(dt * 1.1f, 0f, 0.2f);
                _sparks[i].Rotation += dt * 6f;
            }
        }

        private void SpawnSpark()
        {
            int index = -1;
            for (int i = 0; i < MaxSparks; i++)
            {
                if (_sparks[i].Life <= 0f)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
                return;

            float randA = (float)MuGame.Random.NextDouble();
            float randB = (float)MuGame.Random.NextDouble();
            float randC = (float)MuGame.Random.NextDouble();

            Vector3 outward = _orbitPosition - _center;
            if (outward.LengthSquared() < 0.001f)
                outward = Vector3.UnitX;
            else
                outward.Normalize();

            Vector3 tangent = new Vector3(-outward.Y, outward.X, 0f);
            float tangentScale = MathHelper.Lerp(-0.6f, 0.6f, randA);
            Vector3 dir = outward + tangent * tangentScale + Vector3.UnitZ * MathHelper.Lerp(0.15f, 0.45f, randB);
            if (dir.LengthSquared() < 0.001f)
                dir = Vector3.UnitX;
            dir.Normalize();

            float speed = MathHelper.Lerp(220f, 420f, randC);
            Vector3 velocity = dir * speed;
            velocity.Z += MathHelper.Lerp(80f, 220f, (float)MuGame.Random.NextDouble());

            Vector3 jitter = new Vector3(
                MathHelper.Lerp(-8f, 8f, (float)MuGame.Random.NextDouble()),
                MathHelper.Lerp(-8f, 8f, (float)MuGame.Random.NextDouble()),
                MathHelper.Lerp(-4f, 12f, (float)MuGame.Random.NextDouble()));

            float life = MathHelper.Lerp(0.45f, 0.85f, (float)MuGame.Random.NextDouble());
            _sparks[index] = new SparkParticle
            {
                Position = _orbitPosition + jitter,
                Velocity = velocity,
                Life = life,
                MaxLife = life,
                Rotation = MathHelper.Lerp(0f, MathHelper.TwoPi, (float)MuGame.Random.NextDouble()),
                Scale = MathHelper.Lerp(1.1f, 2.0f, (float)MuGame.Random.NextDouble())
            };
        }

        private void UpdateDynamicLights()
        {
            if (World?.Terrain == null)
                return;

            float lifeAlpha = MathHelper.Clamp(_remaining / _duration, 0f, 1f);
            float pulse = 0.7f + 0.3f * MathF.Sin(_time * 12f);

            _centerLight.Position = _center;
            _centerLight.Intensity = 1.2f * lifeAlpha * pulse;
            _centerLight.Radius = MathHelper.Lerp(260f, 180f, 1f - lifeAlpha);

            _orbitLight.Position = _orbitPosition;
            _orbitLight.Intensity = 1.4f * lifeAlpha * (0.8f + 0.2f * MathF.Sin(_time * 18f));
            _orbitLight.Radius = 160f + 20f * MathF.Sin(_time * 6f);
        }

        private void UpdateWeaponShadow(float orbitAngle)
        {
            if (_weaponShadow == null)
                return;

            float lifeAlpha = MathHelper.Clamp(_remaining / _duration, 0f, 1f);
            _weaponShadow.Alpha = WeaponShadowBaseAlpha * lifeAlpha;

            Vector3 localOffset = (_orbitPosition - _center) * WeaponShadowInnerRadiusScale;
            _weaponShadow.Position = localOffset;

            float wobble = MathF.Sin(_time * 6.0f) * 0.08f;
            float selfSpin = _time * WeaponShadowSpinSpeed;
            _weaponShadow.Angle = new Vector3(selfSpin, WeaponShadowTilt + wobble, orbitAngle + MathHelper.PiOver2);
        }

        private void DrawSprite(Texture2D texture, Vector3 worldPos, Color color, float rotation, Vector2 scale)
        {
            if (texture == null)
                return;

            var viewport = GraphicsDevice.Viewport;
            Vector3 projected = viewport.Project(worldPos, Camera.Instance.Projection, Camera.Instance.View, Matrix.Identity);
            if (projected.Z < 0f || projected.Z > 1f)
                return;

            float baseScale = ComputeScreenScale(worldPos, 1f);
            Vector2 finalScale = scale * baseScale;
            float depth = MathHelper.Clamp(projected.Z, 0f, 1f);

            _spriteBatch.Draw(
                texture,
                new Vector2(projected.X, projected.Y),
                null,
                color,
                rotation,
                new Vector2(texture.Width * 0.5f, texture.Height * 0.5f),
                finalScale,
                SpriteEffects.None,
                depth);
        }

        private static float ComputeScreenScale(Vector3 worldPos, float baseScale)
        {
            float distance = Vector3.Distance(Camera.Instance.Position, worldPos);
            float scale = baseScale / (MathF.Max(distance, 0.1f) / Constants.TERRAIN_SIZE);
            return scale * Constants.RENDER_SCALE;
        }

        private static WeaponObject? GetWeaponSource(PlayerObject player)
        {
            var right = player.Weapon2;
            if (right != null && (right.Model != null || !string.IsNullOrEmpty(right.TexturePath)))
                return right;

            var left = player.Weapon1;
            if (left != null && (left.Model != null || !string.IsNullOrEmpty(left.TexturePath)))
                return left;

            return null;
        }

        private void RemoveSelf()
        {
            if (Parent != null)
                Parent.Children.Remove(this);
            else
                World?.RemoveObject(this);

            Dispose();
        }

        public override void Dispose()
        {
            if (World?.Terrain != null)
            {
                World.Terrain.RemoveDynamicLight(_centerLight);
                World.Terrain.RemoveDynamicLight(_orbitLight);
            }

            base.Dispose();
        }
    }
}
