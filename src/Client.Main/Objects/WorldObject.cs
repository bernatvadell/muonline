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

        public string ObjectName => GetType().Name;

        public float Alpha { get; set; } = 1f;
        public Vector3 Position { get; set; }
        public Vector3 BodyOrigin { get; set; }
        public Vector3 Angle { get; set; }
        public float Scale { get; set; } = 1f;
        public bool LightEnabled { get; set; } = true;
        public BMD Model { get; set; }
        public bool Ready => Model != null && _boneVertexBuffers != null;
        public bool Visible => Ready;
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

            // Inicializamos las matrices y quaternions para los huesos
            _boneMatrix = new Matrix[Model.Bones.Length];
            _boneQuaternion = new Quaternion[Model.Bones.Length];

            for (int i = 0; i < _boneMatrix.Length; i++)
            {
                _boneMatrix[i] = Matrix.Identity;
                _boneQuaternion[i] = Quaternion.Identity;
            }

            InitializeBuffers();

            return Task.CompletedTask;
        }

        public virtual void Update(GameTime gameTime)
        {
            if (!Visible) return;
            Animation(gameTime);
        }

        private void Animation(GameTime gameTime)
        {
            // Calcula el frame actual de la animación basándote en el tiempo del juego
            float animationSpeed = 3f; // Velocidad de animación, ajustable
            float currentFrame = (float)(gameTime.TotalGameTime.TotalSeconds * animationSpeed);

            // Asegúrate de que el frame se ajusta al número de claves de animación disponible
            currentFrame %= Model.Actions[_currentAction].NumAnimationKeys;

            // Configura el frame anterior y prior frame para la interpolación de animación
            float priorFrame = Math.Max(0, currentFrame - 1);

            // Llamar al método Animation para actualizar las transformaciones de los huesos
            Animation(_boneMatrix, currentFrame, priorFrame, 0, Angle, Vector3.Zero, false, true);
        }

        public virtual void Draw(BasicEffect effect, GameTime gameTime)
        {
            if (!Visible) return;

            _graphicsDevice.BlendState = Alpha >= 1f ? BlendState.Opaque : BlendState.AlphaBlend;
            effect.Alpha = Alpha;
            effect.LightingEnabled = LightEnabled;

            // Convierte los ángulos del objeto a radianes y crea la rotación
            Vector3 angleInRadians = new Vector3(
                MathHelper.ToRadians(Angle.X),
                MathHelper.ToRadians(Angle.Y),
                MathHelper.ToRadians(Angle.Z));

            // Crear la transformación global incluyendo rotación, escala y traslación
            Matrix globalTransform = Matrix.CreateScale(Scale)
                                    * Matrix.CreateFromQuaternion(AngleQuaternion(angleInRadians))
                                    * Matrix.CreateTranslation(Position);

            // Usamos las matrices de huesos para transformar las partes del cuerpo en cada draw call
            foreach (var meshIndex in _boneVertexBuffers.Keys)
            {
                // Obtener el vértice del mesh para averiguar a qué hueso pertenece
                var mesh = Model.Meshes[meshIndex];
                if (mesh.Vertices.Length == 0)
                    continue;

                // Todos los vértices del "mesh" deberían pertenecer al mismo hueso
                int boneIndex = mesh.Vertices[0].Node;

                if (boneIndex < 0 || boneIndex >= _boneMatrix.Length)
                    continue;  // Asegúrate de que el índice del hueso sea válido

                Matrix boneTransform = _boneMatrix[boneIndex];

                // Aplicar la transformación global y del hueso
                Matrix worldMatrix = boneTransform * globalTransform;
                effect.World = worldMatrix;

                // Establecer la textura asociada al hueso/mesh
                effect.Texture = _boneTextures[meshIndex];

                if (effect.Texture == null)
                    continue;

                foreach (var pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();

                    // Establecer los buffers
                    _graphicsDevice.SetVertexBuffer(_boneVertexBuffers[meshIndex]);
                    _graphicsDevice.Indices = _boneIndexBuffers[meshIndex];

                    // Dibujar los triángulos
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

                // Obtener los buffers y texturas del BMDLoader
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

            // Frame actual y factores de interpolación
            float currentAnimation = animationFrame;
            int currentAnimationFrame = (int)animationFrame;
            float interpolationFactor = currentAnimation - currentAnimationFrame;

            int priorAnimationFrame = (int)priorFrame;
            if (priorAnimationFrame < 0) priorAnimationFrame = 0;
            if (currentAnimationFrame < 0) currentAnimationFrame = 0;

            // Asegúrate de que las posiciones están dentro del rango
            if (priorAnimationFrame >= Model.Actions[priorAction].NumAnimationKeys) priorAnimationFrame = 0;
            if (currentAnimationFrame >= Model.Actions[_currentAction].NumAnimationKeys) currentAnimationFrame = 0;

            // Recorrer todos los huesos
            for (int i = 0; i < Model.Bones.Length; i++)
            {
                var bone = Model.Bones[i];

                if (bone == BMDTextureBone.Dummy)
                    continue;

                var bm1 = bone.Matrixes[priorAction];
                var bm2 = bone.Matrixes[_currentAction];

                // Interpolamos las rotaciones (quaternions) y las posiciones
                Quaternion q1 = AngleQuaternion(bm1.Rotation[priorAnimationFrame]);
                Quaternion q2 = AngleQuaternion(bm2.Rotation[currentAnimationFrame]);
                _boneQuaternion[i] = Quaternion.Slerp(q1, q2, interpolationFactor);

                // Crear la matriz de transformación del hueso basada en el quaternion interpolado
                Matrix boneMatrixTransform = Matrix.CreateFromQuaternion(_boneQuaternion[i]);

                // Interpolamos las posiciones
                Vector3 position1 = bm1.Position[priorAnimationFrame];
                Vector3 position2 = bm2.Position[currentAnimationFrame];
                Vector3 interpolatedPosition = Vector3.Lerp(position1, position2, interpolationFactor);

                // Asignar la interpolación a la matriz
                boneMatrixTransform.Translation = interpolatedPosition;

                // Si el hueso tiene un padre, combinamos la transformación con la del padre
                if (bone.Parent != -1)
                {
                    boneMatrixTransform *= boneMatrix[bone.Parent];
                }

                // Guardamos la matriz calculada para el hueso
                boneMatrix[i] = boneMatrixTransform;
            }
        }

        private Quaternion AngleQuaternion(Vector3 angles)
        {
            float angle;
            float sr, sp, sy, cr, cp, cy;

            // Rescalado de los ángulos a la mitad
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
