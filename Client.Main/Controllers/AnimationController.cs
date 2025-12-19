using System;
using Client.Main.Models;
using Client.Main.Objects;
using Client.Main.Objects.Player;

namespace Client.Main.Controllers
{
    public enum AnimationType { Idle, Walk, Attack, Skill, Emote, Death, Rest, Sit, Appear }

    public sealed class AnimationController : IDisposable
    {
        private readonly WalkerObject _owner;

        private enum AnimationTimerMode
        {
            None,
            OneShotBackup,
            DeathHold
        }

        private ushort? _currentOneShot;
        private bool _oneShotEnded;
        private bool _serverControlled;
        private volatile bool _forceReturnToIdle;
        private ushort? _returnActionOverride;
        private Action _onOneShotComplete;
        private AnimationTimerMode _timerMode = AnimationTimerMode.None;
        private float _timerRemaining;
        private ushort _timerAction;

        private const float DEATH_HOLD = 3.0f;
        private const float BACKUP_MARGIN = 0.1f;

        public AnimationController(WalkerObject owner) => _owner = owner;

        public bool IsOneShotPlaying => _currentOneShot.HasValue && !_oneShotEnded && !_forceReturnToIdle;

        public void NotifyAnimationCompleted()
        {
            if (_oneShotEnded || !_currentOneShot.HasValue) return;

            // Debug.WriteLine($"[AnimCtrl] notification received → {_currentOneShot}");

            _forceReturnToIdle = true;

            ReturnToIdle();
        }

        public bool TryGetActionDurationSeconds(ushort actionIndex, out float durationSeconds)
        {
            return TryGetDuration(actionIndex, out durationSeconds);
        }

        public bool PlayOneShot(ushort actionIndex, ushort returnActionIndex, Action onCompleted = null)
        {
            var kind = GetAnimationType(actionIndex);

            if (_currentOneShot.HasValue &&
                GetAnimationType(_currentOneShot.Value) == AnimationType.Death &&
                kind != AnimationType.Death)
            {
                return false;
            }

            if (!AllowWhenDead(kind)) return false;

            ClearTimer();
            _oneShotEnded = false;
            _forceReturnToIdle = false;

            _serverControlled = false;
            _currentOneShot = actionIndex;
            _returnActionOverride = returnActionIndex;
            _onOneShotComplete = onCompleted;

            _owner.CurrentAction = actionIndex;
            _owner.InvalidateBuffers();

            if (TryGetDuration(actionIndex, out var real))
            {
                ArmTimer(AnimationTimerMode.OneShotBackup, actionIndex, real + BACKUP_MARGIN);
            }
            else
            {
                ArmTimer(AnimationTimerMode.OneShotBackup, actionIndex, 0.5f);
            }
            return true;
        }

        public void PlayAnimation(ushort idx, bool fromServer = false)
        {
            var kind = GetAnimationType(idx);

            if (_currentOneShot.HasValue &&
                GetAnimationType(_currentOneShot.Value) == AnimationType.Death &&
                kind != AnimationType.Death)
            {
                return;
            }

            if (!AllowWhenDead(kind)) return;

            ClearTimer();
            _oneShotEnded = false;
            _forceReturnToIdle = false;

            _serverControlled = fromServer;
            _owner.CurrentAction = idx;
            _owner.InvalidateBuffers();

            if (IsReturnable(kind))
            {
                _currentOneShot = idx;
                StartBackupTimer(idx);
            }
            else if (kind == AnimationType.Death)
            {
                _currentOneShot = idx;
                StartDeathTimer(idx);
            }
            else
            {
                _currentOneShot = null;
            }
        }

        public void PlayAnimation(ushort idx) => PlayAnimation(idx, false);

