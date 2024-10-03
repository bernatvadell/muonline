using Client.Data;
using Client.Data.BMD;
using Client.Main.Content;
using Client.Main.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    public abstract class WorldObject
    {
        private GraphicsDevice _graphicsDevice;
        private Dictionary<int, VertexBuffer> _boneVertexBuffers;
        private Dictionary<int, IndexBuffer> _boneIndexBuffers;
        private Dictionary<int, Texture2D> _boneTextures;
        private Matrix[] _boneMatrix;
        private Quaternion[] _boneQuaternion;
        private int _currentAction = 0;
        private int _boneHead;
        private float _bodyHeight;
        private float _bodyScale = 1;
        private BoundingSphere _originalBoundingSphere;
        private BoundingSphere _transformedBoundingSphere;
        private bool OutOfView;
        private Matrix _globalTransform;

        public string ObjectName => GetType().Name;

        public float Alpha { get; set; } = 1f;
        public Vector3 Position { get; set; }
        public Vector3 BodyOrigin { get; set; }
        public Vector3 Angle { get; set; }
        public float Scale { get; set; } = 1f;
        public bool LightEnabled { get; set; } = true;
        public BMD Model { get; set; }
        public bool Ready => Model != null && _boneVertexBuffers != null;
        public bool Visible => Ready && !OutOfView;
        public WorldControl World { get; set; }
        public ushort Type { get; set; }

        public virtual Task Load(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;

            if (Model == null)
            {
                Debug.WriteLine($"Model is not assigned for {ObjectName}");
                return Task.CompletedTask;
            }

            _boneMatrix = new Matrix[Model.Bones.Length];
            _boneQuaternion = new Quaternion[Model.Bones.Length];

            for (int i = 0; i < _boneMatrix.Length; i++)
            {
                _boneMatrix[i] = Matrix.Identity;
                _boneQuaternion[i] = Quaternion.Identity;
            }

            InitializeBuffers();

            ComputeBoundingSphere();

            return Task.CompletedTask;
        }

        public virtual void Update(GameTime gameTime)
        {
            if (!Ready) return;

            Vector3 angleInRadians = new Vector3(
                MathHelper.ToRadians(Angle.X),
                MathHelper.ToRadians(Angle.Y),
                MathHelper.ToRadians(Angle.Z));

            _globalTransform = Matrix.CreateScale(Scale)
                                    * Matrix.CreateFromQuaternion(AngleQuaternion(angleInRadians))
                                    * Matrix.CreateTranslation(Position);

            _transformedBoundingSphere = TransformBoundingSphere(_originalBoundingSphere, _globalTransform);

            OutOfView = Camera.Instance.Frustum.Contains(_transformedBoundingSphere) == ContainmentType.Disjoint;

            if (OutOfView)
                return;

            Animation(gameTime);
        }

        private BoundingSphere TransformBoundingSphere(BoundingSphere sphere, Matrix transform)
        {
            transform.Decompose(out Vector3 scale, out Quaternion rotation, out Vector3 translation);
            float maxScale = Math.Max(scale.X, Math.Max(scale.Y, scale.Z));
            Vector3 transformedCenter = Vector3.Transform(sphere.Center, transform);
            return new BoundingSphere(transformedCenter, sphere.Radius * maxScale);
        }

        private void ComputeBoundingSphere()
        {
            List<Vector3> positions = [];

            foreach (var mesh in Model.Meshes)
            {
                foreach (var vertex in mesh.Vertices)
                {
                    positions.Add(vertex.Position);
                }
            }

            _originalBoundingSphere = BoundingSphere.CreateFromPoints(positions);
        }

        private void Animation(GameTime gameTime)
        {
            float animationSpeed = 3f; 
            float currentFrame = (float)(gameTime.TotalGameTime.TotalSeconds * animationSpeed);

            currentFrame %= Model.Actions[_currentAction].NumAnimationKeys;

            float priorFrame = Math.Max(0, currentFrame - 1);

            Animation(_boneMatrix, currentFrame, priorFrame, 0, Angle, Vector3.Zero, false, true);
        }

        public virtual void Draw(BasicEffect effect, GameTime gameTime)
        {
            if (!Visible) return;

            effect.Alpha = Alpha;
            effect.LightingEnabled = LightEnabled;

            foreach (var meshIndex in _boneVertexBuffers.Keys)
            {
                var mesh = Model.Meshes[meshIndex];
                if (mesh.Vertices.Length == 0)
                    continue;

                int boneIndex = mesh.Vertices[0].Node;

                if (boneIndex < 0 || boneIndex >= _boneMatrix.Length)
                    continue; 

                Matrix boneTransform = _boneMatrix[boneIndex];

                Matrix worldMatrix = boneTransform * _globalTransform;
                effect.World = worldMatrix;

                effect.Texture = _boneTextures[meshIndex];

                if (effect.Texture == null)
                    continue;

                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();

                    _graphicsDevice.SetVertexBuffer(_boneVertexBuffers[meshIndex]);
                    _graphicsDevice.Indices = _boneIndexBuffers[meshIndex];

                    _graphicsDevice.DrawIndexedPrimitives(
                        PrimitiveType.TriangleList,
                        0,
                        0,
                        _boneIndexBuffers[meshIndex].IndexCount / 3
                    );
                }
            }
        }

        private void InitializeBuffers()
        {
            _boneVertexBuffers = new Dictionary<int, VertexBuffer>();
            _boneIndexBuffers = new Dictionary<int, IndexBuffer>();
            _boneTextures = new Dictionary<int, Texture2D>();

            for (int meshIndex = 0; meshIndex < Model.Meshes.Length; meshIndex++)
            {
                var mesh = Model.Meshes[meshIndex];

                VertexBuffer vertexBuffer = BMDLoader.Instance.GetVertexBuffer(Model, meshIndex);
                IndexBuffer indexBuffer = BMDLoader.Instance.GetIndexBuffer(Model, meshIndex);
                Texture2D texture = TextureLoader.Instance.GetTexture2D(BMDLoader.Instance.GetTexturePath(Model, mesh.TexturePath));

                _boneVertexBuffers[meshIndex] = vertexBuffer;
                _boneIndexBuffers[meshIndex] = indexBuffer;
                _boneTextures[meshIndex] = texture;
            }
        }

        public void Animation(Matrix[] boneMatrix, float animationFrame, float priorFrame, ushort priorAction, Vector3 angle, Vector3 headAngle, bool parent, bool translate)
        {
            if (Model.Actions.Length <= 0) return;

            if (priorAction >= Model.Actions.Length) priorAction = 0;
            if (_currentAction >= Model.Actions.Length) _currentAction = 0;

            float currentAnimation = animationFrame;
            int currentAnimationFrame = (int)animationFrame;
            float interpolationFactor = currentAnimation - currentAnimationFrame;

            int priorAnimationFrame = (int)priorFrame;
            if (priorAnimationFrame < 0) priorAnimationFrame = 0;
            if (currentAnimationFrame < 0) currentAnimationFrame = 0;

            if (priorAnimationFrame >= Model.Actions[priorAction].NumAnimationKeys) priorAnimationFrame = 0;
            if (currentAnimationFrame >= Model.Actions[_currentAction].NumAnimationKeys) currentAnimationFrame = 0;

            for (int i = 0; i < Model.Bones.Length; i++)
            {
                var bone = Model.Bones[i];

                if (bone == BMDTextureBone.Dummy)
                    continue;

                var bm1 = bone.Matrixes[priorAction];
                var bm2 = bone.Matrixes[_currentAction];

                Quaternion q1 = AngleQuaternion(bm1.Rotation[priorAnimationFrame]);
                Quaternion q2 = AngleQuaternion(bm2.Rotation[currentAnimationFrame]);
                _boneQuaternion[i] = Quaternion.Slerp(q1, q2, interpolationFactor);

                Matrix boneMatrixTransform = Matrix.CreateFromQuaternion(_boneQuaternion[i]);

                Vector3 position1 = bm1.Position[priorAnimationFrame];
                Vector3 position2 = bm2.Position[currentAnimationFrame];
                Vector3 interpolatedPosition = Vector3.Lerp(position1, position2, interpolationFactor);

                boneMatrixTransform.Translation = interpolatedPosition;

                if (bone.Parent != -1)
                    boneMatrixTransform *= boneMatrix[bone.Parent];

                boneMatrix[i] = boneMatrixTransform;
            }
        }

        private Quaternion AngleQuaternion(Vector3 angles)
        {
            float angle;
            float sr, sp, sy, cr, cp, cy;

            angle = angles.Z * 0.5f;
            sy = (float)Math.Sin(angle);
            cy = (float)Math.Cos(angle);
            angle = angles.Y * 0.5f;
            sp = (float)Math.Sin(angle);
            cp = (float)Math.Cos(angle);
            angle = angles.X * 0.5f;
            sr = (float)Math.Sin(angle);
            cr = (float)Math.Cos(angle);

            float x = sr * cp * cy - cr * sp * sy;
            float y = cr * sp * cy + sr * cp * sy;
            float z = cr * cp * sy - sr * sp * cy;
            float w = cr * cp * cy + sr * sp * sy;

            return new Quaternion(x, y, z, w);
        }
    }
}
