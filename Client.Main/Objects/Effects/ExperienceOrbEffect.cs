using System;
using System.Threading.Tasks;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Small glowing orb that homes toward a target and leaves a weapon-style trail.
    /// Intended to visualize experience pickup after killing a monster.
    /// </summary>
    public class ExperienceOrbEffect : SpriteObject
    {
        private const float MaxLifetime = 3.5f;
        private const float CatchDistance = 45f;

        private readonly Func<Vector3> _targetProvider;
        private readonly WeaponTrailEffect _trail;
        private readonly float _hoverPhase;
        private readonly float _swirlStrength;
        private readonly float _sideDrift;
        private readonly Vector3 _sideAxis;
        private readonly float _speedJitter;
        private Vector3 _velocity;
        private float _age;

        public override string TexturePath => "Effect/Shiny05.jpg";

        public ExperienceOrbEffect(Vector3 startPosition, Func<Vector3> targetProvider)
        {
            Position = startPosition;
            _targetProvider = targetProvider ?? throw new ArgumentNullException(nameof(targetProvider));

            BlendState = BlendState.Additive;
            DepthState = DepthStencilState.DepthRead;
            IsTransparent = true;
            AffectedByTransparency = true;
            LightEnabled = false;
            BoundingBoxLocal = new BoundingBox(new Vector3(-22f, -22f, -22f), new Vector3(22f, 22f, 22f));

            float randomScale = MathHelper.Lerp(1.05f, 1.35f, (float)MuGame.Random.NextDouble());
            Scale = randomScale;
            Alpha = 1f;

            var initialTarget = _targetProvider.Invoke();
            Vector3 toward = initialTarget - startPosition;
            if (toward.LengthSquared() < 0.001f)
                toward = Vector3.UnitZ;
            toward.Normalize();

            Vector3 side = Vector3.Cross(toward, Vector3.UnitZ);
            if (side.LengthSquared() < 0.0001f)
                side = Vector3.UnitX;
            side.Normalize();

            float sideSign = MuGame.Random.Next(2) == 0 ? -1f : 1f;
            float sideKick = MathHelper.Lerp(120f, 260f, (float)MuGame.Random.NextDouble()) * sideSign;
            float forwardKick = MathHelper.Lerp(120f, 260f, (float)MuGame.Random.NextDouble());
            float upKick = MathHelper.Lerp(180f, 280f, (float)MuGame.Random.NextDouble());

            _velocity = toward * forwardKick + side * sideKick + Vector3.UnitZ * upKick;

            _hoverPhase = (float)MuGame.Random.NextDouble() * MathHelper.TwoPi;
            _swirlStrength = MathHelper.Lerp(26f, 52f, (float)MuGame.Random.NextDouble());
            _sideDrift = MathHelper.Lerp(65f, 140f, (float)MuGame.Random.NextDouble()) * sideSign;
            _sideAxis = side;
            _speedJitter = MathHelper.Lerp(0.05f, 0.35f, (float)MuGame.Random.NextDouble());

            _trail = new WeaponTrailEffect
            {
                Alpha = 0.9f
            };
            _trail.SamplePoint = () => WorldPosition.Translation;
            _trail.SetTrailColor(new Color(1f, 0.85f, 0.35f));
        }

        public override async Task Load()
        {
            await base.Load();
            Children.Add(_trail);
            await _trail.Load();
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            if (Status != GameControlStatus.Ready)
                return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _age += dt;

            var target = _targetProvider?.Invoke() ?? WorldPosition.Translation;
            Vector3 toTarget = target - WorldPosition.Translation;
            float distance = toTarget.Length();

            if (distance <= CatchDistance || _age >= MaxLifetime)
            {
                Despawn();
                return;
            }

            Vector3 dir = distance > 0.001f ? toTarget / distance : Vector3.Zero;

            float chaseSpeed = MathHelper.Lerp(220f, 640f, MathHelper.Clamp(_age / 1.2f, 0f, 1f)) * (1f + _speedJitter);
            float accel = MathHelper.Lerp(320f, 740f, MathHelper.Clamp(1f - distance / 1200f, 0f, 1f));

            _velocity = Vector3.Lerp(_velocity, dir * chaseSpeed, dt * 4f);
            _velocity += dir * accel * dt;
            _velocity += _sideAxis * (_sideDrift * dt);

            float swirl = (float)Math.Sin((_age + _hoverPhase) * 8f) * _swirlStrength;
            Vector3 sideways = Vector3.Cross(dir, Vector3.UnitZ);
            if (sideways.LengthSquared() < 0.001f)
            {
                sideways = Vector3.UnitX;
            }
            else
            {
                sideways.Normalize();
            }
            _velocity += sideways * swirl * dt * 0.35f;
            _velocity.Z += (float)Math.Sin((_age + _hoverPhase) * 6f) * 28f * dt;

            float maxSpeed = MathHelper.Lerp(260f, 720f, MathHelper.Clamp(1f - distance / 1400f, 0f, 1f)) * (1f + _speedJitter);
            float velSq = _velocity.LengthSquared();
            if (velSq > maxSpeed * maxSpeed && velSq > 0.0001f)
            {
                _velocity = Vector3.Normalize(_velocity) * maxSpeed;
            }

            Position += _velocity * dt;

            Alpha = MathHelper.Clamp(1f - (_age / MaxLifetime), 0.25f, 1f);
        }

        private void Despawn()
        {
            if (World != null)
            {
                World.RemoveObject(this);
            }
            else
            {
                Dispose();
            }
        }

        public static void SpawnBurst(WorldControl world, Vector3 origin, Func<Vector3> targetProvider, int count)
        {
            if (world == null || targetProvider == null || count <= 0)
                return;

            count = Math.Clamp(count, 1, 10);

            for (int i = 0; i < count; i++)
            {
                Vector3 jitter = new Vector3(
                    MathHelper.Lerp(-35f, 35f, (float)MuGame.Random.NextDouble()),
                    MathHelper.Lerp(-35f, 35f, (float)MuGame.Random.NextDouble()),
                    MathHelper.Lerp(10f, 40f, (float)MuGame.Random.NextDouble()));

                var orb = new ExperienceOrbEffect(origin + jitter, targetProvider);
                world.Objects.Add(orb);
                _ = orb.Load();
            }
        }
    }
}
