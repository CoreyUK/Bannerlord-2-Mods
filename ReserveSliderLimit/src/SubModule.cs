using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace ReserveSliderLimit;

public sealed class SubModule : MBSubModuleBase
{
    private Harmony? _harmony;

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();

        _harmony = new Harmony("bannerlord.corey.reserve-slider-limit");
        _harmony.PatchAll();
    }

    protected override void OnSubModuleUnloaded()
    {
        _harmony?.UnpatchAll("bannerlord.corey.reserve-slider-limit");
        _harmony = null;

        base.OnSubModuleUnloaded();
    }
}
