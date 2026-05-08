using System;
using System.IO;
using System.Linq;
using System.Reflection;
using TaleWorlds.MountAndBlade;

namespace TroopHealthBars;

public sealed class SubModule : MBSubModuleBase
{
    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();
        TryLoadMcmBridge();
    }

    public override void OnMissionBehaviorInitialize(Mission mission)
    {
        base.OnMissionBehaviorInitialize(mission);
        mission.AddMissionBehavior(new TroopHealthBarsMissionView());
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

            string bridgePath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty, "TroopHealthBars.MCM.dll");
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
