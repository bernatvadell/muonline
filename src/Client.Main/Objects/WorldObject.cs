using Client.Data;
using Client.Data.BMD;
using Client.Main.Content;
using Client.Main.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using System.Transactions;

namespace Client.Main.Objects
{
    public abstract class WorldObject : IChildItem<WorldObject>, IDisposable
    {
        private Vector3 _position, _angle;
        private float _scale = 1f;
        private BasicEffect _boundingBoxEffect;
        private BoundingBox _boundingBoxLocal = new BoundingBox(new Vector3(-40, -40, 0), new Vector3(40, 40, 80));
        private WorldObject _parent;
        private Matrix _worldPosition;

        public bool LinkParent { get; set; }
        public bool OutOfView { get; private set; } = true;
        public ChildrenCollection<WorldObject> Children { get; private set; }
        public WorldObject Parent { get => _parent; set { var prev = _parent; _parent = value; OnParentChanged(value, prev); } }

        public BoundingBox BoundingBoxLocal { get => _boundingBoxLocal; set { _boundingBoxLocal = value; OnBoundingBoxLocalChanged(); } }
        public BoundingBox BoundingBoxWorld { get; private set; }

        public virtual bool Ready { get; private set; }
        public bool Hidden { get; set; }
        public string ObjectName => GetType().Name;
        public BlendState BlendState { get; set; } = BlendState.Opaque;
        public float Alpha { get; set; } = 1f;
        public float TotalAlpha { get => (Parent?.TotalAlpha ?? 1f) * Alpha; }
        public Vector3 Position { get => _position; set { _position = value; OnPositionChanged(); } }
        public Vector3 Angle { get => _angle; set { _angle = value; OnAngleChanged(); } }
        public Vector3 TotalAngle { get => (Parent?.TotalAngle ?? Vector3.Zero) + Angle; }

        public float Scale { get => _scale; set { _scale = value; OnScaleChanged(); } }
        public Matrix WorldPosition { get => _worldPosition; set { _worldPosition = value; OnWorldPositionChanged(); } }
        public Vector3 Light { get; set; } = new Vector3(0f, 0f, 0f);
        public bool LightEnabled { get; set; } = true;
        public Vector3 BodyLight => LightEnabled ? World.Terrain.RequestTerrainLight(WorldPosition.Translation.X, WorldPosition.Translation.Y) * Light * Alpha : Vector3.One;
        public Vector3 BodyLightMesh => LightEnabled ? World.Terrain.RequestTerrainLight(WorldPosition.Translation.X, WorldPosition.Translation.Y) * Light * Alpha : Vector3.One;
        public bool Visible => Ready && !OutOfView && !Hidden;
        public WorldControl World => MuGame.Instance.ActiveScene?.World;
        public short Type { get; set; }
        public Color BoundingBoxColor { get; set; } = Color.GreenYellow;
        protected GraphicsDevice GraphicsDevice => MuGame.Instance.GraphicsDevice;

        public event EventHandler MatrixChanged;

        public WorldObject()
        {
            Children = new ChildrenCollection<WorldObject>(this);
        }

        public virtual async Task Load()
        {
            lock (GraphicsDevice)
            {
                _boundingBoxEffect = new BasicEffect(GraphicsDevice)
                {
                    VertexColorEnabled = true,
                    View = Camera.Instance.View,
                    Projection = Camera.Instance.Projection,
                    World = Matrix.Identity
                };
            }

            var tasks = new Task[Children.Count];

            for (var i = 0; i < Children.Count; i++)
                tasks[i] = Children[i].Load();

            await Task.WhenAll(tasks);

            RecalculateWorldPosition();
            UpdateWorldBoundingBox();

            Ready = true;
        }
        public virtual void Update(GameTime gameTime)
        {
            if (!Ready) return;

            OutOfView = Camera.Instance.Frustum.Contains(BoundingBoxWorld) == ContainmentType.Disjoint;

            if (OutOfView)
                return;

            if (_boundingBoxEffect != null)
            {
                _boundingBoxEffect.View = Camera.Instance.View;
                _boundingBoxEffect.Projection = Camera.Instance.Projection;
                _boundingBoxEffect.World = Matrix.Identity;
            }

            for (var i = 0; i < Children.Count; i++)
                Children[i].Update(gameTime);
        }

