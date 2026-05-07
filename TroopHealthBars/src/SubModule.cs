using TaleWorlds.MountAndBlade;

namespace TroopHealthBars;

public sealed class SubModule : MBSubModuleBase
{
    public override void OnMissionBehaviorInitialize(Mission mission)
    {
        base.OnMissionBehaviorInitialize(mission);
        mission.AddMissionBehavior(new TroopHealthBarsMissionView());
    }
}
