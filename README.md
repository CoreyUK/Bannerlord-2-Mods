# Bannerlord 2 Mods

Collection of Mount & Blade II: Bannerlord mods.

Each folder is a separate Bannerlord module:

- `CompanionDefense` - lets you send companion-led clan parties to defend owned villages, castles, and towns under attack.
- `CompanionHotswap` - companion swapping and selection UI support.
- `DuelCompanions` - adds roaming duel rumors, elite duelists, gauntlets, rare weapon rewards, and recruitable duel companions.
- `LoadoutPresets` - player loadout preset support.
- `MovingDismount` - lets the player attempt to dismount from a moving horse, with speed-scaled stumble and hard-fall damage.
- `ReserveSliderLimit` - raises the settlement reserve slider from 10,000 to 100,000. Requires Bannerlord Harmony.
- `RoyalHeirStart` - lets a new Sandbox character claim a kingdom as a lost royal heir with faction-specific starting bonuses.
- `StrategicCampaignAI` - improves kingdom army behavior with smarter frontline target selection, siege retreats, supply pressure, war goals, raid response, and persistent strategic memory.
- `StrategicCampaignAI145` - Bannerlord v1.4.5 build of Strategic Campaign AI with anti-army-spam tuning, stuck-army recovery, safer order overrides, and save-safe runtime memory.
- `TroopHealthBars` - adds compact battle HUD bars showing surviving infantry, archers, and mounted troops as percentages, with optional MCM settings for position, colours, opacity, percentages, and category toggles.

## Layout

Each module folder keeps the Bannerlord runtime layout:

- `SubModule.xml`
- `bin/Win64_Shipping_Client/*.dll`
- `GUI/` and `ModuleData/` where used
- `src/` source project

Intermediate build folders such as `src/obj` are intentionally excluded.

## Build Notes

Projects target `.NET Framework 4.7.2` and reference Bannerlord assemblies from a local Steam install. If your Bannerlord path differs, update the `BannerlordPath` property in the relevant `.csproj`.
