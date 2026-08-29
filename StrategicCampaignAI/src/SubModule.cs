using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace StrategicCampaignAI;

public sealed class SubModule : MBSubModuleBase
{
    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();
        StrategicAiSettings.LoadOnce();
    }

    protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
    {
        base.OnGameStart(game, gameStarterObject);

        if (game.GameType is Campaign && gameStarterObject is CampaignGameStarter campaignStarter)
        {
            campaignStarter.AddModel(new StrategicTargetScoreModel());
            campaignStarter.AddModel(new StrategicArmyManagementModel());
            campaignStarter.AddModel(new StrategicGarrisonModel());
            campaignStarter.AddModel(new StrategicPartyAIModel());
            campaignStarter.AddBehavior(new StrategicCampaignAIBehavior());
        }
    }
}
