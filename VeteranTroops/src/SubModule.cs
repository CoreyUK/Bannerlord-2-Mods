using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace VeteranTroops;

public sealed class SubModule : MBSubModuleBase
{
    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();
        TryLoadMcmBridge();
    }

    protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
    {
        base.OnGameStart(game, gameStarterObject);

        if (game.GameType is Campaign && gameStarterObject is CampaignGameStarter campaignStarter)
        {
            campaignStarter.AddBehavior(new VeteranTroopsBehavior());
        }
    }

    public override void OnMissionBehaviorInitialize(Mission mission)
    {
        base.OnMissionBehaviorInitialize(mission);

        // Only campaign missions have a party roster to read veterancy from.
        if (Campaign.Current != null)
        {
            mission.AddMissionBehavior(new VeteranTroopsMissionBehavior());
        }
    }

    /// <summary>
    /// The MCM settings page lives in a separate assembly so the mod still loads
    /// when MCM is not installed. Same pattern as TroopHealthBars.
    /// </summary>
    private static void TryLoadMcmBridge()
    {
        try
        {
            bool mcmLoaded = AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "MCMv5");
            if (!mcmLoaded)
            {
                return;
            }

            string bridgePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "VeteranTroops.MCM.dll");
            if (File.Exists(bridgePath))
            {
                Assembly.LoadFrom(bridgePath);
            }
        }
        catch
        {
        }
    }
}
