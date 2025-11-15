using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Client.Main.Controls.UI.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Client.Main.Controls.UI;

class OptionControl : OptionLabelButton
{
    public new event EventHandler<KeyValuePair<string, int>> Click;
    public KeyValuePair<string, int> Option { get; set; } = new();
    public override bool OnClick()
    {
        Click?.Invoke(this, Option);
        return Click != null;
    }
}

public class OptionPickerControl : UIControl
{
    // CONSTANTS
    int LIST_PADDING_TOP = 10;
    int LIST_PADDING_LEFT = 10;
    int LIST_PADDING_RIGHT = 2;
    int LIST_PADDING_BOTTOM = 10;
    int LIST_GAP = 3;
    int LIST_ITEM_HEIGHT = 29;
    int LIST_ITEM_WIDTH = 180;

    int SCROLLBAR_WIDTH = 18;

    // FLAGS
    bool isInitialized = false;


    // PROPS
    
    private int itemsVisible = 6;
    public int ItemsVisible
    {
        get => itemsVisible;
        set
        {
            if (itemsVisible == value) return;
            itemsVisible = value;
            RefreshScrollableUI();
        }
    }
    private ushort listItemWidth = 180;
    public ushort ListItemWidth
    {
        get => listItemWidth;
        set
        {
            if (listItemWidth == value) return;
            listItemWidth = value;
            RefreshList();
            RefreshScrollableUI();
        }
    }
    private int _currentScrollOffset;
    public int CurrentScrollOffset
    {
        get => _currentScrollOffset;
        set => _currentScrollOffset = value;
    }

    private List<KeyValuePair<string, int>> options;
    public List<KeyValuePair<string, int>> Options
    {
        get => options;
        set
        {
            options = value;
            if (!isInitialized) return;
            RefreshList();
            UpdateScrollbarState();
        }
    }
    private List<OptionControl> CachedOptionControls = [];

    private KeyValuePair<string, int>? _value;
    public KeyValuePair<string, int>? Value
    {
        get => _value;
        set
        {
            if (
                _value.HasValue && value.HasValue
                && _value?.Key == value?.Key
                && _value?.Value == value?.Value
            ) return;
            _value = value;
            RefreshOptionSelectedState();
        } 
    }

    private ScrollBarControl _scrollBar;

    public event EventHandler<KeyValuePair<string, int>?> ValueChanged;
    public OptionPickerControl()
    {
        AutoViewSize = false;
        Interactive = true;
        ViewSize = new Point(
            LIST_PADDING_LEFT + ListItemWidth + LIST_PADDING_RIGHT + SCROLLBAR_WIDTH,
            LIST_PADDING_TOP + LIST_PADDING_BOTTOM + LIST_ITEM_HEIGHT * ItemsVisible + LIST_GAP * ItemsVisible - LIST_GAP
        );
        _scrollBar = new ScrollBarControl(minThumbHeight: 32)
        {
            X = LIST_PADDING_LEFT + ListItemWidth,
            Y = LIST_PADDING_TOP,
            Height = LIST_ITEM_HEIGHT * ItemsVisible + LIST_GAP * ItemsVisible - LIST_GAP,
            Width = SCROLLBAR_WIDTH,
            Visible = false,
            ThumbHeightAdjustmentFactor = 0.2f
        };
        _scrollBar.ValueChanged += (s, e) => OnScrollChanged();
        Controls.Add(_scrollBar);
        // BackgroundColor = Color.Black;
    }

    public override async Task Initialize()
    {

        RefreshList();
        await base.Initialize();
        isInitialized = true;
    }

    private void OnScrollChanged()
    {
        if (_currentScrollOffset != _scrollBar.Value)
        {
            _currentScrollOffset = _scrollBar.Value;
            RefreshOptionsVisibility();
        }
    }
    private void OnOptionClick(object sender, KeyValuePair<string, int> e)
    {
        Value = e;
        ValueChanged?.Invoke(this, Value);
    }

