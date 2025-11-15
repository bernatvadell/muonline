using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Client.Main.Controls.UI;

public class LabelButton : SpriteControl
{
    public LabelControl Label { get; set; }
    public LabelButton()
    {
        Interactive = true;
        TileWidth = 180;
        TileHeight = 29;
        ViewSize = new Point(TileWidth, TileHeight); // Ensure ViewSize is set from tile dimensions
        TileY = 0;
        BlendState = Blendings.Alpha;
        TexturePath = "Interface/newui_btn_empty_big.tga";
    }

    public override async Task Initialize()
    {
        if (Label != null)
        {
            Controls.Add(Label);
        }
        await base.Initialize();
    }

    public override void Dispose()
    {
        Label?.Dispose();
        base.Dispose();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if (IsMouseOver && IsMousePressed) // check IsMousePressed from GameControl for click visual
            TileY = 2;
        else if (IsMouseOver) // hover state
            TileY = 1;
        else
            TileY = 0;
    }
}

public class LabelButtonWide : LabelButton
{
    public LabelButtonWide()
    {
        Interactive = true;
        TileWidth = 180;
        TileHeight = 29;
        ViewSize = new Point(TileWidth, TileHeight); // Ensure ViewSize is set from tile dimensions
        TileY = 0;
        BlendState = Blendings.Alpha;
        TexturePath = "Interface/newui_btn_empty_big.tga";
    }
}


public class LabelButtonMedium : LabelButton
{
    public LabelButtonMedium()
    {
        Interactive = true;
        TileWidth = 108;
        TileHeight = 29;
        ViewSize = new Point(TileWidth, TileHeight); // Ensure ViewSize is set from tile dimensions
        TileY = 0;
        BlendState = Blendings.Alpha;
        TexturePath = "Interface/newui_btn_empty.tga";
    }
}


public class LabelButtonSmall : LabelButton
{
    public LabelButtonSmall()
    {
        Interactive = true;
        TileWidth = 64;
        TileHeight = 29;
        ViewSize = new Point(TileWidth, TileHeight); // Ensure ViewSize is set from tile dimensions
        TileY = 0;
        BlendState = Blendings.Alpha;
        TexturePath = "Interface/newui_btn_empty_small.tga";
    }
}


public class LabelButtonVerySmall : LabelButton
{
    public LabelButtonVerySmall()
    {
        Interactive = true;
        TileWidth = 54;
        TileHeight = 23;
        ViewSize = new Point(TileWidth, TileHeight); // Ensure ViewSize is set from tile dimensions
        TileY = 0;
        BlendState = Blendings.Alpha;
        TexturePath = "Interface/newui_btn_empty_very_small.tga";
    }
}


public class LabelButtonGate : LabelButton
{
    public LabelButtonGate()
    {
        Interactive = true;
        TileWidth = 46;
        TileHeight = 36;
        ViewSize = new Point(TileWidth, TileHeight); // Ensure ViewSize is set from tile dimensions
        TileY = 0;
        BlendState = Blendings.Alpha;
        TexturePath = "Interface/newui_Btn_gate.tga";
    }
}

public class IconButtonGate : SpriteControl
{
    public SpriteControl Icon { get; set; }
    public IconButtonGate()
    {
        Interactive = true;
        TileWidth = 46;
        TileHeight = 36;
        ViewSize = new Point(TileWidth, TileHeight); // Ensure ViewSize is set from tile dimensions
        TileY = 0;
        BlendState = Blendings.Alpha;
        TexturePath = "Interface/newui_Btn_gate.tga";
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

        if (IsMouseOver && IsMousePressed)
        {
            TileY = 2;
            Icon.TileY = 1;
        } // check IsMousePressed from GameControl for click visual
        else if (IsMouseOver) // hover state
        {
            TileY = 1;
            Icon.TileY = 0;
        }
        else
        {
            TileY = 0;
            Icon.TileY = 0;
        }

    }
}


public class LabelButtonRound : LabelButton
{
    public LabelButtonRound()
    {
        Interactive = true;
        TileWidth = 77;
        TileHeight = 47;
        ViewSize = new Point(TileWidth, TileHeight); // Ensure ViewSize is set from tile dimensions
        TileY = 0;
        BlendState = Blendings.Alpha;
        TexturePath = "Interface/newui_Btn_round.tga";
    }
}


