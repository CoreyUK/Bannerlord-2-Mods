using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.Prefabs2;

namespace WeaponDurability.UiExtender;

[PrefabExtension("InventoryTooltip", "descendant::Widget[@Id='TargetItemTooltip']")]
public sealed class InventoryTooltipDurabilityPrefabPatch : PrefabExtensionInsertPatch
{
    public override InsertType Type => InsertType.Append;

    [PrefabExtensionText]
    public string Patch => "<TextWidget Text=\"@WeaponDurabilityText\" IsVisible=\"@HasWeaponDurability\" DoNotAcceptEvents=\"true\" WidthSizePolicy=\"CoverChildren\" HeightSizePolicy=\"Fixed\" MaxWidth=\"280\" SuggestedHeight=\"28\" MarginTop=\"58\" MarginLeft=\"20\" MarginRight=\"20\" HorizontalAlignment=\"Center\" Brush=\"InventoryDefaultFontBrush\" Brush.FontSize=\"20\" Brush.TextColor=\"#D6B56DFF\" Brush.TextVerticalAlignment=\"Center\" Brush.TextHorizontalAlignment=\"Center\"/>";
}
