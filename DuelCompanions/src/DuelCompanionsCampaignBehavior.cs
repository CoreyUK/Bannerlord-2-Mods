using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Settlements.Locations;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace DuelCompanions;

public sealed class DuelCompanionsCampaignBehavior : CampaignBehaviorBase
{
    private const string DuelMenuId = "dc_duel_ground";
    private const string RewardMenuId = "dc_duel_reward";
    private const int CashReward = 6000;
    private const int GauntletCashReward = 15000;
    private const int GauntletRounds = 3;
    private const int EventDurationDays = 10;

    private static readonly DuelTemplate[] DuelTemplates =
    {
        new("dc_heavy_vlandia_v2", "vlandia_noble_sword_3_t5", "{SETTLEMENT}'s old tourney yard"),
        new("dc_heavy_sturgia_v2", "sturgia_2haxe_2_t5", "a marked ring beyond {SETTLEMENT}'s gates"),
        new("dc_heavy_khuzait_v2", "khuzait_noble_sword_1_t5", "the duelling stones outside {SETTLEMENT}")
    };

    private static readonly string[] FirstNames =
    {
        "Aldric", "Kara", "Ivar", "Sarqan", "Mira", "Olek", "Varyn", "Tavan", "Sabira", "Ragan", "Brina", "Eshar"
    };

    private static readonly string[] Epithets =
    {
        "the Red", "Ironhand", "Blade", "the Unbroken", "of the Ash", "the Wolf", "the Hawk", "the Scarred", "the Bold", "Storm-Eye"
    };

    private static readonly string[] DuelGroundTexts =
    {
        "You arrive at {DUEL_GROUND}. {DUELIST} studies you in silence, their war gear already buckled tight. This challenge will not wait forever.",
        "At {DUEL_GROUND}, a small crowd has gathered. {DUELIST} rolls their shoulders and gives you a curt nod. The rumor will move on if ignored.",
        "{DUEL_GROUND} is quiet except for steel scraping leather. {DUELIST} is ready, armed like a veteran of many campaigns.",
        "You find {DUELIST} at {DUEL_GROUND}, surrounded by spectators taking wagers. Their equipment is polished, expensive, and well used.",
        "The path leads to {DUEL_GROUND}. {DUELIST} waits there with a champion's patience, offering glory, coin, or a sharp lesson."
    };

    private static readonly string[] DuelOptionTexts =
    {
        "Challenge {DUELIST} to a formal duel",
        "Call {DUELIST} into the ring",
        "Test your blade against {DUELIST}",
        "Demand satisfaction from {DUELIST}",
        "Face {DUELIST} before the crowd"
    };

    private static readonly string[] GauntletOptionTexts =
    {
        "Enter the gauntlet",
        "Take the champion's gauntlet",
        "Fight through the whole challenge",
        "Accept the three-round trial",
        "Step into the gauntlet ring"
    };

    private static readonly string[] RewardTexts =
    {
        "{DUELIST} lowers their weapon. The crowd waits to see what price you will claim.",
        "{DUELIST} yields the field. By custom, you may choose service, silver, or steel.",
        "The fight is yours. {DUELIST} stands bloodied but composed, ready to honor the terms.",
        "{DUELIST} gives a short bow. Victory grants you the right to name your reward.",
        "The last blow settles the matter. {DUELIST} accepts defeat and awaits your decision."
    };

    private static readonly string[] RecruitOptionTexts =
    {
        "Ask {DUELIST} to ride with your clan",
        "Offer {DUELIST} a place in your company",
        "Recruit {DUELIST} as a companion",
        "Invite {DUELIST} to join your retinue",
        "Claim {DUELIST}'s service"
    };

    private static readonly string[] GoldOptionTexts =
    {
        "Take {CASH_REWARD} denars",
        "Demand {CASH_REWARD} denars in blood-price",
        "Claim the purse of {CASH_REWARD} denars",
        "Accept {CASH_REWARD} denars and walk away",
        "Choose coin: {CASH_REWARD} denars"
    };

    private static readonly string[] ItemOptionTexts =
    {
        "Take the rare weapon",
        "Claim the champion's weapon",
        "Choose the prize blade",
        "Demand the weapon as your trophy",
        "Take steel instead of silver"
    };