        public virtual void Draw(GameTime gameTime)
        {
            if (!Visible) return;

            if (Constants.DRAW_BOUNDING_BOXES)
                DrawBoundingBox();

            for (var i = 0; i < Children.Count; i++)
                Children[i].Draw(gameTime);
        }

        public virtual void DrawAfter(GameTime gameTime)
        {
            if (!Visible) return;

            for (var i = 0; i < Children.Count; i++)
                Children[i].DrawAfter(gameTime);
        }

        public void BringToFront()
        {
            if (!Ready) return;
            if (Parent == null) return;
            if (Parent.Children[^1] == this) return;
            var parent = Parent;
            Parent.Children.Remove(this);
            parent.Children.Add(this);
        }

        public void SendToBack()
        {
            if (!Ready) return;
            if (Parent == null) return;
            if (Parent.Children[0] == this) return;
            var parent = Parent;
            Parent.Children.Remove(this);
            parent.Children.Insert(0, this);
        }

        public virtual void Dispose()
        {
            Ready = false;

            Parent = null;

            var children = Children.ToArray();

            for (var i = 0; i < children.Length; i++)
                children[i].Dispose();

            Children.Clear();

            _boundingBoxEffect?.Dispose();
            _boundingBoxEffect = null;

            GC.SuppressFinalize(this);
        }

        protected virtual void OnPositionChanged() => RecalculateWorldPosition();
        protected virtual void OnAngleChanged() => RecalculateWorldPosition();
        protected virtual void OnScaleChanged() => RecalculateWorldPosition();
        protected virtual void OnParentChanged(WorldObject current, WorldObject prev)
        {
            if (prev != null)
            {
                prev.MatrixChanged -= OnParentMatrixChanged;
                prev.Children.Remove(this);
            }
            if (current != null) current.MatrixChanged += OnParentMatrixChanged;
            RecalculateWorldPosition();
        }
        protected virtual void OnBoundingBoxLocalChanged() => UpdateWorldBoundingBox();

        private void OnParentMatrixChanged(Object s, EventArgs e) => RecalculateWorldPosition();
        protected virtual void RecalculateWorldPosition()
        {
            var localMatrix = Matrix.CreateScale(Scale)
                                * Matrix.CreateFromQuaternion(MathUtils.AngleQuaternion(Angle))
                                * Matrix.CreateTranslation(Position);

            if (Parent != null)
            {
                var worldMatrix = localMatrix * Parent.WorldPosition;

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

        private void OnWorldPositionChanged()
        {
            UpdateWorldBoundingBox();
            MatrixChanged?.Invoke(this, EventArgs.Empty);
        }

        private void DrawBoundingBox()
        {
            if (_boundingBoxEffect == null)
                return;

            Vector3[] corners = BoundingBoxWorld.GetCorners();

            int[] indices =
            [
                0, 1, 1, 2, 2, 3, 3, 0,
                4, 5, 5, 6, 6, 7, 7, 4,
                0, 4, 1, 5, 2, 6, 3, 7
            ];

            var vertexData = new VertexPositionColor[8];
            for (int i = 0; i < corners.Length; i++)
                vertexData[i] = new VertexPositionColor(corners[i], BoundingBoxColor);

            foreach (var pass in _boundingBoxEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.LineList, vertexData, 0, 8, indices, 0, indices.Length / 2);
            }
        }
        private void UpdateWorldBoundingBox()
        {
            Vector3[] boundingBoxCorners = BoundingBoxLocal.GetCorners();

            for (int i = 0; i < boundingBoxCorners.Length; i++)
            {
                boundingBoxCorners[i] = Vector3.Transform(boundingBoxCorners[i], WorldPosition);
            }

            BoundingBoxWorld = BoundingBox.CreateFromPoints(boundingBoxCorners);
        }
    }
}
