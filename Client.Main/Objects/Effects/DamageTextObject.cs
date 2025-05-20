using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;
using Client.Main.Controllers;  // For GraphicsManager
using Client.Main.Models;
using Client.Main.Helpers;
using Client.Main.Objects.Player; // Required for PlayerObject check
using Client.Main.Scenes;      // For GameScene

namespace Client.Main.Objects.Effects
{
    public class DamageTextObject : WorldObject
    {
        public string Text { get; }
        public Color TextColor { get; }
        public ushort TargetId { get; }
        private float _currentVerticalOffset = 0f;

        private const float Lifetime = 1.2f;          // Total lifetime in seconds
        private float _elapsedTime = 0f;
        private Vector2 _screenPosition; // Calculated screen position for drawing
        private const float VerticalSpeed = -40f;     // Pixels per second (negative for upward screen movement)

        // Z-offset constants for positioning text above the target's anchor point
        private const float PlayerHeadBoneTextOffsetZ = 50f;   // Offset above the player's head bone.
        private const float PlayerModelTopTextOffsetZ = 30f;   // Offset above the calculated top of the player model (fallback).
        private const float MonsterBBoxTopTextOffsetZ = 30f;   // Offset above a monster's bounding box top.

        public DamageTextObject(string text, ushort targetId, Color color)
        {
            Text = text;
            TargetId = targetId;
            TextColor = color;
            Alpha = 1.0f;
            Scale = 1.0f; // This scale is for the text itself if SpriteFont supports it
            IsTransparent = true;
            AffectedByTransparency = false; // Damage text should always be visible on top
            Status = GameControlStatus.Ready;
            // Position will be set in Update based on the target.
        }

        public override Task Load()
        {
            Status = GameControlStatus.Ready;
            return Task.CompletedTask;
        }

        public override void Update(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready) return;

            float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _elapsedTime += delta;

            if (_elapsedTime >= Lifetime || Alpha <= 0.01f) // Also remove if fully faded
            {
                this.Hidden = true; // Mark as hidden to stop drawing
                World?.RemoveObject(this);
                Dispose();
                return;
            }

            float fadeStart = Lifetime * 0.6f; // Start fading a bit later
            if (_elapsedTime > fadeStart)
            {
                Alpha = MathHelper.Clamp(1.0f - (_elapsedTime - fadeStart) / (Lifetime - fadeStart), 0f, 1f);
            }

            _currentVerticalOffset += VerticalSpeed * delta;

            WalkerObject target = null;
            var gameScene = MuGame.Instance?.ActiveScene as GameScene; // Get the GameScene instance

            if (World != null && gameScene != null)
            {
                var localPlayerId = MuGame.Network.GetCharacterState().Id;
                if (TargetId == localPlayerId)
                {
                    target = gameScene.Hero;
                }
                else
                {
                    if (!World.TryGetWalkerById(TargetId, out target))
                    {
                        // Target might have been removed from world or is not a WalkerObject; handled by null check below.
                    }
                }
            }

            if (target == null || target.Status == GameControlStatus.Disposed || target.Status == GameControlStatus.Error || target.Hidden ||
                (target.Status == GameControlStatus.Ready && target.OutOfView)) // Added Status.Ready check for OutOfView
            {
                this.Hidden = true;
                // If target is gone, or not ready for display, we don't need to update our 3D position.
                // The lifetime check above will handle removal.
                return;
            }

            Position = CalculateAnchorPoint(target);

            // The base.Update() call below is important.
            // This WorldObject's Position is set by CalculateAnchorPoint.
            // The Position setter calls RecalculateWorldPosition(), which computes WorldPosition matrix.
            // The base.Update() then computes BoundingBoxWorld from BoundingBoxLocal and WorldPosition.
            // It also updates OutOfView based on the new BoundingBoxWorld.
            // This is standard WorldObject lifecycle. For a screen-space effect like this,
            // the 3D bounding box and OutOfView are less critical but maintain consistency.