    private static readonly string[] RumorTexts =
    {
        "A duel rumor has appeared in {SETTLEMENT}.",
        "Travelers speak of a dangerous champion waiting in {SETTLEMENT}.",
        "Word spreads that a duelist is taking challengers in {SETTLEMENT}.",
        "A prize fight is being whispered about in {SETTLEMENT}.",
        "Mercenaries say a hardened fighter has set terms for a duel in {SETTLEMENT}."
    };

    private static readonly string[] DefeatTexts =
    {
        "{DUELIST} beats you back and claims the field. The challenge remains for now.",
        "{DUELIST} wins this bout. You are dragged clear while the crowd roars.",
        "Your guard breaks, and {DUELIST} ends the fight with clinical precision.",
        "{DUELIST} leaves you sprawled in the dust. You may try again while the rumor lasts.",
        "The duel turns against you. {DUELIST} waits to see if you have the nerve for another attempt."
    };

    private static readonly string[] GauntletProgressTexts =
    {
        "Round {ROUND} is complete. {DUELIST} steps forward without ceremony.",
        "You survive the round. Now {DUELIST} enters the ring.",
        "The crowd barely settles before {DUELIST} takes up position.",
        "Another opponent falls. {DUELIST} is already waiting.",
        "There is no time to breathe. {DUELIST} advances for the next round."
    };

    private string? _activeSettlementId;
    private string? _activeDuelistName;
    private string? _activeTemplateId;
    private string? _activeRareItemId;
    private string? _activeGroundDescription;
    private string? _activeHeroId;
    private int _activeTextVariant;
    private double _activeEventExpiresDay;
    private double _nextEventDay;
    private int _eventSerial;

    private string? _pendingRewardHeroId;
    private string? _pendingRewardDuelistName;
    private string? _pendingRewardTemplateId;
    private string? _pendingRewardRareItemId;
    private bool _pendingRewardAllowsRecruit = true;
    private bool _pendingRewardIsGauntlet;

