using TaleWorlds.MountAndBlade;

namespace MovingDismount;

public sealed class SubModule : MBSubModuleBase
{
    public override void OnMissionBehaviorInitialize(Mission mission)
    {
        base.OnMissionBehaviorInitialize(mission);
        mission.AddMissionBehavior(new MovingDismountBehavior());
    }
}
