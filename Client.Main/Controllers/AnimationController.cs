using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Client.Main.Controls;
using Client.Main.Core.Utilities;
using Client.Main.Models;
using Client.Main.Objects;
using Client.Main.Objects.Player;

namespace Client.Main.Controllers
{
    public enum AnimationType { Idle, Walk, Attack, Skill, Emote, Death, Rest, Sit }

    public sealed class AnimationController : IDisposable
    {
        private readonly WalkerObject _owner;

        private ushort? _currentOneShot;
        private CancellationTokenSource _timer;
        private bool _oneShotEnded;
        private bool _serverControlled;
        private volatile bool _forceReturnToIdle;

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

            _timer?.Cancel();
            _timer?.Dispose();
            _timer = null;
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

        private void StartBackupTimer(ushort idx)
        {
            if (!TryGetDuration(idx, out var real)) return;
            float wait = real + BACKUP_MARGIN;

            _timer = new CancellationTokenSource();
            // Debug.WriteLine($"[AnimCtrl] backup {wait:F2}s for {idx}");

            Task.Delay(TimeSpan.FromSeconds(wait), _timer.Token).ContinueWith(t =>
            {
                if (t.IsCanceled || _forceReturnToIdle) return;

                MuGame.ScheduleOnMainThread(() =>
                {
                    if (!_oneShotEnded && _currentOneShot == idx && !_forceReturnToIdle)
                    {
                        // Debug.WriteLine($"[AnimCtrl] backup fired → idle");
                        ReturnToIdle();
                    }
                });
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        /* ----------------------------------------------------------------- */
        /*  DEATH – max 3 s                                                  */
        /* ----------------------------------------------------------------- */
        private void StartDeathTimer(ushort idx)
        {
            if (!TryGetDuration(idx, out var anim)) anim = 0;
            float hold = Math.Min(anim + DEATH_HOLD, DEATH_HOLD);

            _timer = new CancellationTokenSource();
            // Debug.WriteLine($"[AnimCtrl] death hold {hold:F2}s");

            Task.Delay(TimeSpan.FromSeconds(hold), _timer.Token).ContinueWith(t =>
            {
                if (t.IsCanceled) return;

                MuGame.ScheduleOnMainThread(() =>
                {
                    if (_owner is PlayerObject pl && pl.IsMainWalker)
                    {
                        var state = MuGame.Network?.GetCharacterState();
                        if (state?.CurrentHealth > 0) ReturnToIdle();
                    }
                });
            }, TaskContinuationOptions.OnlyOnRanToCompletion);
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
            _timer?.Cancel();
            _timer?.Dispose();
            _timer = null;

            if (_owner.Status == GameControlStatus.Disposed) return;
            if (GetAnimationType((ushort)_owner.CurrentAction) == AnimationType.Death
                && !_owner.IsAlive()) return;

            ushort idle = _owner switch
            {
                PlayerObject p => p.GetIdleAction(),
                _ => (ushort)MonsterActionType.Stop1
            };

            // Debug.WriteLine($"[AnimCtrl] Setting idle action: {_owner.CurrentAction} → {idle}");
            _owner.CurrentAction = idle;
            _owner.InvalidateBuffers();

            _currentOneShot = null;
            _serverControlled = false;
        }

        private bool TryGetDuration(ushort idx, out float duration)
        {
            duration = 0;
            var a = _owner.Model?.Actions;
            if (a == null || idx >= a.Length) return false;

            var act = a[idx];
            duration = CalcDuration(act);
            return true;
        }

        private static bool IsReturnable(AnimationType t)
            => t is AnimationType.Attack or AnimationType.Skill or AnimationType.Emote;

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
            PlayerAction.PlayerStandingRest or PlayerAction.PlayerFlyingRest => AnimationType.Rest,
            PlayerAction.PlayerSit1 or PlayerAction.PlayerSitFemale1 => AnimationType.Sit,
            PlayerAction.StopMale or PlayerAction.StopFemale or PlayerAction.StopFlying
                                                                               => AnimationType.Idle,
            PlayerAction.WalkMale or PlayerAction.WalkFemale or
            PlayerAction.RunSwim or PlayerAction.Fly => AnimationType.Walk,
            PlayerAction.AttackFist or PlayerAction.PlayerAttackBow or
            PlayerAction.PlayerAttackSwordRight1 or PlayerAction.PlayerAttackCrossbow
                                                                               => AnimationType.Attack,
            PlayerAction.BlowSkill or PlayerAction.TwistingSlashSkill or
            PlayerAction.FlameSkill or PlayerAction.EvilSpiritSkill => AnimationType.Skill,
            PlayerAction.PlayerGreeting1 or PlayerAction.PlayerGoodbye1 or
            PlayerAction.PlayerClap1 or PlayerAction.PlayerCheer1 => AnimationType.Emote,
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
            _ => AnimationType.Idle
        };

        private float CalcDuration(Client.Data.BMD.BMDTextureAction act)
        {
            float frames = Math.Max(act.NumAnimationKeys, 1);
            float mul = act.PlaySpeed == 0 ? 1f : act.PlaySpeed;
            float fps = Math.Max(0.1f, _owner.AnimationSpeed * mul);
            return Math.Max(0.3f, frames / fps);
        }

        public void Dispose()
        {
            _timer?.Cancel();
            _timer?.Dispose();
        }
    }

    internal static class WalkerExt
    {
        public static bool IsAlive(this WalkerObject w) =>
            w is not PlayerObject pl || !(pl.IsMainWalker &&
                MuGame.Network?.GetCharacterState()?.CurrentHealth == 0);

        public static ushort GetIdleAction(this PlayerObject p)
        {
            bool female = PlayerActionMapper.IsCharacterFemale(p.CharacterClass);
            bool fly = p.World is WalkableWorldControl ww && (ww.WorldIndex == 8 || ww.WorldIndex == 11);
            return (ushort)(fly ? PlayerAction.StopFlying :
                            female ? PlayerAction.StopFemale : PlayerAction.StopMale);
        }
    }
}