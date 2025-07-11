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
    /// 3-D wizualizacja przedmiotu w oknie inwentarza.
    /// Całość ładowana synchronicznie w wątku gry,
    /// z rozbudowanym logowaniem przebiegu i błędów.
    /// </summary>
    public class ItemModelObject : ModelObject
    {
        private readonly ItemDefinition _definition;
        private static new readonly ILogger<ItemModelObject> _logger =
            AppLoggerFactory?.CreateLogger<ItemModelObject>();

        public ItemModelObject(ItemDefinition definition)
        {
            _definition = definition;

            // Ustawienia domyślne dla obiektów w inventory
            LinkParentAnimation = false;
            IsTransparent = false;
        }

        /// <summary>
        /// Ładuje model:
        /// 1) I/O (odczyt *.bmd* z dysku),
        /// 2) tworzenie zasobów GPU (wątek główny).
        /// </summary>
        public override Task Load()
        {
            // --- Walidacja danych ------------------------------------------------
            if (_definition == null)
            {
                _logger?.LogWarning("[ITEM] Brak definicji – Load przerwane.");
                Status = Models.GameControlStatus.Error;
                return Task.CompletedTask;
            }

            if (string.IsNullOrWhiteSpace(_definition.TexturePath))
            {
                _logger?.LogWarning(
                    "[ITEM] „{Name}” ({Id}) nie ma ustawionej TexturePath.",
                    _definition.Name, _definition.Id);

                Status = Models.GameControlStatus.Error;
                return Task.CompletedTask;
            }

            // --- Ładowanie modelu + zasoby GPU ----------------------------------
            try
            {
                // 1) I/O – w tym samym wątku (brak ryzyka kolizji z GPU)
                var bmd = BMDLoader.Instance
                                   .Prepare(_definition.TexturePath)
                                   .GetAwaiter()
                                   .GetResult();

                if (bmd == null)
                {
                    _logger?.LogError(
                        "[ITEM] Nie znaleziono BMD „{Path}”.",
                        _definition.TexturePath);

                    Status = Models.GameControlStatus.Error;
                    return Task.CompletedTask;
                }

                Model = bmd;

                // 2) GPU – musi być wywołane w wątku z GraphicsDevice
                base.LoadContent()
                    .GetAwaiter()
                    .GetResult();

                Status = Models.GameControlStatus.Ready;
                _logger?.LogDebug(
                    "[ITEM] „{Name}” ({Id}) załadowany OK.",
                    _definition.Name, _definition.Id);
            }
            catch (Exception ex)
            {
                _logger?.LogError(
                    ex,
                    "[ITEM] Błąd podczas ładowania „{Name}” ({Id}).",
                    _definition?.Name,
                    _definition?.Id);

                Status = Models.GameControlStatus.Error;
            }

            return Task.CompletedTask;
        }

        public override void Update(GameTime gameTime)
        {
            // Delikatna rotacja dla efektu „obracającego się diamentu”
            Angle = new Vector3(Angle.X,
                                Angle.Y + 0.01f,
                                Angle.Z);

            base.Update(gameTime);
        }
    }
}