    public override bool ProcessMouseScroll(int scrollDelta)
    {
        if (!Visible || !IsMouseOver) // only process if visible and mouse is over this control
            return false;
        int scrollDirection = scrollDelta > 0 ? 1 : -1;

        if (_scrollBar != null && _scrollBar.Visible && _scrollBar.IsMouseOver)
        {
            // if the scrollbar handles it, then the OptionPickerDialog also considers it handled.
            if (_scrollBar.ProcessMouseScroll(scrollDirection))
            {
                return true;
            }
        }
        // scrollbar didn't handle (or wasn't applicable), the OptionPickerDialog itself doesn't have other scrollable content here.
        // However, if it had its own direct scrollable list without a scrollbar, logic would go here.
        // For now, if mouse is over the window but not its scrollbar, and scroll happens,
        // we could argue it's still "consuming" it for this window context even if no direct action.
        // But return false if no action taken by the window itself.
        // For simplicity and to prevent pass-through if the window is clearly the focus:
        if (_scrollBar != null && _scrollBar.Visible)  // a scrollbar exists, assume scroll should be for it
        {
            _scrollBar.Value -= scrollDirection; // scrollDelta: positive for up, negative for down
            return true;
        }

        return false; // window didn't use the scroll (e.g., no scrollbar visible)
    }


    private void UpdateScrollbarState()
    {
        bool needsScrollbar = Options.Count > ItemsVisible;

        if (_scrollBar.Visible != needsScrollbar)
        {
            _scrollBar.Visible = needsScrollbar;
            RefreshList();
        }

        if (needsScrollbar)
        {
            _scrollBar.Min = 0;
            _scrollBar.Max = Math.Max(0, Options.Count - ItemsVisible);
            _scrollBar.Value = _currentScrollOffset;
            _scrollBar.SetListMetrics(ItemsVisible, Options.Count);
        }
    }

    void RefreshList()
    {
        var optionControls = Controls.OfType<OptionControl>().ToList(); // copy to avoid modifying during iteration
        foreach (var optionControl in optionControls)
        {
            Controls.Remove(optionControl);
            optionControl.Click -= OnOptionClick;
            optionControl.Dispose();
        }
        CachedOptionControls = [];
        if (Options == null || Options.Count < 1)
        {
            return;
        }
        foreach (var option in Options)
        {
            OptionControl optionControl = new OptionControl()
            {
                Label = new LabelControl
                {
                    Text = option.Key,
                    Align = Models.ControlAlign.VerticalCenter,
                    X = 8 + 15 + 5,
                },
                X = LIST_PADDING_LEFT,
                Option = option,
                Visible = false,
            };
            if (ListItemWidth != 180)
            {
                optionControl.TileWidth = ListItemWidth;
                optionControl.ViewSize = new Point(ListItemWidth, 29);
                optionControl.BorderColor = new Color(0xff, 0xff, 0xff, 0x4);
                optionControl.BorderThickness = 1;
                optionControl.BackgroundColor = new Color(0x44, 0x44, 0x44);
            }
            optionControl.Click += OnOptionClick;
            Controls.Add(optionControl);
            CachedOptionControls.Add(optionControl);

        }
        RefreshOptionsVisibility();
    }

    void RefreshScrollableUI()
    {
        ViewSize = new Point(
            LIST_PADDING_LEFT + ListItemWidth + LIST_PADDING_RIGHT + SCROLLBAR_WIDTH,
            LIST_PADDING_TOP + LIST_PADDING_BOTTOM + LIST_ITEM_HEIGHT * ItemsVisible + LIST_GAP * ItemsVisible - LIST_GAP
        );
        _scrollBar.X = LIST_PADDING_LEFT + ListItemWidth;
        _scrollBar.Height = LIST_ITEM_HEIGHT * ItemsVisible + LIST_GAP * ItemsVisible - LIST_GAP;
    }

    void RefreshOptionsVisibility()
    {
        int itemY = LIST_PADDING_TOP;
        for (int i = 0; i < CachedOptionControls.Count; i++)
        {
            var item = CachedOptionControls[i];
            bool visible = i >= CurrentScrollOffset && i < CurrentScrollOffset + ItemsVisible;
            if (!visible)
            {
                item.Visible = false;
                continue;
            }
            item.Y = itemY;
            item.Visible = true;
            itemY += LIST_GAP + LIST_ITEM_HEIGHT;
        }
    }
    void RefreshOptionSelectedState()
    {
        for (int i = 0; i < CachedOptionControls.Count; i++)
        {
            var item = CachedOptionControls[i];
            item.Checked = Value != null && item.Option.Key == Value?.Key && item.Option.Value == Value?.Value;
            if (!item.Checked)
            {
                item.Label.TextColor = Color.WhiteSmoke;
                continue;
            }
            item.Label.TextColor = Color.Gold;
        }
    }
}
