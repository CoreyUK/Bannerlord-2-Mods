using TaleWorlds.MountAndBlade;

namespace CompanionHotswap;

public sealed class SubModule : MBSubModuleBase
{
    public override void OnMissionBehaviorInitialize(Mission mission)
    {
        base.OnMissionBehaviorInitialize(mission);
        mission.AddMissionBehavior(new CompanionSwapBehavior());
        mission.AddMissionBehavior(new CompanionSelectBehavior());
    }
}
