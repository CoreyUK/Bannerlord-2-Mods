using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace FieldFortifications;

public sealed class SubModule : MBSubModuleBase
{
    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();
        try
        {
            SiegeAiPatches.Apply(new Harmony("FieldFortifications"));
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Harmony patching failed; AI crews will crash the game in field battles: " + ex);
        }
    }

    protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
    {
        base.OnGameStart(game, gameStarterObject);
        if (game.GameType is Campaign && gameStarterObject is CampaignGameStarter starter)
            starter.AddBehavior(new FortificationCampaignBehavior());
    }

    public override void OnMissionBehaviorInitialize(Mission mission)
    {
        base.OnMissionBehaviorInitialize(mission);
        // The flags stay set until the map event ends, so a battle fought in several rounds keeps its works.
        if (FortificationState.AnyPending)
            mission.AddMissionBehavior(new FortificationMissionBehavior());
    }
}
