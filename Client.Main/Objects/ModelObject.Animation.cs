using Client.Data.BMD;
using Client.Main.Content;
using Client.Main.Controls.UI.Game.Inventory;
using Client.Main.Models;
using Client.Main.Objects.Player;
using Client.Main.Objects.Wings;
using Microsoft.Xna.Framework;
using System;
using System.Buffers;
using System.Threading;

namespace Client.Main.Objects
{
    public abstract partial class ModelObject
    {
        // Local animation optimization - per object only
        private struct LocalAnimationState : IEquatable<LocalAnimationState>
        {
            public int ActionIndex;
            public int Frame0;
            public int Frame1;
            public float InterpolationFactor;

            public bool Equals(LocalAnimationState other)
            {
                return ActionIndex == other.ActionIndex &&
                       Frame0 == other.Frame0 &&
                       Frame1 == other.Frame1 &&
                       MathF.Abs(InterpolationFactor - other.InterpolationFactor) < 0.001f;
            }

            public override bool Equals(object obj) => obj is LocalAnimationState other && Equals(other);
            public override int GetHashCode() => HashCode.Combine(ActionIndex, Frame0, Frame1, InterpolationFactor);
        }

        private void Animation(GameTime gameTime)
        {
            if (LinkParentAnimation || Model?.Actions == null || Model.Actions.Length == 0) return;

            int currentActionIndex = Math.Clamp(CurrentAction, 0, Model.Actions.Length - 1);
            var action = Model.Actions[currentActionIndex];
            if (action == null) return; // Skip animation if action is null

            int totalFrames = Math.Max(action.NumAnimationKeys, 1);
            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Detect death action for walkers to clamp on second-to-last key
            bool isDeathAction = false;
            if (this is WalkerObject)
            {
                if (this is PlayerObject)
                {
                    var pa = (PlayerAction)currentActionIndex;
                    isDeathAction = pa == PlayerAction.PlayerDie1 || pa == PlayerAction.PlayerDie2;
                }
                else if (this is MonsterObject)
                {
                    isDeathAction = currentActionIndex == (int)Client.Main.Models.MonsterActionType.Die;
                }
                else if (this is NPCObject)
                {
                    var pa = (PlayerAction)currentActionIndex;
                    isDeathAction = pa == PlayerAction.PlayerDie1 || pa == PlayerAction.PlayerDie2;
                }
            }

            if (totalFrames == 1 && !ContinuousAnimation)
            {
                if (_priorActionIndex != currentActionIndex)
                {
                    GenerateBoneMatrix(currentActionIndex, 0, 0, 0);
                    _priorActionIndex = currentActionIndex;
                    InvalidateBuffers(BUFFER_FLAG_ANIMATION);
                }
                CurrentFrame = 0;
                return;
            }

            if (_priorActionIndex != currentActionIndex)
            {
                _blendFromAction = _priorActionIndex;
                _blendFromTime = _animTime;
                _blendElapsed = 0f;
                _isBlending = true;
                _animTime = 0.0;

                // _blendFromBones is pre-allocated in LoadContent - no need to allocate here
            }

            _animTime += delta * (action.PlaySpeed == 0 ? 1.0f : action.PlaySpeed) * AnimationSpeed;
            double framePos;

            if (isDeathAction || HoldOnLastFrame)
            {
                int endIdx = Math.Max(0, totalFrames - 2);
                _animTime = Math.Min(_animTime, endIdx + 0.0001f);
                framePos = _animTime;
            }
            else if (this is WalkerObject walker && walker.IsOneShotPlaying)
            {
                int endIdx = Math.Max(0, totalFrames - 1);
                if (_animTime >= endIdx)
                {
                    _animTime = endIdx;
                    framePos = _animTime;
                    walker.NotifyOneShotAnimationCompleted();
                }
                else
                {
                    framePos = _animTime;
                }
            }
            else
            {
                framePos = _animTime % totalFrames;
            }

            int f0 = (int)framePos;
            int f1 = (f0 + 1) % totalFrames;
            float t = (float)(framePos - f0);
            CurrentFrame = f0;

            var forceRestartSmoothly = f0 == totalFrames - 1 && action.Positions.Length > f0 && action.Positions[f0] == Vector3.Zero;

            if (forceRestartSmoothly)
            {
                f0 = 0;
                f1 = 1;
                t = 0.0f;
                _animTime = _animTime - (totalFrames - 1);
            }

            GenerateBoneMatrix(currentActionIndex, f0, f1, t);

            if (_isBlending)
            {
                _blendElapsed += delta;
                float blendFactor = MathHelper.Clamp(_blendElapsed / _blendDuration, 0f, 1f);

                if (_blendFromAction >= 0 && _blendFromBones != null)
                {
                    var prevAction = Model.Actions[_blendFromAction];
                    _blendFromTime += delta * (prevAction.PlaySpeed == 0 ? 1.0f : prevAction.PlaySpeed) * AnimationSpeed;
                    int prevTotal = Math.Max(prevAction.NumAnimationKeys, 1);
                    double pf = _blendFromTime % prevTotal;
                    int pf0 = (int)pf;
                    int pf1 = (pf0 + 1) % prevTotal;
                    float pt = (float)(pf - pf0);
                    ComputeBoneMatrixTo(_blendFromAction, pf0, pf1, pt, _blendFromBones);

                    // blending
                    for (int i = 0; i < BoneTransform.Length; i++)
                    {
                        Matrix.Lerp(ref _blendFromBones[i], ref BoneTransform[i], blendFactor, out BoneTransform[i]);
                    }
                }

                if (blendFactor >= 1.0f)
                {
                    _isBlending = false;
                    _blendFromAction = -1;
                }

                InvalidateBuffers(BUFFER_FLAG_ANIMATION);
            }

            _priorActionIndex = currentActionIndex;
        }