        public void Reset()
        {
            // Cancel any running timers
            ClearTimer();

            // Reset all state variables
            _oneShotEnded = false;
            _forceReturnToIdle = false;
            _currentOneShot = null;
            _serverControlled = false;
            _returnActionOverride = null;
            _onOneShotComplete = null;

            // Force return to idle animation
            if (_owner.Status != GameControlStatus.Disposed)
            {
                ushort idle = _owner switch
                {
                    PlayerObject p => p.GetCorrectIdleAction(),
                    _ => (ushort)MonsterActionType.Stop1
                };

                _owner.CurrentAction = idle;
                _owner.InvalidateBuffers();
            }
        }

        private void StartBackupTimer(ushort idx)
        {
            if (!TryGetDuration(idx, out var real)) return;
            float wait = real + BACKUP_MARGIN;

            ArmTimer(AnimationTimerMode.OneShotBackup, idx, wait);
        }

        /* ----------------------------------------------------------------- */
        /*  DEATH – max 3 s                                                  */
        /* ----------------------------------------------------------------- */
        private void StartDeathTimer(ushort idx)
        {
            if (!TryGetDuration(idx, out var anim)) anim = 0;
            float hold = Math.Min(anim + DEATH_HOLD, DEATH_HOLD);

            ArmTimer(AnimationTimerMode.DeathHold, idx, hold);
        }

        /* ----------------------------------------------------------------- */
        /*  IDLE                                                   */
        /* ----------------------------------------------------------------- */
        private void ReturnToIdle()
        {
            // Debug.WriteLine($"[AnimCtrl] ReturnToIdle called");

            _oneShotEnded = true;
            _forceReturnToIdle = true;

            // Anuluj wszelkie timery
            ClearTimer();

            if (_owner.Status == GameControlStatus.Disposed) return;
            if (GetAnimationType((ushort)_owner.CurrentAction) == AnimationType.Death
                && !_owner.IsAlive()) return;

            ushort idle;
            if (_returnActionOverride.HasValue)
            {
                idle = _returnActionOverride.Value;
            }
            else
            {
                idle = _owner switch
                {
                    PlayerObject p => p.GetCorrectIdleAction(),
                    _ => (ushort)MonsterActionType.Stop1
                };
            }

            // Debug.WriteLine($"[AnimCtrl] Setting idle action: {_owner.CurrentAction} → {idle}");
            _owner.CurrentAction = idle;
            _owner.InvalidateBuffers();

            _currentOneShot = null;
            _serverControlled = false;

            var cb = _onOneShotComplete;
            _onOneShotComplete = null;
            _returnActionOverride = null;
            cb?.Invoke();
        }

        private bool TryGetDuration(ushort idx, out float duration)
        {
            duration = 0;
            var a = _owner.Model?.Actions;
            if (a == null || idx >= a.Length) return false;

            var act = a[idx];
            if (act == null) return false; // Handle null actions gracefully

            duration = CalcDuration(act);
            return true;
        }

        public void Update(float deltaSeconds)
        {
            if (_timerMode == AnimationTimerMode.None)
                return;

            if (_forceReturnToIdle)
            {
                ClearTimer();
                return;
            }

            _timerRemaining -= deltaSeconds;
            if (_timerRemaining > 0f)
                return;

            var mode = _timerMode;
            var action = _timerAction;
            ClearTimer();

            switch (mode)
            {
                case AnimationTimerMode.OneShotBackup:
                    if (!_oneShotEnded && _currentOneShot == action && !_forceReturnToIdle)
                    {
                        ReturnToIdle();
                    }
                    break;
                case AnimationTimerMode.DeathHold:
                    if (_owner is PlayerObject pl && pl.IsMainWalker)
                    {
                        var state = MuGame.Network?.GetCharacterState();
                        if (state?.CurrentHealth > 0)
                        {
                            ReturnToIdle();
                        }
                    }
                    break;
            }
        }

        private void ArmTimer(AnimationTimerMode mode, ushort actionIdx, float duration)
        {
            _timerMode = mode;
            _timerAction = actionIdx;
            _timerRemaining = Math.Max(0.01f, duration);
        }

        private void ClearTimer()
        {
            _timerMode = AnimationTimerMode.None;
            _timerRemaining = 0f;
            _timerAction = 0;
        }