    private bool _isGauntletActive;
    private int _gauntletRound;

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
    }

    public override void SyncData(IDataStore dataStore)
    {
        dataStore.SyncData("dc_active_settlement_id_v2", ref _activeSettlementId);
        dataStore.SyncData("dc_active_duelist_name_v2", ref _activeDuelistName);
        dataStore.SyncData("dc_active_template_id_v2", ref _activeTemplateId);
        dataStore.SyncData("dc_active_rare_item_id_v2", ref _activeRareItemId);
        dataStore.SyncData("dc_active_ground_description_v2", ref _activeGroundDescription);
        dataStore.SyncData("dc_active_hero_id_v2", ref _activeHeroId);
        dataStore.SyncData("dc_active_text_variant_v2", ref _activeTextVariant);
        dataStore.SyncData("dc_active_event_expires_day_v2", ref _activeEventExpiresDay);
        dataStore.SyncData("dc_next_event_day_v2", ref _nextEventDay);
        dataStore.SyncData("dc_event_serial_v2", ref _eventSerial);
        dataStore.SyncData("dc_pending_reward_hero_id_v2", ref _pendingRewardHeroId);
        dataStore.SyncData("dc_pending_reward_duelist_name_v2", ref _pendingRewardDuelistName);
        dataStore.SyncData("dc_pending_reward_template_id_v2", ref _pendingRewardTemplateId);
        dataStore.SyncData("dc_pending_reward_rare_item_id_v2", ref _pendingRewardRareItemId);
        dataStore.SyncData("dc_pending_reward_allows_recruit_v2", ref _pendingRewardAllowsRecruit);
        dataStore.SyncData("dc_pending_reward_is_gauntlet_v2", ref _pendingRewardIsGauntlet);
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        AddMenus(starter);
        RecoverDetachedDuelCompanions();

        if (string.IsNullOrEmpty(_activeSettlementId) && _nextEventDay <= 0)
        {
            CreateRandomEvent(showMessage: true);
        }
    }

    private void OnDailyTick()
    {
        double today = CampaignTime.Now.ToDays;

        if (!string.IsNullOrEmpty(_activeSettlementId) && today > _activeEventExpiresDay)
        {
            ClearActiveEvent();
            ScheduleNextEvent(3, 7);
        }

        if (string.IsNullOrEmpty(_activeSettlementId) && today >= _nextEventDay)
        {
            CreateRandomEvent(showMessage: true);
        }
    }

    private void AddMenus(CampaignGameStarter starter)
    {
        starter.AddGameMenu(
            DuelMenuId,
            "{DUEL_GROUND_TEXT}",
            OnDuelMenuInit,
            GameMenu.MenuOverlayType.SettlementWithBoth);

        starter.AddGameMenuOption(
            "town",
            "dc_visit_duel_ground",
            "{=dc_visit_duel_ground}Follow the duel rumor",
            TownDuelGroundCondition,
            args => GameMenu.SwitchToMenu(DuelMenuId),
            false,
            6);

        starter.AddGameMenuOption(DuelMenuId, "dc_start_duel", "{DUEL_OPTION_TEXT}", DuelChallengeCondition, args => StartDuelMission(GetActiveEvent(), 210f, false), false, 0);
        starter.AddGameMenuOption(DuelMenuId, "dc_start_gauntlet", "{GAUNTLET_OPTION_TEXT}", GauntletStartCondition, GauntletStartConsequence, false, 1);
        starter.AddGameMenuOption(DuelMenuId, "dc_continue_gauntlet", "{=dc_continue_gauntlet}Continue the gauntlet: round {ROUND} of 3 against {DUELIST}", GauntletContinueCondition, args => StartDuelMission(GetGauntletOpponent(), GetGauntletHealth(), true), false, 1);

        starter.AddGameMenuOption(
            DuelMenuId,
            "dc_leave_duel_ground",
            "{=dc_leave_duel_ground}Leave",
            args =>
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                return true;
            },
            args => GameMenu.SwitchToMenu("town"),
            true,
            2);

        starter.AddGameMenu(
            RewardMenuId,
            "{REWARD_TEXT}",
            OnRewardMenuInit,
            GameMenu.MenuOverlayType.SettlementWithBoth);

        starter.AddGameMenuOption(RewardMenuId, "dc_reward_recruit", "{RECRUIT_OPTION_TEXT}", RewardRecruitCondition, args => CompleteReward(RewardType.Recruit), false, 0);
        starter.AddGameMenuOption(RewardMenuId, "dc_reward_gold", "{GOLD_OPTION_TEXT}", RewardCondition, args => CompleteReward(RewardType.Gold), false, 1);
        starter.AddGameMenuOption(RewardMenuId, "dc_reward_item", "{ITEM_OPTION_TEXT}", RewardCondition, args => CompleteReward(RewardType.Item), false, 2);
    }

    private bool TownDuelGroundCondition(MenuCallbackArgs args)
    {
        if (GetActiveEvent() == null)
        {
            return false;
        }

        args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
        return true;
    }

    private void OnDuelMenuInit(MenuCallbackArgs args)
    {
        DuelEvent? duel = GetActiveEvent();
        if (duel == null)
        {
            return;
        }

        DuelEvent displayDuel = _isGauntletActive ? GetGauntletOpponent() ?? duel : duel;
        MBTextManager.SetTextVariable("DUEL_GROUND", duel.GroundDescription);
        MBTextManager.SetTextVariable("DUELIST", displayDuel.DisplayName);
        MBTextManager.SetTextVariable("ROUND", _gauntletRound + 1);
        MBTextManager.SetTextVariable("DUEL_GROUND_TEXT", Pick(DuelGroundTexts));
        MBTextManager.SetTextVariable("DUEL_OPTION_TEXT", Pick(DuelOptionTexts));
        MBTextManager.SetTextVariable("GAUNTLET_OPTION_TEXT", Pick(GauntletOptionTexts));
    }

    private bool DuelChallengeCondition(MenuCallbackArgs args)
    {
        DuelEvent? duel = GetActiveEvent();
        if (duel == null || _isGauntletActive)
        {
            return false;
        }

        MBTextManager.SetTextVariable("DUELIST", duel.DisplayName);
        MBTextManager.SetTextVariable("DUEL_OPTION_TEXT", Pick(DuelOptionTexts));
        args.optionLeaveType = GameMenuOption.LeaveType.PracticeFight;
        return true;
    }

    private bool GauntletStartCondition(MenuCallbackArgs args)
    {
        if (GetActiveEvent() == null || _isGauntletActive)
        {
            return false;
        }

        args.optionLeaveType = GameMenuOption.LeaveType.PracticeFight;
        return true;
    }

    private void GauntletStartConsequence(MenuCallbackArgs args)
    {
        _isGauntletActive = true;
        _gauntletRound = 0;
        StartDuelMission(GetGauntletOpponent(), GetGauntletHealth(), true);
    }

    private bool GauntletContinueCondition(MenuCallbackArgs args)
    {
        DuelEvent? opponent = GetGauntletOpponent();
        if (!_isGauntletActive || opponent == null)
        {
            return false;
        }

        MBTextManager.SetTextVariable("DUELIST", opponent.DisplayName);
        MBTextManager.SetTextVariable("ROUND", _gauntletRound + 1);
        args.optionLeaveType = GameMenuOption.LeaveType.PracticeFight;
        return true;
    }

    private void StartDuelMission(DuelEvent? duel, float opponentHealth, bool isGauntlet)
    {
        Settlement? settlement = Settlement.CurrentSettlement;
        Location? arena = settlement?.LocationComplex?.GetLocationWithId("arena");
        CharacterObject? duelCharacter = GetOrCreateDuelHero(duel)?.CharacterObject;

        if (duel == null || arena == null || duelCharacter == null)
        {
            InformationManager.DisplayMessage(new InformationMessage("This fight cannot be started because the arena or opponent is unavailable."));
            GameMenu.SwitchToMenu("town");
            return;
        }

        _isGauntletActive = isGauntlet;
        CampaignMission.OpenArenaDuelMission(arena.GetSceneName(0), arena, duelCharacter, false, false, OnDuelEnded, opponentHealth);
    }

    private void OnDuelEnded(CharacterObject winner)
    {
        DuelEvent? duel = _isGauntletActive ? GetGauntletOpponent() : GetActiveEvent();
        if (duel == null)
        {
            GameMenu.SwitchToMenu("town");
            return;
        }

        if (winner == CharacterObject.PlayerCharacter)
        {
            if (_isGauntletActive && _gauntletRound < GauntletRounds - 1)
            {
                _gauntletRound++;
                DuelEvent? nextOpponent = GetGauntletOpponent();
                MBTextManager.SetTextVariable("ROUND", _gauntletRound);
                MBTextManager.SetTextVariable("DUELIST", nextOpponent?.DisplayName ?? "The next opponent");
                InformationManager.DisplayMessage(new InformationMessage(Pick(GauntletProgressTexts)));
                GameMenu.SwitchToMenu(DuelMenuId);
                return;
            }

            DuelEvent? activeEvent = GetActiveEvent() ?? duel;
            _pendingRewardHeroId = activeEvent.HeroId;
            _pendingRewardDuelistName = activeEvent.DisplayName;
            _pendingRewardTemplateId = activeEvent.TemplateId;
            _pendingRewardRareItemId = activeEvent.RareItemId;
            _pendingRewardAllowsRecruit = !_isGauntletActive;
            _pendingRewardIsGauntlet = _isGauntletActive;
            _isGauntletActive = false;
            _gauntletRound = 0;
            ClearActiveEvent();
            ScheduleNextEvent(3, 8);
            GameMenu.SwitchToMenu(RewardMenuId);
            return;
        }

        MBTextManager.SetTextVariable("DUELIST", duel.DisplayName);
        InformationManager.DisplayMessage(new InformationMessage(Pick(DefeatTexts)));
        _isGauntletActive = false;
        _gauntletRound = 0;
        GameMenu.SwitchToMenu("town");
    }

    private void OnRewardMenuInit(MenuCallbackArgs args)
    {
        MBTextManager.SetTextVariable("DUELIST", _pendingRewardIsGauntlet ? "The gauntlet" : _pendingRewardDuelistName ?? "The duelist");
        MBTextManager.SetTextVariable("CASH_REWARD", _pendingRewardIsGauntlet ? GauntletCashReward : CashReward);
        MBTextManager.SetTextVariable("REWARD_TEXT", Pick(RewardTexts));
        MBTextManager.SetTextVariable("RECRUIT_OPTION_TEXT", Pick(RecruitOptionTexts));
        MBTextManager.SetTextVariable("GOLD_OPTION_TEXT", Pick(GoldOptionTexts));
        MBTextManager.SetTextVariable("ITEM_OPTION_TEXT", Pick(ItemOptionTexts));
    }

    private bool RewardRecruitCondition(MenuCallbackArgs args)
    {
        if (!_pendingRewardAllowsRecruit || !RewardCondition(args))
        {
            return false;
        }

        if (IsCompanionLimitReached())
        {
            args.Tooltip = new TextObject("{=dc_companion_limit_reached}Your clan is already at its companion limit.");
            return false;
        }

        return true;
    }

    private bool RewardCondition(MenuCallbackArgs args)
    {
        if (string.IsNullOrEmpty(_pendingRewardHeroId))
        {
            return false;
        }

        MBTextManager.SetTextVariable("DUELIST", _pendingRewardIsGauntlet ? "The gauntlet" : _pendingRewardDuelistName ?? "The duelist");
        MBTextManager.SetTextVariable("CASH_REWARD", _pendingRewardIsGauntlet ? GauntletCashReward : CashReward);
        MBTextManager.SetTextVariable("RECRUIT_OPTION_TEXT", Pick(RecruitOptionTexts));
        MBTextManager.SetTextVariable("GOLD_OPTION_TEXT", Pick(GoldOptionTexts));
        MBTextManager.SetTextVariable("ITEM_OPTION_TEXT", Pick(ItemOptionTexts));
        args.optionLeaveType = GameMenuOption.LeaveType.Continue;
        return true;
    }

    private void CompleteReward(RewardType rewardType)
    {
        switch (rewardType)
        {
            case RewardType.Recruit:
                Hero? hero = GetOrCreatePendingRewardHero();
                RecruitHeroToMainParty(hero, _pendingRewardDuelistName ?? "The duelist");
                break;
            case RewardType.Gold:
                int cashReward = _pendingRewardIsGauntlet ? GauntletCashReward : CashReward;
                Hero.MainHero.ChangeHeroGold(cashReward);
                InformationManager.DisplayMessage(new InformationMessage($"You received {cashReward} denars."));
                break;
            case RewardType.Item:
                GiveRareItem(_pendingRewardRareItemId);
                break;
        }

        ClearPendingReward();
        GameMenu.SwitchToMenu("town");
    }

    private Hero? GetOrCreatePendingRewardHero()
    {
        if (string.IsNullOrEmpty(_pendingRewardHeroId) ||
            string.IsNullOrEmpty(_pendingRewardDuelistName) ||
            string.IsNullOrEmpty(_pendingRewardRareItemId))
        {
            return null;
        }

        Hero? existingHero = Hero.FindFirst(hero => hero.StringId == _pendingRewardHeroId);
        if (existingHero != null)
        {
            return existingHero;
        }

        string heroId = _pendingRewardHeroId!;
        string duelistName = _pendingRewardDuelistName!;
        string rareItemId = _pendingRewardRareItemId!;
        string templateId = string.IsNullOrEmpty(_pendingRewardTemplateId)
            ? GetTemplateIdForRewardItem(rareItemId)
            : _pendingRewardTemplateId!;

        DuelEvent rewardEvent = new(
            heroId,
            duelistName,
            templateId,
            rareItemId,
            string.Empty);

        return GetOrCreateDuelHero(rewardEvent);
    }

    private static string GetTemplateIdForRewardItem(string rareItemId)
    {
        if (rareItemId.IndexOf("sturgia", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "dc_heavy_sturgia_v2";
        }

        if (rareItemId.IndexOf("khuzait", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return "dc_heavy_khuzait_v2";
        }

        return "dc_heavy_vlandia_v2";
    }

    private void RecruitHeroToMainParty(Hero? hero, string displayName)
    {
        if (hero == null)
        {
            InformationManager.DisplayMessage(new InformationMessage("The companion could not be found, so recruitment failed."));
            return;
        }

        if (hero.CompanionOf != Clan.PlayerClan && IsCompanionLimitReached())
        {
            InformationManager.DisplayMessage(new InformationMessage("Your clan is already at its companion limit. Choose gold or the item instead."));
            return;
        }

        if (!hero.IsActive)
        {
            hero.ChangeState(Hero.CharacterStates.Active);
        }

        hero.SetNewOccupation(Occupation.Wanderer);
        hero.IsKnownToPlayer = true;
        hero.SetHasMet();

        if (hero.CompanionOf != Clan.PlayerClan)
        {
            AddCompanionAction.Apply(Clan.PlayerClan, hero);
        }

        if (hero.PartyBelongedTo != MobileParty.MainParty)
        {
            AddHeroToPartyAction.Apply(hero, MobileParty.MainParty);
        }

        if (hero.PartyBelongedTo != MobileParty.MainParty)
        {
            MobileParty.MainParty.MemberRoster.AddToCounts(hero.CharacterObject, 1, false, 0, 0, true, -1);
        }

        if (hero.PartyBelongedTo == MobileParty.MainParty || MobileParty.MainParty.MemberRoster.Contains(hero.CharacterObject))
        {
            InformationManager.DisplayMessage(new InformationMessage($"{displayName} has joined your party."));
        }
        else
        {
            InformationManager.DisplayMessage(new InformationMessage($"{displayName} joined your clan, but could not be moved into the main party."));
        }
    }

    private static bool IsCompanionLimitReached()
    {
        return Clan.PlayerClan.Companions.Count >= Clan.PlayerClan.CompanionLimit;
    }

    private void RecoverDetachedDuelCompanions()
    {
        foreach (Hero hero in Hero.FindAll(hero =>
                     hero.StringId.StartsWith("dc_event_hero_", StringComparison.Ordinal) &&
                     hero.CompanionOf == Clan.PlayerClan &&
                     hero.PartyBelongedTo != MobileParty.MainParty))
        {
            RecruitHeroToMainParty(hero, hero.Name.ToString());
        }
    }

    private void GiveRareItem(string? itemId)
    {
        ItemObject? item = itemId == null ? null : MBObjectManager.Instance.GetObject<ItemObject>(itemId);
        if (item == null)
        {
            Hero.MainHero.ChangeHeroGold(CashReward);
            InformationManager.DisplayMessage(new InformationMessage("The rare weapon was unavailable, so you received denars instead."));
            return;
        }

        MobileParty.MainParty.ItemRoster.AddToCounts(item, 1);
        InformationManager.DisplayMessage(new InformationMessage($"You received {item.Name}."));
    }

    private Hero? GetOrCreateDuelHero(DuelEvent? duel)
    {
        if (duel == null)
        {
            return null;
        }

        Hero? existingHero = Hero.FindFirst(hero => hero.StringId == duel.HeroId);
        if (existingHero != null)
        {
            return existingHero;
        }

        CharacterObject? template = MBObjectManager.Instance.GetObject<CharacterObject>(duel.TemplateId);
        if (template == null)
        {
            return null;
        }

        HeroCreator.CreateBasicHero(duel.HeroId, template, out Hero hero);
        hero.SetName(new TextObject(duel.DisplayName), new TextObject(duel.DisplayName.Split(' ')[0]));
        return hero;
    }

    private DuelEvent? GetActiveEvent()
    {
        if (Settlement.CurrentSettlement?.StringId != _activeSettlementId || string.IsNullOrEmpty(_activeHeroId))
        {
            return null;
        }

        return BuildActiveEvent();
    }

    private DuelEvent? BuildActiveEvent()
    {
        if (string.IsNullOrEmpty(_activeSettlementId) ||
            string.IsNullOrEmpty(_activeDuelistName) ||
            string.IsNullOrEmpty(_activeTemplateId) ||
            string.IsNullOrEmpty(_activeRareItemId) ||
            string.IsNullOrEmpty(_activeGroundDescription) ||
            string.IsNullOrEmpty(_activeHeroId))
        {
            return null;
        }

        return new DuelEvent(_activeHeroId!, _activeDuelistName!, _activeTemplateId!, _activeRareItemId!, _activeGroundDescription!);
    }

    private DuelEvent? GetGauntletOpponent()
    {
        DuelEvent? active = BuildActiveEvent();
        if (active == null)
        {
            return null;
        }

        if (_gauntletRound == 0)
        {
            return active;
        }

        DuelTemplate template = DuelTemplates[(_eventSerial + _gauntletRound) % DuelTemplates.Length];
        string name = BuildName(_eventSerial + _gauntletRound);
        return new DuelEvent($"dc_gauntlet_{_eventSerial}_{_gauntletRound}", name, template.TemplateId, template.RareItemId, active.GroundDescription);
    }

    private float GetGauntletHealth()
    {
        return _gauntletRound switch
        {
            0 => 190f,
            1 => 230f,
            _ => 280f
        };
    }

    private void CreateRandomEvent(bool showMessage)
    {
        List<Town> towns = Town.AllTowns
            .Where(town => town.Settlement?.LocationComplex?.GetLocationWithId("arena") != null && !town.IsUnderSiege)
            .ToList();

        if (towns.Count == 0)
        {
            ScheduleNextEvent(1, 2);
            return;
        }

        _eventSerial++;
        Town town = towns[MBRandom.RandomInt(towns.Count)];
        DuelTemplate template = DuelTemplates[MBRandom.RandomInt(DuelTemplates.Length)];
        string settlementName = town.Settlement.Name.ToString();

        _activeSettlementId = town.Settlement.StringId;
        _activeDuelistName = BuildName(_eventSerial);
        _activeTemplateId = template.TemplateId;
        _activeRareItemId = template.RareItemId;
        _activeGroundDescription = template.GroundDescription.Replace("{SETTLEMENT}", settlementName);
        _activeHeroId = $"dc_event_hero_{_eventSerial}";
        _activeTextVariant = MBRandom.RandomInt(1000);
        _activeEventExpiresDay = CampaignTime.Now.ToDays + EventDurationDays;

        if (showMessage)
        {
            InformationManager.DisplayMessage(new InformationMessage(Pick(RumorTexts).Replace("{SETTLEMENT}", settlementName)));
        }
    }

    private static string BuildName(int seed)
    {
        string firstName = FirstNames[Math.Abs(seed) % FirstNames.Length];
        string epithet = Epithets[Math.Abs(seed * 7 + 3) % Epithets.Length];
        return $"{firstName} {epithet}";
    }

    private string Pick(string[] variants)
    {
        int index = Math.Abs(_activeTextVariant + _eventSerial) % variants.Length;
        return variants[index];
    }

    private void ScheduleNextEvent(int minDays, int maxDays)
    {
        _nextEventDay = CampaignTime.Now.ToDays + MBRandom.RandomInt(minDays, maxDays + 1);
    }

    private void ClearActiveEvent()
    {
        _activeSettlementId = null;
        _activeDuelistName = null;
        _activeTemplateId = null;
        _activeRareItemId = null;
        _activeGroundDescription = null;
        _activeHeroId = null;
        _activeTextVariant = 0;
        _activeEventExpiresDay = 0;
        _isGauntletActive = false;
        _gauntletRound = 0;
    }

    private void ClearPendingReward()
    {
        _pendingRewardHeroId = null;
        _pendingRewardDuelistName = null;
        _pendingRewardTemplateId = null;
        _pendingRewardRareItemId = null;
        _pendingRewardAllowsRecruit = true;
        _pendingRewardIsGauntlet = false;
    }

    private enum RewardType
    {
        Recruit,
        Gold,
        Item
    }

    private sealed class DuelTemplate
    {
        public DuelTemplate(string templateId, string rareItemId, string groundDescription)
        {
            TemplateId = templateId;
            RareItemId = rareItemId;
            GroundDescription = groundDescription;
        }

        public string TemplateId { get; }
        public string RareItemId { get; }
        public string GroundDescription { get; }
    }

    private sealed class DuelEvent
    {
        public DuelEvent(string heroId, string displayName, string templateId, string rareItemId, string groundDescription)
        {
            HeroId = heroId;
            DisplayName = displayName;
            TemplateId = templateId;
            RareItemId = rareItemId;
            GroundDescription = groundDescription;
        }

        public string HeroId { get; }
        public string DisplayName { get; }
        public string TemplateId { get; }
        public string RareItemId { get; }
        public string GroundDescription { get; }
    }
}
