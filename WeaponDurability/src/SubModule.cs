using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace WeaponDurability;

public sealed class SubModule : MBSubModuleBase
{
    private Harmony? _harmony;

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();

        TryLoadMcmBridge();
        TryLoadUiExtenderBridge();
        _harmony = new Harmony("cuk.bannerlord.weapon_durability");
        WeaponDurabilityDebugLog.Write("SubModule loading; applying Harmony patches.");
        _harmony.PatchAll();
        WeaponDurabilityDebugLog.Write("Harmony PatchAll complete.");
    }

    protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
    {
        base.OnGameStart(game, gameStarterObject);

        if (game.GameType is Campaign && gameStarterObject is CampaignGameStarter campaignStarter)
        {
            campaignStarter.AddBehavior(new WeaponDurabilityBehavior());
        }
    }

    private static void TryLoadMcmBridge()
    {
        try
        {
            bool mcmLoaded = AppDomain.CurrentDomain.GetAssemblies().Any(assembly => assembly.GetName().Name == "MCMv5");
            if (!mcmLoaded)
            {
                return;
            }

            string bridgePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "WeaponDurability.MCM.dll");
            if (File.Exists(bridgePath))
            {
                Assembly.LoadFrom(bridgePath);
            }
        }
        catch
        {
        }
    }

    private static void TryLoadUiExtenderBridge()
    {
        try
        {
            string bridgePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "WeaponDurability.UIExtender.dll");
            if (File.Exists(bridgePath))
            {
                Assembly bridge = Assembly.LoadFrom(bridgePath);
                bridge.GetType("WeaponDurability.UiExtender.WeaponDurabilityUiExtenderBridge")
                    ?.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)
                    ?.Invoke(null, null);
            }
        }
        catch
        {
        }
    }
}
