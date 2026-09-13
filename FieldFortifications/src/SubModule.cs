using System;
using System.IO;
using System.Runtime.ExceptionServices;
using HarmonyLib;
using TaleWorlds.Library;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace FieldFortifications;

public sealed class SubModule : MBSubModuleBase
{
    private static int _traced;

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();
        if (FortificationSettings.Load().Debug)
            AppDomain.CurrentDomain.FirstChanceException += TraceException;
        try
        {
            SiegeAiPatches.Apply(new Harmony("FieldFortifications"));
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Harmony patching failed; AI crews will crash the game in field battles: " + ex);
        }
    }

    /// <summary>Debug aid: records where the game throws during a battle, since the crash reporter hides it.</summary>
    private static void TraceException(object sender, FirstChanceExceptionEventArgs e)
    {
        if (Mission.Current == null || _traced >= 10) return;
        Exception ex = e.Exception;
        if (ex is not NullReferenceException && ex is not IndexOutOfRangeException && ex is not InvalidCastException) return;
        _traced++;
        try
        {
            string path = System.IO.Path.Combine(BasePath.Name, "Modules", "FieldFortifications", "debug.txt");
            File.AppendAllText(path, DateTime.Now.ToString("HH:mm:ss") + " " + ex.GetType().Name + ": " + ex.Message + Environment.NewLine + ex.StackTrace + Environment.NewLine);
        }
        catch { /* diagnostics only */ }
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
