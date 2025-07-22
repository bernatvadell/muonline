// Client.Main/Objects/WorldObject.cs

using Client.Main.Controllers;
using Client.Main.Controls;
using Client.Main.Helpers;
using Client.Main.Models;
using Client.Main.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Extensions.Logging;
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

        private ILogger _logger = ModelObject.AppLoggerFactory?.CreateLogger<WorldObject>();

        public virtual float Depth
        {
            get => Position.Y + Position.Z;
        }
        public virtual bool AffectedByTransparency { get; set; } = true;
        public virtual bool IsTransparent { get; set; } = false;
        public int RenderOrder { get; set; }
        public DepthStencilState DepthState { get; set; } = DepthStencilState.Default;

        private SpriteFont _font;
        private Texture2D _whiteTexture;
        private float _cullingCheckTimer = 0;
        private const float CullingCheckInterval = 0.1f; // Check culling every 100ms instead of every frame
        
        // Advanced update optimization for invisible objects
        private float _lowPriorityUpdateTimer = 0;
        private const float LowPriorityUpdateInterval = 0.25f; // Update invisible objects every 250ms
        private const float FarObjectUpdateInterval = 0.5f; // Update very far objects every 500ms
        private float _lastDistanceToCamera = float.MaxValue;
        private bool _wasOutOfView = true;
        private const int MaxSkipFrames = 15; // Skip up to 15 frames for very distant objects
        
        // Static frame counter for staggered updates
        private static int _globalFrameCounter = 0;
        private readonly int _updateOffset; // Unique offset for each object to stagger updates
        
        // Debug counters
        public static int TotalSkippedUpdates { get; private set; } = 0;
        public static int TotalUpdatesPerformed { get; private set; } = 0;
        private static int _lastResetTime = Environment.TickCount;
        
        public static string GetOptimizationStats()
        {
            int total = TotalSkippedUpdates + TotalUpdatesPerformed;
            if (total == 0) return "No updates tracked yet";
            
            float skipPercentage = (TotalSkippedUpdates / (float)total) * 100f;
            return $"Updates: {TotalUpdatesPerformed}, Skipped: {TotalSkippedUpdates} ({skipPercentage:F1}%)";
        }

        public bool LinkParentAnimation { get; set; }
        public bool OutOfView { get; private set; } = true;
        public ChildrenCollection<WorldObject> Children { get; private set; }
        public WorldObject Parent { get => _parent; set { var prev = _parent; _parent = value; OnParentChanged(value, prev); } }

        public BoundingBox BoundingBoxLocal { get => _boundingBoxLocal; set { _boundingBoxLocal = value; OnBoundingBoxLocalChanged(); } }
        public BoundingBox BoundingBoxWorld { get; protected set; }

        public GameControlStatus Status { get; protected set; } = GameControlStatus.NonInitialized;
        public bool Hidden { get; set; }
        public string ObjectName => GetType().Name;
        public virtual string DisplayName => ObjectName;
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
        /// <summary>
        /// Indicates that the object is far from the camera and should be rendered in lower quality.
        /// </summary>
        public bool LowQuality { get; private set; }
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

            _font = GraphicsManager.Instance.Font;
            
            // Initialize update offset for staggered updates - spread objects across frames
            _updateOffset = GetHashCode() % 60; // Spread across ~1 second at 60fps
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
                _logger?.LogDebug(e, "Exception in WorldObject");
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
                Load().ConfigureAwait(false);
            }
            if (Status != GameControlStatus.Ready) return;

            // Increment global frame counter for staggered updates
            _globalFrameCounter++;

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            // Update OutOfView flag with intelligent frequency based on object state
            bool shouldCheckCulling = false;
            if (World != null)
            {
                _cullingCheckTimer += deltaTime;
                
                // Adjust culling check frequency based on object state
                float checkInterval = _wasOutOfView ? CullingCheckInterval * 2f : CullingCheckInterval;
                if (_cullingCheckTimer >= checkInterval)
                {
                    shouldCheckCulling = true;
                }
            }

            if (shouldCheckCulling)
            {
                _cullingCheckTimer = 0;
                _wasOutOfView = OutOfView;
                OutOfView = World != null && !World.IsObjectInView(this);

                // If object was just marked as out of view, give it another chance soon
                if (!_wasOutOfView && OutOfView)
                {
                    _cullingCheckTimer = CullingCheckInterval - 0.016f; // Check again in ~1 frame
                }
            }

            // AGGRESSIVE: Skip most updates for invisible objects
            if (OutOfView)
            {
                _lowPriorityUpdateTimer += deltaTime;
                
                // Much more aggressive - update invisible objects only every 1 second!
                if (_lowPriorityUpdateTimer < 1.0f && _globalFrameCounter % 60 != (_updateOffset % 60))
                {
                    TotalSkippedUpdates++;
                    return; // Skip this frame entirely for invisible objects - VERY aggressive
                }
                
                _lowPriorityUpdateTimer = 0;

                // Only update critical children for invisible objects
                for (int i = 0; i < Children.Count; i++)
                {
                    var child = Children[i];
                    // Only update if it's a player or monster - skip everything else
                    if (child is Player.PlayerObject || child is MonsterObject)
                    {
                        child.Update(gameTime);
                    }
                }
                return;
            }

            // Reset low priority timer when object becomes visible
            _lowPriorityUpdateTimer = 0;

            // Simplified distance-based optimization for visible objects
            float distanceToCamera = float.MaxValue;
            if (World != null && Camera.Instance != null)
            {
                distanceToCamera = Vector3.Distance(Camera.Instance.Position, WorldPosition.Translation);
                _lastDistanceToCamera = distanceToCamera;

                // AGGRESSIVE: Skip every other frame for very distant visible objects
                if (distanceToCamera > Constants.LOW_QUALITY_DISTANCE * 2f)
                {
                    if (_globalFrameCounter % 2 != (_updateOffset % 2))
                    {
                        TotalSkippedUpdates++;
                        return; // Skip every other frame for distant objects
                    }
                }
            }

            // Full update for all visible objects (simplified)
            PerformFullUpdate(gameTime, distanceToCamera);
        }

        private void UpdateChildrenSelectively(GameTime gameTime)
        {
            // Only update children that are likely to be important (players, animated objects, etc.)
            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                
                // Always update players and important objects
                if (child is Player.PlayerObject || 
                    child is MonsterObject ||
                    child.Interactive ||
                    !child.OutOfView)
                {
                    child.Update(gameTime);
                }
                // For other children, use staggered updates
                else if (((_globalFrameCounter + child._updateOffset) % (MaxSkipFrames * 2)) == 0)
                {
                    child.Update(gameTime);
                }
            }
        }

        private void PerformFullUpdate(GameTime gameTime, float distanceToCamera)
        {
            TotalUpdatesPerformed++;
            
            // Reset debug counters every 5 seconds
            if (Environment.TickCount - _lastResetTime > 5000)
            {
                _lastResetTime = Environment.TickCount;
                // log these values or display them in debug UI
                //Console.WriteLine($"WorldObject Optimization: {TotalSkippedUpdates} skipped, {TotalUpdatesPerformed} performed");
                TotalSkippedUpdates = 0;
                TotalUpdatesPerformed = 0;
            }
            // Determine whether the object should be rendered in low quality based on distance to the camera
            if (World != null)
            {
                bool isLoginScene = World.Scene is LoginScene;
                if (!Constants.ENABLE_LOW_QUALITY_SWITCH ||
                    (isLoginScene && !Constants.ENABLE_LOW_QUALITY_IN_LOGIN_SCENE))
                {
                    LowQuality = false;
                }
                else
                {
                    LowQuality = distanceToCamera > Constants.LOW_QUALITY_DISTANCE;
                }
            }
            else
            {
                LowQuality = false;
            }

            // Mouse hover detection optimization - skip for very distant objects
            bool shouldCheckMouseHover = distanceToCamera < Constants.LOW_QUALITY_DISTANCE * 3f;
            
            if (shouldCheckMouseHover)
            {
                // Determine if UI should block hover detection for world objects
                bool uiBlockingHover = false;
                if (World?.Scene != null)
                {
                    var scene = World.Scene;
                    if (scene.MouseHoverControl != null && scene.MouseHoverControl != scene.World)
                    {
                        uiBlockingHover = true; // a UI element is hovered, ignore world hover
                    }
                }

                // Cache parent's mouse hover state
                bool parentIsMouseHover = Parent?.IsMouseHover ?? false;

                // Only calculate intersections if needed and not blocked by UI
                bool wouldBeMouseHover = parentIsMouseHover;
                if (!parentIsMouseHover && !uiBlockingHover && (Interactive || Constants.DRAW_BOUNDING_BOXES))
                {
                    float? intersectionDistance = MuGame.Instance.MouseRay.Intersects(BoundingBoxWorld);
                    ContainmentType contains = BoundingBoxWorld.Contains(MuGame.Instance.MouseRay.Position);
                    wouldBeMouseHover = intersectionDistance.HasValue || contains == ContainmentType.Contains;
                }

                IsMouseHover = !uiBlockingHover && wouldBeMouseHover;

                if (!parentIsMouseHover && IsMouseHover)
                    World.Scene.MouseHoverObject = this;
            }
            else
            {
                IsMouseHover = false; // Distant objects can't be hovered
            }

            // Update all children for visible objects
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
            DrawHoverName();

            // Avoid enumeration overhead
            int count = Children.Count;
            for (int i = 0; i < count; i++)
                Children[i].DrawAfter(gameTime);
        }

        /// <summary>
        /// Draws the object's <see cref="DisplayName"/> above it when hovered.
        /// </summary>
        public virtual void DrawHoverName()
        {
            if (_font == null)
                _font = GraphicsManager.Instance.Font;

            if (!Constants.SHOW_NAMES_ON_HOVER || !IsMouseHover || _font == null)
                return;

            // Limit name display to player, monster and NPC entities
            if (this is not Player.PlayerObject &&
                this is not MonsterObject &&
                this is not NPCObject)
                return;

            string name = DisplayName;
            if (string.IsNullOrEmpty(name))
                return;

            Vector3 anchor = new((BoundingBoxWorld.Min.X + BoundingBoxWorld.Max.X) * 0.5f,
                (BoundingBoxWorld.Min.Y + BoundingBoxWorld.Max.Y) * 0.5f,
                BoundingBoxWorld.Max.Z + 20f);

            Vector3 screen = GraphicsDevice.Viewport.Project(
                anchor,
                Camera.Instance.Projection,
                Camera.Instance.View,
                Matrix.Identity);

            if (screen.Z < 0f || screen.Z > 1f)
                return;

            const float scale = 0.5f;
            Vector2 size = _font.MeasureString(name) * scale;
            var sb = GraphicsManager.Instance.Sprite;

            void draw() => sb.DrawString(_font, name,
                new Vector2(screen.X - size.X * 0.5f, screen.Y - size.Y),
                Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, screen.Z);

            if (!SpriteBatchScope.BatchIsBegun)
            {
                using (new SpriteBatchScope(sb, SpriteSortMode.BackToFront, BlendState.NonPremultiplied, SamplerState.PointClamp, DepthStencilState.DepthRead))
                {
                    draw();
                }
            }
            else
            {
                draw();
            }
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

        protected void DrawBoundingBox3D()
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

        public void DrawBoundingBox2D()
        {
            if (!(Constants.DRAW_BOUNDING_BOXES && IsMouseHover && _font != null))
                return;

            // Build the info string and compute positions as before...
            var sbInfo = new System.Text.StringBuilder();
            sbInfo.AppendLine(GetType().Name);
            sbInfo.Append("Type ID: ").AppendLine(Type.ToString());
            sbInfo.Append("Alpha: ").AppendLine(TotalAlpha.ToString());
            sbInfo.Append("X: ").Append(Position.X).Append(" Y: ").Append(Position.Y)
                  .Append(" Z: ").AppendLine(Position.Z.ToString());
            sbInfo.Append("Depth: ").AppendLine(Depth.ToString());
            sbInfo.Append("Render order: ").AppendLine(RenderOrder.ToString());
            sbInfo.Append("DepthStencilState: ").Append(DepthState.Name);
            string objectInfo = sbInfo.ToString();

            float scaleFactor = DebugFontSize / Constants.BASE_FONT_SIZE;
            Vector2 textSize = _font.MeasureString(objectInfo) * scaleFactor;
            Vector2 baseTextPos = new Vector2(
                (int)(GraphicsDevice.Viewport.Project(
                    new Vector3(
                        (BoundingBoxWorld.Min.X + BoundingBoxWorld.Max.X) / 2,
                        BoundingBoxWorld.Max.Y + 0.5f,
                        (BoundingBoxWorld.Min.Z + BoundingBoxWorld.Max.Z) / 2),
                    Camera.Instance.Projection,
                    Camera.Instance.View,
                    Matrix.Identity
                ).X - textSize.X / 2),
                (int)GraphicsDevice.Viewport.Project(
                    new Vector3(
                        (BoundingBoxWorld.Min.X + BoundingBoxWorld.Max.X) / 2,
                        BoundingBoxWorld.Max.Y + 0.5f,
                        (BoundingBoxWorld.Min.Z + BoundingBoxWorld.Max.Z) / 2),
                    Camera.Instance.Projection,
                    Camera.Instance.View,
                    Matrix.Identity
                ).Y
            );

            // Save previous states
            var prevBlend = GraphicsDevice.BlendState;
            var prevDepth = GraphicsDevice.DepthStencilState;
            var prevRaster = GraphicsDevice.RasterizerState;

            var sb = GraphicsManager.Instance.Sprite;
            using (new SpriteBatchScope(
                sb,
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.PointClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                effect: null,
                transform: Matrix.Identity))
            {
                // Background
                var bgColor = new Color(0, 0, 0, 180);
                var bgRect = new Rectangle(
                    (int)baseTextPos.X - 5,
                    (int)baseTextPos.Y - 5,
                    (int)textSize.X + 10,
                    (int)textSize.Y + 10);
                DrawTextBackground(sb, bgRect, bgColor);

                // Text
                sb.DrawString(
                    _font,
                    objectInfo,
                    baseTextPos,
                    Color.Yellow,
                    0f,
                    Vector2.Zero,
                    scaleFactor,
                    SpriteEffects.None,
                    0f);
            }

            // Restore previous GPU states
            GraphicsDevice.BlendState = prevBlend;
            GraphicsDevice.DepthStencilState = prevDepth;
            GraphicsDevice.RasterizerState = prevRaster;
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

        protected virtual void UpdateWorldBoundingBox()
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

        public virtual ushort NetworkId { get; protected set; }
    }
}