            // Project to screen coordinates using the final world position of the text
            Vector3 projectedPos = GraphicsDevice.Viewport.Project(
                this.Position, // Use the 3D anchor point
                Camera.Instance.Projection,
                Camera.Instance.View,
                Matrix.Identity);

            _screenPosition = new Vector2(projectedPos.X, projectedPos.Y + _currentVerticalOffset);
            base.Update(gameTime);
            // Update Hidden based on Z-clipping for drawing purposes
            this.Hidden = (projectedPos.Z < 0f || projectedPos.Z > 1f);
        }

        private Vector3 CalculateAnchorPoint(WalkerObject target)
        {
            const int PLAYER_HEAD_BONE_INDEX = 20; // Assumed head bone index for Player.bmd (used for player head attachment)
            const float PLAYER_LOCAL_HEAD_HEIGHT_APPROX = 130f; // Approximate Z height of player's head from their local origin (feet)

            if (target is PlayerObject playerTarget)
            {
                var boneTransforms = playerTarget.GetBoneTransforms();
                if (boneTransforms != null && boneTransforms.Length > PLAYER_HEAD_BONE_INDEX && boneTransforms[PLAYER_HEAD_BONE_INDEX] != default)
                {
                    // Preferred method: Attach to head bone if available.
                    // BoneTransform[X].Translation is the local-to-model-origin position of the bone.
                    Vector3 headBoneLocalPosition = boneTransforms[PLAYER_HEAD_BONE_INDEX].Translation;
                    Vector3 headBoneWorldPosition = Vector3.Transform(headBoneLocalPosition, playerTarget.WorldPosition);
                    return headBoneWorldPosition + Vector3.UnitZ * PlayerHeadBoneTextOffsetZ;
                }
                else
                {
                    // Fallback for Player: Use an estimated head height.
                    // Player's `Position.Z` is at their feet. Add scaled local head height and an additional offset.
                    float worldHeadHeight = playerTarget.Position.Z + (PLAYER_LOCAL_HEAD_HEIGHT_APPROX * playerTarget.TotalScale);
                    return new Vector3(playerTarget.Position.X, playerTarget.Position.Y, worldHeadHeight + PlayerModelTopTextOffsetZ);
                }
            }
            else // For MonsterObject or other non-PlayerObject WalkerObjects
            {
                // Use the top-center of the monster's world bounding box.
                return new Vector3(
                    (target.BoundingBoxWorld.Min.X + target.BoundingBoxWorld.Max.X) * 0.5f,
                    (target.BoundingBoxWorld.Min.Y + target.BoundingBoxWorld.Max.Y) * 0.5f,
                    target.BoundingBoxWorld.Max.Z + MonsterBBoxTopTextOffsetZ);
            }
        }

        public override void Draw(GameTime gameTime)
        {
            // DamageTextObject is a 2D screen effect, so it doesn't have a 3D model to draw in the main Draw pass.
        }

        public override void DrawAfter(GameTime gameTime)
        {
            if (!Visible || Alpha <= 0.01f)
                return;

            var spriteBatch = GraphicsManager.Instance.Sprite;
            var font = GraphicsManager.Instance.Font;
            if (spriteBatch == null || font == null)
                return;

            float fontSize = 14f;
            // Ensure text is not too small by clamping scaleFactor
            float scaleFactorClamped = System.Math.Max(0.5f, fontSize / Constants.BASE_FONT_SIZE);
            float scaleFactor = fontSize / Constants.BASE_FONT_SIZE;
            Vector2 origin = font.MeasureString(Text) * 0.5f;
            Color color = TextColor * Alpha;

            using (new SpriteBatchScope(
                spriteBatch,
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone))
            {
                spriteBatch.DrawString(
                    font,
                    Text,
                    _screenPosition, // _screenPosition is already calculated with _currentVerticalOffset
                    color,
                    0f,
                    origin,
                    scaleFactorClamped,
                    SpriteEffects.None,
                    0f);
            }
        }

    }
}
