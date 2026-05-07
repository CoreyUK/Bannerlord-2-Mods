using HarmonyLib;
using System.Reflection;
using TaleWorlds.MountAndBlade;

namespace ReserveSliderLimit;

public sealed class SubModule : MBSubModuleBase
{
    private Harmony? _harmony;

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();

        _harmony = new Harmony("bannerlord.corey.reserve-slider-limit");
        _harmony.PatchAll(Assembly.GetExecutingAssembly());
    }

    protected override void OnSubModuleUnloaded()
    {
        _harmony?.UnpatchAll("bannerlord.corey.reserve-slider-limit");
        _harmony = null;

        base.OnSubModuleUnloaded();
    }
}
