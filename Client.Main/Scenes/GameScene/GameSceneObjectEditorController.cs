using System.Collections.Generic;
using System.Linq;
using Client.Main.Controls;
using Client.Main.Controls.UI.Game;
using Client.Main.Models;
using Client.Main.Objects;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;

namespace Client.Main.Scenes
{
    internal sealed class GameSceneObjectEditorController
    {
        private readonly GameScene _scene;
        private readonly ILogger _logger;
        private BlendingEditorControl _blendingEditor;
        private ObjectSelectionDialog _blendingObjectSelectionDialog;
        private ObjectSelectionDialog _deletionObjectSelectionDialog;

        public GameSceneObjectEditorController(GameScene scene, ILogger logger)
        {
            _scene = scene;
            _logger = logger;
        }

        public void Initialize()
        {
#if DEBUG
            if (_blendingEditor == null)
            {
                _blendingEditor = new BlendingEditorControl();
                _scene.Controls.Add(_blendingEditor);
            }
#endif
        }

        public void HandleBlendingEditorActivation()
        {
            if (_scene.World == null || _scene.World.Status != GameControlStatus.Ready || !Constants.DRAW_BOUNDING_BOXES)
                return;

            var hoveredObjects = new List<ModelObject>();
            foreach (var obj in _scene.World.Objects)
            {
                if (obj is ModelObject modelObj && modelObj.IsMouseHover)
                {
                    hoveredObjects.Add(modelObj);
                }
            }

            if (hoveredObjects.Count == 0)
                return;

            if (hoveredObjects.Count == 1)
            {
                ShowBlendingEditor(hoveredObjects[0]);
            }
            else
            {
                ShowObjectSelectionDialog(hoveredObjects);
            }
        }

        public void HandleObjectDeletion()
        {
            if (_scene.World == null || _scene.World.Status != GameControlStatus.Ready || !Constants.DRAW_BOUNDING_BOXES)
                return;

            var hoveredObjects = new List<WorldObject>();
            foreach (var obj in _scene.World.Objects)
            {
                if (obj.IsMouseHover)
                {
                    hoveredObjects.Add(obj);
                }
            }

            if (hoveredObjects.Count == 0)
                return;

            if (hoveredObjects.Count == 1)
            {
                DeleteObject(hoveredObjects[0]);
            }
            else
            {
                ShowObjectSelectionDialogForDeletion(hoveredObjects);
            }
        }

        private void ShowBlendingEditor(ModelObject targetObject)
        {
            if (_blendingEditor == null)
                return;

            _blendingEditor.ShowForObject(targetObject);
            _logger?.LogDebug($"Opening blending editor for {targetObject.GetType().Name}");
        }

        private void ShowObjectSelectionDialog(List<ModelObject> objects)
        {
            if (_blendingObjectSelectionDialog != null)
            {
                _scene.Controls.Remove(_blendingObjectSelectionDialog);
                _blendingObjectSelectionDialog = null;
            }

            _blendingObjectSelectionDialog = new ObjectSelectionDialog(objects.Cast<WorldObject>().ToList());
            _blendingObjectSelectionDialog.ObjectSelected += OnObjectSelectedForEditing;
            _scene.Controls.Add(_blendingObjectSelectionDialog);

            var mousePos = MuGame.Instance.UiMouseState.Position;
            _blendingObjectSelectionDialog.ShowAt(mousePos.X + 10, mousePos.Y + 10);

            _logger?.LogDebug($"Showing selection dialog for {objects.Count} objects");
        }

        private void OnObjectSelectedForEditing(WorldObject selectedObject)
        {
            if (selectedObject is ModelObject modelObj)
            {
                ShowBlendingEditor(modelObj);
            }

            if (_blendingObjectSelectionDialog != null)
            {
                _scene.Controls.Remove(_blendingObjectSelectionDialog);
                _blendingObjectSelectionDialog = null;
            }
        }

        private void DeleteObject(WorldObject targetObject)
        {
            if (targetObject == null)
                return;

            _logger?.LogDebug($"Deleting object: {targetObject.GetType().Name} (ID: {targetObject.NetworkId})");

            _scene.World.RemoveObject(targetObject);
            targetObject.Dispose();
        }

        private void ShowObjectSelectionDialogForDeletion(List<WorldObject> objects)
        {
            if (_deletionObjectSelectionDialog != null)
            {
                _scene.Controls.Remove(_deletionObjectSelectionDialog);
                _deletionObjectSelectionDialog = null;
            }

            _deletionObjectSelectionDialog = new ObjectSelectionDialog(objects);
            _deletionObjectSelectionDialog.ObjectSelected += OnObjectSelectedForDeletion;
            _scene.Controls.Add(_deletionObjectSelectionDialog);

            var mousePos = MuGame.Instance.UiMouseState.Position;
            _deletionObjectSelectionDialog.ShowAt(mousePos.X + 10, mousePos.Y + 10);

            _logger?.LogDebug($"Showing deletion selection dialog for {objects.Count} objects");
        }

        private void OnObjectSelectedForDeletion(WorldObject selectedObject)
        {
            DeleteObject(selectedObject);

            if (_deletionObjectSelectionDialog != null)
            {
                _scene.Controls.Remove(_deletionObjectSelectionDialog);
                _deletionObjectSelectionDialog = null;
            }
        }
    }
}
