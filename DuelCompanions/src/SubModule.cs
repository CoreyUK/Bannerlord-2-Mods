using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace DuelCompanions;

public sealed class SubModule : MBSubModuleBase
{
    protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
    {
        base.OnGameStart(game, gameStarterObject);

        if (game.GameType is Campaign && gameStarterObject is CampaignGameStarter campaignStarter)
        {
            campaignStarter.AddBehavior(new DuelCompanionsCampaignBehavior());
        }
    }

    public override void OnMissionBehaviorInitialize(Mission mission)
    {
        base.OnMissionBehaviorInitialize(mission);

        if (DuelCompanionsMissionState.TryConsumeNextDuel(out bool isGauntlet))
        {
            mission.AddMissionBehavior(new DuelCompanionsCombatBehavior(isGauntlet));
        }
    }
}