        protected void GenerateBoneMatrix(int actionIdx, int frame0, int frame1, float t)
        {
            var bones = Model?.Bones;

            if (bones == null || bones.Length == 0)
            {
                // Reset animation cache for invalid models
                _animationStateValid = false;
                return;
            }

            // Armor items use the player's idle pose so they match equipped visuals
            if (TryApplyPlayerIdlePose(bones))
            {
                _animationStateValid = true;
                _lastAnimationState = default;
                return;
            }

            if (Model.Actions == null || Model.Actions.Length == 0)
            {
                _animationStateValid = false;
                return;
            }

            actionIdx = Math.Clamp(actionIdx, 0, Model.Actions.Length - 1);
            var action = Model.Actions[actionIdx];

            // Create animation state for comparison - only for animated objects
            LocalAnimationState currentAnimState = default;
            bool shouldCheckCache = !LinkParentAnimation && ParentBoneLink < 0 &&
                                   action.NumAnimationKeys > 1; // Only cache animated objects

            if (shouldCheckCache)
            {
                currentAnimState = new LocalAnimationState
                {
                    ActionIndex = actionIdx,
                    Frame0 = frame0,
                    Frame1 = frame1,
                    InterpolationFactor = t
                };

                // Check if we can skip expensive calculation using local cache
                // But be more conservative - only skip if frames and interpolation are identical
                if (_animationStateValid && currentAnimState.Equals(_lastAnimationState) &&
                    BoneTransform != null && BoneTransform.Length == bones.Length)
                {
                    // Animation state hasn't changed - no need to recalculate
                    return;
                }
            }

            // Initialize or resize bone transform array if needed
            if (BoneTransform == null || BoneTransform.Length != bones.Length)
                BoneTransform = new Matrix[bones.Length];

            // Rent temp array from pool for safer hierarchical calculations
            // ArrayPool may return larger array, so we use bones.Length for actual operations
            Matrix[] tempBoneTransforms = _matrixArrayPool.Rent(bones.Length);

            try
            {
                bool lockPositions = action.LockPositions;
                float bodyHeight = BodyHeight;
                bool anyBoneChanged = false;

                // Pre-clamp frame indices to valid ranges
                int maxFrameIndex = action.NumAnimationKeys - 1;
                frame0 = Math.Clamp(frame0, 0, maxFrameIndex);
                frame1 = Math.Clamp(frame1, 0, maxFrameIndex);

                // If frames are the same, no interpolation needed
                if (frame0 == frame1) t = 0f;

                // Process bones in order (parents before children)
                for (int i = 0; i < bones.Length; i++)
                {
                    var bone = bones[i];

                    // Skip invalid bones
                    if (bone == BMDTextureBone.Dummy || bone.Matrixes == null || actionIdx >= bone.Matrixes.Length)
                    {
                        tempBoneTransforms[i] = Matrix.Identity;
                        if (BoneTransform[i] != Matrix.Identity)
                            anyBoneChanged = true;
                        continue;
                    }

                    var bm = bone.Matrixes[actionIdx];
                    int numPosKeys = bm.Position?.Length ?? 0;
                    int numQuatKeys = bm.Quaternion?.Length ?? 0;

                    if (numPosKeys == 0 || numQuatKeys == 0)
                    {
                        tempBoneTransforms[i] = Matrix.Identity;
                        if (BoneTransform[i] != Matrix.Identity)
                            anyBoneChanged = true;
                        continue;
                    }

                    // Ensure frame indices are valid for this specific bone
                    int boneMaxFrame = Math.Min(numPosKeys, numQuatKeys) - 1;
                    int boneFrame0 = Math.Min(frame0, boneMaxFrame);
                    int boneFrame1 = Math.Min(frame1, boneMaxFrame);
                    float boneT = (boneFrame0 == boneFrame1) ? 0f : t;

                    Matrix localTransform;

                    // Optimize for common case: no interpolation needed
                    if (boneT == 0f)
                    {
                        // Direct keyframe - no interpolation
                        localTransform = Matrix.CreateFromQuaternion(bm.Quaternion[boneFrame0]);
                        localTransform.Translation = bm.Position[boneFrame0];
                    }
                    else
                    {
                        // Interpolated keyframe - use fast normalized lerp instead of costly Slerp
                        Quaternion q = Nlerp(bm.Quaternion[boneFrame0], bm.Quaternion[boneFrame1], boneT);
                        Vector3 p0 = bm.Position[boneFrame0];
                        Vector3 p1 = bm.Position[boneFrame1];

                        localTransform = Matrix.CreateFromQuaternion(q);
                        localTransform.M41 = p0.X + (p1.X - p0.X) * boneT;
                        localTransform.M42 = p0.Y + (p1.Y - p0.Y) * boneT;
                        localTransform.M43 = p0.Z + (p1.Z - p0.Z) * boneT;
                    }

                    // Apply position locking for root bone
                    if (i == 0 && lockPositions && bm.Position.Length > 0)
                    {
                        var rootPos = bm.Position[0];
                        localTransform.Translation = new Vector3(rootPos.X, rootPos.Y, localTransform.M43 + bodyHeight);
                    }

                    // Apply parent transformation with safety checks
                    Matrix worldTransform;
                    if (bone.Parent >= 0 && bone.Parent < bones.Length)
                    {
                        worldTransform = localTransform * tempBoneTransforms[bone.Parent];
                    }
                    else
                    {
                        worldTransform = localTransform;
                    }

                    // Store in temp array
                    tempBoneTransforms[i] = worldTransform;

                    // Check if this bone actually changed (simple comparison for performance)
                    if (BoneTransform[i] != worldTransform)
                    {
                        anyBoneChanged = true;
                    }
                }

                // For static objects (single frame) or first-time setup, always update
                bool forceUpdate = action.NumAnimationKeys <= 1 || !_animationStateValid;

                // Allow derived objects to apply procedural bone post-processing (e.g., head look-at).
                // Must run on the temp array so the result also propagates to children using LinkParentAnimation.
                if (PostProcessBoneTransforms(bones, tempBoneTransforms))
                {
                    anyBoneChanged = true;
                }

                // Only update final transforms and invalidate if something actually changed OR force update
                if (anyBoneChanged || forceUpdate)
                {
                    Array.Copy(tempBoneTransforms, BoneTransform, bones.Length);

                    // Always invalidate animation for walkers (players/monsters/NPCs) to preserve smooth pacing
                    bool isImportantObject = RequiresPerFrameAnimation;
                    if (forceUpdate || isImportantObject)
                    {
                        InvalidateBuffers(BUFFER_FLAG_ANIMATION);
                    }
                    else
                    {
                        // Only throttle animation updates for non-critical objects (NPCs, monsters)
                        const double ANIMATION_UPDATE_INTERVAL_MS = 20; // Max 20 Hz for non-critical objects

                        if (_lastFrameTimeMs - _lastAnimationUpdateTime > ANIMATION_UPDATE_INTERVAL_MS)
                        {
                            InvalidateBuffers(BUFFER_FLAG_ANIMATION);
                            _lastAnimationUpdateTime = _lastFrameTimeMs;
                        }
                    }
                }

                // Always update cache for objects that should use it
                if (shouldCheckCache)
                {
                    _lastAnimationState = currentAnimState;
                    _animationStateValid = true;
                }
                else if (action.NumAnimationKeys <= 1)
                {
                    // Mark static objects as having valid animation state
                    _animationStateValid = true;
                }
            }
            finally
            {
                // CRITICAL: Always return rented array to pool to prevent memory leaks
                // clearArray: false because we don't need to zero out Matrix structs (performance)
                _matrixArrayPool.Return(tempBoneTransforms, clearArray: false);
            }
        }

