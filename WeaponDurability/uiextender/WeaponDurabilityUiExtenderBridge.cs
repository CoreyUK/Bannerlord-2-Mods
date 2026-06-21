using Bannerlord.UIExtenderEx;

namespace WeaponDurability.UiExtender;

public static class WeaponDurabilityUiExtenderBridge
{
    private static UIExtender? _extender;

    public static void Initialize()
    {
        if (_extender != null)
        {
            return;
        }

        _extender = new UIExtender("WeaponDurability");
        _extender.Register(typeof(WeaponDurabilityUiExtenderBridge).Assembly);
        _extender.Enable();
    }
}
