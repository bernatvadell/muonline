// --- START OF FILE WorldControl.cs ---
using Client.Data.ATT;
using Client.Data.CAP;
using Client.Data.OBJS;
using Client.Main.Controllers;
using Client.Main.Models;
using Client.Main.Objects;
using Client.Main.Objects.Player;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic; // Potrzebne dla Dictionary
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Client.Main.Objects; // Potrzebne dla WalkerObject
using System.Linq; // Potrzebne dla OfType (nadal może być używane gdzie indziej)


namespace Client.Main.Controls
{

    sealed class WorldObjectDepthAsc : IComparer<WorldObject>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(WorldObject a, WorldObject b)
            => a.Depth.CompareTo(b.Depth);
    }

    sealed class WorldObjectDepthDesc : IComparer<WorldObject>
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(WorldObject a, WorldObject b)
            => b.Depth.CompareTo(a.Depth);
    }

    public abstract class WorldControl : GameControl
    {
        public string BackgroundMusicPath { get; set; }
        public TerrainControl Terrain { get; }
        public short WorldIndex { get; private set; }
        public ChildrenCollection<WorldObject> Objects { get; private set; } = new ChildrenCollection<WorldObject>(null);
        public Type[] MapTileObjects { get; } = new Type[Constants.TERRAIN_SIZE];
        private int renderCounter = 0;

        private readonly List<WorldObject> solidBehind = new List<WorldObject>();
        private readonly List<WorldObject> transparentObjects = new List<WorldObject>();
        private readonly List<WorldObject> solidInFront = new List<WorldObject>();

        private DepthStencilState _currentDepthState = DepthStencilState.Default;
        private readonly WorldObjectDepthAsc _cmpAsc = new();
        private readonly WorldObjectDepthDesc _cmpDesc = new();

        private static readonly DepthStencilState DepthStateDefault = DepthStencilState.Default;
        private static readonly DepthStencilState DepthStateDepthRead = DepthStencilState.DepthRead;

        private BoundingFrustum boundingFrustum;

        private readonly float cullingOffset = 800f;

        // **** RE-ADDED DICTIONARY ****
        // Słownik do szybkiego wyszukiwania WalkerObject po NetworkId
        protected Dictionary<ushort, WalkerObject> WalkerObjectsById { get; } = new Dictionary<ushort, WalkerObject>();
        // **** END RE-ADDED DICTIONARY ****

        public WorldControl(short worldIndex)
        {
            AutoViewSize = false;
            ViewSize = new(MuGame.Instance.Width, MuGame.Instance.Height);
            WorldIndex = worldIndex;
            Controls.Add(Terrain = new TerrainControl() { WorldIndex = worldIndex });
            Objects.ControlAdded += Object_Added; // Subskrybuj event

            Camera.Instance.CameraMoved += OnCameraMoved;

            UpdateBoundingFrustum();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetDepthState(DepthStencilState state)
        {
            if (_currentDepthState != state)
            {
                GraphicsDevice.DepthStencilState = state;
                _currentDepthState = state;
            }
        }

        private void OnCameraMoved(object sender, EventArgs e)
        {
            UpdateBoundingFrustum();
        }

        // **** RE-ADDED LOGIC TO Object_Added ****
        private void Object_Added(object sender, ChildrenEventArgs<WorldObject> e)
        {
            e.Control.World = this;
            // Dodaj do słownika, jeśli to WalkerObject z poprawnym ID
            if (e.Control is WalkerObject walker && walker.NetworkId != 0 && walker.NetworkId != 0xFFFF)
            {
                if (!WalkerObjectsById.TryAdd(walker.NetworkId, walker))
                {
                    Debug.WriteLine($"Warning: WalkerObject with NetworkId {walker.NetworkId:X4} already exists in WalkerObjectsById dictionary.");
                }
            }
        }
        // **** END RE-ADDED LOGIC ****

        public override async Task Load()
        {
            await base.Load();

            CreateMapTileObjects();

            var worldFolder = $"World{WorldIndex}";

            Camera.Instance.AspectRatio = GraphicsDevice.Viewport.AspectRatio;

            var tasks = new List<Task>();

            var objReader = new OBJReader();
            var objectPath = Path.Combine(Constants.DataPath, worldFolder, $"EncTerrain{WorldIndex}.obj");

            if (File.Exists(objectPath))
            {
                OBJ obj = await objReader.Load(objectPath);

                foreach (var mapObj in obj.Objects)
                {
                    var instance = WorldObjectFactory.CreateMapTileObject(this, mapObj);
                    if (instance != null) tasks.Add(instance.Load());
                }
            }

            await Task.WhenAll(tasks);

            var cameraAnglePositionPath = Path.Combine(Constants.DataPath, worldFolder, "Camera_Angle_Position.bmd");
            if (File.Exists(cameraAnglePositionPath))
            {
                var capReader = new CAPReader();
                var data = await capReader.Load(cameraAnglePositionPath);

                Camera.Instance.FOV = data.CameraFOV;
                Camera.Instance.Position = data.CameraPosition;
                Camera.Instance.Target = data.HeroPosition;
            }

            if (!string.IsNullOrEmpty(BackgroundMusicPath))
            {
                SoundController.Instance.PlayBackgroundMusic(BackgroundMusicPath);
            }
            else
            {
                SoundController.Instance.StopBackgroundMusic();
            }
        }

        public override void AfterLoad()
        {
            base.AfterLoad();

            SendToBack();
        }

        public override void Update(GameTime time)
        {
            base.Update(time);
            if (Status != GameControlStatus.Ready)
                return;

            // **** MODIFIED: Iterate over a copy ****
            // Stwórz kopię kolekcji, aby uniknąć problemów z modyfikacją podczas iteracji
            var objectsCopy = Objects.ToArray();
            foreach (var obj in objectsCopy)
            {
                // Sprawdź, czy obiekt nie został usunięty w międzyczasie (opcjonalne, ale bezpieczniejsze)
                // W tym konkretnym miejscu (Update) jest to mniej prawdopodobne niż w Draw,
                // ale dobra praktyka. Można by też sprawdzać Status obiektu.
                if (obj.Status != GameControlStatus.Disposed)
                {
                    obj.Update(time);
                }
            }
            // **** END MODIFIED ****
        }

        public override void Draw(GameTime time)
        {
            if (Status != GameControlStatus.Ready)
                return;

            base.Draw(time);
            RenderObjects(time);
        }

        public bool IsWalkable(Vector2 position)
        {
            var terrainFlag = Terrain.RequestTerrainFlag((int)position.X, (int)position.Y);
            return !terrainFlag.HasFlag(TWFlags.NoMove);
        }

        protected virtual void CreateMapTileObjects()
        {
            var typeMapObject = typeof(MapTileObject);

            for (var i = 0; i < MapTileObjects.Length; i++)
                MapTileObjects[i] = typeMapObject;
        }

        // **** RE-ADDED HELPER METHODS ****
        public virtual bool TryGetWalkerById(ushort networkId, out WalkerObject walker)
        {
            // 1) jeżeli to świat „walkable” i ID pasuje do lokalnego gracza
            if (this is WalkableWorldControl walkable &&
                walkable.Walker is { NetworkId: > 0 } hero &&
                hero.NetworkId == networkId)
            {
                walker = hero;
                return true;
            }

            // 2) słownik wszystkich pozostałych obiektów
            return WalkerObjectsById.TryGetValue(networkId, out walker);
        }

        public bool ContainsWalkerId(ushort networkId)
        {
            return WalkerObjectsById.ContainsKey(networkId);
        }

        // Metoda do usuwania obiektu - teraz aktualizuje też słownik
        public bool RemoveObject(WorldObject obj)
        {
            bool removed = Objects.Remove(obj); // Usuń z głównej listy
            if (removed && obj is WalkerObject walker && walker.NetworkId != 0 && walker.NetworkId != 0xFFFF)
            {
                WalkerObjectsById.Remove(walker.NetworkId); // Usuń ze słownika
            }
            return removed;
        }
        // **** END RE-ADDED HELPER METHODS ****

        private void RenderObjects(GameTime gameTime)
        {
            renderCounter = 0;

            solidBehind.Clear();
            transparentObjects.Clear();
            solidInFront.Clear();

            // **** MODIFIED: Iterate over a copy ****
            // Stwórz kopię kolekcji, aby uniknąć problemów z modyfikacją podczas iteracji
            var objectsCopy = Objects.ToArray();
            foreach (var obj in objectsCopy)
            {
                // Sprawdź, czy obiekt nie został usunięty lub ukryty w międzyczasie
                if (obj.Status == GameControlStatus.Disposed || !obj.Visible) continue;

                if (!IsObjectInView(obj)) continue;

                if (obj.IsTransparent)
                    transparentObjects.Add(obj);
                else if (obj.AffectedByTransparency)
                    solidBehind.Add(obj);
                else
                    solidInFront.Add(obj);
            }
            // **** END MODIFIED ****

            // --- Logika sortowania i rysowania (bez zmian) ---
            if (solidBehind.Count > 1) solidBehind.Sort(_cmpAsc);
            SetDepthState(DepthStateDefault);
            foreach (var obj in solidBehind)
            {
                obj.DepthState = DepthStateDefault;
                obj.Draw(gameTime);
                obj.RenderOrder = ++renderCounter;
            }

            if (transparentObjects.Count > 1) transparentObjects.Sort(_cmpDesc);
            if (transparentObjects.Count > 0)
                SetDepthState(DepthStateDepthRead);
            foreach (var obj in transparentObjects)
            {
                obj.DepthState = DepthStateDepthRead;
                obj.Draw(gameTime);
                obj.RenderOrder = ++renderCounter;
            }

            if (solidInFront.Count > 1) solidInFront.Sort(_cmpAsc);
            if (solidInFront.Count > 0)
                SetDepthState(DepthStateDefault);
            foreach (var obj in solidInFront)
            {
                obj.DepthState = DepthStateDefault;
                obj.Draw(gameTime);
                obj.RenderOrder = ++renderCounter;
            }

            // --- Rysowanie DrawAfter (bez zmian) ---
            if (solidBehind.Count > 0)
            {
                SetDepthState(DepthStateDefault);
                foreach (var obj in solidBehind) obj.DrawAfter(gameTime);
            }

            if (transparentObjects.Count > 0)
            {
                SetDepthState(DepthStateDepthRead);
                foreach (var obj in transparentObjects) obj.DrawAfter(gameTime);
            }

            if (solidInFront.Count > 0)
            {
                SetDepthState(DepthStateDefault);
                foreach (var obj in solidInFront) obj.DrawAfter(gameTime);
            }
        }

        private bool IsObjectInView(WorldObject obj)
        {
            // Sprawdzenie null dla obj i jego pozycji
            if (obj == null) return false;

            // Sprawdzenie null dla Camera.Instance
            if (Camera.Instance == null) return false;


            Vector2 cam2D = new(Camera.Instance.Position.X, Camera.Instance.Position.Y);
            // Użyj WorldPosition.Translation zamiast Position, bo Position może być lokalne
            Vector3 objPos = obj.WorldPosition.Translation;
            Vector2 obj2D = new(objPos.X, objPos.Y);

            float viewFarSq = (Camera.Instance.ViewFar + cullingOffset) * (Camera.Instance.ViewFar + cullingOffset);

            if (Vector2.DistanceSquared(cam2D, obj2D) > viewFarSq)
                return false;

            // Sprawdzenie null dla boundingFrustum
            if (boundingFrustum == null)
            {
                UpdateBoundingFrustum(); // Spróbuj zaktualizować, jeśli null
                if (boundingFrustum == null) return false; // Jeśli nadal null, nie można sprawdzić
            }

            // Użyj BoundingBoxWorld zamiast Position
            //return boundingFrustum.Contains(new BoundingSphere(objPos, cullingOffset)) != ContainmentType.Disjoint;
            return boundingFrustum.Contains(obj.BoundingBoxWorld) != ContainmentType.Disjoint; // Sprawdź BoundingBox
        }

        private void UpdateBoundingFrustum()
        {
            // Sprawdzenie null dla Camera.Instance
            if (Camera.Instance == null) return;

            Matrix view = Camera.Instance.View;
            Matrix projection = Camera.Instance.Projection;
            Matrix viewProjection = view * projection;
            boundingFrustum = new BoundingFrustum(viewProjection);
        }

        // **** RE-ADDED LOGIC TO Dispose ****
        public override void Dispose()
        {
            var sw = Stopwatch.StartNew();

            var objects = Objects.ToArray(); // Nadal potrzebujemy kopii do iteracji podczas usuwania

            for (var i = 0; i < objects.Length; i++)
            {
                var obj = objects[i];
                // Nie usuwaj lokalnego gracza
                if (this is WalkableWorldControl walkeableWorld && obj is PlayerObject player && walkeableWorld.Walker == player)
                    continue;

                RemoveObject(obj); // Usuń z listy i słownika
                obj.Dispose();
            }

            Objects.Clear(); // Upewnij się, że lista jest czysta
            WalkerObjectsById.Clear(); // Wyczyść słownik

            sw.Stop();
            var elapsedDisposingObjects = sw.ElapsedMilliseconds;
            sw.Restart();
            base.Dispose();
            sw.Stop();
            var elapsedDisposingBase = sw.ElapsedMilliseconds;

            Debug.WriteLine($"Dispose WorldControl {WorldIndex} - Disposing Objects: {elapsedDisposingObjects}ms - Disposing Base: {elapsedDisposingBase}ms");
        }
        // **** END RE-ADDED LOGIC ****
    }
}
// --- END OF FILE WorldControl.cs ---