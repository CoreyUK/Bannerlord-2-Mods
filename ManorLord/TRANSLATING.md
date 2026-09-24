# Translating Manor Lord

All player-visible text in this mod lives in `ModuleData/Languages/`. No code changes are
needed to add a language.

## Layout

```
ModuleData/Languages/
  std_module_strings_xml.xml      <- English master (the source of truth)
  CNs/std_module_strings_xml.xml  <- 简体中文
  DE/std_module_strings_xml.xml   <- Deutsch
  FR/std_module_strings_xml.xml   <- Français
  RU/std_module_strings_xml.xml   <- Русский
  SP/std_module_strings_xml.xml   <- Español (LA)
  TR/std_module_strings_xml.xml   <- Türkçe
```

## Adding a language

1. Copy `std_module_strings_xml.xml` into a new folder named with the game's language
   code (see `Modules/Native/ModuleData/Languages/` for the list: `BR`, `CNt`, `IT`,
   `JP`, `KO`, `PL`, ...).
2. Change the `<tag language="English" />` line to the language's exact id. Take it from
   that language's `Modules/Native/ModuleData/Languages/<CODE>/language_data.xml`, from
   the `id="..."` attribute — for example `id="Polski"` means `<tag language="Polski" />`.
   The id must match character for character, accents included, or the file is ignored.
3. **Add a `language_data.xml` next to it.** The game only loads a translation folder that
   declares its files this way; without it the strings file is silently ignored. Copy one
   of the existing ones (`RU/language_data.xml`, say), then replace the `id`, `name`,
   `subtitle_extension`, `supported_iso` and `text_processor` attributes with the values
   from the Native `language_data.xml` for your language, and set `xml_path` to
   `<CODE>/std_module_strings_xml.xml`.
4. Translate the `text="..."` values. Leave `id="..."` untouched.
5. Save both files as UTF-8.

Any string you leave out simply falls back to the English text, so a partial translation
is fine and will not break anything.

## Rules for the text

**Keep every `{PLACEHOLDER}` exactly as written.** They are substituted at runtime:

| Placeholder | Becomes |
|---|---|
| `{SETTLEMENT}` | the village name |
| `{MANOR_NAME}` | the estate's name |
| `{GUARDS}`, `{GUARD_CAP}` | guard count and capacity |
| `{TREASURY}`, `{GOLD}`, `{VALUE}`, `{REFUND}` | amounts in denars |
| `{DAYS}`, `{AMOUNT}`, `{PRICE}` | numbers |
| `{GOLD_ICON}` | the coin icon |
| `{ML_REPAIR_COST}`, `{ML_SALE_PRICE}` | menu-level values |

You may **reorder** placeholders freely to suit your grammar — the Turkish file does this,
putting `{SETTLEMENT}` before `{MANOR_NAME}`. You may not rename, translate, or drop them.
A missing placeholder means the number never appears in game.

**Newlines are written as `&#10;`.** A literal line break inside an XML attribute is
collapsed to a space by the parser, so the menu layout depends on the entity. Two of them
in a row (`&#10;&#10;`) makes a blank line.

**Escape these characters** in the text: `&` as `&amp;`, `<` as `&lt;`, `>` as `&gt;`,
and `"` as `&quot;`.

**`ml_list_separator`** joins the building and staff lists (`palisade, storehouse, mill`).
It defaults to a comma and a space. Chinese overrides it with the ideographic comma `、`.
Change it if your script needs something different.

## Notes on specific strings

- `ml_msg_guard_joined_one` and `ml_msg_guards_joined` are the singular and plural forms
  of the same message. Languages with more plural classes can make both forms read
  naturally for their most common case (the mod only ever passes 1 or 3).
- `ml_msg_order_watch` / `_patrol` / `_drill` and `ml_msg_focus_mixed` / `_crops` /
  `_livestock` are complete sentences rather than a template plus a noun, so you can
  inflect the whole line.
- `ml_cond_damaged` and `ml_cond_threat` are shown in caps as a status flag. Use whatever
  reads as emphatic in your language; caps are not required for scripts that lack them.
- `ml_scene_*` strings are diagnostics that only appear if the manor scene fails to load.
  They are worth translating but players should never see them.

## Checking your file

The game silently ignores a malformed language file. Before submitting, confirm the XML
parses — any browser or editor with XML support will tell you — and compare your file's
`id` list against the English master to make sure nothing was renamed by accident.
