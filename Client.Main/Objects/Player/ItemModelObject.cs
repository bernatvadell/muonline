// <file path="Client.Main/Objects/Player/ItemModelObject.cs">
using Client.Main.Content;
using Client.Main.Controls.UI.Game.Inventory;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using System;
using System.Threading.Tasks;

namespace Client.Main.Objects.Player
{
    /// <summary>
    /// 3-D visualization of an item in the inventory window.
    /// Everything loaded synchronously in the game thread,
    /// with extensive logging of progress and errors.
    /// </summary>
    public class ItemModelObject : ModelObject
    {
        private readonly ItemDefinition _definition;
        private static new readonly ILogger<ItemModelObject> _logger =
            AppLoggerFactory?.CreateLogger<ItemModelObject>();

        public ItemModelObject(ItemDefinition definition)
        {
            _definition = definition;

            // Default settings for objects in inventory
            LinkParentAnimation = false;
            IsTransparent = false;
        }

        /// <summary>
        /// Loads the model:
        /// 1) I/O (reading *.bmd* from disk),
        /// 2) creating GPU resources (main thread).
        /// </summary>
        public override Task Load()
        {
            // --- Data validation ------------------------------------------------
            if (_definition == null)
            {
                _logger?.LogWarning("[ITEM] No definition - Load interrupted.");
                Status = Models.GameControlStatus.Error;
                return Task.CompletedTask;
            }

            if (string.IsNullOrWhiteSpace(_definition.TexturePath))
            {
                _logger?.LogWarning(
                    "[ITEM] \"{Name}\" ({Id}) has no TexturePath set.",
                    _definition.Name, _definition.Id);

                Status = Models.GameControlStatus.Error;
                return Task.CompletedTask;
            }

            // --- Model loading + GPU resources ----------------------------------
            try
            {
                // 1) I/O - in the same thread (no risk of GPU collision)
                var bmd = BMDLoader.Instance
                                   .Prepare(_definition.TexturePath)
                                   .GetAwaiter()
                                   .GetResult();

                if (bmd == null)
                {
                    _logger?.LogError(
                        "[ITEM] BMD not found \"{Path}\".",
                        _definition.TexturePath);

                    Status = Models.GameControlStatus.Error;
                    return Task.CompletedTask;
                }

                Model = bmd;

                // 2) GPU - must be called in thread with GraphicsDevice
                base.LoadContent()
                    .GetAwaiter()
                    .GetResult();

                Status = Models.GameControlStatus.Ready;
                _logger?.LogDebug(
                    "[ITEM] \"{Name}\" ({Id}) loaded OK.",
                    _definition.Name, _definition.Id);
            }
            catch (Exception ex)
            {
                _logger?.LogError(
                    ex,
                    "[ITEM] Error while loading \"{Name}\" ({Id}).",
                    _definition?.Name,
                    _definition?.Id);

                Status = Models.GameControlStatus.Error;
            }

            return Task.CompletedTask;
        }

        public override void Update(GameTime gameTime)
        {
            // Gentle rotation for "rotating diamond" effect
            Angle = new Vector3(Angle.X,
                                Angle.Y + 0.01f,
                                Angle.Z);

            base.Update(gameTime);
        }
    }
}