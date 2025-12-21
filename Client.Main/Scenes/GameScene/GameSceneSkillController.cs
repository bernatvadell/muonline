using System;
using System.Collections.Generic;
using Client.Data.ATT;
using Client.Main.Controls;
using Client.Main.Controls.UI.Game.Skills;
using Client.Main.Core.Utilities;
using Client.Main.Objects;
using Client.Main.Objects.Player;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Client.Main.Scenes
{
    internal sealed class GameSceneSkillController
    {
        private readonly GameScene _scene;
        private readonly SkillQuickSlot _skillQuickSlot;
        private readonly ILogger _logger;
        private readonly Func<PlayerObject, bool> _isDuelAttackTarget;

        private Core.Client.SkillEntryState _pendingSkill;
        private ushort _pendingSkillTargetId;
        private uint _pendingSkillRange;
        private bool _pendingSkillIsArea;
        private bool _pendingSkillTargetIsPlayer;
        private readonly Dictionary<ushort, double> _nextSkillAllowedMs = new();

        public GameSceneSkillController(
            GameScene scene,
            SkillQuickSlot skillQuickSlot,
            ILogger logger,
            Func<PlayerObject, bool> isDuelAttackTarget)
        {
            _scene = scene ?? throw new ArgumentNullException(nameof(scene));
            _skillQuickSlot = skillQuickSlot ?? throw new ArgumentNullException(nameof(skillQuickSlot));
            _logger = logger;
            _isDuelAttackTarget = isDuelAttackTarget ?? (_ => false);
        }

        public void Update()
        {
            UpdatePendingSkill();
        }

        public void ClearPending()
        {
            ClearPendingSkill();
        }

        public void HandleRightClickSkillUsage()
        {
            if (_scene.IsMouseInputConsumedThisFrame)
                return;

            if (IsMouseOverUi())
                return;

            var mouse = MuGame.Instance.Mouse;

            // Allow continuous skill usage while holding right mouse button
            // The cooldown system (TryConsumeSkillDelay) will rate-limit the casting
            if (mouse.RightButton != ButtonState.Pressed)
                return;

            var skill = _skillQuickSlot.SelectedSkill;
            if (skill == null)
                return;

            var hero = _scene.Hero;
            if (hero == null || hero.IsDead || _scene.World is not WalkableWorldControl walkableForSkills)
                return;

            // Check if player is in SafeZone
            var terrainFlags = walkableForSkills.Terrain.RequestTerrainFlag((int)hero.Location.X, (int)hero.Location.Y);
            if (terrainFlags.HasFlag(TWFlags.SafeZone))
            {
                _logger?.LogDebug("Cannot use skill in SafeZone");
                _scene.SetMouseInputConsumed();
                return;
            }

            ClearPendingSkill();
            uint allowedRange = SkillDatabase.GetSkillRange(skill.SkillId);

            var hoveredTarget = GetHoveredSkillTarget();
            if (IsAreaSkill(skill.SkillId))
            {
                var skillTarget = hoveredTarget;
                if (skillTarget == null)
                {
                    UseAreaSkill(skill, 0);
                }
                else if (IsInSkillRange(skillTarget.Location, allowedRange))
                {
                    UseAreaSkill(skill, skillTarget.NetworkId);
                }
                else
                {
                    QueueSkillCast(skill, skillTarget, allowedRange, isAreaSkill: true);
                }
            }
            else
            {
                if (hoveredTarget is MonsterObject targetMonster)
                {
                    if (IsInSkillRange(targetMonster.Location, allowedRange))
                        UseSkillOnTarget(skill, targetMonster);
                    else
                        QueueSkillCast(skill, targetMonster, allowedRange, isAreaSkill: false);
                }
                else if (hoveredTarget is PlayerObject targetPlayer)
                {
                    if (IsInSkillRange(targetPlayer.Location, allowedRange))
                        UseSkillOnPlayerTarget(skill, targetPlayer);
                    else
                        QueueSkillCast(skill, targetPlayer, allowedRange, isAreaSkill: false);
                }
            }

            _scene.SetMouseInputConsumed();
        }

        private WalkerObject GetHoveredSkillTarget()
        {
            if (_scene.MouseHoverObject is MonsterObject monster)
            {
                if (!monster.IsDead && monster.World == _scene.World)
                    return monster;
                return null;
            }

            if (_scene.MouseHoverObject is PlayerObject player)
            {
                if (player != _scene.Hero &&
                    !player.IsDead &&
                    player.World == _scene.World &&
                    _isDuelAttackTarget(player))
                {
                    return player;
                }
            }

            return null;
        }

        private bool IsMouseOverUi()
        {
            return _scene.MouseHoverControl != null && _scene.MouseHoverControl != _scene.World;
        }

        private static bool IsAreaSkill(ushort skillId)
        {
            return SkillDatabase.IsAreaSkill(skillId);
        }

        private bool IsInSkillRange(Vector2 targetLocation, uint allowedRange)
        {
            var hero = _scene.Hero;
            if (hero == null)
                return false;

            return allowedRange == 0 || Vector2.Distance(hero.Location, targetLocation) <= allowedRange;
        }

        private void QueueSkillCast(Core.Client.SkillEntryState skill, WalkerObject target, uint allowedRange, bool isAreaSkill)
        {
            var hero = _scene.Hero;
            if (skill == null || target == null || hero == null)
                return;

            _pendingSkill = skill;
            _pendingSkillTargetId = target.NetworkId;
            _pendingSkillRange = allowedRange;
            _pendingSkillIsArea = isAreaSkill;
            _pendingSkillTargetIsPlayer = target is PlayerObject;

            MoveHeroTowardsTarget(target.Location, force: true);
        }

        private void UpdatePendingSkill()
        {
            var hero = _scene.Hero;
            if (_pendingSkill == null || _pendingSkillTargetId == 0 || hero == null || hero.IsDead)
            {
                ClearPendingSkill();
                return;
            }

            if (_skillQuickSlot.SelectedSkill == null || _skillQuickSlot.SelectedSkill.SkillId != _pendingSkill.SkillId)
            {
                ClearPendingSkill();
                return;
            }

            if (_scene.World is not WalkableWorldControl walkableWorld)
            {
                ClearPendingSkill();
                return;
            }

            var terrainFlags = walkableWorld.Terrain.RequestTerrainFlag((int)hero.Location.X, (int)hero.Location.Y);
            if (terrainFlags.HasFlag(TWFlags.SafeZone))
            {
                ClearPendingSkill();
                return;
            }

            if (!walkableWorld.WalkerObjectsById.TryGetValue(_pendingSkillTargetId, out var walker))
            {
                ClearPendingSkill();
                return;
            }

            if (_pendingSkillTargetIsPlayer)
            {
                if (walker is not PlayerObject targetPlayer || targetPlayer.IsDead || !_isDuelAttackTarget(targetPlayer))
                {
                    ClearPendingSkill();
                    return;
                }

                if (IsInSkillRange(targetPlayer.Location, _pendingSkillRange))
                {
                    bool sent = _pendingSkillIsArea
                        ? UseAreaSkill(_pendingSkill, targetPlayer.NetworkId)
                        : UseSkillOnPlayerTarget(_pendingSkill, targetPlayer);
                    if (sent)
                        ClearPendingSkill();
                }
                else
                {
                    MoveHeroTowardsTarget(targetPlayer.Location, force: false);
                }
                return;
            }

            if (walker is not MonsterObject targetMonster || targetMonster.IsDead || targetMonster.World != _scene.World)
            {
                ClearPendingSkill();
                return;
            }

            if (IsInSkillRange(targetMonster.Location, _pendingSkillRange))
            {
                bool sent = _pendingSkillIsArea
                    ? UseAreaSkill(_pendingSkill, targetMonster.NetworkId)
                    : UseSkillOnTarget(_pendingSkill, targetMonster);
                if (sent)
                    ClearPendingSkill();
            }
            else
            {
                MoveHeroTowardsTarget(targetMonster.Location, force: false);
            }
        }

        private void ClearPendingSkill()
        {
            _pendingSkill = null;
            _pendingSkillTargetId = 0;
            _pendingSkillRange = 0;
            _pendingSkillIsArea = false;
            _pendingSkillTargetIsPlayer = false;
        }

        private void MoveHeroTowardsTarget(Vector2 targetLocation, bool force)
        {
            var hero = _scene.Hero;
            if (hero == null)
                return;

            if (!force && (hero.IsMoving || hero.MovementIntent))
                return;

            bool usePathfinding = !hero.IsAttackOrSkillAnimationPlaying();
            hero.MoveTo(targetLocation, sendToServer: true, usePathfinding: usePathfinding);
        }

        private bool UseSkillOnTarget(Core.Client.SkillEntryState skill, MonsterObject target)
        {
            var hero = _scene.Hero;
            if (skill == null || target == null || hero == null)
                return false;

            if (hero.IsDead)
                return false;

            if (!TryBeginSkillCast(skill, hero))
                return false;

            _logger?.LogInformation("Using targeted skill {SkillId} (Level {Level}) on target {TargetId}",
                skill.SkillId, skill.SkillLevel, target.NetworkId);

            _ = MuGame.Network.GetCharacterService().SendSkillRequestAsync(
                skill.SkillId,
                target.NetworkId);

            return true;
        }

        private bool UseSkillOnPlayerTarget(Core.Client.SkillEntryState skill, PlayerObject target)
        {
            var hero = _scene.Hero;
            if (skill == null || target == null || hero == null)
                return false;

            if (hero.IsDead || target.IsDead)
                return false;

            if (!_isDuelAttackTarget(target))
                return false;

            if (!TryBeginSkillCast(skill, hero))
                return false;

            _logger?.LogInformation("Using targeted skill {SkillId} (Level {Level}) on duel target player {TargetId}",
                skill.SkillId, skill.SkillLevel, target.NetworkId);

            _ = MuGame.Network.GetCharacterService().SendSkillRequestAsync(
                skill.SkillId,
                target.NetworkId);

            return true;
        }

        private bool UseAreaSkill(Core.Client.SkillEntryState skill, ushort extraTargetId = 0)
        {
            var hero = _scene.Hero;
            if (skill == null || hero == null)
                return false;

            if (hero.IsDead)
                return false;

            if (!TryBeginSkillCast(skill, hero))
                return false;

            if (extraTargetId != 0)
            {
                _logger?.LogInformation("Using skill {SkillId} (Level {Level}) at position ({X},{Y}) with target {TargetId}",
                    skill.SkillId, skill.SkillLevel, (byte)hero.Location.X, (byte)hero.Location.Y, extraTargetId);
            }
            else
            {
                _logger?.LogInformation("Using area skill {SkillId} (Level {Level}) at position ({X},{Y})",
                    skill.SkillId, skill.SkillLevel, (byte)hero.Location.X, (byte)hero.Location.Y);
            }

            byte targetX = (byte)hero.Location.X;
            byte targetY = (byte)hero.Location.Y;
            byte rotation = (byte)((hero.Angle.Y / (2 * Math.PI)) * 255);

            _ = MuGame.Network.GetCharacterService().SendAreaSkillRequestAsync(
                skill.SkillId,
                targetX,
                targetY,
                rotation,
                extraTargetId);

            return true;
        }

        private bool TryBeginSkillCast(Core.Client.SkillEntryState skill, PlayerObject hero)
        {
            if (hero.IsAttackOrSkillAnimationPlaying())
                return false;

            if (!TryConsumeSkillDelay(skill.SkillId))
                return false;

            bool isInSafeZone = false;
            if (_scene.World is WalkableWorldControl walkableWorld)
            {
                var flags = walkableWorld.Terrain.RequestTerrainFlag((int)hero.Location.X, (int)hero.Location.Y);
                isInSafeZone = flags.HasFlag(TWFlags.SafeZone);
            }

            var action = hero.GetSkillAction(skill.SkillId, isInSafeZone);
            hero.PlayAction((ushort)action);
            hero.TriggerVehicleSkillAnimation();
            return true;
        }

        private bool TryConsumeSkillDelay(ushort skillId)
        {
            int delayMs = SkillDatabase.GetSkillCooldown(skillId);
            if (delayMs <= 0)
                return true;

            double now = GetNowMs();
            if (_nextSkillAllowedMs.TryGetValue(skillId, out double nextAllowed) && now < nextAllowed)
                return false;

            _nextSkillAllowedMs[skillId] = now + delayMs;
            return true;
        }

        private static double GetNowMs()
        {
            var gameTime = MuGame.Instance?.GameTime;
            if (gameTime != null)
                return gameTime.TotalGameTime.TotalMilliseconds;

            return Environment.TickCount64;
        }
    }
}
