using TaleWorlds.MountAndBlade;

namespace LoadoutPresets;

public sealed class SubModule : MBSubModuleBase
{
    private InventoryPresetOverlay? _overlay;

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();
        _overlay = new InventoryPresetOverlay();
    }

    protected override void OnSubModuleUnloaded()
    {
        _overlay?.Dispose();
        _overlay = null;
        base.OnSubModuleUnloaded();
    }

    protected override void OnApplicationTick(float dt)
    {
        base.OnApplicationTick(dt);
        _overlay?.Tick();
    }
}
