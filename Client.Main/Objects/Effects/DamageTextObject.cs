// W folderze Objects/Effects lub podobnym
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Threading.Tasks;
using Client.Main.Controllers; // Dla GraphicsManager
using Client.Main.Models;
using System;
using System.Diagnostics;     // Dla GameControlStatus

namespace Client.Main.Objects.Effects
{
    public class DamageTextObject : WorldObject
    {
        public string Text { get; }
        public Color TextColor { get; }

        private float _lifetime = 1.2f; // Czas życia w sekundach (np. 1.2 sekundy)
        private float _elapsedTime = 0f;
        private Vector2 _screenPosition;
        private float _verticalSpeed = 40f; // Prędkość unoszenia w pikselach na sekundę (ujemna = w górę)
        private float _initialZOffset = 40f; // Początkowe przesunięcie w górę nad pozycją trafienia

        // Konstruktor
        public DamageTextObject(string text, Vector3 worldHitPosition, Color color)
        {
            Text = text;
            // Ustaw początkową pozycję nieco nad miejscem trafienia
            Position = worldHitPosition + Vector3.UnitZ * _initialZOffset;
            TextColor = color;
            Alpha = 1.0f; // Start w pełni widoczny
            Scale = 1.0f; // Domyślna skala, można dostosować
            IsTransparent = true; // Ważne dla poprawnego sortowania przez WorldControl
            AffectedByTransparency = false; // Nie powinien być sortowany jako solidny obiekt
            Status = GameControlStatus.Ready; // Oznaczamy jako gotowy od razu
        }

        // Load nie jest potrzebny, jeśli nie ładujemy specyficznych zasobów
        public override Task Load()
        {
            Status = GameControlStatus.Ready;
            return Task.CompletedTask;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (Status != GameControlStatus.Ready) return;

            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _elapsedTime += deltaTime;

            // Zanikanie (Fade out)
            float fadeStartTime = _lifetime * 0.4f;
            if (_elapsedTime > fadeStartTime)
            {
                Alpha = MathHelper.Clamp(1.0f - (_elapsedTime - fadeStartTime) / (_lifetime - fadeStartTime), 0f, 1f);
            }

            // Unoszenie się w górę
            Position += Vector3.UnitZ * _verticalSpeed * deltaTime;
            RecalculateWorldPosition(); // Aktualizuj WorldPosition

            // Sprawdzenie końca czasu życia
            if (_elapsedTime >= _lifetime)
            {
                World?.RemoveObject(this);
                Dispose();
                return;
            }

            // Oblicz pozycję na ekranie dla DrawAfter
            Vector3 screenPos3D = GraphicsDevice.Viewport.Project(
                WorldPosition.Translation,
                Camera.Instance.Projection,
                Camera.Instance.View,
                Matrix.Identity);

            // Ukryj, jeśli za kamerą lub za daleko (projekcja nieudana)
            // Ustawiamy flagę Hidden, która jest sprawdzana przez standardową właściwość Visible
            if (screenPos3D.Z < 0 || screenPos3D.Z > 1)
            {
                Hidden = true;
            }
            else
            {
                Hidden = false; // Upewnij się, że resetujesz Hidden, gdy jest widoczny
                _screenPosition = new Vector2(screenPos3D.X, screenPos3D.Y);
            }

            // Logowanie dla debugowania (opcjonalne)
            //Debug.WriteLine($"DamageText Update: ID {this.GetHashCode()}, Elapsed: {_elapsedTime:F2}, Alpha: {Alpha:F2}, Hidden: {Hidden}, ScreenPos: {_screenPosition}");
        }

        // Draw nie rysuje nic w 3D
        public override void Draw(GameTime gameTime)
        {
            base.Draw(gameTime); // Puste lub pominięte
        }

        // Rysowanie tekstu odbywa się w DrawAfter
        public override void DrawAfter(GameTime gameTime)
        {
            base.DrawAfter(gameTime); // Puste lub pominięte
            //Debug.WriteLine($"--- DamageText DrawAfter CALLED: ID {this.GetHashCode()}, Visible: {Visible} ---");
            // Używamy właściwości Visible z WorldObject, która sprawdza Status, Hidden i OutOfView
            if (!Visible) return;

            var spriteBatch = GraphicsManager.Instance.Sprite;
            var font = GraphicsManager.Instance.Font;

            // Sprawdzenie null dla pewności
            if (spriteBatch == null || font == null) return;

            // Ustawienia rysowania tekstu
            float fontSize = 14f; // Rozmiar czcionki
            float scale = fontSize / Constants.BASE_FONT_SIZE; // Skala na podstawie bazowego rozmiaru
            Vector2 origin = font.MeasureString(Text) * 0.5f; // Wyśrodkowanie tekstu
            Color color = TextColor * Alpha; // Zastosuj przezroczystość zanikania

            try
            {
                // Używamy Deferred, bo DrawAfter jest zwykle na końcu
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);

                // Rysuj tekst
                //Debug.WriteLine($"Drawing DamageText: Text='{Text}', Pos={_screenPosition}, Color={color}, Scale={scale}, Alpha={Alpha}");
                spriteBatch.DrawString(font, Text, _screenPosition, color, 0f, origin, scale, SpriteEffects.None, 0f);

                spriteBatch.End();
            }
            catch (Exception ex)
            {
                // Logowanie błędu rysowania, jeśli wystąpi
                // _logger?.LogError(ex, "Error drawing DamageTextObject"); // Potrzebowałbyś ILogger w tej klasie
                //Debug.WriteLine($"Error drawing DamageTextObject: {ex.Message}");
            }
            finally
            {
                // Upewnij się, że stany renderowania są resetowane, jeśli Begin/End je zmieniają
                // Chociaż główna pętla renderowania powinna to robić.
                GraphicsDevice.BlendState = BlendState.Opaque;
                GraphicsDevice.DepthStencilState = DepthStencilState.Default;
                GraphicsDevice.RasterizerState = RasterizerState.CullNone;
                GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;
            }
        }
    }
}