# Bannerlord 2 Mods

Collection of Mount & Blade II: Bannerlord mods.

Each folder is a separate Bannerlord module:

- `CompanionDefense` - lets you send companion-led clan parties to defend owned villages, castles, and towns under attack.
- `CompanionHotswap` - companion swapping and selection UI support.
- `LoadoutPresets` - player loadout preset support.
- `ReserveSliderLimit` - raises the settlement reserve slider from 10,000 to 100,000. Requires Bannerlord Harmony.

## Layout

Each module folder keeps the Bannerlord runtime layout:

- `SubModule.xml`
- `bin/Win64_Shipping_Client/*.dll`
- `GUI/` and `ModuleData/` where used
- `src/` source project

Intermediate build folders such as `src/obj` are intentionally excluded.

## Build Notes

Projects target `.NET Framework 4.7.2` and reference Bannerlord assemblies from a local Steam install. If your Bannerlord path differs, update the `BannerlordPath` property in the relevant `.csproj`.
