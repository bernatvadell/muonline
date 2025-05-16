using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Client.Main.Controls.UI
{
    public class ChatLogWindow : UIControl
    {
        public const int CHATLOG_WIDTH = 281;
        private const int MAX_TOTAL_MESSAGES = 200;
        private const int MAX_MESSAGES_PER_TYPE = 200;
        private const int DEFAULT_SHOWING_LINES = 6;
        private const int MIN_SHOWING_LINES = 3;
        private const int MAX_SHOWING_LINES = 15;
        private const int FONT_LEADING = 4;
        private const int WND_TOP_BOTTOM_EDGE = 2;
        private const int WND_LEFT_RIGHT_EDGE = 4;
        private const int RESIZING_BTN_HEIGHT = 10;
        private const int SCROLL_BAR_WIDTH = 7;
        private const int SCROLL_BTN_WIDTH = 15;
        private const int SCROLL_BTN_HEIGHT = 30;
        private const float DEFAULT_BACK_ALPHA = 0.6f;
        private const float LINE_HEIGHT_FALLBACK = 15f;

        // --- UI Textures ---
        private Texture2D _texScrollTop;
        private Texture2D _texScrollMiddle;
        private Texture2D _texScrollBottom;
        private Texture2D _texScrollThumb; // SCROLLBAR_ON
        private Texture2D _texResizeHandle; // DRAG_BTN

        // --- State ---
        private readonly Dictionary<MessageType, List<ChatMessage>> _messages;
        // private readonly List<string> _filters; // Omitted filters for now
        private SpriteFont _font;
        private SpriteFont _fontBold;
        private float _fontScale = 0.40f; // Font scale if needed

        private int _showingLines;
        private int _scrollOffset; // Number of lines scrolled UP from the bottom (0 = at bottom)
        private MessageType _currentViewType;
        private float _backgroundAlpha;
        private int _hoveredMessageGlobalIndex = -1; // Index in _messages[_currentViewType]
        private bool _isDraggingScrollbar = false;
        private bool _isDraggingResize = false;
        private float _dragStartOffsetY;

        private Rectangle _scrollBarArea;
        private Rectangle _scrollThumbArea;
        private Rectangle _resizeHandleArea;
        private float _lineHeight; // Calculated line height from the font

        // --- Public Properties ---
        public int Width { get; private set; }
        public MessageType CurrentViewType => _currentViewType;
        public int NumberOfShowingLines => _showingLines;
        private bool _showFrame; // Internal state for frame visibility
        private bool _isSysMsgVisible = true; // Internal state for system messages
        private bool _isChatLogVisible = true; // Internal state for chat log messages
        public bool IsFrameVisible => _showFrame; // Read-only public accessor
        public bool IsSysMsgVisible => _isSysMsgVisible; // Read-only public accessor
        public bool IsChatLogVisible => _isChatLogVisible; // Read-only public accessor

        // --- Constructor ---
        public ChatLogWindow(int width = CHATLOG_WIDTH)
        {
            Width = width;
            _messages = new Dictionary<MessageType, List<ChatMessage>>();
            // _filters = new List<string>(); // Omitted filters

            // Initialize lists for all message types
            foreach (MessageType type in Enum.GetValues(typeof(MessageType)))
            {
                if (type != MessageType.Unknown)
                {
                    _messages[type] = new List<ChatMessage>();
                }
            }

            _showingLines = DEFAULT_SHOWING_LINES;
            _scrollOffset = 0;
            _currentViewType = MessageType.All;
            _showFrame = false;
            _backgroundAlpha = DEFAULT_BACK_ALPHA;
            Interactive = true; // Enable mouse interaction

            UpdateLayout(); // Initial layout calculation
        }

        // --- Loading Resources ---
        public override async Task Load()
        {
            _font = GraphicsManager.Instance.Font; // Use default font
            // TODO: Load the bold font if available
            // _fontBold = MuGame.Instance.Content.Load<SpriteFont>("Fonts/YourBoldFont");
            _fontBold = _font; // Fallback to default

            CalculateLineHeight(); // Calculate line height after loading font

            // --- Loading Textures ---
            try
            {
                TextureLoader tl = TextureLoader.Instance;

                _texScrollTop = await tl.PrepareAndGetTexture("Interface/newui_scrollbar_up.tga") ?? GraphicsManager.Instance.Pixel;
                _texScrollMiddle = await tl.PrepareAndGetTexture("Interface/newui_scrollbar_m.tga") ?? GraphicsManager.Instance.Pixel;
                _texScrollBottom = await tl.PrepareAndGetTexture("Interface/newui_scrollbar_down.tga") ?? GraphicsManager.Instance.Pixel;
                _texScrollThumb = await tl.PrepareAndGetTexture("Interface/newui_scroll_on.tga") ?? GraphicsManager.Instance.Pixel;
                _texResizeHandle = await tl.PrepareAndGetTexture("Interface/newui_scrollbar_stretch.jpg") ?? GraphicsManager.Instance.Pixel;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChatLogWindow] Error loading textures: {ex.Message}");
                // Use default pixel as fallback
                _texScrollTop = _texScrollMiddle = _texScrollBottom = _texScrollThumb = _texResizeHandle = GraphicsManager.Instance.Pixel;
            }
            // --- End of Texture Loading ---

            UpdateLayout(); // Recalculate layout after resources are loaded
            await base.Load();
        }

        // --- Public Control Methods ---

        /// <summary>
        /// Adds a message to the appropriate lists. Future network data will come here.
        /// </summary>
        public void AddMessage(string senderId, string text, MessageType type)
        {
            if (string.IsNullOrEmpty(text) && string.IsNullOrEmpty(senderId)) return;
            if (!_messages.ContainsKey(type)) return; // Ignore unknown types

            // TODO: In future, filtering logic will go here (CheckFilterText)
            // C++ plays whisper sound for incoming chat messages that match a filter.
            // For now, we'll play it for incoming whisper messages if the option is enabled.
            // Adjust this logic if you implement filters and want the C++ behavior.
            if (type == MessageType.Whisper) // && (MuGame.Instance.GameOptions?.IsWhisperSoundEnabled ?? false)) // Assuming GameOptions exists and has this property
            {
                SoundController.Instance.PlayBuffer("Sound/iWhisper.wav");
            }


            // Split text into lines if too long
            List<ChatMessage> splitLines = SplitText(senderId, text, type);
            if (splitLines.Count == 0) return; // Nothing to add

            // Add to specific list (if not 'All')
            if (type != MessageType.All)
            {
                AddSplitLinesInternal(_messages[type], splitLines, MAX_MESSAGES_PER_TYPE);
                // Update scroll only if this type is active
                if (_currentViewType == type)
                {
                    if (_scrollOffset == 0) ScrollToBottom(); // Auto-scroll if at bottom
                    UpdateScrollbar();
                }
            }

            // Always add to 'All'
            AddSplitLinesInternal(_messages[MessageType.All], splitLines, MAX_TOTAL_MESSAGES);
            // Update scroll if 'All' is active
            if (_currentViewType == MessageType.All && type != MessageType.All)
            {
                if (_scrollOffset == 0) ScrollToBottom();
                UpdateScrollbar();
            }
            else if (_currentViewType == MessageType.All && type == MessageType.All)
            {
                if (_scrollOffset == 0) ScrollToBottom();
                UpdateScrollbar();
            }

        }

        // Add methods to control visibility state if needed (called by ChatInputBoxControl)
        public void ShowSystemMessages(bool show)
        {
            if (_isSysMsgVisible != show)
            {
                _isSysMsgVisible = show;
                // TODO: Add logic here if system messages are actually filtered/hidden based on this flag
                Console.WriteLine($"[ChatLog] System Messages Visible set to: {show}");
            }
        }

        public void ShowChatLogMessages(bool show)
        {
            if (_isChatLogVisible != show)
            {
                _isChatLogVisible = show;
                // TODO: Add logic here if chat messages are actually filtered/hidden based on this flag
                Console.WriteLine($"[ChatLog] Chat Log Messages Visible set to: {show}");
            }
        }

        /// <summary>
        /// Changes the currently displayed message type.
        /// </summary>
        public void ChangeViewType(MessageType newType)
        {
            if (_messages.ContainsKey(newType) && _currentViewType != newType)
            {
                _currentViewType = newType;
                ScrollToBottom(); // Reset scroll when changing type
                UpdateScrollbar();
            }
        }

        /// <summary>
        /// Cycles the number of displayed lines (like C++ F4/button).
        /// </summary>
        public void CycleSize()
        {
            int newLines = _showingLines + 3;
            if (newLines > MAX_SHOWING_LINES)
            {
                newLines = MIN_SHOWING_LINES;
            }
            SetShowingLines(newLines);
        }

        /// <summary>
        /// Sets a specific number of displayed lines.
        /// </summary>
        public void SetShowingLines(int lines)
        {
            int newLinesClamped = Math.Clamp(lines, MIN_SHOWING_LINES, MAX_SHOWING_LINES);
            if (_showingLines != newLinesClamped)
            {
                _showingLines = newLinesClamped;
                ClampScrollOffset();
                UpdateLayout();
                UpdateScrollbar();
            }
        }

        /// <summary>
        /// Shows or hides the frame (scrollbar, handle).
        /// </summary>
        public void ShowFrame(bool show)
        {
            if (_showFrame != show)
            {
                _showFrame = show;
                UpdateLayout();
                if (!show)
                {
                    ScrollToBottom(); // Auto-scroll to bottom when hiding frame
                }
                UpdateScrollbar();
            }
        }

        /// <summary>
        /// Toggles frame visibility.
        /// </summary>
        public void ToggleFrame() => ShowFrame(!_showFrame);

        /// <summary>
        /// Cycles background transparency (like C++ button).
        /// </summary>
        public void CycleBackgroundAlpha()
        {
            _backgroundAlpha += 0.2f;
            if (_backgroundAlpha > 0.91f) // Safely more than 0.9f
            {
                _backgroundAlpha = 0.2f;
            }
        }

        /// <summary>
        /// Sets a specific background transparency.
        /// </summary>
        public void SetBackgroundAlpha(float alpha)
        {
            _backgroundAlpha = Math.Clamp(alpha, 0f, 1f);
        }

        /// <summary>
        /// Scrolls the view by a specified number of lines (e.g., for PageUp/Down).
        /// </summary>
        public void ScrollLines(int lineDelta)
        {
            if (!_showFrame) return; // Only when frame is visible

            _scrollOffset -= lineDelta; // Negative delta scrolls down (increases offset)
            ClampScrollOffset();
            UpdateScrollbar();
        }

        /// <summary>
        /// Clears messages of a given type.
        /// </summary>
        public void Clear(MessageType type)
        {
            if (_messages.ContainsKey(type))
            {
                _messages[type].Clear();
                if (type == _currentViewType)
                {
                    _scrollOffset = 0;
                    UpdateScrollbar();
                }
            }
        }

        /// <summary>
        /// Clears all messages from all lists.
        /// </summary>
        public void ClearAll()
        {
            foreach (var list in _messages.Values)
            {
                list.Clear();
            }
            _scrollOffset = 0;
            UpdateScrollbar();
        }

        // --- Update and Draw ---

        public override void Update(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready || !Visible || _font == null) return;

            base.Update(gameTime); // Updates IsMouseOver among other things

            var mouse = MuGame.Instance.Mouse;
            var prevMouse = MuGame.Instance.PrevMouseState;

            _hoveredMessageGlobalIndex = -1; // Reset hover state

            bool mouseInInteractableArea = DisplayRectangle.Contains(mouse.Position) || (_showFrame && _resizeHandleArea.Contains(mouse.Position));

            // --- Mouse Input Handling ---
            if (mouseInInteractableArea || _isDraggingScrollbar || _isDraggingResize)
            {
                // Mouse wheel (only in main window)
                if (DisplayRectangle.Contains(mouse.Position) && mouse.ScrollWheelValue != prevMouse.ScrollWheelValue)
                {
                    int delta = (prevMouse.ScrollWheelValue - mouse.ScrollWheelValue);
                    // Change here: invert the delta sign
                    // old: _scrollOffset += delta;
                    _scrollOffset -= delta; // inverted wheel scroll direction
                    ClampScrollOffset();
                    UpdateScrollbar();
                }

                // Frame interactions (only when visible)
                if (_showFrame)
                {
                    HandleScrollbarInteraction(mouse, prevMouse);
                    HandleResizeInteraction(mouse, prevMouse);
                }

                // Hover over message and right-click (when not dragging UI)
                if (DisplayRectangle.Contains(mouse.Position) && !_isDraggingScrollbar && !_isDraggingResize)
                {
                    DetectHoveredMessage(mouse.Position);

                    // PLACEHOLDER: Handle right-click for whisper
                    if (_hoveredMessageGlobalIndex != -1 && mouse.RightButton == ButtonState.Pressed && prevMouse.RightButton == ButtonState.Released)
                    {
                        var msgList = _messages[_currentViewType];
                        if (_hoveredMessageGlobalIndex < msgList.Count)
                        {
                            ChatMessage msg = msgList[_hoveredMessageGlobalIndex];
                            // Check if whisper can be sent to this message type
                            if (!string.IsNullOrEmpty(msg.SenderID) && msg.Type != MessageType.System && msg.Type != MessageType.Error && msg.Type != MessageType.Unknown)
                            {
                                // TODO: Invoke method in ChatInputBox to set whisper target
                                Console.WriteLine($"[ChatLog] PLACEHOLDER: Set Whisper Target -> {msg.SenderID}");
                            }
                        }
                    }
                }
            }
            else
            {
                // Stop dragging if the mouse leaves the area
                if (_isDraggingScrollbar && mouse.LeftButton == ButtonState.Released) _isDraggingScrollbar = false;
                if (_isDraggingResize && mouse.LeftButton == ButtonState.Released) _isDraggingResize = false;
            }
        }

        public override void Draw(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready || !Visible || _font == null)
                return;

            var spriteBatch = GraphicsManager.Instance.Sprite;
            var currentMsgList = _messages[_currentViewType];
            int totalLinesInView = currentMsgList.Count;

            float startY = DisplayRectangle.Y + WND_TOP_BOTTOM_EDGE;
            float startX = DisplayRectangle.X + WND_LEFT_RIGHT_EDGE;
            float clientWidth = CalculateClientWidth();

            // 1. Draw background (only when frame is visible)
            if (_showFrame)
            {
                Color bg = new Color(0, 0, 0) * _backgroundAlpha;
                spriteBatch.Draw(GraphicsManager.Instance.Pixel, DisplayRectangle, bg);
            }

            // 2. Draw messages
            if (totalLinesInView > 0 && _lineHeight > 0)
            {
                int first = Math.Max(0, totalLinesInView - _showingLines - _scrollOffset);
                int last = Math.Min(totalLinesInView - 1, first + _showingLines - 1);
                float y = startY;

                // Align to bottom if fewer messages than space
                int drawCount = last - first + 1;
                if (drawCount < _showingLines)
                    y += (_showingLines - drawCount) * _lineHeight;

                for (int i = first; i <= last; i++)
                {
                    var msg = currentMsgList[i];
                    var font = msg.Type == MessageType.GM ? _fontBold : _font;
                    var text = msg.DisplayText;
                    var textPos = new Vector2(startX, y);
                    var txtCol = GetMessageColor(msg.Type);
                    var bgCol = GetMessageBackgroundColor(msg.Type, _showFrame);

                    // Draw message background if any
                    if (bgCol.A > 0)
                    {
                        // MeasureString can be slow, consider caching or estimating
                        float textWidth = font.MeasureString(text).X * _fontScale;
                        float bgWidth = Math.Min(clientWidth, textWidth);
                        spriteBatch.Draw(GraphicsManager.Instance.Pixel,
                                new Rectangle((int)textPos.X, (int)textPos.Y, (int)bgWidth, (int)_lineHeight),
                                bgCol);
                    }

                    // Hover effect (only when frame is visible)
                    if (_showFrame && i == _hoveredMessageGlobalIndex)
                    {
                        // Always draw hover highlight (for testing)
                        var hoverRect = new Rectangle(
                            (int)startX, (int)y,
                            (int)clientWidth, (int)_lineHeight);
                        spriteBatch.Draw(GraphicsManager.Instance.Pixel, hoverRect, new Color(30, 30, 30, 180));
                        txtCol = new Color(255, 128, 255); // Hover text color
                    }

                    // Draw text
                    spriteBatch.DrawString(font, text, textPos, txtCol, 0f, Vector2.Zero, _fontScale, SpriteEffects.None, 0f);

                    y += _lineHeight;
                }
            }

            // 3. Draw frame (if visible)
            if (_showFrame)
            {
                DrawScrollbar(spriteBatch);
                DrawResizeHandle(spriteBatch);
            }
        }

        // --- Private Helper Methods ---

        private void CalculateLineHeight()
        {
            if (_font != null)
            {
                _lineHeight = (_font.LineSpacing + FONT_LEADING) * _fontScale;
            }
            else
            {
                _lineHeight = LINE_HEIGHT_FALLBACK; // Fallback value
            }
        }

        private float CalculateClientWidth()
        {
            float clientWidth = Width - (WND_LEFT_RIGHT_EDGE * 2);
            if (_showFrame)
            {
                clientWidth -= SCROLL_BAR_WIDTH + WND_LEFT_RIGHT_EDGE;
            }
            return Math.Max(10, clientWidth); // Minimum width
        }

        private void HandleScrollbarInteraction(MouseState mouse, MouseState prevMouse)
        {
            if (_scrollBarArea.IsEmpty) return;

            bool hoverScrollTrack = _scrollBarArea.Contains(mouse.Position);
            bool hoverThumb = !_scrollThumbArea.IsEmpty && _scrollThumbArea.Contains(mouse.Position);

            if (mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released && hoverThumb)
            {
                _isDraggingScrollbar = true;
                _dragStartOffsetY = mouse.Y - _scrollThumbArea.Y;
            }
            else if (_isDraggingScrollbar && mouse.LeftButton == ButtonState.Pressed)
            {
                float trackHeight = _scrollBarArea.Height - SCROLL_BTN_HEIGHT;
                if (trackHeight <= 0) { _isDraggingScrollbar = false; return; }

                float newThumbTopY = mouse.Y - _dragStartOffsetY;
                float clampedThumbTopY = Math.Clamp(newThumbTopY, _scrollBarArea.Y, _scrollBarArea.Y + trackHeight);

                // Change here: calculate scrollRatio based on position and then invert it
                float visualScrollRatio = (clampedThumbTopY - _scrollBarArea.Y) / trackHeight;
                float logicalScrollRatio = 1.0f - visualScrollRatio; // Invert ratio

                int maxScrollOffset = Math.Max(0, _messages[_currentViewType].Count - _showingLines);
                _scrollOffset = (int)Math.Round(maxScrollOffset * logicalScrollRatio); // Use inverted ratio

                ClampScrollOffset();
                UpdateScrollbar(); // Update the visual thumb position
            }
            else if (_isDraggingScrollbar && mouse.LeftButton == ButtonState.Released)
            {
                _isDraggingScrollbar = false;
            }
            else if (hoverScrollTrack && !hoverThumb && mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released)
            {
                int lineDelta = _showingLines;
                // Click above thumb (visually lower) -> scroll up
                // Click below thumb (visually higher) -> scroll down
                _scrollOffset += (mouse.Y > _scrollThumbArea.Center.Y) ? lineDelta : -lineDelta; // Corrected Y check relative to thumb
                ClampScrollOffset();
                UpdateScrollbar();
            }
        }

        private void HandleResizeInteraction(MouseState mouse, MouseState prevMouse)
        {
            if (_resizeHandleArea.IsEmpty) return;
            bool hoverResize = _resizeHandleArea.Contains(mouse.Position);

            // For now, clicking the handle = resize (like F4)
            if (hoverResize && mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton == ButtonState.Released)
            {
                CycleSize();
                // Sound is played by ChatInputBoxControl when F4 is pressed or button is clicked
            }
            // Dragging logic can be added here if needed
            else if (_isDraggingResize && mouse.LeftButton == ButtonState.Pressed) { }
            else if (_isDraggingResize && mouse.LeftButton == ButtonState.Released) { _isDraggingResize = false; }
            else if (hoverResize && mouse.LeftButton == ButtonState.Pressed && prevMouse.LeftButton != ButtonState.Pressed)
            {
                _isDraggingResize = true; // Start dragging if clicked
            }
        }

        private void DetectHoveredMessage(Point mousePos)
        {
            if (_lineHeight <= 0) return;

            float clientWidth = CalculateClientWidth();
            float areaStartY = DisplayRectangle.Y + WND_TOP_BOTTOM_EDGE;
            float lineStartX = DisplayRectangle.X + WND_LEFT_RIGHT_EDGE;

            var currentMsgList = _messages[_currentViewType];
            int totalLinesInView = currentMsgList.Count;
            int firstVisibleLineGlobalIndex = Math.Max(0, totalLinesInView - _showingLines - _scrollOffset);
            int actualMessagesToDraw = Math.Max(0, Math.Min(_showingLines, totalLinesInView - firstVisibleLineGlobalIndex));

            float drawingStartY = areaStartY;
            if (actualMessagesToDraw < _showingLines)
            {
                drawingStartY += (_showingLines - actualMessagesToDraw) * _lineHeight;
            }
            float drawingEndY = drawingStartY + actualMessagesToDraw * _lineHeight;

            if (mousePos.Y >= drawingStartY && mousePos.Y < drawingEndY && mousePos.X >= lineStartX && mousePos.X < lineStartX + clientWidth)
            {
                int relativeIndex = (int)((mousePos.Y - drawingStartY) / _lineHeight);
                int globalIndex = firstVisibleLineGlobalIndex + relativeIndex;
                if (globalIndex >= 0 && globalIndex < totalLinesInView)
                {
                    _hoveredMessageGlobalIndex = globalIndex;
                    return; // Found
                }
            }
            // Not found or mouse outside X range
            _hoveredMessageGlobalIndex = -1;
        }

        // Adds lines and trims the list
        private void AddSplitLinesInternal(List<ChatMessage> list, List<ChatMessage> linesToAdd, int maxSize)
        {
            list.AddRange(linesToAdd);
            int removeCount = list.Count - maxSize;
            if (removeCount > 0)
            {
                list.RemoveRange(0, removeCount);
                // Adjust offset if current view list was trimmed
                if (list == _messages[_currentViewType])
                {
                    _scrollOffset -= removeCount;
                }
            }
            // Clamp after trimming because offset could have become negative
            if (list == _messages[_currentViewType])
            {
                ClampScrollOffset();
            }
        }


        private void ScrollToBottom()
        {
            _scrollOffset = 0;
            // No need to call UpdateScrollbar here, as it will be called after AddMessage
        }

        private void ClampScrollOffset()
        {
            if (!_messages.ContainsKey(_currentViewType)) return;
            int maxScrollOffset = Math.Max(0, _messages[_currentViewType].Count - _showingLines);
            _scrollOffset = Math.Clamp(_scrollOffset, 0, maxScrollOffset);
        }

        private void UpdateLayout()
        {
            CalculateLineHeight(); // Ensure line height is up to date
            if (_lineHeight <= 0) return; // Cannot layout without line height

            // --- CHANGE: Remember old height ---
            int oldHeight = ViewSize.Y;

            // Calculate new size based on number of lines
            int newHeight = (int)(_lineHeight * _showingLines) + WND_TOP_BOTTOM_EDGE * 2;
            ViewSize = new Point(Width, newHeight);
            ControlSize = ViewSize; // Sync ControlSize

            // --- CHANGE: Calculate height difference ---
            int deltaHeight = newHeight - oldHeight;

            // --- CHANGE: Adjust Y so bottom edge remains fixed ---
            // If height increased (deltaHeight > 0), move Y up (decrease Y)
            // If height decreased (deltaHeight < 0), move Y down (increase Y)
            Y -= deltaHeight; // Adjust upper-edge Y

            // --- Update interactive areas (Scrollbar, Resize Handle) ---
            // These calculations already use the updated DisplayRectangle (which accounts for new Y and ViewSize)
            if (_showFrame)
            {
                int scrollX = DisplayRectangle.X + Width - WND_LEFT_RIGHT_EDGE - SCROLL_BAR_WIDTH;
                int scrollY = DisplayRectangle.Y + WND_TOP_BOTTOM_EDGE;
                int scrollHeight = DisplayRectangle.Height - WND_TOP_BOTTOM_EDGE * 2;
                _scrollBarArea = new Rectangle(scrollX, scrollY, SCROLL_BAR_WIDTH, scrollHeight);

                int resizeX = DisplayRectangle.X;
                // The resize handle is always ABOVE the top edge of the window
                int resizeY = DisplayRectangle.Y - RESIZING_BTN_HEIGHT;
                _resizeHandleArea = new Rectangle(resizeX, resizeY, Width, RESIZING_BTN_HEIGHT);

                UpdateScrollbar(); // Update thumb position based on new layout
            }
            else
            {
                _scrollBarArea = Rectangle.Empty;
                _scrollThumbArea = Rectangle.Empty;
                _resizeHandleArea = Rectangle.Empty;
            }
        }

        private void UpdateScrollbar()
        {
            if (!_showFrame || _scrollBarArea.Height <= 0)
            {
                _scrollThumbArea = Rectangle.Empty;
                return;
            }

            int totalLines = _messages[_currentViewType].Count;
            int maxScrollOffset = Math.Max(0, totalLines - _showingLines);
            float scrollRatio = (maxScrollOffset == 0) ? 0 : (float)_scrollOffset / maxScrollOffset;

            int trackHeight = _scrollBarArea.Height - SCROLL_BTN_HEIGHT;

            if (trackHeight <= 0)
            {
                _scrollThumbArea = Rectangle.Empty;
                return;
            }

            int thumbTopY = _scrollBarArea.Y + (int)(trackHeight * (1.0f - scrollRatio)); // Inverted ratio

            int thumbX = _scrollBarArea.X - 4; // Adjust to C++ positions

            _scrollThumbArea = new Rectangle(thumbX, thumbTopY, SCROLL_BTN_WIDTH, SCROLL_BTN_HEIGHT);
        }

        private Color GetMessageColor(MessageType type)
        {
            // Text colors as in C++
            switch (type)
            {
                case MessageType.Whisper: return Color.Black;
                case MessageType.System: return new Color(100, 150, 255);
                case MessageType.Error: return new Color(255, 30, 0);
                case MessageType.Chat: return new Color(205, 220, 239);
                case MessageType.Party: return Color.Black;
                case MessageType.Guild: return Color.Black;
                case MessageType.Union: return Color.Black;
                case MessageType.Gens: return Color.Black;
                case MessageType.GM: return new Color(250, 200, 50);
                case MessageType.Info: return Color.Goldenrod; // For GoldenCenter etc.
                default: return Color.White;
            }
        }

        private Color GetMessageBackgroundColor(MessageType type, bool frameVisible)
        {
            // Message background colors as in C++
            byte chatAlpha = (byte)(frameVisible ? 100 : 150);
            switch (type)
            {
                case MessageType.Whisper: return new Color(255, 200, 50, 150);
                case MessageType.System: return new Color(0, 0, 0, 150);
                case MessageType.Error: return new Color(0, 0, 0, 150);
                case MessageType.Chat: return new Color(0, 0, 0, (int)chatAlpha);
                case MessageType.Party: return new Color(0, 200, 255, 150);
                case MessageType.Guild: return new Color(0, 255, 150, 200);
                case MessageType.Union: return new Color(200, 200, 0, 200);
                case MessageType.Gens: return new Color(150, 200, 100, 200);
                case MessageType.GM: return new Color(30, 30, 30, 200);
                case MessageType.Info: return Color.Transparent; // Info usually has no background
                default: return Color.Transparent;
            }
        }

        private List<ChatMessage> SplitText(string senderId, string text, MessageType type)
        {
            var lines = new List<ChatMessage>();
            string originalText = text ?? string.Empty; // Preserve the original text
            string currentSenderId = senderId;

            if (_font == null) // If font not loaded
            {
                if (!string.IsNullOrEmpty(currentSenderId) || !string.IsNullOrEmpty(originalText))
                    lines.Add(new ChatMessage(currentSenderId, originalText, type));
                return lines;
            }

            float maxLineWidth = CalculateClientWidth();
            string remainingText = originalText;

            while (true)
            {
                string prefix = "";
                float prefixWidth = 0f;
                if (!string.IsNullOrEmpty(currentSenderId))
                {
                    prefix = $"{currentSenderId} : ";
                    prefixWidth = _font.MeasureString(prefix).X * _fontScale;
                }

                float availableWidth = maxLineWidth - prefixWidth;
                string lineContent;

                // If no text remains but there's a sender, add an empty line with sender
                if (string.IsNullOrEmpty(remainingText))
                {
                    if (!string.IsNullOrEmpty(currentSenderId))
                    {
                        lines.Add(new ChatMessage(currentSenderId, string.Empty, type));
                    }
                    break;
                }

                Vector2 measuredSize = _font.MeasureString(remainingText) * _fontScale;

                if (measuredSize.X <= availableWidth)
                {
                    // Whole text fits
                    lineContent = remainingText;
                    remainingText = string.Empty;
                }
                else
                {
                    // Need to split
                    int splitIndex = -1;
                    for (int i = remainingText.Length - 1; i >= 0; i--)
                    {
                        string sub = remainingText.Substring(0, i + 1);
                        if (_font.MeasureString(sub).X * _fontScale <= availableWidth)
                        {
                            int lastSpace = sub.LastIndexOf(' ');
                            // Prefer splitting at space if it's not too far back
                            splitIndex = (lastSpace > 0 && (i - lastSpace < 15)) ? lastSpace : i + 1;
                            break;
                        }
                    }

                    if (splitIndex <= 0)
                    {
                        // Find last character that fits
                        for (int k = 1; k < remainingText.Length; k++)
                        {
                            if (_font.MeasureString(remainingText.Substring(0, k)).X * _fontScale > availableWidth)
                            {
                                splitIndex = k - 1;
                                break;
                            }
                        }
                        if (splitIndex <= 0) splitIndex = 1; // At least one character
                    }

                    lineContent = remainingText.Substring(0, splitIndex);
                    remainingText = remainingText.Substring(splitIndex).TrimStart();
                }

                lines.Add(new ChatMessage(currentSenderId, lineContent.TrimEnd(), type));
                currentSenderId = string.Empty; // Subsequent lines have no sender

                if (string.IsNullOrEmpty(remainingText)) break;
            }

            return lines;
        }

        // Draw scrollbar and resize handle
        private void DrawScrollbar(SpriteBatch spriteBatch)
        {
            if (_scrollBarArea.IsEmpty) return;

            Color barColor = Color.White; // Use textures, so white tint
            Color thumbColor = _isDraggingScrollbar ? new Color(0.7f, 0.7f, 0.7f) : Color.White;

            int scrollX = _scrollBarArea.X;
            int scrollY = _scrollBarArea.Y;
            int scrollW = _scrollBarArea.Width;
            int scrollH = _scrollBarArea.Height;

            int topH = _texScrollTop?.Height ?? 3;
            int botH = _texScrollBottom?.Height ?? 3;
            int midScrollPartH = _texScrollMiddle?.Height ?? 15;
            int midAreaH = Math.Max(0, scrollH - topH - botH);

            // Top
            if (_texScrollTop != null)
                spriteBatch.Draw(_texScrollTop, new Rectangle(scrollX, scrollY, scrollW, topH), barColor);

            // Middle (tiled)
            if (_texScrollMiddle != null && midAreaH > 0 && midScrollPartH > 0)
            {
                Rectangle middleSourceRect = new Rectangle(0, 0, _texScrollMiddle.Width, _texScrollMiddle.Height);
                for (int yOffset = 0; yOffset < midAreaH; yOffset += midScrollPartH)
                {
                    int currentPartH = Math.Min(midScrollPartH, midAreaH - yOffset);
                    Rectangle destRect = new Rectangle(scrollX, scrollY + topH + yOffset, scrollW, currentPartH);
                    Rectangle sourceRect = new Rectangle(middleSourceRect.X, middleSourceRect.Y, middleSourceRect.Width, (int)(middleSourceRect.Height * ((float)currentPartH / midScrollPartH)));
                    spriteBatch.Draw(_texScrollMiddle, destRect, sourceRect, barColor);
                }
            }

            // Bottom
            if (_texScrollBottom != null)
                spriteBatch.Draw(_texScrollBottom, new Rectangle(scrollX, scrollY + topH + midAreaH, scrollW, botH), barColor);

            // Thumb
            if (!_scrollThumbArea.IsEmpty && _texScrollThumb != null)
            {
                spriteBatch.Draw(_texScrollThumb, _scrollThumbArea, thumbColor);
            }
        }

        private void DrawResizeHandle(SpriteBatch spriteBatch)
        {
            if (_resizeHandleArea.IsEmpty || _texResizeHandle == null) return;
            Color handleColor = _isDraggingResize ? new Color(0.7f, 0.7f, 0.7f) : Color.White;
            spriteBatch.Draw(_texResizeHandle, _resizeHandleArea, handleColor);
        }

        public override bool ProcessMouseScroll(int scrollDelta)
        {
            if (!Visible || !IsMouseOver) return false; // not handled if not visible or mouse not over

            // Check if mouse is over the scrollbar area (not a separate _scrollBar control, but the area/logic is internal)
            if (_showFrame && !_scrollBarArea.IsEmpty && _scrollBarArea.Contains(MuGame.Instance.Mouse.Position))
            {
                // If the thumb is hovered, treat as handled (simulate scrollbar logic)
                if (!_scrollThumbArea.IsEmpty && _scrollThumbArea.Contains(MuGame.Instance.Mouse.Position))
                {
                    // Simulate thumb scroll: scroll by one line per wheel event
                    _scrollOffset -= scrollDelta;
                    ClampScrollOffset();
                    UpdateScrollbar();
                    return true;
                }
                // If over the scrollbar but not the thumb, still treat as handled
                _scrollOffset -= scrollDelta;
                ClampScrollOffset();
                UpdateScrollbar();
                return true;
            }
            // Not over scrollbar, process for main content
            _scrollOffset -= scrollDelta; // inverted from original logic; scrollDelta positive = wheel up = content scroll down = offset decrease
            ClampScrollOffset();
            UpdateScrollbar();
            return true; // scroll handled
        }
    }
}