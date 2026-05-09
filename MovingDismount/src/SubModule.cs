using HarmonyLib;
using TaleWorlds.MountAndBlade;

namespace MovingDismount;

public sealed class SubModule : MBSubModuleBase
{
    private Harmony? _harmony;

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();

        _harmony = new Harmony("corey.bannerlord.movingdismount");
        _harmony.PatchAll();
    }

    protected override void OnSubModuleUnloaded()
    {
        _harmony?.UnpatchAll("corey.bannerlord.movingdismount");
        _harmony = null;

        base.OnSubModuleUnloaded();
    }

    public override void OnMissionBehaviorInitialize(Mission mission)
    {
        base.OnMissionBehaviorInitialize(mission);
        mission.AddMissionBehavior(new MovingDismountBehavior());
    }
}