        private static bool IsReturnable(AnimationType t)
            => t is AnimationType.Attack or AnimationType.Skill or AnimationType.Emote or AnimationType.Appear;

        private bool AllowWhenDead(AnimationType t)
        {
            if (_owner is not PlayerObject p || !p.IsMainWalker) return true;

            var s = MuGame.Network?.GetCharacterState();
            return s?.CurrentHealth > 0 || t == AnimationType.Death;
        }

        public AnimationType GetAnimationType(ushort actionIndex)
        {
            if (_owner is PlayerObject)
                return GetPlayerAnimationType((PlayerAction)actionIndex);
            else
                return GetMonsterAnimationType((MonsterActionType)actionIndex);
        }

        private static AnimationType GetPlayerAnimationType(PlayerAction a) => a switch
        {
            PlayerAction.PlayerDie1 or PlayerAction.PlayerDie2 => AnimationType.Death,
            PlayerAction.PlayerPoseMale1 or PlayerAction.PlayerPoseFemale1 => AnimationType.Rest,
            PlayerAction.PlayerSit1 or PlayerAction.PlayerSitFemale1 => AnimationType.Sit,
            PlayerAction.PlayerStopMale or PlayerAction.PlayerStopFemale or PlayerAction.PlayerStopSummoner or
            PlayerAction.PlayerStopSword or PlayerAction.PlayerStopTwoHandSword or PlayerAction.PlayerStopTwoHandSwordTwo or
            PlayerAction.PlayerStopSpear or PlayerAction.PlayerStopScythe or PlayerAction.PlayerStopBow or
            PlayerAction.PlayerStopCrossbow or PlayerAction.PlayerStopWand or
            PlayerAction.PlayerStopFly or PlayerAction.PlayerStopFlyCrossbow or
            PlayerAction.PlayerStopRide or PlayerAction.PlayerStopRideWeapon or PlayerAction.PlayerStopRideHorse or
            PlayerAction.PlayerDarklordStand or
            PlayerAction.PlayerFenrirStand or PlayerAction.PlayerFenrirStandTwoSword or
            PlayerAction.PlayerFenrirStandOneRight or PlayerAction.PlayerFenrirStandOneLeft or
            PlayerAction.PlayerRageFenrirStand or PlayerAction.PlayerRageFenrirStandTwoSword or
            PlayerAction.PlayerRageFenrirStandOneRight or PlayerAction.PlayerRageFenrirStandOneLeft or
            PlayerAction.PlayerStopRagefighter or PlayerAction.PlayerIdle1Darkhorse or PlayerAction.PlayerIdle2Darkhorse
                                                                               => AnimationType.Idle,

            PlayerAction walkAction when walkAction >= PlayerAction.PlayerWalkMale && walkAction <= PlayerAction.PlayerWalkSwim
                                                                               => AnimationType.Walk,
            PlayerAction runAction when runAction >= PlayerAction.PlayerRun && runAction <= PlayerAction.PlayerRunSwim
                                                                               => AnimationType.Walk,
            PlayerAction.PlayerFly or PlayerAction.PlayerFlyCrossbow or
            PlayerAction.PlayerRunRide or PlayerAction.PlayerRunRideWeapon or
            PlayerAction.PlayerRunRideHorse or PlayerAction.PlayerDarklordWalk
                                                                               => AnimationType.Walk,
            PlayerAction fenrirRunAction when fenrirRunAction >= PlayerAction.PlayerFenrirRun && fenrirRunAction <= PlayerAction.PlayerFenrirRunOneLeftElf
                                                                               => AnimationType.Walk,
            PlayerAction fenrirWalkAction when fenrirWalkAction >= PlayerAction.PlayerFenrirWalk && fenrirWalkAction <= PlayerAction.PlayerFenrirWalkOneLeft
                                                                               => AnimationType.Walk,
            PlayerAction rageFenrirAction when rageFenrirAction >= PlayerAction.PlayerRageFenrirWalk && rageFenrirAction <= PlayerAction.PlayerRageFenrirRunOneLeft
                                                                               => AnimationType.Walk,
            PlayerAction.PlayerRageUniRun or PlayerAction.PlayerRageUniRunOneRight
                                                                               => AnimationType.Walk,

            PlayerAction.PlayerShock => AnimationType.Emote,
            PlayerAction attackAction when attackAction >= PlayerAction.PlayerAttackFist && attackAction <= PlayerAction.PlayerAttackRideCrossbow
                                                                               => AnimationType.Attack,
            PlayerAction attackUpAction when attackUpAction >= PlayerAction.PlayerAttackBowUp && attackUpAction <= PlayerAction.PlayerAttackStun
                                                                               => AnimationType.Attack,
            PlayerAction.PlayerAttackTwoHandSwordTwo or
            PlayerAction.PlayerAttackStrike or PlayerAction.PlayerAttackTeleport or
            PlayerAction.PlayerAttackRideStrike or PlayerAction.PlayerAttackRideTeleport or
            PlayerAction.PlayerAttackRideHorseSword or PlayerAction.PlayerAttackRideAttackFlash or
            PlayerAction.PlayerAttackRideAttackMagic or PlayerAction.PlayerAttackDarkhorse or
            PlayerAction.PlayerRageUniAttack or PlayerAction.PlayerRageUniAttackOneRight or
            PlayerAction.PlayerRageFenrirAttackRight
                                                                               => AnimationType.Attack,
            PlayerAction fenrirAttackAction when fenrirAttackAction >= PlayerAction.PlayerFenrirAttack && fenrirAttackAction <= PlayerAction.PlayerFenrirAttackBow
                                                                               => AnimationType.Attack,

            PlayerAction.PlayerSkillHell or PlayerAction.PlayerSkillHellBegin or
            PlayerAction.PlayerSkillHellStart => AnimationType.Skill,

            PlayerAction action when action.ToString().Contains("Skill", StringComparison.Ordinal)
                => AnimationType.Skill,

            PlayerAction.PlayerGreeting1 or PlayerAction.PlayerGoodbye1 or
            PlayerAction.PlayerClap1 or PlayerAction.PlayerCheer1 or
            PlayerAction.PlayerSee1 or PlayerAction.PlayerSeeFemale1 or
            PlayerAction.PlayerWin1 or PlayerAction.PlayerWinFemale1 or
            PlayerAction.PlayerSmile1 or PlayerAction.PlayerSmileFemale1 => AnimationType.Emote,
            _ => AnimationType.Idle
        };

