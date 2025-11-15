

using Client.Main.Models;
using MUnique.OpenMU.Network.Packets;

namespace Client.Main.Core.Utilities;

public class AppearanceConfig
{
    public PlayerClass PlayerClass { get; set; } = PlayerClass.DarkWizard;
    public byte Pose { get; set; } = 0;


    // LEFT HAND
    public byte LeftHandItemIndex { get; set; } = 0xFF;
    public byte LeftHandItemGroup { get; set; } = 0;
    public byte LeftHandItemLevel { get; set; } = 0;
    public bool LeftHandExcellent { get; set; } = false;
    public bool LeftHandAncient { get; set; } = false;

    // RIGHT HAND
    public byte RightHandItemIndex { get; set; } = 0xFF;
    public byte RightHandItemGroup { get; set; } = 0;
    public byte RightHandItemLevel { get; set; } = 0;
    public bool RightHandExcellent { get; set; } = false;
    public bool RightHandAncient { get; set; } = false;

    // HELM
    public int HelmItemIndex { get; set; } = 0xFFFF;
    public int HelmItemLevel { get; set; } = 0;
    public bool HelmExcellent { get; set; } = false;
    public bool HelmAncient { get; set; } = false;

    // ARMOR
    public int ArmorItemIndex { get; set; } = 0xFFFF;
    public int ArmorItemLevel { get; set; } = 0;
    public bool ArmorExcellent { get; set; } = false;
    public bool ArmorAncient { get; set; } = false;

    // PANTS
    public int PantsItemIndex { get; set; } = 0xFFFF;
    public int PantsItemLevel { get; set; } = 0;
    public bool PantsExcellent { get; set; } = false;
    public bool PantsAncient { get; set; } = false;

    // GLOVES
    public int GlovesItemIndex { get; set; } = 0xFFFF;
    public int GlovesItemLevel { get; set; } = 0;
    public bool GlovesExcellent { get; set; } = false;
    public bool GlovesAncient { get; set; } = false;

    // BOOTS
    public int BootsItemIndex { get; set; } = 0xFFFF;
    public int BootsItemLevel { get; set; } = 0;
    public bool BootsExcellent { get; set; } = false;
    public bool BootsAncient { get; set; } = false;

    // PET
    public byte Pet { get; set; } = 0;

    // WING
    public WingAppearance WingInfo { get; set; } = new WingAppearance(0, 0);

    // RIDE
    public bool HasDarkHorse { get; set; } = false;
    public bool HasFenrir { get; set; } = false;
    public bool HasGoldFenrir { get; set; } = false;
    public bool HasBlackFenrir { get; set; } = false;
    public bool HasBlueFenrir { get; set; } = false;
    public bool HasDinorant { get; set; } = false;
    public bool HasImp { get; set; } = false;
    public bool HasUnicorn { get; set; } = false;
    public bool HasSkeleton { get; set; } = false;
    public bool HasRudolph { get; set; } = false;
    public bool HasSpiritOfGuardian { get; set; } = false;
    public short RidingVehicle { get; set; }
}
