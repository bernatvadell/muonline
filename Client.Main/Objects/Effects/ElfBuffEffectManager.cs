using System.Collections.Generic;
using Client.Main;
using Client.Main.Controls;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Client.Main.Scenes;
using Client.Main.Objects;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Manages hand-attached mist emitters and orbiting light trails for the Elf Soldier NPC buff.
    /// </summary>
    public class ElfBuffEffectManager
    {
        public static ElfBuffEffectManager Instance { get; private set; }

        private sealed class BuffVisualSet
        {
            public ElfBuffMistEmitter Left { get; init; }
            public ElfBuffMistEmitter Right { get; init; }
            public List<ElfBuffOrbitingLight> Orbits { get; init; } = new();
        }

        private readonly Dictionary<ushort, BuffVisualSet> _visuals = new();
        private readonly HashSet<ushort> _activePlayers = new();
        
        public ElfBuffEffectManager() => Instance = this;

        public void HandleBuff(byte effectId, ushort playerId, bool isActive)
        {
            if (effectId != 3)
                return;

            ushort maskedId = (ushort)(playerId & 0x7FFF);

            MuGame.ScheduleOnMainThread(() =>
            {
                if (isActive)
                {
                    _activePlayers.Add(maskedId);
                    Attach(maskedId);
                }
                else
                {
                    _activePlayers.Remove(maskedId);
                    Detach(maskedId);
                }
            });
        }

        private void Attach(ushort playerId)
        {
            if (!_activePlayers.Contains(playerId))
                return;

            if (_visuals.TryGetValue(playerId, out var existing))
            {
                if (IsAlive(existing.Left) && IsAlive(existing.Right) && AreAlive(existing.Orbits))
                    return;

                Detach(playerId);
            }

            if (MuGame.Instance?.ActiveScene is not GameScene gameScene)
                return;

            if (gameScene.World is not WalkableWorldControl world || world.Status != GameControlStatus.Ready)
                return;

            PlayerObject target = world.FindPlayerById(playerId);
            if (target == null && gameScene.Hero != null && gameScene.Hero.NetworkId == playerId)
            {
                target = gameScene.Hero;
            }

            if (target == null || target.Status != GameControlStatus.Ready)
                return;

            var left = CreateEmitter(target, PlayerObject.LeftHandBoneIndex, new Vector3(-6f, 0f, 16f));
            var right = CreateEmitter(target, PlayerObject.RightHandBoneIndex, new Vector3(6f, 0f, 16f));
            List<ElfBuffOrbitingLight> orbits = CreateOrbits(target);

            world.Objects.Add(left);
            world.Objects.Add(right);
            for (int i = 0; i < orbits.Count; i++)
                world.Objects.Add(orbits[i]);

            _ = left.Load();
            _ = right.Load();
            for (int i = 0; i < orbits.Count; i++)
                _ = orbits[i].Load();

            _visuals[playerId] = new BuffVisualSet
            {
                Left = left,
                Right = right,
                Orbits = orbits
            };
        }

        public void EnsureBuffsForPlayer(ushort playerId)
        {
            if (_activePlayers.Contains(playerId))
                Attach(playerId);
        }

        private void Detach(ushort playerId)
        {
            if (!_visuals.TryGetValue(playerId, out var visuals))
                return;

            _visuals.Remove(playerId);

            RemoveObject(visuals.Left);
            RemoveObject(visuals.Right);
            if (visuals.Orbits != null)
            {
                for (int i = 0; i < visuals.Orbits.Count; i++)
                    RemoveObject(visuals.Orbits[i]);
            }
        }

        private ElfBuffMistEmitter CreateEmitter(PlayerObject target, int boneIndex, Vector3 offset)
            => new ElfBuffMistEmitter(target, boneIndex, offset);

        private List<ElfBuffOrbitingLight> CreateOrbits(PlayerObject target)
        {
            float scale = MathHelper.Clamp(target?.TotalScale ?? 1f, 0.6f, 1.4f);

            // Create two layers of orbiting lights for richer visual effect
            // Orbits encompass the entire player model
            // Inner layer: 3 orbs at mid height
            // Outer layer: 3 orbs at varied heights
            const int innerCount = 3;
            const int outerCount = 3;
            int totalCount = innerCount + outerCount;

            // Larger radius to encompass entire player
            float innerRadius = 95f * scale;
            float outerRadius = 130f * scale;
            float innerHeight = 70f * scale;
            float outerHeight = 95f * scale;

            var list = new List<ElfBuffOrbitingLight>(totalCount);

            // Inner layer orbs - mid-body height
            for (int i = 0; i < innerCount; i++)
            {
                float radiusJitter = MathHelper.Lerp(-8f, 12f, (float)MuGame.Random.NextDouble());
                float heightJitter = MathHelper.Lerp(-20f, 25f, (float)MuGame.Random.NextDouble());
                list.Add(new ElfBuffOrbitingLight(
                    target,
                    innerRadius + radiusJitter,
                    innerHeight + heightJitter,
                    i,
                    innerCount));
            }

            // Outer layer orbs - upper body / head height
            for (int i = 0; i < outerCount; i++)
            {
                float radiusJitter = MathHelper.Lerp(-12f, 18f, (float)MuGame.Random.NextDouble());
                float heightJitter = MathHelper.Lerp(-25f, 30f, (float)MuGame.Random.NextDouble());
                list.Add(new ElfBuffOrbitingLight(
                    target,
                    outerRadius + radiusJitter,
                    outerHeight + heightJitter,
                    i,
                    outerCount));
            }

            return list;
        }

        private static void RemoveObject(WorldObject obj)
        {
            if (obj == null)
                return;

            if (obj.Parent != null)
            {
                obj.Parent.Children.Remove(obj);
                return;
            }

            if (obj.World != null)
            {
                obj.World.Objects.Remove(obj);
                return;
            }

            obj.Dispose();
        }

        private static bool IsAlive(WorldObject obj) =>
            obj != null && obj.Status != GameControlStatus.Disposed;

        private static bool AreAlive(List<ElfBuffOrbitingLight> orbits)
        {
            if (orbits == null || orbits.Count == 0)
                return false;

            for (int i = 0; i < orbits.Count; i++)
            {
                if (!IsAlive(orbits[i]))
                    return false;
            }

            return true;
        }
    }
}
