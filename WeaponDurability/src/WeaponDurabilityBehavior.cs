using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace WeaponDurability;

public sealed class WeaponDurabilityBehavior : CampaignBehaviorBase
{
    private const int MaxDurability = 100;
    private const int MinDurability = 0;
    private const string RepairMenuOptionId = "wd_repair_weapons_at_smithy";
    private const string RepairSoundEvent = "event:/ui/crafting/crafting_success";
    private const int CriticalDurabilityThreshold = 25;
    private const int MaintenanceSkillRequirement = 90;
    private const int MaintenanceTargetDurability = 90;
    private const int PostBattleMaintenanceGain = 15;
    private const string DurabilityModifierPrefix = "wd_";
    private const string CharcoalItemId = "charcoal";

    private static WeaponDurabilityBehavior? _instance;
    private static readonly EquipmentIndex[] WeaponSlots =
    {
        EquipmentIndex.Weapon0,
        EquipmentIndex.Weapon1,
        EquipmentIndex.Weapon2,
        EquipmentIndex.Weapon3
    };

    private Dictionary<string, int> _durabilityByWeaponKey = new();
    private CampaignTime _maintenanceEndTime = CampaignTime.Never;
    private bool _maintenanceInProgress;

    public WeaponDurabilityBehavior()
    {
        _instance = this;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.OnHeroCombatHitEvent.AddNonSerializedListener(this, OnHeroCombatHit);
        CampaignEvents.OnNewItemCraftedEvent.AddNonSerializedListener(this, OnNewItemCrafted);
        CampaignEvents.OnPlayerBattleEndEvent.AddNonSerializedListener(this, OnPlayerBattleEnd);
        CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, OnHourlyTick);
    }

    public override void SyncData(IDataStore dataStore)
    {
        dataStore.SyncData("wd_durability_by_weapon_key", ref _durabilityByWeaponKey);
        dataStore.SyncData("wd_maintenance_end_time", ref _maintenanceEndTime);
        dataStore.SyncData("wd_maintenance_in_progress", ref _maintenanceInProgress);
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        starter.AddGameMenuOption(
            "town",
            RepairMenuOptionId,
            "{=wd_repair_weapons}Repair weapons at the smithy",
            RepairWeaponsCondition,
            RepairWeaponsConsequence,
            false,
            4);
    }

    private bool RepairWeaponsCondition(MenuCallbackArgs args)
    {
        args.optionLeaveType = GameMenuOption.LeaveType.Craft;
        return Settlement.CurrentSettlement?.IsTown == true && GetRepairableWeaponStacks().Any();
    }

    private void RepairWeaponsConsequence(MenuCallbackArgs args)
    {
        ShowSmithyWeaponsMenu();
    }

    private void OnHeroCombatHit(CharacterObject attacker, CharacterObject victim, PartyBase party, WeaponComponentData weapon, bool isFatal, int damage)
    {
        if (!WeaponDurabilitySettings.DurabilityLossEnabled ||
            party != MobileParty.MainParty?.Party ||
            weapon == null ||
            !weapon.IsMeleeWeapon)
        {
            return;
        }

        WeaponStack? stack = FindAttackerWeaponStack(attacker, weapon);
        if (stack == null)
        {
            return;
        }

        int current = GetDurability(stack.Key);
        if (current <= MinDurability)
        {
            return;
        }

        int newDurability = current - Math.Max(1, WeaponDurabilitySettings.HitDurabilityLoss);
        SetDurability(stack.Key, newDurability);
        if (!stack.IsTroopMaintenance)
        {
            ApplyDurabilityModifier(stack, newDurability);
        }

        if (current == 76)
        {
            InformationManager.DisplayMessage(new InformationMessage(
                $"{stack.Name} is worn and should be repaired at a town smithy.",
                new Color(0.95f, 0.68f, 0.22f, 1f)));
        }
    }

    private void OnNewItemCrafted(ItemObject itemObject, ItemModifier itemModifier, bool isCraftingOrderItem)
    {
        if (itemObject?.HasWeaponComponent != true)
        {
            return;
        }

        SetDurability(BuildKey(itemObject, itemModifier), MaxDurability);
    }

    private void ShowSmithyWeaponsMenu()
    {
        List<WeaponStack> allWeapons = GetRepairableWeaponStacks().ToList();
        bool canMaintain = GetMaintenanceSmith() != null && allWeapons.Any(stack => GetDurability(stack.Key) < MaintenanceTargetDurability);

        var elements = new List<InquiryElement>
        {
            new InquiryElement(RepairCategory.Personal, "Repair personal weapons", null, allWeapons.Any(stack => stack.Category == RepairCategory.Personal), "Repair your equipped and inventory weapons."),
            new InquiryElement(RepairCategory.Companions, "Repair companion weapons", null, allWeapons.Any(stack => stack.Category == RepairCategory.Companions), "Repair weapons equipped by companions."),
            new InquiryElement(RepairCategory.Company, "Repair company / army weapons", null, allWeapons.Any(stack => stack.Category == RepairCategory.Company), "Repair regular troop weapons as denar-only party maintenance."),
            new InquiryElement("maintain", "Maintain weapons to 90%", null, canMaintain, BuildMaintenanceHint(allWeapons))
        };

        var inquiry = new MultiSelectionInquiryData(
            "Repair Weapons at Smithy",
            "Choose which weapons the smith should inspect.",
            elements,
            true,
            1,
            1,
            "Select",
            "Leave",
            OnSmithyMenuOptionSelected,
            null,
            string.Empty,
            false);

        MBInformationManager.ShowMultiSelectionInquiry(inquiry, true, false);
    }

    private void OnSmithyMenuOptionSelected(List<InquiryElement> selectedElements)
    {
        object? selected = selectedElements.FirstOrDefault()?.Identifier;
        if (selected is RepairCategory category)
        {
            ShowRepairInquiry(category);
            return;
        }

        if (selected is string value && value == "maintain")
        {
            StartMaintenance();
        }
    }

    private void ShowRepairInquiry(RepairCategory category)
    {
        List<WeaponStack> weapons = GetRepairableWeaponStacks()
            .Where(stack => stack.Category == category)
            .ToList();
        if (weapons.Count == 0)
        {
            InformationManager.DisplayMessage(new InformationMessage("You have no weapons that can be repaired."));
            return;
        }

        List<InquiryElement> elements = weapons
            .Select(stack =>
            {
                int durability = GetDurability(stack.Key);
                int cost = GetRepairCost(stack, durability);
                Hero? smith = stack.IsTroopMaintenance ? Hero.MainHero : GetBestAvailableSmith(durability);
                bool meetsSkill = stack.IsTroopMaintenance || !WeaponDurabilitySettings.RequireSmithingSkill || smith != null;
                List<RepairMaterialRequirement> materials = stack.IsTroopMaintenance ? new List<RepairMaterialRequirement>() : GetRepairMaterials(stack, durability);
                bool hasMaterials = HasRepairMaterials(materials);
                bool canRepair = durability < MaxDurability && meetsSkill && hasMaterials && Hero.MainHero.Gold >= cost;
                string hint = durability >= MaxDurability ? "Already fully repaired." : $"Repair cost: {cost} denars.";
                hint = $"{stack.Name}\n{hint}";
                if (stack.IsTroopMaintenance && durability < MaxDurability)
                {
                    hint += "\nRegular troop weapons are repaired as party maintenance and require denars only.";
                }

                if (!stack.IsTroopMaintenance && durability < MaxDurability && WeaponDurabilitySettings.RequireSmithingSkill)
                {
                    int requiredSkill = GetRequiredSmithingSkill(durability);
                    hint += smith == null
                        ? $"\nRequires Smithing {requiredSkill}."
                        : $"\nSmith: {smith.Name} ({smith.GetSkillValue(DefaultSkills.Crafting)} Smithing).";
                }

                if (!stack.IsTroopMaintenance && durability < MaxDurability && materials.Count > 0)
                {
                    hint += $"\nMaterials: {BuildMaterialText(materials)}.";
                    string missingMaterials = BuildMissingMaterialText(materials);
                    if (!string.IsNullOrWhiteSpace(missingMaterials))
                    {
                        hint += $"\nMissing: {missingMaterials}.";
                    }
                }

                if (durability < MaxDurability && Hero.MainHero.Gold < cost)
                {
                    hint += $"\nYou need {cost - Hero.MainHero.Gold} more denars.";
                }

                string label = BuildRepairListLabel(stack, durability, cost);

                return new InquiryElement(stack, label, new ItemImageIdentifier(stack.Item), canRepair, hint);
            })
            .ToList();

        var inquiry = new MultiSelectionInquiryData(
            GetRepairTitle(category),
            "The smith lays out your weapons and checks each edge, head, grip, and binding.",
            elements,
            true,
            1,
            1,
            "Repair",
            "Back",
            OnRepairWeaponSelected,
            _ => ShowSmithyWeaponsMenu(),
            string.Empty,
            false);

        MBInformationManager.ShowMultiSelectionInquiry(inquiry, true, false);
    }

    private static string GetRepairTitle(RepairCategory category)
    {
        return category switch
        {
            RepairCategory.Personal => "Repair Personal Weapons",
            RepairCategory.Companions => "Repair Companion Weapons",
            RepairCategory.Company => "Repair Company Weapons",
            _ => "Repair Weapons"
        };
    }

    private string BuildMaintenanceHint(List<WeaponStack> allWeapons)
    {
        Hero? smith = GetMaintenanceSmith();
        if (smith == null)
        {
            return $"Requires the player or a companion with Smithing {MaintenanceSkillRequirement}.";
        }

        List<WeaponStack> damaged = allWeapons
            .Where(stack => GetDurability(stack.Key) < MaintenanceTargetDurability)
            .ToList();
        if (damaged.Count == 0)
        {
            return "No weapons need maintenance.";
        }

        float hours = GetMaintenanceHours(damaged);
        return $"{smith.Name} will maintain {damaged.Count} weapon entries to {MaintenanceTargetDurability}% over about {FormatMaintenanceTime(hours)}. No denars or materials required.";
    }

    private void StartMaintenance()
    {
        Hero? smith = GetMaintenanceSmith();
        if (smith == null)
        {
            InformationManager.DisplayMessage(new InformationMessage(
                $"You need the player or a companion with Smithing {MaintenanceSkillRequirement} to maintain weapons.",
                new Color(0.95f, 0.2f, 0.18f, 1f)));
            return;
        }

        List<WeaponStack> damaged = GetRepairableWeaponStacks()
            .Where(stack => GetDurability(stack.Key) < MaintenanceTargetDurability)
            .ToList();
        if (damaged.Count == 0)
        {
            InformationManager.DisplayMessage(new InformationMessage("No weapons need maintenance."));
            return;
        }

        float hours = GetMaintenanceHours(damaged);
        _maintenanceInProgress = true;
        _maintenanceEndTime = CampaignTime.HoursFromNow(hours);
        Campaign.Current.TimeControlMode = CampaignTimeControlMode.UnstoppableFastForwardForPartyWaitTime;
        InformationManager.DisplayMessage(new InformationMessage(
            $"{smith.Name} begins maintaining weapons. This will take about {FormatMaintenanceTime(hours)}.",
            new Color(0.45f, 0.82f, 0.42f, 1f)));
        GameMenu.SwitchToMenu("town_wait");
    }

    private void OnHourlyTick()
    {
        if (!_maintenanceInProgress || CampaignTime.Now < _maintenanceEndTime)
        {
            return;
        }

        _maintenanceInProgress = false;
        _maintenanceEndTime = CampaignTime.Never;
        Campaign.Current.TimeControlMode = CampaignTimeControlMode.Stop;

        int maintained = MaintainWeaponsTo(MaintenanceTargetDurability);
        PlayRepairSound();
        InformationManager.DisplayMessage(new InformationMessage(
            maintained == 1
                ? $"Weapon maintenance complete. 1 weapon entry was restored to {MaintenanceTargetDurability}%."
                : $"Weapon maintenance complete. {maintained} weapon entries were restored to {MaintenanceTargetDurability}%.",
            new Color(0.45f, 0.82f, 0.42f, 1f)));
    }

    private void OnPlayerBattleEnd(MapEvent mapEvent)
    {
        if (!WeaponDurabilitySettings.DurabilityLossEnabled)
        {
            return;
        }

        Hero? smith = GetMaintenanceSmith();
        if (smith == null)
        {
            return;
        }

        int maintained = MaintainWeaponsBy(PostBattleMaintenanceGain);
        if (maintained > 0)
        {
            InformationManager.DisplayMessage(new InformationMessage(
                $"{smith.Name} maintained party weapons after the battle (+{PostBattleMaintenanceGain}% durability).",
                new Color(0.45f, 0.82f, 0.42f, 1f)));
        }
    }

    private static string BuildRepairListLabel(WeaponStack stack, int durability, int cost)
    {
        string name = stack.Name;
        const int maxNameLength = 24;
        if (name.Length > maxNameLength)
        {
            name = name.Substring(0, maxNameLength - 3) + "...";
        }

        string condition = GetConditionLabel(durability);
        return durability >= MaxDurability
            ? $"{name} x{stack.Count} - {condition}"
            : $"{name} x{stack.Count} - {condition} - {cost} denars";
    }

    private void OnRepairWeaponSelected(List<InquiryElement> selectedElements)
    {
        WeaponStack? stack = selectedElements.FirstOrDefault()?.Identifier as WeaponStack;
        if (stack == null)
        {
            return;
        }

        int durability = GetDurability(stack.Key);
        int cost = GetRepairCost(stack, durability);
        if (durability >= MaxDurability)
        {
            InformationManager.DisplayMessage(new InformationMessage($"{stack.Name} is already fully repaired."));
            return;
        }

        if (Hero.MainHero.Gold < cost)
        {
            InformationManager.DisplayMessage(new InformationMessage($"You need {cost} denars to repair {stack.Name}."));
            return;
        }

        Hero? smith = stack.IsTroopMaintenance ? Hero.MainHero : GetBestAvailableSmith(durability);
        if (!stack.IsTroopMaintenance && WeaponDurabilitySettings.RequireSmithingSkill && smith == null)
        {
            InformationManager.DisplayMessage(new InformationMessage(
                $"{stack.Name} requires Smithing {GetRequiredSmithingSkill(durability)} to repair.",
                new Color(0.95f, 0.2f, 0.18f, 1f)));
            return;
        }

        List<RepairMaterialRequirement> materials = stack.IsTroopMaintenance ? new List<RepairMaterialRequirement>() : GetRepairMaterials(stack, durability);
        if (!HasRepairMaterials(materials))
        {
            InformationManager.DisplayMessage(new InformationMessage(
                $"You need {BuildMissingMaterialText(materials)} to repair {stack.Name}.",
                new Color(0.95f, 0.2f, 0.18f, 1f)));
            return;
        }

        Hero.MainHero.ChangeHeroGold(-cost);
        ConsumeRepairMaterials(materials);
        RepairOneWeapon(stack);
        PlayRepairSound();
        string smithName = stack.IsTroopMaintenance
            ? "The smith"
            : smith == null || smith == Hero.MainHero ? Hero.MainHero.Name.ToString() : smith.Name.ToString();
        InformationManager.DisplayMessage(new InformationMessage(
            $"{smithName} repaired {stack.Name} for {cost} denars.",
            new Color(0.45f, 0.82f, 0.42f, 1f)));
    }

    private IEnumerable<WeaponStack> GetRepairableWeaponStacks()
    {
        Hero? mainHero = Hero.MainHero;
        Equipment? battleEquipment = mainHero?.BattleEquipment;
        if (battleEquipment != null)
        {
            foreach (EquipmentIndex slot in WeaponSlots)
            {
                EquipmentElement equipmentElement = battleEquipment[slot];
                ItemObject item = equipmentElement.Item;
                if (item?.HasWeaponComponent == true && IsRepairableWeapon(item))
                {
                    yield return WeaponStack.ForHero(mainHero!, equipmentElement, 1, slot);
                }
            }
        }

        if (WeaponDurabilitySettings.EnableCompanionDurability)
        {
            foreach (Hero companion in GetMainPartyCompanionHeroes())
            {
                Equipment? companionEquipment = companion.BattleEquipment;
                if (companionEquipment == null)
                {
                    continue;
                }

                foreach (EquipmentIndex slot in WeaponSlots)
                {
                    EquipmentElement equipmentElement = companionEquipment[slot];
                    ItemObject item = equipmentElement.Item;
                    if (item?.HasWeaponComponent == true && IsRepairableWeapon(item))
                    {
                        yield return WeaponStack.ForHero(companion, equipmentElement, 1, slot);
                    }
                }
            }
        }

        if (WeaponDurabilitySettings.EnableRegularTroopDurability)
        {
            foreach (WeaponStack stack in GetRegularTroopWeaponStacks())
            {
                yield return stack;
            }
        }

        ItemRoster? itemRoster = MobileParty.MainParty?.ItemRoster;
        if (itemRoster == null)
        {
            yield break;
        }

        foreach (ItemRosterElement rosterElement in itemRoster)
        {
            EquipmentElement equipmentElement = rosterElement.EquipmentElement;
            ItemObject item = equipmentElement.Item;
            if (item?.HasWeaponComponent == true && IsRepairableWeapon(item))
            {
                yield return WeaponStack.ForInventory(equipmentElement, rosterElement.Amount);
            }
        }
    }

    private WeaponStack? FindAttackerWeaponStack(CharacterObject attacker, WeaponComponentData weapon)
    {
        if (attacker == Hero.MainHero?.CharacterObject)
        {
            return GetHeroWeaponStacks(Hero.MainHero).FirstOrDefault(stack => stack.Item.Weapons.Any(itemWeapon => ReferenceEquals(itemWeapon, weapon)));
        }

        Hero? attackerHero = attacker.HeroObject;
        if (WeaponDurabilitySettings.EnableCompanionDurability && attackerHero != null && IsMainPartyCompanion(attackerHero))
        {
            return GetHeroWeaponStacks(attackerHero).FirstOrDefault(stack => stack.Item.Weapons.Any(itemWeapon => ReferenceEquals(itemWeapon, weapon)));
        }

        if (WeaponDurabilitySettings.EnableRegularTroopDurability && attackerHero == null && IsMainPartyRegularTroop(attacker))
        {
            return GetRegularTroopWeaponStacks(attacker).FirstOrDefault(stack => stack.Item.Weapons.Any(itemWeapon => ReferenceEquals(itemWeapon, weapon)));
        }

        return null;
    }

    private static IEnumerable<WeaponStack> GetHeroWeaponStacks(Hero hero)
    {
        Equipment? battleEquipment = hero.BattleEquipment;
        if (battleEquipment == null)
        {
            yield break;
        }

        foreach (EquipmentIndex slot in WeaponSlots)
        {
            EquipmentElement equipmentElement = battleEquipment[slot];
            ItemObject item = equipmentElement.Item;
            if (item?.HasWeaponComponent == true && IsRepairableWeapon(item))
            {
                yield return WeaponStack.ForHero(hero, equipmentElement, 1, slot);
            }
        }
    }

    private static IEnumerable<Hero> GetMainPartyCompanionHeroes()
    {
        TroopRoster? roster = MobileParty.MainParty?.MemberRoster;
        if (roster == null)
        {
            yield break;
        }

        foreach (TroopRosterElement element in roster.GetTroopRoster())
        {
            Hero? hero = element.Character?.HeroObject;
            if (hero != null && hero != Hero.MainHero)
            {
                yield return hero;
            }
        }
    }

    private static bool IsMainPartyCompanion(Hero hero)
    {
        return GetMainPartyCompanionHeroes().Any(companion => companion == hero);
    }

    private static bool IsMainPartyRegularTroop(CharacterObject character)
    {
        TroopRoster? roster = MobileParty.MainParty?.MemberRoster;
        return roster?.GetTroopRoster().Any(element => element.Character == character && element.Character?.HeroObject == null && element.Number > 0) == true;
    }

    private static IEnumerable<WeaponStack> GetRegularTroopWeaponStacks(CharacterObject? onlyCharacter = null)
    {
        TroopRoster? roster = MobileParty.MainParty?.MemberRoster;
        if (roster == null)
        {
            yield break;
        }

        foreach (TroopRosterElement element in roster.GetTroopRoster())
        {
            CharacterObject? character = element.Character;
            if (character == null || character.HeroObject != null || element.Number <= 0)
            {
                continue;
            }

            if (onlyCharacter != null && character != onlyCharacter)
            {
                continue;
            }

            Equipment? equipment = character.FirstBattleEquipment;
            if (equipment == null)
            {
                continue;
            }

            foreach (EquipmentIndex slot in WeaponSlots)
            {
                EquipmentElement equipmentElement = equipment[slot];
                ItemObject item = equipmentElement.Item;
                if (item?.HasWeaponComponent == true && IsRepairableWeapon(item))
                {
                    yield return WeaponStack.ForTroop(character, equipmentElement, element.Number);
                }
            }
        }
    }

    private static bool IsRepairableWeapon(ItemObject item)
    {
        return item.Weapons.Any(weapon => weapon.IsMeleeWeapon && !weapon.IsAmmo && !weapon.IsConsumable);
    }

    public static int? GetDurabilityForRosterElement(ItemRosterElement rosterElement)
    {
        EquipmentElement equipmentElement = rosterElement.EquipmentElement;
        return GetDurabilityForEquipmentElement(equipmentElement);
    }

    public static int? GetDurabilityForEquipmentElement(EquipmentElement equipmentElement)
    {
        ItemObject item = equipmentElement.Item;
        if (_instance == null || item?.HasWeaponComponent != true || !IsRepairableWeapon(item))
        {
            return null;
        }

        int durability = _instance.GetDurability(BuildKey(item, equipmentElement.ItemModifier));
        if (durability == MaxDurability && IsDurabilityModifier(equipmentElement.ItemModifier))
        {
            durability = GetDurabilityFromModifier(equipmentElement.ItemModifier);
        }

        return durability;
    }

    public static bool IsCriticalDurability(ItemRosterElement rosterElement)
    {
        int? durability = GetDurabilityForRosterElement(rosterElement);
        return durability.HasValue && durability.Value < CriticalDurabilityThreshold;
    }

    public static string StripInventoryMarker(string text)
    {
        return Regex.Replace(text, @"\s+\[\d{1,3}%\s+(Pristine|Good|Worn|Damaged|Cracked|Broken)\]$", string.Empty);
    }

    public static string GetConditionLabel(int durability)
    {
        return $"{durability}% {GetConditionText(durability)}";
    }

    public static Color GetConditionColor(int durability)
    {
        if (durability <= 0)
        {
            return new Color(0f, 0f, 0f, 1f);
        }

        if (durability >= 80)
        {
            return new Color(0.48f, 0.86f, 0.38f, 1f);
        }

        if (durability >= 50)
        {
            return new Color(0.95f, 0.74f, 0.28f, 1f);
        }

        if (durability >= 30)
        {
            return new Color(0.95f, 0.46f, 0.20f, 1f);
        }

        return new Color(0.95f, 0.16f, 0.14f, 1f);
    }

    private int GetDurability(string key)
    {
        if (_durabilityByWeaponKey.TryGetValue(key, out int durability))
        {
            return MBMath.ClampInt(durability, MinDurability, MaxDurability);
        }

        return MaxDurability;
    }

    private void SetDurability(string key, int durability)
    {
        _durabilityByWeaponKey[key] = MBMath.ClampInt(durability, MinDurability, MaxDurability);
    }

    private void ApplyDurabilityModifier(WeaponStack stack, int durability)
    {
        ItemModifier? targetModifier = GetConditionModifier(stack.Item, durability);
        string targetKey = BuildKey(stack.Item, targetModifier);
        SetDurability(targetKey, durability);

        if (ReferenceEquals(stack.ItemModifier, targetModifier))
        {
            return;
        }

        ItemRoster? roster = MobileParty.MainParty?.ItemRoster;
        if (roster == null)
        {
            return;
        }

        EquipmentElement targetElement = new EquipmentElement(stack.Item, targetModifier, null, false);
        if (stack.EquipmentSlot.HasValue)
        {
            stack.OwnerHero?.BattleEquipment.AddEquipmentToSlotWithoutAgent(stack.EquipmentSlot.Value, targetElement);
            return;
        }

        roster.AddToCounts(stack.EquipmentElement, -1);
        roster.AddToCounts(targetElement, 1);
    }

    private void RepairOneWeapon(WeaponStack stack)
    {
        ItemRoster? roster = MobileParty.MainParty?.ItemRoster;
        if (roster == null)
        {
            return;
        }

        EquipmentElement repairedElement = new EquipmentElement(stack.Item, null, null, false);
        if (stack.IsTroopMaintenance)
        {
            SetDurability(stack.Key, MaxDurability);
            return;
        }

        if (stack.EquipmentSlot.HasValue)
        {
            stack.OwnerHero?.BattleEquipment.AddEquipmentToSlotWithoutAgent(stack.EquipmentSlot.Value, repairedElement);
            SetDurability(BuildKey(stack.Item, null), MaxDurability);
            return;
        }

        roster.AddToCounts(stack.EquipmentElement, -1);
        roster.AddToCounts(repairedElement, 1);
        SetDurability(BuildKey(stack.Item, null), MaxDurability);
    }

    private static int GetRepairCost(WeaponStack stack, int durability)
    {
        int missing = MaxDurability - durability;
        if (missing <= 0)
        {
            return 0;
        }

        int baseCost = Math.Max(1, stack.Item.Value / Math.Max(1, WeaponDurabilitySettings.RepairCostPerMissingPointDivisor));
        int countMultiplier = stack.IsTroopMaintenance ? Math.Max(1, stack.Count) : 1;
        return Math.Max(5, missing * baseCost * countMultiplier);
    }

    private static List<RepairMaterialRequirement> GetRepairMaterials(WeaponStack stack, int durability)
    {
        var requirements = new List<RepairMaterialRequirement>();
        if (!WeaponDurabilitySettings.RequireRepairMaterials || durability >= MaxDurability)
        {
            return requirements;
        }

        int units = Math.Max(1, (int)Math.Ceiling((MaxDurability - durability) / 50f)) *
            Math.Max(1, WeaponDurabilitySettings.MaterialUnitsPerMissingFiftyPercent);

        AddMaterialRequirement(requirements, CharcoalItemId, units);
        AddMaterialRequirement(requirements, GetTierMaterialItemId(GetWeaponTier(stack.Item)), units);
        return requirements;
    }

    private static void AddMaterialRequirement(List<RepairMaterialRequirement> requirements, string itemId, int count)
    {
        ItemObject? item = MBObjectManager.Instance.GetObject<ItemObject>(itemId);
        if (item != null && count > 0)
        {
            requirements.Add(new RepairMaterialRequirement(item, count));
        }
    }

    private static int GetWeaponTier(ItemObject item)
    {
        return MBMath.ClampInt((int)item.Tier, 0, 6);
    }

    private static string GetTierMaterialItemId(int tier)
    {
        return tier switch
        {
            <= 1 => "crude_iron",
            2 => "wrought_iron",
            3 => "iron",
            4 => "steel",
            5 => "fine_steel",
            _ => "thamaskene_steel"
        };
    }

    private static bool HasRepairMaterials(List<RepairMaterialRequirement> materials)
    {
        ItemRoster? roster = MobileParty.MainParty?.ItemRoster;
        return roster != null && materials.All(material => GetItemCount(roster, material.Item) >= material.Count);
    }

    private static void ConsumeRepairMaterials(List<RepairMaterialRequirement> materials)
    {
        ItemRoster? roster = MobileParty.MainParty?.ItemRoster;
        if (roster == null)
        {
            return;
        }

        foreach (RepairMaterialRequirement material in materials)
        {
            roster.AddToCounts(material.Item, -material.Count);
        }
    }

    private static int GetItemCount(ItemRoster roster, ItemObject item)
    {
        int count = 0;
        foreach (ItemRosterElement element in roster)
        {
            if (element.EquipmentElement.Item == item)
            {
                count += element.Amount;
            }
        }

        return count;
    }

    private static string BuildMaterialText(List<RepairMaterialRequirement> materials)
    {
        return string.Join(", ", materials.Select(material => $"{material.Count} {material.Item.Name}"));
    }

    private static string BuildMissingMaterialText(List<RepairMaterialRequirement> materials)
    {
        ItemRoster? roster = MobileParty.MainParty?.ItemRoster;
        if (roster == null)
        {
            return BuildMaterialText(materials);
        }

        return string.Join(", ", materials
            .Select(material => new
            {
                material.Item,
                Missing = material.Count - GetItemCount(roster, material.Item)
            })
            .Where(material => material.Missing > 0)
            .Select(material => $"{material.Missing} {material.Item.Name}"));
    }

    private static Hero? GetBestAvailableSmith(int durability)
    {
        if (!WeaponDurabilitySettings.RequireSmithingSkill)
        {
            return Hero.MainHero;
        }

        int requiredSkill = GetRequiredSmithingSkill(durability);
        IEnumerable<Hero> candidates = WeaponDurabilitySettings.AllowCompanionRepair
            ? MobileParty.MainParty.MemberRoster.GetTroopRoster()
                .Where(element => element.Character?.HeroObject != null)
                .Select(element => element.Character.HeroObject)
            : new[] { Hero.MainHero };

        return candidates
            .Where(hero => hero != null && hero.GetSkillValue(DefaultSkills.Crafting) >= requiredSkill)
            .OrderByDescending(hero => hero.GetSkillValue(DefaultSkills.Crafting))
            .FirstOrDefault();
    }

    private static Hero? GetMaintenanceSmith()
    {
        IEnumerable<Hero> candidates = WeaponDurabilitySettings.AllowCompanionRepair
            ? MobileParty.MainParty.MemberRoster.GetTroopRoster()
                .Where(element => element.Character?.HeroObject != null)
                .Select(element => element.Character.HeroObject)
            : new[] { Hero.MainHero };

        return candidates
            .Where(hero => hero != null && hero.GetSkillValue(DefaultSkills.Crafting) >= MaintenanceSkillRequirement)
            .OrderByDescending(hero => hero.GetSkillValue(DefaultSkills.Crafting))
            .FirstOrDefault();
    }

    private int MaintainWeaponsTo(int targetDurability)
    {
        int maintained = 0;
        foreach (WeaponStack stack in GetRepairableWeaponStacks().ToList())
        {
            int current = GetDurability(stack.Key);
            if (current >= targetDurability)
            {
                continue;
            }

            SetStackDurability(stack, targetDurability);
            maintained++;
        }

        return maintained;
    }

    private int MaintainWeaponsBy(int amount)
    {
        int maintained = 0;
        foreach (WeaponStack stack in GetRepairableWeaponStacks().ToList())
        {
            int current = GetDurability(stack.Key);
            if (current >= MaxDurability)
            {
                continue;
            }

            SetStackDurability(stack, Math.Min(MaxDurability, current + Math.Max(1, amount)));
            maintained++;
        }

        return maintained;
    }

    private void SetStackDurability(WeaponStack stack, int durability)
    {
        SetDurability(stack.Key, durability);
        if (!stack.IsTroopMaintenance)
        {
            ApplyDurabilityModifier(stack, durability);
        }
    }

    private static float GetMaintenanceHours(List<WeaponStack> damaged)
    {
        int workUnits = damaged.Sum(stack => stack.IsTroopMaintenance ? Math.Max(1, stack.Count) : 1);
        return MBMath.ClampFloat(12f + workUnits * 2f, 12f, 96f);
    }

    private static string FormatMaintenanceTime(float hours)
    {
        if (hours < 24f)
        {
            return $"{Math.Ceiling(hours)} hours";
        }

        return $"{Math.Ceiling(hours / 24f)} days";
    }

    private static int GetRequiredSmithingSkill(int durability)
    {
        int missing = MaxDurability - durability;
        return MBMath.ClampInt(missing * Math.Max(0, WeaponDurabilitySettings.SmithingSkillPerMissingPoint), 0, 300);
    }

    private static void PlayRepairSound()
    {
        try
        {
            SoundEvent.PlaySound2D(RepairSoundEvent);
        }
        catch
        {
        }
    }

    private static string BuildKey(ItemObject item, ItemModifier? itemModifier)
    {
        string modifierId = itemModifier?.StringId ?? "none";
        return $"{item.StringId}|{modifierId}";
    }

    private static string BuildTroopKey(CharacterObject troop, ItemObject item, ItemModifier? itemModifier)
    {
        string modifierId = itemModifier?.StringId ?? "none";
        return $"troop|{troop.StringId}|{item.StringId}|{modifierId}";
    }

    private static ItemModifier? GetConditionModifier(ItemObject item, int durability)
    {
        if (durability >= MaintenanceTargetDurability)
        {
            return null;
        }

        string? group = GetDurabilityModifierGroup(item);
        if (group == null)
        {
            return null;
        }

        string condition = durability switch
        {
            <= 0 => "broken",
            <= CriticalDurabilityThreshold => "cracked",
            <= 50 => "damaged",
            <= 75 => "worn",
            _ => "worn"
        };

        return MBObjectManager.Instance.GetObject<ItemModifier>($"{DurabilityModifierPrefix}{condition}_{group}");
    }

    private static string? GetDurabilityModifierGroup(ItemObject item)
    {
        WeaponComponentData? weapon = item.Weapons.FirstOrDefault(weaponData => !weaponData.IsAmmo && !weaponData.IsConsumable);
        if (weapon == null)
        {
            return null;
        }

        return weapon.WeaponClass switch
        {
            WeaponClass.OneHandedSword or WeaponClass.TwoHandedSword or WeaponClass.Dagger => "sword",
            WeaponClass.OneHandedAxe or WeaponClass.TwoHandedAxe => "axe",
            WeaponClass.Mace or WeaponClass.Pick or WeaponClass.TwoHandedMace => "mace",
            WeaponClass.OneHandedPolearm or WeaponClass.TwoHandedPolearm or WeaponClass.LowGripPolearm => "polearm",
            WeaponClass.ThrowingAxe => "axe_throwing",
            WeaponClass.ThrowingKnife => "knife_throwing",
            WeaponClass.Javelin => "spear_dart_throwing",
            WeaponClass.Bow => "bow",
            WeaponClass.Crossbow => "crossbow",
            _ => "cheap_weapon"
        };
    }

    private static bool IsDurabilityModifier(ItemModifier? itemModifier)
    {
        return itemModifier?.StringId?.StartsWith(DurabilityModifierPrefix, StringComparison.Ordinal) == true;
    }

    private static int GetDurabilityFromModifier(ItemModifier? itemModifier)
    {
        string id = itemModifier?.StringId ?? string.Empty;
        if (id.Contains("_broken_"))
        {
            return 0;
        }

        if (id.Contains("_cracked_"))
        {
            return 25;
        }

        if (id.Contains("_damaged_"))
        {
            return 50;
        }

        if (id.Contains("_worn_"))
        {
            return 75;
        }

        return MaxDurability;
    }

    public static string BuildDurabilityLabel(int durability)
    {
        return $"Durability: {durability}% {GetConditionText(durability)}";
    }

    private enum RepairCategory
    {
        Personal,
        Companions,
        Company
    }

    private static string GetConditionText(int durability)
    {
        if (durability >= 90)
        {
            return "Pristine";
        }

        if (durability >= 76)
        {
            return "Good";
        }

        if (durability >= 51)
        {
            return "Worn";
        }

        if (durability >= 26)
        {
            return "Damaged";
        }

        if (durability > 0)
        {
            return "Cracked";
        }

        return "Broken";
    }

    private sealed class WeaponStack
    {
        private WeaponStack(
            EquipmentElement equipmentElement,
            int count,
            EquipmentIndex? equipmentSlot,
            Hero? ownerHero,
            CharacterObject? troopCharacter,
            bool isTroopMaintenance,
            RepairCategory category)
        {
            EquipmentElement = equipmentElement;
            Item = equipmentElement.Item;
            ItemModifier = equipmentElement.ItemModifier;
            Count = count;
            EquipmentSlot = equipmentSlot;
            OwnerHero = ownerHero;
            TroopCharacter = troopCharacter;
            IsTroopMaintenance = isTroopMaintenance;
            Category = category;
            Key = isTroopMaintenance && troopCharacter != null
                ? BuildTroopKey(troopCharacter, Item, ItemModifier)
                : BuildKey(Item, ItemModifier);
            Name = BuildName();
        }

        public static WeaponStack ForHero(Hero ownerHero, EquipmentElement equipmentElement, int count, EquipmentIndex? equipmentSlot)
        {
            RepairCategory category = ownerHero == Hero.MainHero ? RepairCategory.Personal : RepairCategory.Companions;
            return new WeaponStack(equipmentElement, count, equipmentSlot, ownerHero, null, false, category);
        }

        public static WeaponStack ForInventory(EquipmentElement equipmentElement, int count)
        {
            return new WeaponStack(equipmentElement, count, null, null, null, false, RepairCategory.Personal);
        }

        public static WeaponStack ForTroop(CharacterObject troopCharacter, EquipmentElement equipmentElement, int count)
        {
            return new WeaponStack(equipmentElement, count, null, null, troopCharacter, true, RepairCategory.Company);
        }

        public EquipmentElement EquipmentElement { get; }
        public EquipmentIndex? EquipmentSlot { get; }
        public Hero? OwnerHero { get; }
        public CharacterObject? TroopCharacter { get; }
        public bool IsTroopMaintenance { get; }
        public RepairCategory Category { get; }
        public ItemObject Item { get; }
        public ItemModifier? ItemModifier { get; }
        public int Count { get; }
        public string Key { get; }
        public string Name { get; }

        private string BuildName()
        {
            string weaponName = ItemModifier == null ? Item.Name.ToString() : $"{ItemModifier.Name} {Item.Name}";
            if (IsTroopMaintenance && TroopCharacter != null)
            {
                return $"{TroopCharacter.Name}: {weaponName}";
            }

            if (OwnerHero != null && OwnerHero != Hero.MainHero)
            {
                return $"{OwnerHero.Name}: {weaponName}";
            }

            return weaponName;
        }
    }

    private sealed class RepairMaterialRequirement
    {
        public RepairMaterialRequirement(ItemObject item, int count)
        {
            Item = item;
            Count = count;
        }

        public ItemObject Item { get; }
        public int Count { get; }
    }
}