        /// <summary>
        /// Allows derived objects to procedurally adjust the computed bone transforms (in-place).
        /// Return true if any bone was modified.
        /// </summary>
        protected virtual bool PostProcessBoneTransforms(BMDTextureBone[] bones, Matrix[] boneTransforms)
        {
            return false;
        }

        private static Quaternion Nlerp(in Quaternion q1, in Quaternion q2, float t)
        {
            var target = q2;
            if (Quaternion.Dot(q1, q2) < 0f)
            {
                target.X = -target.X;
                target.Y = -target.Y;
                target.Z = -target.Z;
                target.W = -target.W;
            }

            var blended = new Quaternion(
                q1.X + (target.X - q1.X) * t,
                q1.Y + (target.Y - q1.Y) * t,
                q1.Z + (target.Z - q1.Z) * t,
                q1.W + (target.W - q1.W) * t);

            return Quaternion.Normalize(blended);
        }

        private void ComputeBoneMatrixTo(int actionIdx, int frame0, int frame1, float t, Matrix[] output)
        {
            if (Model?.Bones == null || output == null)
                return;

            var bones = Model.Bones;
            if (actionIdx < 0 || actionIdx >= Model.Actions.Length)
                actionIdx = 0;

            var action = Model.Actions[actionIdx];

            for (int i = 0; i < bones.Length; i++)
            {
                var bone = bones[i];

                if (bone == BMDTextureBone.Dummy || bone.Matrixes == null || actionIdx >= bone.Matrixes.Length)
                    continue;

                var bm = bone.Matrixes[actionIdx];

                int numPosKeys = bm.Position?.Length ?? 0;
                int numQuatKeys = bm.Quaternion?.Length ?? 0;
                if (numPosKeys == 0 || numQuatKeys == 0)
                    continue;

                if (frame0 < 0 || frame1 < 0 || frame0 >= numPosKeys || frame1 >= numPosKeys || frame0 >= numQuatKeys || frame1 >= numQuatKeys)
                {
                    int maxValidIndex = Math.Min(numPosKeys, numQuatKeys) - 1;
                    if (maxValidIndex < 0) maxValidIndex = 0;
                    frame0 = Math.Clamp(frame0, 0, maxValidIndex);
                    frame1 = Math.Clamp(frame1, 0, maxValidIndex);
                    if (frame0 == frame1) t = 0f;
                }

                Quaternion q = Nlerp(bm.Quaternion[frame0], bm.Quaternion[frame1], t);
                Matrix m = Matrix.CreateFromQuaternion(q);

                Vector3 p0 = bm.Position[frame0];
                Vector3 p1 = bm.Position[frame1];

                m.M41 = p0.X + (p1.X - p0.X) * t;
                m.M42 = p0.Y + (p1.Y - p0.Y) * t;
                m.M43 = p0.Z + (p1.Z - p0.Z) * t;

                if (i == 0 && action.LockPositions)
                    m.Translation = new Vector3(bm.Position[0].X, bm.Position[0].Y, m.M43 + BodyHeight);

                Matrix world = bone.Parent != -1 && bone.Parent < output.Length
                    ? m * output[bone.Parent]
                    : m;

                output[i] = world;
            }
        }

