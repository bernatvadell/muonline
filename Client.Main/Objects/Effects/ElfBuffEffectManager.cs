using System.Collections.Generic;
using Client.Main;
using Client.Main.Controls;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;

namespace Client.Main.Objects.Effects
{
    /// <summary>
    /// Manages hand-attached mist emitters for the Elf Soldier NPC buff.
    /// </summary>
    public class ElfBuffEffectManager
    {
        public static ElfBuffEffectManager Instance { get; private set; }

        private readonly Dictionary<ushort, (ElfBuffMistEmitter Left, ElfBuffMistEmitter Right)> _emitters = new();
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

            if (_emitters.TryGetValue(playerId, out var existing))
            {
                if (IsAlive(existing.Left) && IsAlive(existing.Right))
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

            world.Objects.Add(left);
            world.Objects.Add(right);
            _ = left.Load();
            _ = right.Load();

            _emitters[playerId] = (left, right);
        }

        public void EnsureBuffsForPlayer(ushort playerId)
        {
            if (_activePlayers.Contains(playerId))
                Attach(playerId);
        }

        private void Detach(ushort playerId)
        {
            if (!_emitters.TryGetValue(playerId, out var emitters))
                return;

            _emitters.Remove(playerId);

            RemoveEmitter(emitters.Left);
            RemoveEmitter(emitters.Right);
        }

        private ElfBuffMistEmitter CreateEmitter(PlayerObject target, int boneIndex, Vector3 offset)
            => new ElfBuffMistEmitter(target, boneIndex, offset);

        private static void RemoveEmitter(ElfBuffMistEmitter emitter)
        {
            if (emitter == null)
                return;

            if (emitter.Parent != null)
            {
                emitter.Parent.Children.Remove(emitter);
                return;
            }

            if (emitter.World != null)
            {
                emitter.World.Objects.Remove(emitter);
                return;
            }

            emitter.Dispose();
        }

        private static bool IsAlive(ElfBuffMistEmitter emitter) =>
            emitter != null && emitter.Status != GameControlStatus.Disposed;
    }
}
