using Client.Data.BMD;
using Client.Data.Texture;
using Client.Main.Content;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    public abstract class ModelObject : WorldObject
    {
        private VertexBuffer[] _boneVertexBuffers;
        private IndexBuffer[] _boneIndexBuffers;
        private Texture2D[] _boneTextures;
        private TextureScript[] _scriptTextures;
        private TextureData[] _dataTextures;

        private int _priorAction = 0;
        private Effect _effect;
        private bool _invalidatedBuffers = true;
        private float _blendMeshLight = 1f;
        private float _previousFrame = 0;
        public float ShadowOpacity { get; set; } = 1f;
        public Color Color { get; set; } = Color.White;
        protected Matrix[] BoneTransform { get; set; }
        public int CurrentAction { get; set; }
        public virtual int OriginBoneIndex => 0;
        public BMD Model { get; set; }
        public Matrix BodyOrigin => OriginBoneIndex < BoneTransform.Length ? BoneTransform[OriginBoneIndex] : Matrix.Identity;
        public float BodyHeight { get; private set; }
        public int HiddenMesh { get; set; } = -1;
        public int BlendMesh { get; set; } = -1;
        public BlendState BlendMeshState { get; set; } = BlendState.Additive;
        public float BlendMeshLight
        {
            get => _blendMeshLight;
            set
            {
                _blendMeshLight = value;
                _invalidatedBuffers = true;
            }
        }
        public bool RenderShadow { get; set; }
        public float AnimationSpeed { get; set; } = 4f;

        public ModelObject()
        {
            MatrixChanged += (_s, _e) => UpdateWorldPosition();
        }

        public override async Task Load()
        {
            lock (GraphicsDevice)
            {
                if (_effect == null)
                {
                    _effect = new AlphaTestEffect(GraphicsDevice)
                    {
                        VertexColorEnabled = true,
                        World = Matrix.Identity,
                        AlphaFunction = CompareFunction.Greater,
                        ReferenceAlpha = (int)(255 * 0.25f)
                    };
                }
            }

            await base.Load();

            if (Model == null)
            {
                Debug.WriteLine($"Model is not assigned for {ObjectName} -> Type: {Type}");
                Status = Models.GameControlStatus.Error;
                return;
            }

            UpdateWorldPosition();
            GenerateBoneMatrix(0, 0, 0, 0);
        }

        public override void Update(GameTime gameTime)
        {
            if (World == null)
                return;

            base.Update(gameTime);

            if (!Visible) return;

            if (_effect != null)
            {
                if (_effect is IEffectMatrices effectMatrices)
                {
                    effectMatrices.View = Camera.Instance.View;
                    effectMatrices.Projection = Camera.Instance.Projection;
                }
                else
                {
                    _effect.Parameters["View"]?.SetValue(Camera.Instance.View);
                    _effect.Parameters["Projection"]?.SetValue(Camera.Instance.Projection);
                }
            }

            Animation(gameTime);
            SetDynamicBuffers();
        }

        public override void Draw(GameTime gameTime)
        {
            if (World == null)
                return;

            if (!Visible || _boneIndexBuffers == null) return;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            DrawModel(false);
            base.Draw(gameTime);
        }

        public virtual void DrawModel(bool isAfterDraw)
        {
            int meshCount = Model.Meshes.Length; // Cache mesh count

            for (int i = 0; i < meshCount; i++)
            {
                if (_dataTextures[i] == null) continue;
                bool isRGBA = _dataTextures[i].Components == 4;
                bool isBlendMesh = BlendMesh == i;
                bool draw = isAfterDraw
                    ? isRGBA || isBlendMesh
                    : !isRGBA && !isBlendMesh;

                if (!isAfterDraw && RenderShadow)
                {
                    GraphicsDevice.DepthStencilState = MuGame.Instance.DisableDepthMask;
                    DrawShadowMesh(i);
                }

                if (!draw) continue;

                GraphicsDevice.DepthStencilState = isAfterDraw
                    ? MuGame.Instance.DisableDepthMask
                    : DepthStencilState.Default;

                DrawMesh(i);
            }
        }

        public virtual void DrawMesh(int mesh)
        {
            if (HiddenMesh == mesh || _boneVertexBuffers == null)
                return;

            Texture2D texture = _boneTextures[mesh];
            VertexBuffer vertexBuffer = _boneVertexBuffers[mesh];
            IndexBuffer indexBuffer = _boneIndexBuffers[mesh];
            int primitiveCount = indexBuffer.IndexCount / 3;

            _effect.Parameters["Texture"]?.SetValue(texture);

            GraphicsDevice.BlendState = BlendMesh == mesh ? BlendMeshState : BlendState;
            if (_effect is AlphaTestEffect alphaTestEffect)
                alphaTestEffect.Alpha = TotalAlpha;

            foreach (EffectPass pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();

                GraphicsDevice.SetVertexBuffer(vertexBuffer);
                GraphicsDevice.Indices = indexBuffer;
                GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, primitiveCount);
            }
        }

        public virtual void DrawShadowMesh(int mesh)
        {
            if (HiddenMesh == mesh || _boneVertexBuffers == null)
                return;

            Texture2D texture = _boneTextures[mesh];
            VertexBuffer vertexBuffer = _boneVertexBuffers[mesh];
            IndexBuffer indexBuffer = _boneIndexBuffers[mesh];
            int primitiveCount = indexBuffer.IndexCount / 3;

            _effect.Parameters["Texture"]?.SetValue(texture);

            VertexPositionColorNormalTexture[] shadowVertices = new VertexPositionColorNormalTexture[vertexBuffer.VertexCount];

            // Ensure alpha blending is enabled
            GraphicsDevice.BlendState = BlendState.AlphaBlend;

            if (_effect is AlphaTestEffect effect)
            {
                vertexBuffer.GetData(shadowVertices);

                // Clamp ShadowOpacity to a valid range (0 to 1)
                float clampedShadowOpacity = MathHelper.Clamp(ShadowOpacity, 0f, 1f);

                // Ensure that ShadowOpacity is being applied to each vertex color
                for (int i = 0; i < shadowVertices.Length; i++)
                {
                    // Apply shadow opacity to the alpha channel, ensuring the value is between 0 and 255
                    byte shadowAlpha = (byte)(255 * clampedShadowOpacity);
                    shadowVertices[i].Color = new Color((byte)0, (byte)0, (byte)0, shadowAlpha);  // Apply shadow with calculated alpha
                }

                Matrix originalWorld = effect.World;
                Matrix originalView = effect.View;
                Matrix originalProjection = effect.Projection;

                // Get the model's rotation from the original world matrix
                Vector3 scale, translation;
                Quaternion rotation;
                originalWorld.Decompose(out scale, out rotation, out translation);

                // Create a world matrix for the shadow with the model's rotation
                Matrix world = Matrix.CreateFromQuaternion(rotation) *
                               Matrix.CreateRotationX(MathHelper.ToRadians(-20)) *
                               Matrix.CreateScale(0.8f, 1.0f, 0.8f) *
                               Matrix.CreateTranslation(translation);

                // Add light and shadow offset
                Vector3 lightDirection = new(-1, 0, 1);
                Vector3 shadowOffset = new(0.05f, 0, 0.1f);
                world.Translation += lightDirection * 0.3f + shadowOffset;

                effect.World = world;

                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, shadowVertices, 0, primitiveCount);
                }

                // Restore original matrices
                effect.World = originalWorld;
                effect.View = originalView;
                effect.Projection = originalProjection;
            }
        }

        public override void DrawAfter(GameTime gameTime)
        {
            if (!Visible) return;
            DrawModel(true);
            base.DrawAfter(gameTime);
        }

        public override void Dispose()
        {
            base.Dispose();

            _effect?.Dispose();
            Model = null;
            BoneTransform = null;
            _invalidatedBuffers = true;
        }

        private void UpdateWorldPosition()
        {
            _invalidatedBuffers = true;

            if (_effect != null)
            {
                if (_effect is IEffectMatrices effectMatrices)
                {
                    effectMatrices.World = WorldPosition;
                }
                else
                {
                    _effect.Parameters["World"]?.SetValue(WorldPosition);
                }
            }
        }

        private void UpdateBoundings()
        {
            if (Model == null) return;

            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);

            Matrix[] boneTransforms = BoneTransform; // Cache BoneTransform
            BMDTextureBone[] modelBones = Model.Bones; // Cache Model.Bones

            foreach (var mesh in Model.Meshes)
            {
                foreach (var vertex in mesh.Vertices)
                {
                    int boneIndex = vertex.Node;

                    if (boneIndex < 0 || boneIndex >= boneTransforms.Length)
                        continue;

                    Matrix boneMatrix = boneTransforms[boneIndex];
                    Vector3 transformedPosition = Vector3.Transform(vertex.Position, boneMatrix);
                    min = Vector3.Min(min, transformedPosition);
                    max = Vector3.Max(max, transformedPosition);
                }
            }

            BoundingBoxLocal = new BoundingBox(min, max);
        }

        private void Animation(GameTime gameTime)
        {
            if (LinkParent || Model?.Actions.Length <= 0) return;

            var currentActionData = Model.Actions[CurrentAction];

            if (currentActionData.NumAnimationKeys <= 1)
            {
                if (_priorAction != CurrentAction || BoneTransform == null)
                {
                    GenerateBoneMatrix(CurrentAction, 0, 0, 0);
                    _priorAction = CurrentAction;
                }
                return;
            }

            float currentFrame = (float)(gameTime.TotalGameTime.TotalSeconds * AnimationSpeed);
            int totalFrames = currentActionData.NumAnimationKeys - 1;
            currentFrame %= totalFrames;

            Animation(currentFrame);

            _priorAction = CurrentAction;
        }

        private void Animation(float currentFrame)
        {
            if (LinkParent || Model == null || Model.Actions.Length <= 0) return;

            if (CurrentAction >= Model.Actions.Length)
                CurrentAction = 0;

            int currentAnimationFrame = (int)Math.Floor(currentFrame);
            float interpolationFactor = currentFrame - currentAnimationFrame;

            var currentActionData = Model.Actions[CurrentAction];
            int totalFrames = currentActionData.NumAnimationKeys - 1;
            int nextAnimationFrame = (currentAnimationFrame + 1) % totalFrames;

            GenerateBoneMatrix(CurrentAction, currentAnimationFrame, nextAnimationFrame, interpolationFactor);
        }

        private void GenerateBoneMatrix(int currentAction, int currentAnimationFrame, int nextAnimationFrame, float interpolationFactor)
        {
            BoneTransform ??= new Matrix[Model.Bones.Length];
            var currentActionData = Model.Actions[currentAction];
            bool changed = false;

            Matrix[] boneTransforms = BoneTransform; // Cache BoneTransform
            BMDTextureBone[] modelBones = Model.Bones; // Cache Model.Bones

            for (int i = 0; i < modelBones.Length; i++)
            {
                var bone = modelBones[i];

                if (bone == BMDTextureBone.Dummy)
                    continue;

                var bm = bone.Matrixes[currentAction];

                Quaternion q1 = bm.Quaternion[currentAnimationFrame];
                Quaternion q2 = bm.Quaternion[nextAnimationFrame];

                Quaternion boneQuaternion = Quaternion.Slerp(q1, q2, interpolationFactor);
                Matrix matrix = Matrix.CreateFromQuaternion(boneQuaternion);

                Vector3 position1 = bm.Position[currentAnimationFrame];
                Vector3 position2 = bm.Position[nextAnimationFrame];
                Vector3 interpolatedPosition = Vector3.Lerp(position1, position2, interpolationFactor);

                if (i == 0 && currentActionData.LockPositions)
                {
                    matrix.M41 = bm.Position[0].X;
                    matrix.M42 = bm.Position[0].Y;
                    matrix.M43 = position1.Z * (1 - interpolationFactor) + position2.Z * interpolationFactor + BodyHeight;
                }
                else
                {
                    matrix.Translation = interpolatedPosition;
                }

                Matrix newMatrix = bone.Parent != -1
                    ? matrix * boneTransforms[bone.Parent]
                    : matrix;

                if (!changed && boneTransforms[i] != newMatrix)
                    changed = true;

                boneTransforms[i] = newMatrix;
            }

            if (changed)
            {
                InvalidateBuffers();
                UpdateBoundings();
            }
        }

        private void SetDynamicBuffers()
        {
            if (!_invalidatedBuffers)
                return;

            int meshCount = Model.Meshes.Length; // Cache mesh count

            _boneVertexBuffers ??= new VertexBuffer[meshCount];
            _boneIndexBuffers ??= new IndexBuffer[meshCount];
            _boneTextures ??= new Texture2D[meshCount];
            _scriptTextures ??= new TextureScript[meshCount];
            _dataTextures ??= new TextureData[meshCount];

            for (int meshIndex = 0; meshIndex < meshCount; meshIndex++)
            {
                var mesh = Model.Meshes[meshIndex];

                // Resolve the body light conflict
                Vector3 bodyLight = Vector3.Zero;

                if (LightEnabled && World.Terrain != null)
                {
                    Vector3 terrainLight = World.Terrain.RequestTerrainLight(WorldPosition.Translation.X, WorldPosition.Translation.Y);
                    terrainLight += Light;
                    bodyLight = terrainLight;
                }
                else
                {
                    bodyLight = Light;
                }

                bodyLight = meshIndex == BlendMesh
                    ? bodyLight * BlendMeshLight
                    : bodyLight * TotalAlpha;

                _boneVertexBuffers[meshIndex]?.Dispose();
                _boneIndexBuffers[meshIndex]?.Dispose();

                Matrix[] bones = (LinkParent && Parent is ModelObject parentModel) ? parentModel.BoneTransform : BoneTransform;

                // Use the updated color calculation method from the improvements branch
                byte r = Color.R;
                byte g = Color.G;
                byte b = Color.B;

                // Calculate color components considering lighting and clamping values
                byte bodyR = (byte)Math.Min(r * bodyLight.X, 255);
                byte bodyG = (byte)Math.Min(g * bodyLight.Y, 255);
                byte bodyB = (byte)Math.Min(b * bodyLight.Z, 255);

                Color bodyColor = new Color(bodyR, bodyG, bodyB);

                BMDLoader.Instance.GetModelBuffers(Model, meshIndex, bodyColor, bones, out var vertexBuffer, out var indexBuffer);

                _boneVertexBuffers[meshIndex] = vertexBuffer;
                _boneIndexBuffers[meshIndex] = indexBuffer;

                if (_boneTextures[meshIndex] == null)
                {
                    string texturePath = BMDLoader.Instance.GetTexturePath(Model, mesh.TexturePath);
                    _boneTextures[meshIndex] = TextureLoader.Instance.GetTexture2D(texturePath);
                    _scriptTextures[meshIndex] = TextureLoader.Instance.GetScript(texturePath);
                    _dataTextures[meshIndex] = TextureLoader.Instance.Get(texturePath);

                    var script = _scriptTextures[meshIndex];

                    if (script != null)
                    {
                        if (script.HiddenMesh)
                            HiddenMesh = meshIndex;

                        if (script.Bright)
                            BlendMesh = meshIndex;
                    }
                }
            }

            _invalidatedBuffers = false;
        }

        protected void InvalidateBuffers()
        {
            _invalidatedBuffers = true;

            for (int i = 0; i < Children.Count; i++)
            {
                if (Children[i] is ModelObject modelObject && modelObject.LinkParent)
                {
                    modelObject.InvalidateBuffers();
                }
            }
        }

        protected override void RecalculateWorldPosition()
        {
            Matrix localMatrix = Matrix.CreateScale(Scale) *
                                 Matrix.CreateFromQuaternion(MathUtils.AngleQuaternion(Angle)) *
                                 Matrix.CreateTranslation(Position);

            if (Parent != null)
            {
                Matrix worldMatrix = localMatrix * Parent.WorldPosition;

                if (WorldPosition != worldMatrix)
                {
                    WorldPosition = worldMatrix;
                }
            }
            else if (WorldPosition != localMatrix)
            {
                WorldPosition = localMatrix;
            }
        }
    }
}
