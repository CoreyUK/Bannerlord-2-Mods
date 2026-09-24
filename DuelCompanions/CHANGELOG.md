# Changelog

## v1.1.0

Existing saves are safe to continue.

### New

- **Difficulty settings.** A `settings.txt` in the mod folder sets how strong the champions are —
  easy, normal, hard or legendary — and whether they grow stronger as your own melee skill rises.
  Legendary is the original tuning; the default is now normal. Champions' speed, damage, health
  and fighting tricks all scale with it. Edits apply to the next duel without restarting.
- **Lighter, tunable defeat penalties.** Losing now costs 10% of your denars (up to 20,000)
  instead of a flat 20,000, 25 morale instead of 70, and has a 20% chance of costing your weapon
  instead of 35%. All four are set in `settings.txt`, as are the rewards.
- **Recruit the gauntlet's champion.** Winning the gauntlet now lets you recruit its round-one
  champion, as a single duel does, alongside the gold and weapon.
- **Rumours you can reach.** Duel rumours now appear only in towns you are not at war with, among
  the dozen nearest to you, instead of anywhere on the map.

### Fixed

- **Dead duel companions no longer come back to life.** A background repair ran on every duel
  hero every few moments and forced any that weren't "active" back to active — reviving the
  dead and breaking captivity for companions taken prisoner. It now leaves the dead, prisoners
  and fugitives alone, and runs hourly instead of every frame.
- **Companions you assign elsewhere stay there.** On every load the mod pulled duel companions
  back into your party, including ones leading their own party or governing a town. It now only
  recovers a companion who is genuinely stranded with no party.
- **A gauntlet in progress survives saving and loading.** The round you were on, and your
  carried-over wounds, were not saved, so reloading between rounds dropped the gauntlet.
- **Wounds now carry between gauntlet rounds reliably.**
- **Losing your weapon after a defeat takes only the weapon you had equipped.** It could also
  take a spare copy of the same weapon from your inventory.
- **A failed recruitment no longer forfeits your reward.** You stay on the reward screen and can
  take the gold or the weapon instead.
- **Beaten duellists are cleaned up.** Every duel and gauntlet round left a hero behind for the
  rest of the campaign. Unrecruited ones are now removed once the event is over, including any
  left in existing saves.
- The project now builds from any game install location.

## v1.0.1

- Translations now load: each language folder was missing the `language_data.xml` the game needs,
  and the Spanish folder used the wrong language id.
