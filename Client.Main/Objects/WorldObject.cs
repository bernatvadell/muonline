﻿using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Client.Main.Objects
{
    public abstract class WorldObject : IChildItem<WorldObject>, IDisposable
    {
        private Vector3 _position, _angle;
        private float _scale = 1f;
        private BoundingBox _boundingBoxLocal = new(new Vector3(-40, -40, 0), new Vector3(40, 40, 80));
        private WorldObject _parent;
        private Matrix _worldPosition;
        private WorldControl _world;
        private bool _interactive;

        public virtual float Depth
        {
            get => Position.Y + Position.Z;
        }
        public virtual bool AffectedByTransparency { get; set; } = true;
        public virtual bool IsTransparent { get; set; } = false;
        public int RenderOrder { get; set; }
        public DepthStencilState DepthState { get; set; } = DepthStencilState.Default;

        private SpriteBatch _spriteBatch;
        private SpriteFont _font;
        private Texture2D _whiteTexture;

        public bool LinkParentAnimation { get; set; }
        public bool OutOfView { get; private set; } = true;
        public ChildrenCollection<WorldObject> Children { get; private set; }
        public WorldObject Parent { get => _parent; set { var prev = _parent; _parent = value; OnParentChanged(value, prev); } }

        public BoundingBox BoundingBoxLocal { get => _boundingBoxLocal; set { _boundingBoxLocal = value; OnBoundingBoxLocalChanged(); } }
        public BoundingBox BoundingBoxWorld { get; private set; }

        public GameControlStatus Status { get; protected set; } = GameControlStatus.NonInitialized;
        public bool Hidden { get; set; }
        public string ObjectName => GetType().Name;
        public BlendState BlendState { get; set; } = BlendState.Opaque;
        public float Alpha { get; set; } = 1f;
        public float TotalAlpha { get => (Parent?.TotalAlpha ?? 1f) * Alpha; }
        public Vector3 Position { get => _position; set { if (_position != value) { _position = value; OnPositionChanged(); } } }
        public Vector3 Angle { get => _angle; set { _angle = value; OnAngleChanged(); } }
        public Vector3 TotalAngle { get => (Parent?.TotalAngle ?? Vector3.Zero) + Angle; }

        public float Scale { get => _scale; set { _scale = value; OnScaleChanged(); } }
        public float TotalScale { get => (Parent?.Scale ?? 1f) * Scale; }
        public Matrix WorldPosition { get => _worldPosition; set { _worldPosition = value; OnWorldPositionChanged(); } }
        public bool Interactive { get => _interactive || (Parent?.Interactive ?? false); set { _interactive = value; } }
        public Vector3 Light { get; set; } = new Vector3(0f, 0f, 0f);
        public bool LightEnabled { get; set; } = true;
        public bool Visible => Status == GameControlStatus.Ready && !OutOfView && !Hidden;
        public WorldControl World { get => _world; set { _world = value; OnChangeWorld(); } }
        public short Type { get; set; }
        public Color BoundingBoxColor { get; set; } = Color.GreenYellow;
        protected GraphicsDevice GraphicsDevice => MuGame.Instance.GraphicsDevice;

        public event EventHandler MatrixChanged;
        public bool IsMouseHover { get; private set; }
        public float DebugFontSize { get; set; } = 12f;

        public event EventHandler Click;

        public WorldObject()
        {
            Children = new ChildrenCollection<WorldObject>(this);
            Children.ControlAdded += Children_ControlAdded;

            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _font = GraphicsManager.Instance.Font;
        }

        public virtual void OnClick()
        {
            Click?.Invoke(this, EventArgs.Empty);
        }

        private void Children_ControlAdded(object sender, ChildrenEventArgs<WorldObject> e)
        {
            e.Control.World = World;
        }

        private void OnChangeWorld()
        {
            var children = Children.ToArray();
            for (var i = 0; i < children.Length; i++)
                Children[i].World = World;

            if (World is WalkableWorldControl && this is WalkerObject walker)
                walker.OnDirectionChanged();
        }

        public virtual async Task Load()
        {
            if (Status != GameControlStatus.NonInitialized)
                return;

            try
            {
                Status = GameControlStatus.Initializing;

                if (World == null) throw new ApplicationException("World is not assigned to object");

                var tasks = new Task[Children.Count + 1];

                tasks[0] = LoadContent();

                for (var i = 0; i < Children.Count; i++)
                    tasks[i + 1] = Children[i].Load();

                await Task.WhenAll(tasks);

                RecalculateWorldPosition();
                UpdateWorldBoundingBox();

                Status = GameControlStatus.Ready;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                Status = GameControlStatus.Error;
            }
        }

        public virtual Task LoadContent()
        {
            return Task.CompletedTask;
        }

        public virtual void Update(GameTime gameTime)
        {
            if (Status == GameControlStatus.NonInitialized)
            {
                // Use ConfigureAwait(false) to avoid context switching
                Load().ConfigureAwait(false);
            }

            if (Status != GameControlStatus.Ready) return;

            // Early exit if object is out of view
            OutOfView = Camera.Instance.Frustum.Contains(BoundingBoxWorld) == ContainmentType.Disjoint;
            if (OutOfView) return;

            // Cache parent's mouse hover state
            bool parentIsMouseHover = Parent?.IsMouseHover ?? false;

            // Only calculate intersections if needed
            bool wouldBeMouseHover = parentIsMouseHover;
            if (!parentIsMouseHover && (Interactive || Constants.DRAW_BOUNDING_BOXES))
            {
                float? intersectionDistance = MuGame.Instance.MouseRay.Intersects(BoundingBoxWorld);
                ContainmentType contains = BoundingBoxWorld.Contains(MuGame.Instance.MouseRay.Position);
                wouldBeMouseHover = intersectionDistance.HasValue || contains == ContainmentType.Contains;
            }

            IsMouseHover = wouldBeMouseHover;

            if (!parentIsMouseHover && IsMouseHover)
                World.Scene.MouseHoverObject = this;

            // Direct indexing instead of foreach to avoid enumerator allocation
            for (int i = 0; i < Children.Count; i++)
                Children[i].Update(gameTime);
        }

        public virtual void Draw(GameTime gameTime)
        {
            if (!Visible) return;

            DrawBoundingBox3D();

            // Avoid enumeration overhead
            int count = Children.Count;
            for (int i = 0; i < count; i++)
                Children[i].Draw(gameTime);
        }

        public virtual void DrawAfter(GameTime gameTime)
        {
            if (!Visible) return;

            DrawBoundingBox2D();

            // Avoid enumeration overhead
            int count = Children.Count;
            for (int i = 0; i < count; i++)
                Children[i].DrawAfter(gameTime);
        }

        public void BringToFront()
        {
            if (Parent == null) return;
            if (Parent.Children[^1] == this) return;
            var parent = Parent;
            Parent.Children.Remove(this);
            parent.Children.Add(this);
        }

        public void SendToBack()
        {
            if (Parent == null) return;
            if (Parent.Children[0] == this) return;
            var parent = Parent;
            Parent.Children.Remove(this);
            parent.Children.Insert(0, this);
        }

        public virtual void Dispose()
        {
            Status = GameControlStatus.Disposed;

            var children = Children.ToArray();
            for (int i = 0; i < children.Length; i++)
            {
                children[i].Dispose();
            }
            Children.Clear();

            Parent?.Children.Remove(this);
            Parent = null;

            _whiteTexture?.Dispose();
            _spriteBatch?.Dispose();
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
            Matrix localMatrix = Matrix.CreateScale(Scale)
                * Matrix.CreateFromQuaternion(MathUtils.AngleQuaternion(Angle))
                * Matrix.CreateTranslation(Position);

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

            // Use direct indexing instead of ToArray() to avoid allocation
            for (int i = 0; i < Children.Count; i++)
                Children[i].RecalculateWorldPosition();
        }

        private void OnWorldPositionChanged()
        {
            UpdateWorldBoundingBox();
            MatrixChanged?.Invoke(this, EventArgs.Empty);
        }

        private void DrawBoundingBox3D()
        {
            var draw = Constants.DRAW_BOUNDING_BOXES || (Interactive && Constants.DRAW_BOUNDING_BOXES_INTERACTIVES);

            if (!draw) return;

            var previousDepthState = GraphicsDevice.DepthStencilState;

            GraphicsDevice.DepthStencilState = DepthStencilState.Default;

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

            GraphicsManager.Instance.BoundingBoxEffect3D.View = Camera.Instance.View;
            GraphicsManager.Instance.BoundingBoxEffect3D.Projection = Camera.Instance.Projection;
            GraphicsManager.Instance.BoundingBoxEffect3D.World = Matrix.Identity;

            foreach (var pass in GraphicsManager.Instance.BoundingBoxEffect3D.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserIndexedPrimitives(PrimitiveType.LineList, vertexData, 0, 8, indices, 0, indices.Length / 2);
            }

            GraphicsDevice.DepthStencilState = previousDepthState;
        }

        private void DrawBoundingBox2D()
        {
            if (Constants.DRAW_BOUNDING_BOXES && IsMouseHover && _spriteBatch != null && _font != null)
            {
                Vector3 textPosition = new Vector3(
                    (BoundingBoxWorld.Min.X + BoundingBoxWorld.Max.X) / 2,
                    BoundingBoxWorld.Max.Y + 0.5f,
                    (BoundingBoxWorld.Min.Z + BoundingBoxWorld.Max.Z) / 2
                );

                Vector3 screenPos = GraphicsDevice.Viewport.Project(
                    textPosition,
                    Camera.Instance.Projection,
                    Camera.Instance.View,
                    Matrix.Identity
                );

                // Use StringBuilder to build the informational text efficiently
                var sb = new System.Text.StringBuilder();
                sb.AppendLine(GetType().Name);
                sb.Append("Type ID: ").AppendLine(Type.ToString());
                sb.Append("Alpha: ").AppendLine(TotalAlpha.ToString());
                sb.Append("X: ").Append(Position.X).Append(" Y: ").Append(Position.Y)
                  .Append(" Z: ").AppendLine(Position.Z.ToString());
                sb.Append("Depth: ").AppendLine(Depth.ToString());
                sb.Append("Render order: ").AppendLine(RenderOrder.ToString());
                sb.Append("DepthStencilState: ").Append(DepthState.Name);
                string objectInfo = sb.ToString();

                float baseFontSize = Constants.BASE_FONT_SIZE;
                float scaleFactor = DebugFontSize / baseFontSize;
                Vector2 textSize = _font.MeasureString(objectInfo) * scaleFactor;
                Vector2 baseTextPos = new Vector2((int)(screenPos.X - textSize.X / 2), (int)screenPos.Y);

                // Save current graphic states
                var previousBlendState = GraphicsDevice.BlendState;
                var previousDepthState = GraphicsDevice.DepthStencilState;
                var previousRasterizerState = GraphicsDevice.RasterizerState;

                try
                {
                    _spriteBatch.Begin(
                        SpriteSortMode.Deferred,
                        BlendState.AlphaBlend,
                        SamplerState.PointClamp,
                        DepthStencilState.None,
                        RasterizerState.CullNone,
                        null,
                        Matrix.Identity
                    );

                    // Draw text background
                    var backgroundColor = new Color(0, 0, 0, 180);
                    var backgroundRect = new Rectangle(
                        (int)baseTextPos.X - 5,
                        (int)baseTextPos.Y - 5,
                        (int)textSize.X + 10,
                        (int)textSize.Y + 10
                    );

                    DrawTextBackground(_spriteBatch, backgroundRect, backgroundColor);

                    // Draw the informational text
                    _spriteBatch.DrawString(_font, objectInfo, baseTextPos, Color.Yellow, 0f, Vector2.Zero, scaleFactor, SpriteEffects.None, 0);
                    _spriteBatch.End();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }

                // Restore previous graphic states
                GraphicsDevice.BlendState = previousBlendState;
                GraphicsDevice.DepthStencilState = previousDepthState;
                GraphicsDevice.RasterizerState = previousRasterizerState;
            }
        }

        private void DrawTextBackground(SpriteBatch spriteBatch, Rectangle rect, Color color)
        {
            if (_whiteTexture == null)
            {
                _whiteTexture = new Texture2D(GraphicsDevice, 1, 1);
                _whiteTexture.SetData([Color.White]);
            }
            spriteBatch.Draw(_whiteTexture, rect, color);

            var borderColor = Color.White * 0.3f;
            var borderRect = new Rectangle(rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2);
            spriteBatch.Draw(_whiteTexture, borderRect, borderColor);
        }

        private void UpdateWorldBoundingBox()
        {
            Vector3[] boundingBoxCorners = BoundingBoxLocal.GetCorners();
            Matrix worldPos = WorldPosition;

            // Transform all corners in one loop
            for (int i = 0; i < boundingBoxCorners.Length; i++)
            {
                boundingBoxCorners[i] = Vector3.Transform(boundingBoxCorners[i], worldPos);
            }

            BoundingBoxWorld = BoundingBox.CreateFromPoints(boundingBoxCorners);
        }
    }
}