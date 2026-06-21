using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace WeaponDurability.UiExtender;

[PrefabExtension("InventoryItemTuple", "descendant::Widget[@Id='MainControls']")]
public sealed class InventoryItemDurabilityOverlayPrefabPatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Append;

    [PrefabExtensionText]
    public string Patch =>
        "<Widget DoNotAcceptEvents=\"true\" WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\" SuggestedWidth=\"111\" SuggestedHeight=\"51\" MarginLeft=\"!Inventory.Tuple.ThumbnailMargin\" MarginTop=\"2\" HorizontalAlignment=\"Left\" IsVisible=\"@HasWeaponDurabilityOverlay\">" +
        "  <Children>" +
        "    <Widget WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\" Sprite=\"BlankWhiteSquare_9\" Color=\"#1FB64CFF\" AlphaFactor=\"0.14\" IsVisible=\"@ShowWeaponDurabilityGreen\"/>" +
        "    <Widget WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\" Sprite=\"BlankWhiteSquare_9\" Color=\"#D6B52AFF\" AlphaFactor=\"0.22\" IsVisible=\"@ShowWeaponDurabilityYellow\"/>" +
        "    <Widget WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\" Sprite=\"BlankWhiteSquare_9\" Color=\"#D67A1EFF\" AlphaFactor=\"0.32\" IsVisible=\"@ShowWeaponDurabilityAmber\"/>" +
        "    <Widget WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\" Sprite=\"BlankWhiteSquare_9\" Color=\"#B51F1FFF\" AlphaFactor=\"0.46\" IsVisible=\"@ShowWeaponDurabilityRed\"/>" +
        "    <Widget WidthSizePolicy=\"StretchToParent\" HeightSizePolicy=\"StretchToParent\" Sprite=\"BlankWhiteSquare_9\" Color=\"#000000FF\" AlphaFactor=\"0.72\" IsVisible=\"@ShowWeaponDurabilityBlack\"/>" +
        "    <TextWidget Text=\"@WeaponDurabilityOverlayText\" WidthSizePolicy=\"Fixed\" HeightSizePolicy=\"Fixed\" SuggestedWidth=\"42\" SuggestedHeight=\"20\" HorizontalAlignment=\"Right\" VerticalAlignment=\"Bottom\" MarginRight=\"2\" MarginBottom=\"1\" Brush=\"InventoryDefaultFontBrush\" Brush.FontSize=\"14\" Brush.TextColor=\"#FFFFFFFF\" Brush.TextHorizontalAlignment=\"Right\" Brush.TextVerticalAlignment=\"Center\"/>" +
        "  </Children>" +
        "</Widget>";
}
