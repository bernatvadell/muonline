using Client.Main.Content;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Client.Main.Objects.Vehicle;

public class VehicleObject : ModelObject
{

    private short itemIndex = -1;
    public short ItemIndex
    {
        get => itemIndex;
        set
        {
            if (itemIndex == value) return;
            itemIndex = value;
            _ = OnChangeIndex();
        }

    }

    public VehicleObject()
    {
        RenderShadow = true;
        IsTransparent = true;
        AffectedByTransparency = true;
        BlendState = BlendState.AlphaBlend;
        BlendMesh = -1;
        BlendMeshState = BlendState.Additive;
        Alpha = 1f;
        LinkParentAnimation = false;
    }

    private async Task OnChangeIndex()
    {
        if (ItemIndex < 0)
        {
            Model = null;
            return;
        }
        VehicleDefinition riderDefinition = VehicleDatabase.GetVehicleDefinition(itemIndex);
        if (riderDefinition == null) return;

        string modelPath = riderDefinition.TexturePath;

        Model = await BMDLoader.Instance.Prepare(Path.Combine("Skill", modelPath));

        if (Model == null)
        {
            Status = GameControlStatus.Error;
        }
        else if (Status == GameControlStatus.Error)
        {
            Status = GameControlStatus.Ready;
        }
    }

    public override async Task Load()
    {
        await base.Load();
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

    }

    public override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);
        foreach (var child in Children)
        {
            child.Draw(gameTime);
        }
    }

    public override void DrawAfter(GameTime gameTime)
    {
        base.DrawAfter(gameTime);
        foreach (var child in Children)
        {
            child.DrawAfter(gameTime);
        }
    }
}