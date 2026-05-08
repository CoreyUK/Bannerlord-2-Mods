using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using MCM.Abstractions.Base.Global;
using MCM.Common;
using TroopHealthBars;

namespace TroopHealthBars.Mcm;

public sealed class TroopHealthBarsMcmSettings : AttributeGlobalSettings<TroopHealthBarsMcmSettings>
{
    public override string Id => "TroopHealthBars";
    public override string DisplayName => "Troop Health Bars";
    public override string FolderName => "TroopHealthBars";
    public override string FormatType => "json";

    [SettingPropertyDropdown("HUD Location", Order = 0, RequireRestart = false, HintText = "Choose where the troop bars appear during battle.")]
    [SettingPropertyGroup("Layout", GroupOrder = 0)]
    public Dropdown<string> Location { get; set; } = new(new[] { "Top Left", "Bottom Right", "Bottom Left" }, 0);

    [SettingPropertyDropdown("Bar Colour", Order = 1, RequireRestart = false, HintText = "Choose the fill colour used by the troop bars.")]
    [SettingPropertyGroup("Layout")]
    public Dropdown<string> BarColor { get; set; } = new(new[] { "Bannerlord Red", "Deep Red", "Gold", "Green", "Blue" }, 0);

    [SettingPropertyFloatingInteger("Opacity", 0.15f, 1f, "#0%", Order = 2, RequireRestart = false, HintText = "Adjust the opacity of the troop bars.")]
    [SettingPropertyGroup("Layout")]
    public float Opacity { get; set; } = 1f;

    [SettingPropertyBool("Show Percentages", Order = 3, RequireRestart = false)]
    [SettingPropertyGroup("Layout")]
    public bool ShowPercentages { get; set; } = true;

    [SettingPropertyBool("Show Infantry", Order = 0, RequireRestart = false)]
    [SettingPropertyGroup("Troop Bars", GroupOrder = 1)]
    public bool ShowInfantry { get; set; } = true;

    [SettingPropertyBool("Show Archers", Order = 1, RequireRestart = false)]
    [SettingPropertyGroup("Troop Bars")]
    public bool ShowArchers { get; set; } = true;

    [SettingPropertyBool("Show Mounted", Order = 2, RequireRestart = false)]
    [SettingPropertyGroup("Troop Bars")]
    public bool ShowMounted { get; set; } = true;
}
