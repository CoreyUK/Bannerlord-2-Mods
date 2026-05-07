using System;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace RoyalHeirStart;

public sealed class SubModule : MBSubModuleBase
{
    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();
        RoyalHeirStartLog.Write("SubModule loaded.");
    }

    protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
    {
        base.OnGameStart(game, gameStarterObject);
        RoyalHeirStartLog.Write($"OnGameStart: {game.GameType?.GetType().FullName ?? "null"}");

        if (game.GameType is Campaign && gameStarterObject is CampaignGameStarter campaignStarter)
        {
            RoyalHeirStartLog.Write("Registering RoyalHeirStartBehavior.");
            campaignStarter.AddBehavior(new RoyalHeirStartBehavior());
        }
    }
}

internal static class RoyalHeirStartLog
{
    private const string LogPath = @"C:\tmp\RoyalHeirStart.log";

    public static void Write(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
