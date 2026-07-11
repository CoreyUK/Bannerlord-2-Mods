using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace StrategicCampaignAI145;

public sealed class SubModule : MBSubModuleBase
{
    protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
    {
        base.OnGameStart(game, gameStarterObject);

        if (game.GameType is Campaign && gameStarterObject is CampaignGameStarter campaignStarter)
        {
            campaignStarter.AddModel(new StrategicTargetScoreModel());
            campaignStarter.AddModel(new StrategicArmyManagementModel());
            campaignStarter.AddBehavior(new StrategicCampaignAI145Behavior());
        }
    }
}

