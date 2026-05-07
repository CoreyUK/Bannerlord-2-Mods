using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;

namespace LoadoutPresets;

internal sealed class LoadoutPresetService
{
    public const int PresetCount = 3;
    private static readonly EquipmentIndex[] Slots =
    {
        EquipmentIndex.Weapon0,
        EquipmentIndex.Weapon1,
        EquipmentIndex.Weapon2,
        EquipmentIndex.Weapon3,
        EquipmentIndex.Head,
        EquipmentIndex.Body,
        EquipmentIndex.Leg,
        EquipmentIndex.Gloves,
        EquipmentIndex.Cape,
        EquipmentIndex.Horse,
        EquipmentIndex.HorseHarness
    };

    private readonly LoadoutPreset[] _presets = Enumerable.Range(0, PresetCount).Select(_ => new LoadoutPreset()).ToArray();
    private bool _loaded;

    public static LoadoutPresetService Instance { get; } = new();

    public bool HasPreset(int index)
    {
        EnsureLoaded();
        return IsValid(index) && _presets[index].Slots.Any(slot => !string.IsNullOrWhiteSpace(slot.ItemId));
    }

    public string GetSummary(int index)
    {
        EnsureLoaded();
        if (!IsValid(index) || !HasPreset(index))
        {
            return "Empty";
        }

        int itemCount = _presets[index].Slots.Count(slot => !string.IsNullOrWhiteSpace(slot.ItemId));
        return itemCount == 1 ? "1 item" : itemCount + " items";
    }

    public string SavePreset(int index, int equipmentMode)
    {
        EnsureLoaded();
        if (!IsValid(index))
        {
            return "Invalid preset";
        }

        Hero? hero = Hero.MainHero;
        Equipment? equipment = GetActiveEquipment(hero, equipmentMode);
        if (equipment == null)
        {
            return "No player equipment";
        }

        LoadoutPreset preset = new();
        foreach (EquipmentIndex slot in Slots)
        {
            EquipmentElement element = equipment[slot];
            if (!element.IsEmpty && element.Item != null)
            {
                preset.Slots.Add(new PresetItem
                {
                    Slot = slot.ToString(),
                    ItemId = element.Item.StringId,
                    ModifierId = element.ItemModifier?.StringId ?? string.Empty
                });
            }
        }

        _presets[index] = preset;
        Save();
        return "Saved preset " + (index + 1);
    }

    public string ApplyPreset(int index, int equipmentMode)
    {
        EnsureLoaded();
        if (!IsValid(index) || !HasPreset(index))
        {
            return "Preset " + (index + 1) + " is empty";
        }

        Hero? hero = Hero.MainHero;
        Equipment? equipment = GetActiveEquipment(hero, equipmentMode);
        MobileParty? mainParty = MobileParty.MainParty;
        if (hero == null || equipment == null || mainParty?.ItemRoster == null)
        {
            return "Inventory is unavailable";
        }

        List<ResolvedPresetItem> desired = ResolvePreset(_presets[index]);
        if (desired.Count == 0)
        {
            return "Preset items no longer exist";
        }

        ItemRoster roster = mainParty.ItemRoster;
        int skipped = 0;

        foreach (EquipmentIndex slot in Slots)
        {
            EquipmentElement current = equipment[slot];
            if (!current.IsEmpty && current.Item != null)
            {
                roster.AddToCounts(current, 1);
                equipment.AddEquipmentToSlotWithoutAgent(slot, default);
            }
        }

        foreach (ResolvedPresetItem desiredItem in desired)
        {
            if (roster.FindIndexOfElement(desiredItem.Element) < 0)
            {
                skipped++;
                continue;
            }

            roster.AddToCounts(desiredItem.Element, -1);
            equipment.AddEquipmentToSlotWithoutAgent(desiredItem.Slot, desiredItem.Element);
        }

        hero.CheckInvalidEquipmentsAndReplaceIfNeeded();
        return skipped == 0
            ? "Equipped preset " + (index + 1)
            : "Equipped preset " + (index + 1) + ", missing " + skipped;
    }

    private static Equipment? GetActiveEquipment(Hero? hero, int equipmentMode)
    {
        if (hero == null)
        {
            return null;
        }

        return equipmentMode switch
        {
            0 => hero.CivilianEquipment,
            2 => hero.StealthEquipment,
            _ => hero.BattleEquipment
        };
    }

    private static List<ResolvedPresetItem> ResolvePreset(LoadoutPreset preset)
    {
        List<ResolvedPresetItem> result = new();
        MBObjectManager objectManager = MBObjectManager.Instance;

        foreach (PresetItem saved in preset.Slots)
        {
            if (!Enum.TryParse(saved.Slot, out EquipmentIndex slot) || string.IsNullOrWhiteSpace(saved.ItemId))
            {
                continue;
            }

            ItemObject? item = objectManager.GetObject<ItemObject>(saved.ItemId);
            if (item == null)
            {
                continue;
            }

            ItemModifier? modifier = string.IsNullOrWhiteSpace(saved.ModifierId)
                ? null
                : objectManager.GetObject<ItemModifier>(saved.ModifierId);

            result.Add(new ResolvedPresetItem(slot, new EquipmentElement(item, modifier, null, false)));
        }

        return result;
    }

    private void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        string path = GetPresetPath();
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            XDocument document = XDocument.Load(path);
            foreach (XElement presetElement in document.Root?.Elements("Preset") ?? Enumerable.Empty<XElement>())
            {
                int index = (int?)presetElement.Attribute("index") ?? -1;
                if (!IsValid(index))
                {
                    continue;
                }

                LoadoutPreset preset = new();
                foreach (XElement slotElement in presetElement.Elements("Slot"))
                {
                    preset.Slots.Add(new PresetItem
                    {
                        Slot = (string?)slotElement.Attribute("name") ?? string.Empty,
                        ItemId = (string?)slotElement.Attribute("item") ?? string.Empty,
                        ModifierId = (string?)slotElement.Attribute("modifier") ?? string.Empty
                    });
                }

                _presets[index] = preset;
            }
        }
        catch
        {
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(GetPresetPath())!);
        XDocument document = new(new XElement("LoadoutPresets",
            _presets.Select((preset, index) => new XElement("Preset",
                new XAttribute("index", index),
                preset.Slots.Select(slot => new XElement("Slot",
                    new XAttribute("name", slot.Slot),
                    new XAttribute("item", slot.ItemId),
                    new XAttribute("modifier", slot.ModifierId)))))));

        document.Save(GetPresetPath());
    }

    private static string GetPresetPath()
    {
        string assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
        string moduleDirectory = Path.GetFullPath(Path.Combine(assemblyDirectory, "..", ".."));
        return Path.Combine(moduleDirectory, "ModuleData", "player_loadout_presets.xml");
    }

    private static bool IsValid(int index) => index >= 0 && index < PresetCount;

    private sealed class LoadoutPreset
    {
        public List<PresetItem> Slots { get; } = new();
    }

    private sealed class PresetItem
    {
        public string Slot { get; set; } = string.Empty;
        public string ItemId { get; set; } = string.Empty;
        public string ModifierId { get; set; } = string.Empty;
    }

    private readonly struct ResolvedPresetItem
    {
        public ResolvedPresetItem(EquipmentIndex slot, EquipmentElement element)
        {
            Slot = slot;
            Element = element;
        }

        public EquipmentIndex Slot { get; }
        public EquipmentElement Element { get; }
    }
}
