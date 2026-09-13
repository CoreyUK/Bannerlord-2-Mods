# Changelog

## v1.4.0

**The estate is now a place.** Walking the grounds shows what you have actually built:
every completed improvement places its buildings and props in the manor scene — palisade
stakes ring the yard, the mill and orchard stand in the fields, the guard quarters, training
butts, storehouse and workshop appear as you add them, and a landed or grand estate gains a
barn and a proper house. After a raid, wreckage litters the yard until repairs are done.

Your household guard stands in ranks in the yard, with the captain, steward and physician
about their business, whenever you visit. The same dressing applies during a manor defense.

The manor house itself stands at the head of the yard and grows with the estate's rank. Walk
up to its door and press **F** to review the ledger, open the storehouse, or move funds
without leaving the grounds.

The walls and the house are solid: the mod lays blocker navmesh along them, so neither you nor
anyone else can walk through. The estate menus now use the village backdrop instead of the
placeholder texture, and the trees that stood where the yard is have been cleared.

The layout lives in `ModuleData/manor_lord_props.xml` and can be edited without rebuilding.

## v1.3.0

**Localization support.** Manor Lord can now be translated. All 208 player-visible
strings — menus, options, tooltips, messages and dialogs — moved out of the code and
into `ModuleData/Languages/`.

Included translations:

- 简体中文 (Simplified Chinese)
- Deutsch
- Español (LA)
- Français
- Русский
- Türkçe

English remains the fallback, so any string a translation omits still displays correctly.

Adding a language needs no code changes — see `TRANSLATING.md`. Contributions welcome;
the community translations are a first pass and native-speaker corrections are wanted.

No gameplay changes. Existing saves are unaffected.
