// File: Client.Main/Content/PlayerIdlePoseProvider.cs
using System;
using System.Threading.Tasks;
using Client.Data.BMD;
using Microsoft.Xna.Framework;

namespace Client.Main.Content
{
    /// <summary>
    /// Provides bone transformation matrices from Player.bmd's idle animation.
    /// Used to render inventory items with the same bone poses as they would appear on the player.
    /// </summary>
    public static class PlayerIdlePoseProvider
    {
        // Cached bone matrices for idle pose
        private static Matrix[] _idleBoneMatrices;
        private static bool _isLoaded;
        private static bool _isLoading;
        private static readonly object _loadLock = new();

        // Idle action index (PlayerAction.PlayerStopMale = 0)
        private const int IdleActionIndex = 0;
        private const int IdleFrame = 0;

        /// <summary>
        /// Ensures the idle pose data is loaded. Call this at game startup.
        /// </summary>
        public static async Task EnsureLoadedAsync()
        {
            if (_isLoaded) return;

            lock (_loadLock)
            {
                if (_isLoaded || _isLoading) return;
                _isLoading = true;
            }

            try
            {
                var playerBmd = await BMDLoader.Instance.Prepare("Player/Player.bmd");
                if (playerBmd != null)
                {
                    _idleBoneMatrices = BuildIdlePoseBoneMatrices(playerBmd, IdleActionIndex, IdleFrame);
                    _isLoaded = true;
                }
            }
            finally
            {
                _isLoading = false;
            }
        }

        /// <summary>
        /// Gets the bone transformation matrices from player's idle pose.
        /// Returns null if not loaded.
        /// </summary>
        public static Matrix[] GetIdleBoneMatrices()
        {
            if (!_isLoaded || _idleBoneMatrices == null)
                return null;

            return _idleBoneMatrices;
        }

        /// <summary>
        /// Checks if the provider has loaded data.
        /// </summary>
        public static bool IsLoaded => _isLoaded;

        /// <summary>
        /// Builds bone matrices for a specific animation frame.
        /// </summary>
        private static Matrix[] BuildIdlePoseBoneMatrices(BMD bmd, int actionIndex, int frame)
        {
            if (bmd?.Bones == null || bmd.Bones.Length == 0)
                return Array.Empty<Matrix>();

            var bones = bmd.Bones;
            var result = new Matrix[bones.Length];

            // Get the action for animation data
            BMDTextureAction action = null;
            if (bmd.Actions != null && actionIndex >= 0 && actionIndex < bmd.Actions.Length)
            {
                action = bmd.Actions[actionIndex];
            }

            for (int i = 0; i < bones.Length; i++)
            {
                var bone = bones[i];
                Matrix local = Matrix.Identity;

                if (bone != null && bone != BMDTextureBone.Dummy)
                {
                    // Try to get animation data from the action
                    if (action != null && bone.Matrixes != null && bone.Matrixes.Length > 0)
                    {
                        var boneMatrix = bone.Matrixes[0];

                        // Get position for this frame
                        System.Numerics.Vector3 position = System.Numerics.Vector3.Zero;
                        if (boneMatrix.Position != null && boneMatrix.Position.Length > 0)
                        {
                            int posFrame = Math.Min(frame, boneMatrix.Position.Length - 1);
                            position = boneMatrix.Position[posFrame];
                        }

                        // Get rotation for this frame
                        System.Numerics.Quaternion rotation = System.Numerics.Quaternion.Identity;
                        if (boneMatrix.Quaternion != null && boneMatrix.Quaternion.Length > 0)
                        {
                            int rotFrame = Math.Min(frame, boneMatrix.Quaternion.Length - 1);
                            rotation = boneMatrix.Quaternion[rotFrame];
                        }

                        // Build local transform
                        local = Matrix.CreateFromQuaternion(ToXna(rotation));
                        local.Translation = ToXna(position);
                    }
                }

                // Combine with parent
                if (bone != null && bone.Parent >= 0 && bone.Parent < result.Length)
                    result[i] = local * result[bone.Parent];
                else
                    result[i] = local;
            }

            return result;
        }

        private static Vector3 ToXna(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
        private static Quaternion ToXna(System.Numerics.Quaternion q) => new Quaternion(q.X, q.Y, q.Z, q.W);
    }
}
