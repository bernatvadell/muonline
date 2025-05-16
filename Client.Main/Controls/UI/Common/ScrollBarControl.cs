using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Client.Main.Controllers; // Dla GraphicsManager
using Client.Main.Helpers;    // Dla SpriteBatchScope
using System;
using Client.Main.Content;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI.Common
{
    public class ScrollBarControl : UIControl
    {
        private const int MIN_THUMB_HEIGHT = 20;
        public float ThumbHeightAdjustmentFactor { get; set; } = 1.0f;
        private const int SCROLL_BUTTON_VISUAL_WIDTH = 15; // Szerokość grafiki suwaka
        private const int SCROLL_TRACK_VISUAL_WIDTH = 7;   // Szerokość grafiki tła scrollbara
        private Texture2D _texScrollTop;
        private Texture2D _texScrollMiddle;
        private Texture2D _texScrollBottom;
        private Texture2D _texScrollThumb;

        private int _value;
        private int _max;
        private int _min;
        private float _thumbHeight;
        private float _thumbY;
        private bool _isDragging;
        private float _dragStartY;

        public int Value
        {
            get => _value;
            set
            {
                int clamped = Math.Clamp(value, _min, _max);
                if (_value != clamped)
                {
                    _value = clamped;
                    UpdateThumbPosition();
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public int Max
        {
            get => _max;
            set
            {
                _max = Math.Max(_min, value); // Max nie może być mniejsze niż Min
                Value = Math.Clamp(_value, _min, _max); // Upewnij się, że Value jest w nowym zakresie
                UpdateThumbPosition();
            }
        }
        public int Min { get => _min; set { _min = value; Max = Math.Max(_min, _max); Value = Math.Clamp(_value, _min, _max); UpdateThumbPosition(); } }
        public int Width { get; set; } = 15;
        public int Height { get; set; } = 100;

        public event EventHandler ValueChanged;

        public ScrollBarControl()
        {
            _min = 0;
            _max = 0;
            _value = 0;
            Interactive = true;
            Width = SCROLL_BUTTON_VISUAL_WIDTH; // Domyślna szerokość kontrolki to szerokość suwaka
            ViewSize = new Point(Width, Height);
        }

        public override async Task Load()
        {
            TextureLoader tl = TextureLoader.Instance;
            _texScrollTop = await tl.PrepareAndGetTexture("Interface/newui_scrollbar_up.tga") ?? GraphicsManager.Instance.Pixel;
            _texScrollMiddle = await tl.PrepareAndGetTexture("Interface/newui_scrollbar_m.tga") ?? GraphicsManager.Instance.Pixel;
            _texScrollBottom = await tl.PrepareAndGetTexture("Interface/newui_scrollbar_down.tga") ?? GraphicsManager.Instance.Pixel;
            _texScrollThumb = await tl.PrepareAndGetTexture("Interface/newui_scroll_on.tga") ?? GraphicsManager.Instance.Pixel;

            // Ustaw szerokość kontrolki na podstawie najszerszego elementu (suwaka)
            Width = _texScrollThumb?.Width ?? SCROLL_BUTTON_VISUAL_WIDTH;
            UpdateThumbPosition();
            await base.Load();
        }

        public override void Draw(GameTime gameTime)
        {
            if (!Visible) return; // Nie rysuj, jeśli nie ma co przewijać lub jest niewidoczny

            var spriteBatch = GraphicsManager.Instance.Sprite;
            Rectangle displayRect = DisplayRectangle;

            int topHeight = _texScrollTop?.Height ?? 3;
            int bottomHeight = _texScrollBottom?.Height ?? 3;
            int middleTextureHeight = _texScrollMiddle?.Height ?? 1;

            // Szerokość wizualna tła scrollbara
            int trackVisualWidth = _texScrollTop?.Width ?? SCROLL_TRACK_VISUAL_WIDTH;
            // Pozycja X tła scrollbara (może być wyśrodkowana w kontrolce lub wyrównana)
            int trackX = displayRect.X + (Width - trackVisualWidth) / 2;


            // Rysowanie tła scrollbara
            if (_texScrollTop != null)
                spriteBatch.Draw(_texScrollTop, new Rectangle(trackX, displayRect.Y, trackVisualWidth, topHeight), Color.White);

            if (_texScrollMiddle != null && middleTextureHeight > 0)
            {
                int middlePartY = displayRect.Y + topHeight;
                int middlePartHeight = displayRect.Height - topHeight - bottomHeight;
                if (middlePartHeight > 0)
                {
                    for (int yOffset = 0; yOffset < middlePartHeight; yOffset += middleTextureHeight)
                    {
                        int currentPartHeight = Math.Min(middleTextureHeight, middlePartHeight - yOffset);
                        Rectangle destRect = new Rectangle(trackX, middlePartY + yOffset, trackVisualWidth, currentPartHeight);
                        Rectangle sourceRect = new Rectangle(0, 0, _texScrollMiddle.Width, currentPartHeight);
                        spriteBatch.Draw(_texScrollMiddle, destRect, sourceRect, Color.White);
                    }
                }
            }

            if (_texScrollBottom != null)
                spriteBatch.Draw(_texScrollBottom, new Rectangle(trackX, displayRect.Y + displayRect.Height - bottomHeight, trackVisualWidth, bottomHeight), Color.White);

            // Rysowanie suwaka (thumb)
            if (_texScrollThumb != null && Max > Min)
            {
                int thumbActualHeight = (int)Math.Max(MIN_THUMB_HEIGHT, _thumbHeight);
                int thumbVisualWidth = _texScrollThumb?.Width ?? SCROLL_BUTTON_VISUAL_WIDTH;
                Rectangle thumbRect = new Rectangle(
                    displayRect.X + (Width - thumbVisualWidth) / 2, // Wyśrodkuj suwak w kontrolce
                    displayRect.Y + (int)_thumbY,
                    thumbVisualWidth,
                    thumbActualHeight);

                spriteBatch.Draw(_texScrollThumb, thumbRect, _isDragging ? Color.LightSlateGray : Color.White);
            }
        }

        private void UpdateThumbPosition()
        {
            ViewSize = new Point(Width, Height);

            int topTexH = _texScrollTop?.Height ?? 0;
            int botTexH = _texScrollBottom?.Height ?? 0;
            int actualTrackHeight = Height - topTexH - botTexH;

            if (actualTrackHeight <= 0 || _max <= _min || _itemsInListCount <= _maxVisibleItemsInList)
            {
                _thumbHeight = actualTrackHeight > 0 ? actualTrackHeight : MIN_THUMB_HEIGHT;
                _thumbY = topTexH;
                // Visible = false; // Decyzję o widoczności podejmujemy na podstawie _itemsInListCount vs _maxVisibleItemsInList
            }
            else
            {
                // Visible = true; // Pokaż, jeśli jest co przewijać
                float visibleRatio = (float)_maxVisibleItemsInList / Math.Max(1, _itemsInListCount);
                visibleRatio = Math.Clamp(visibleRatio, 0.05f, 1.0f);

                // === ZASTOSOWANIE WSPÓŁCZYNNIKA ===
                float calculatedThumbHeight = actualTrackHeight * visibleRatio * ThumbHeightAdjustmentFactor;
                // === KONIEC ZASTOSOWANIA WSPÓŁCZYNNIKA ===

                _thumbHeight = Math.Max(MIN_THUMB_HEIGHT, calculatedThumbHeight); // Upewnij się, że nie jest mniejszy niż minimum

                float scrollableTrackHeight = actualTrackHeight - _thumbHeight;
                float scrollRatio = 0f;
                if (_max > _min)
                {
                    scrollRatio = (float)(_value - _min) / (_max - _min);
                }

                _thumbY = topTexH + (scrollableTrackHeight * scrollRatio);
                _thumbY = Math.Clamp(_thumbY, topTexH, Height - botTexH - _thumbHeight);
            }

            // Decyzja o widoczności scrollbara na podstawie tego, czy jest co przewijać
            Visible = _itemsInListCount > _maxVisibleItemsInList && actualTrackHeight > MIN_THUMB_HEIGHT;
        }


        private int _maxVisibleItemsInList = 1;
        private int _itemsInListCount = 1;

        public void SetListMetrics(int maxVisibleInList, int totalItemsInList)
        {
            bool changed = _maxVisibleItemsInList != maxVisibleInList || _itemsInListCount != totalItemsInList;
            _maxVisibleItemsInList = Math.Max(1, maxVisibleInList);
            _itemsInListCount = Math.Max(1, totalItemsInList);
            // Zawsze aktualizuj pozycję suwaka po zmianie metryk,
            // ponieważ nawet jeśli proporcje się nie zmienią, to wartość Value mogła się zmienić
            // (np. jeśli lista się skróciła i Value jest teraz poza zakresem)
            UpdateThumbPosition();
        }

        public override void Update(GameTime gameTime)
        {
            if (!Visible) return;
            base.Update(gameTime);
            ViewSize = new Point(Width, Height);

            var mouse = MuGame.Instance.Mouse;
            var prevMouse = MuGame.Instance.PrevMouseState;
            Rectangle displayRect = DisplayRectangle;

            Rectangle thumbRect = new Rectangle(displayRect.X, displayRect.Y + (int)_thumbY, Width, (int)_thumbHeight);

            if (IsMouseOver)
            {
                if (mouse.LeftButton == ButtonState.Pressed)
                {
                    if (prevMouse.LeftButton == ButtonState.Released)
                    {
                        if (thumbRect.Contains(mouse.Position))
                        {
                            _isDragging = true;
                            _dragStartY = mouse.Y - _thumbY;
                        }
                        else
                        {
                            int trackClickY = mouse.Y - displayRect.Y - (_texScrollTop?.Height ?? 0);
                            int trackHeight = Height - (_texScrollTop?.Height ?? 0) - (_texScrollBottom?.Height ?? 0);
                            if (trackHeight > 0)
                            {
                                float clickRatio = Math.Clamp((float)trackClickY / trackHeight, 0f, 1f);
                                Value = _min + (int)(clickRatio * (_max - _min));
                            }
                        }
                    }
                    else if (_isDragging)
                    {
                        float newThumbY = mouse.Y - _dragStartY;
                        int trackHeight = Height - (_texScrollTop?.Height ?? 0) - (_texScrollBottom?.Height ?? 0) - (int)_thumbHeight;
                        if (trackHeight > 0)
                        {
                            float scrollRatio = Math.Clamp((newThumbY - (_texScrollTop?.Height ?? 0)) / trackHeight, 0f, 1f);
                            Value = _min + (int)(scrollRatio * (_max - _min));
                        }
                    }
                }
                else
                {
                    _isDragging = false;
                }
            }
            else
            {
                if (_isDragging && mouse.LeftButton == ButtonState.Released) _isDragging = false;
            }
        }

        public override bool ProcessMouseScroll(int scrollDelta)
        {
            if (!Visible || !IsMouseOver) // only process if visible and mouse is over this control
                return false;

            Value -= scrollDelta; // scrollDelta: positive for up, negative for down
            return true; // scroll was handled
        }
    }
}