        private static AnimationType GetMonsterAnimationType(MonsterActionType a) => a switch
        {
            MonsterActionType.Die => AnimationType.Death,
            MonsterActionType.Stop1 or MonsterActionType.Stop2
                                                        => AnimationType.Idle,
            MonsterActionType.Walk or MonsterActionType.Run
                                                        => AnimationType.Walk,
            MonsterActionType.Attack1 or MonsterActionType.Attack2
                                                        => AnimationType.Attack,
            MonsterActionType.Shock => AnimationType.Emote,
            MonsterActionType.Appear => AnimationType.Appear,
            _ => AnimationType.Idle
        };

        private float CalcDuration(Client.Data.BMD.BMDTextureAction act)
        {
            if (act == null) return 1.0f; // Default duration for null actions

            float frames = Math.Max(act.NumAnimationKeys, 1);
            float mul = act.PlaySpeed == 0 ? 1f : act.PlaySpeed;
            float fps = Math.Max(0.1f, _owner.AnimationSpeed * mul);
            return Math.Max(0.3f, frames / fps);
        }

        public void Dispose()
        {
            ClearTimer();
        }
    }

    internal static class WalkerExt
    {
        public static bool IsAlive(this WalkerObject w) =>
            w is not PlayerObject pl || !(pl.IsMainWalker &&
                MuGame.Network?.GetCharacterState()?.CurrentHealth == 0);

        public static ushort GetIdleAction(this PlayerObject p)
        {
            return p.GetCorrectIdleAction();
        }
    }
}
