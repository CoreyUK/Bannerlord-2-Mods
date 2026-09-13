using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ManorLord;

public sealed class SubModule : MBSubModuleBase
{
    protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
    {
        base.OnGameStart(game, gameStarterObject);
        if (game.GameType is Campaign && gameStarterObject is CampaignGameStarter starter)
            starter.AddBehavior(new ManorLordBehavior());
    }

    public override void OnMissionBehaviorInitialize(Mission mission)
    {
        base.OnMissionBehaviorInitialize(mission);
        if (!ManorDefenseMissionState.TryConsume(out bool defense, out EstateSnapshot estate)) return;
        // The yard is dressed in both modes; guards stand in formation only on a peaceful visit.
        mission.AddMissionBehavior(new ManorEstateSceneBehavior(estate, spawnPeople: !defense));
        if (defense) mission.AddMissionBehavior(new ManorDefenseMissionBehavior());
    }
}