        /// <summary>
        /// Allows derived objects to provide modified bone transforms for rendering.
        /// Default returns the input bones unchanged.
        /// Useful for lightweight procedural deformations (e.g., cape flutter).
        /// </summary>
        protected virtual Matrix[] GetRenderBoneTransforms(Matrix[] bones)
        {
            return bones;
        }

        private bool TryApplyPlayerIdlePose(BMDTextureBone[] bones)
        {
            var def = ItemDefinition;
            int group = def?.Group ?? -1;
            bool isArmor = group >= 7 && group <= 11;
            if (!isArmor)
                return false;

            var playerBones = PlayerIdlePoseProvider.GetIdleBoneMatrices();
            if (playerBones == null || playerBones.Length == 0)
                return false;

            if (BoneTransform == null || BoneTransform.Length != bones.Length)
                BoneTransform = new Matrix[bones.Length];

            for (int i = 0; i < bones.Length; i++)
            {
                BoneTransform[i] = (i < playerBones.Length)
                    ? playerBones[i]
                    : BuildBoneFromBmd(bones[i], BoneTransform);
            }

            InvalidateBuffers(BUFFER_FLAG_ANIMATION);
            return true;
        }

        private static Matrix BuildBoneFromBmd(BMDTextureBone bone, Matrix[] parentResults)
        {
            Matrix local = Matrix.Identity;

            if (bone?.Matrixes != null && bone.Matrixes.Length > 0)
            {
                var bm = bone.Matrixes[0];
                if (bm.Position?.Length > 0 && bm.Quaternion?.Length > 0)
                {
                    var q = bm.Quaternion[0];
                    local = Matrix.CreateFromQuaternion(new Quaternion(q.X, q.Y, q.Z, q.W));
                    var p = bm.Position[0];
                    local.Translation = new Vector3(p.X, p.Y, p.Z);
                }
            }

            if (bone != null && bone.Parent >= 0 && bone.Parent < parentResults.Length)
                return local * parentResults[bone.Parent];

            return local;
        }
    }
}
