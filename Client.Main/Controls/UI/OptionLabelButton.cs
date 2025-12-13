using Client.Main.Graphics;
using Microsoft.Xna.Framework;

namespace Client.Main.Controls.UI;

public class OptionLabelButton : LabelButton
{
    public bool Checked { get; set; }
    public SpriteControl Icon { get; }
    public OptionLabelButton()
    {
        Interactive = true;
        TileWidth = 180;
        TileHeight = 29;
        ViewSize = new Point(TileWidth, TileHeight); // Ensure ViewSize is set from tile dimensions
        TileY = 0;
        BlendState = Blendings.Alpha;
        TexturePath = "Interface/newui_btn_empty_big.tga";
        Icon = new SpriteControl
        {
            X = 7,
            Y = 7,
            Interactive = false,
            TileWidth = 15,
            TileHeight = 15,
            ViewSize = new Point(15, 15),
            TileY = 0,
            BlendState = Blendings.Alpha,
            TexturePath = "Interface/MacroUI/MacroUI_OptionButton.tga"
        };
    }

    public override async Task Initialize()
    {
        if (Icon != null)
        {
            Controls.Add(Icon);
        }
        await base.Initialize();
    }

    public override void Dispose()
    {
        Icon?.Dispose();
        base.Dispose();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        if (Icon != null)
        {
            if (Checked)
            {
                Icon.TileY = 0;
            }
            else
            {
                Icon.TileY = 1;
            }
        }
        if (IsMouseOver && IsMousePressed) // check IsMousePressed from GameControl for click visual
            TileY = 2;
        else if (IsMouseOver) // hover state
            TileY = 1;
        else
            TileY = 0;
    }
}