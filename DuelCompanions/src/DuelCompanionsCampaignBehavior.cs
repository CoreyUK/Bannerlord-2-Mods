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
using TaleWorlds.SaveSystem;

namespace DuelCompanions;

public sealed class DuelCompanionsCampaignBehavior : CampaignBehaviorBase
{
    private const string DuelMenuId = "dc_duel_ground";
    private const string RewardMenuId = "dc_duel_reward";
    private const int CashReward = 6000;
    private const int GauntletCashReward = 15000;
    private const int GauntletRounds = 3;
    private const int DefeatGoldPenalty = 20000;
    private const float DefeatMoralePenalty = 70f;
    private const float DefeatWeaponLossChance = 0.35f;
    private const int EventDurationDays = 10;
    private const int FirstRumorMinDays = 7;
    private const int FirstRumorMaxDays = 12;
    private const int FollowupRumorMinDays = 8;
    private const int FollowupRumorMaxDays = 16;
    private static readonly bool ImmediateRumorForTesting = false;
    private static readonly bool ForceLagetaTesting = false;
    private const string TestSettlementId = "town_EW1";

    private static readonly DuelTemplate[] DuelTemplates =
    {
        new("dc_heavy_vlandia_v2", "vlandia_noble_sword_3_t5", "{SETTLEMENT}'s old tourney yard"),
        new("dc_vlandia_longsword", "vlandia_noble_sword_4_t5", "the tiltyard beneath {SETTLEMENT}'s west wall"),
        new("dc_vlandia_mace", "vlandia_mace_3_t5", "a fenced yard beside {SETTLEMENT}'s old stables"),
        new("dc_vlandia_axe", "vlandia_axe_2_t4", "the knightly lists outside {SETTLEMENT}"),
        new("dc_vlandia_falchion", "vlandia_sword_5_t5", "a trampled tourney ring near {SETTLEMENT}"),
        new("dc_heavy_sturgia_v2", "sturgia_2haxe_2_t5", "a marked ring beyond {SETTLEMENT}'s gates"),
        new("dc_sturgia_sword", "sturgia_noble_sword_4_t5", "a frost-bitten practice ground outside {SETTLEMENT}"),
        new("dc_sturgia_axe", "sturgia_axe_5_t5", "a timber duelling circle near {SETTLEMENT}"),
        new("dc_sturgia_mace", "sturgia_mace_2_t4", "the shield-yard behind {SETTLEMENT}'s barracks"),
        new("dc_sturgia_roundshield", "sturgia_noble_sword_2_t5", "a ring of split logs beyond {SETTLEMENT}"),
        new("dc_heavy_khuzait_v2", "khuzait_noble_sword_1_t5", "the duelling stones outside {SETTLEMENT}")
        ,new("dc_khuzait_sabre", "khuzait_noble_sword_2_t5", "a wind-scoured ring near {SETTLEMENT}")
        ,new("dc_khuzait_mace", "khuzait_mace_4_t5", "a steppe camp beyond {SETTLEMENT}'s road")
        ,new("dc_khuzait_axe", "bamboo_axe_t4", "a horseman's challenge circle outside {SETTLEMENT}")
        ,new("dc_khuzait_guardian", "khuzait_noble_sword_3_t5", "the guardian stones above {SETTLEMENT}")
        ,new("dc_empire_spatha", "empire_noble_sword_1_t5", "the imperial drill yard inside {SETTLEMENT}'s walls")
        ,new("dc_empire_cataphract", "empire_noble_sword_2_t5", "a marble-lined arena court in {SETTLEMENT}")
        ,new("dc_empire_mace", "empire_mace_5_t5", "the legionary sand pit at {SETTLEMENT}")
        ,new("dc_empire_axe", "imperial_axe_t3", "an old legion training ring near {SETTLEMENT}")
        ,new("dc_empire_guard", "empire_noble_sword_3_t5", "the court guard yard of {SETTLEMENT}")
        ,new("dc_aserai_sabre", "aserai_noble_sword_1_t5", "a sun-baked fighting square in {SETTLEMENT}")
        ,new("dc_aserai_longblade", "aserai_noble_sword_4_t5", "the merchant guard yard of {SETTLEMENT}")
        ,new("dc_aserai_axe", "aserai_2haxe_3_t5", "a marked sand ring beyond {SETTLEMENT}")
        ,new("dc_aserai_mace", "aserai_mace_5_t4", "the red-dust circle outside {SETTLEMENT}")
        ,new("dc_aserai_shield", "aserai_noble_sword_3_t5", "the emir's old practice court at {SETTLEMENT}")
        ,new("dc_battania_falx", "battania_noble_sword_1_t5", "a moss-ringed clearing near {SETTLEMENT}")
        ,new("dc_battania_longsword", "battania_noble_sword_3_t5", "the standing stones beyond {SETTLEMENT}")
        ,new("dc_battania_axe", "battania_axe_3_t5", "a woodland duelling hollow outside {SETTLEMENT}")
        ,new("dc_battania_twohand", "battania_2haxe_1_t2", "the old clan circle above {SETTLEMENT}")
        ,new("dc_battania_targe", "battania_sword_5_t5", "a highland challenge ground near {SETTLEMENT}")
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
    private string? _activeQuestId;
    private bool _pendingRumorNotification;
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
    private string? _gauntletRoundOneTemplateId;
    private float _rumorNotificationDelaySeconds;

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.TickEvent.AddNonSerializedListener(this, OnCampaignTick);
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
        dataStore.SyncData("dc_active_quest_id_v2", ref _activeQuestId);
        dataStore.SyncData("dc_pending_rumor_notification_v2", ref _pendingRumorNotification);
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
        dataStore.SyncData("dc_gauntlet_round_one_template_id_v2", ref _gauntletRoundOneTemplateId);
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        AddMenus(starter);
        RecoverDetachedDuelCompanions();

        if (ImmediateRumorForTesting && string.IsNullOrEmpty(_activeSettlementId))
        {
            CreateRandomEvent(showMessage: true);
            return;
        }

        EnsureActiveRumorQuestAndPrompt();

        if (ForceLagetaTesting)
        {
            ClearActiveEvent();
            CreateRandomEvent(showMessage: true);
            return;
        }

        if (string.IsNullOrEmpty(_activeSettlementId) && _nextEventDay <= 0)
        {
            ScheduleNextEvent(FirstRumorMinDays, FirstRumorMaxDays);
        }
    }

    private void OnCampaignTick(float dt)
    {
        if (!_pendingRumorNotification)
        {
            return;
        }

        if (_rumorNotificationDelaySeconds <= 0f)
        {
            _rumorNotificationDelaySeconds = 2f;
            return;
        }

        _rumorNotificationDelaySeconds -= dt;
        if (_rumorNotificationDelaySeconds > 0f)
        {
            return;
        }

        ShowPendingRumorNotification();
    }

    private void OnDailyTick()
    {
        double today = CampaignTime.Now.ToDays;

        if (!string.IsNullOrEmpty(_activeSettlementId) && today > _activeEventExpiresDay)
        {
            ClearActiveEvent(ActiveEventClearReason.Expired);
            ScheduleNextEvent(FollowupRumorMinDays, FollowupRumorMaxDays);
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

        starter.AddGameMenuOption(DuelMenuId, "dc_start_duel", "{DUEL_OPTION_TEXT}", DuelChallengeCondition, args => StartDuelMission(GetActiveEvent(), 360f, false), false, 0);
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
        _gauntletRoundOneTemplateId = GetActiveEvent()?.TemplateId;
        DuelCompanionsMissionState.ResetGauntletPlayerHealth();
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
        DuelCompanionsMissionState.ArmNextDuel(isGauntlet);
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
            _gauntletRoundOneTemplateId = null;
            DuelCompanionsMissionState.ResetGauntletPlayerHealth();
            ClearActiveEvent(ActiveEventClearReason.Succeeded);
            ScheduleNextEvent(FollowupRumorMinDays, FollowupRumorMaxDays);
            GameMenu.SwitchToMenu(RewardMenuId);
            return;
        }

        ApplyDuelDefeatConsequences(duel.DisplayName);
        _isGauntletActive = false;
        _gauntletRound = 0;
        _gauntletRoundOneTemplateId = null;
        DuelCompanionsMissionState.ResetGauntletPlayerHealth();
        GameMenu.SwitchToMenu("town");
    }

    private void ApplyDuelDefeatConsequences(string duelistName)
    {
        MobileParty.MainParty.RecentEventsMorale -= DefeatMoralePenalty;
        Hero.MainHero.ChangeHeroGold(-DefeatGoldPenalty);

        string consequence = "Your morale is in tatters after the humiliating defeat.\n\nYou lose 20,000 denars and your party morale collapses.";

        if (MBRandom.RandomFloat < DefeatWeaponLossChance && TryLoseEquippedWeapon(out string weaponName))
        {
            consequence += $"\n\nIn disgust, you leave {weaponName} in the ring.";
        }

        InformationManager.ShowInquiry(
            new InquiryData(
                "Duel Defeat",
                $"{duelistName} claims the field.\n\n{consequence}",
                true,
                false,
                "Continue",
                string.Empty,
                null,
                null,
                string.Empty,
                0f,
                null,
                null,
                null),
            true,
            false);
    }

    private static bool TryLoseEquippedWeapon(out string weaponName)
    {
        weaponName = string.Empty;
        Equipment battleEquipment = Hero.MainHero.BattleEquipment;
        EquipmentIndex[] weaponSlots =
        {
            EquipmentIndex.Weapon0,
            EquipmentIndex.Weapon1,
            EquipmentIndex.Weapon2,
            EquipmentIndex.Weapon3
        };

        foreach (EquipmentIndex slot in weaponSlots)
        {
            EquipmentElement element = battleEquipment[slot];
            ItemObject? item = element.Item;
            if (item == null || element.IsEmpty || element.IsQuestItem || !item.HasWeaponComponent)
            {
                continue;
            }

            weaponName = item.Name.ToString();
            battleEquipment[slot] = EquipmentElement.Invalid;

            if (MobileParty.MainParty.ItemRoster.FindIndexOfElement(element) >= 0)
            {
                MobileParty.MainParty.ItemRoster.AddToCounts(element, -1);
            }

            return true;
        }

        return false;
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

        DuelTemplate template = GetGauntletTemplateForRound(active.TemplateId, _gauntletRound);
        string name = BuildName(_eventSerial + _gauntletRound);
        return new DuelEvent($"dc_gauntlet_{_eventSerial}_{_gauntletRound}", name, template.TemplateId, template.RareItemId, active.GroundDescription);
    }

    private DuelTemplate GetGauntletTemplateForRound(string activeTemplateId, int round)
    {
        List<string> usedStyles = new();
        string firstTemplateId = _gauntletRoundOneTemplateId ?? activeTemplateId;
        usedStyles.Add(GetTemplateStyle(firstTemplateId));

        if (round > 1)
        {
            DuelTemplate previousTemplate = GetGauntletTemplateForRound(activeTemplateId, round - 1);
            usedStyles.Add(GetTemplateStyle(previousTemplate.TemplateId));
        }

        List<DuelTemplate> candidates = DuelTemplates
            .Where(template =>
                template.TemplateId != activeTemplateId &&
                !usedStyles.Contains(GetTemplateStyle(template.TemplateId)))
            .ToList();

        if (candidates.Count == 0)
        {
            candidates = DuelTemplates
                .Where(template => template.TemplateId != activeTemplateId)
                .ToList();
        }

        int index = Math.Abs(_eventSerial + (round * 11)) % candidates.Count;
        return candidates[index];
    }

    private static string GetTemplateStyle(string templateId)
    {
        if (ContainsIgnoreCase(templateId, "mace"))
        {
            return "mace_shield";
        }

        if (ContainsIgnoreCase(templateId, "roundshield") ||
            ContainsIgnoreCase(templateId, "guard") ||
            ContainsIgnoreCase(templateId, "shield") ||
            ContainsIgnoreCase(templateId, "targe") ||
            ContainsIgnoreCase(templateId, "heavy_vlandia") ||
            ContainsIgnoreCase(templateId, "heavy_khuzait"))
        {
            return "sword_shield";
        }

        if (ContainsIgnoreCase(templateId, "axe") ||
            ContainsIgnoreCase(templateId, "twohand") ||
            ContainsIgnoreCase(templateId, "heavy_sturgia") ||
            ContainsIgnoreCase(templateId, "falx"))
        {
            return "axe";
        }

        return "sword";
    }

    private static bool ContainsIgnoreCase(string value, string pattern)
    {
        return value.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private float GetGauntletHealth()
    {
        return _gauntletRound switch
        {
            0 => 320f,
            1 => 380f,
            _ => 460f
        };
    }

    private void CreateRandomEvent(bool showMessage)
    {
        List<Town> towns = Town.AllTowns
            .Where(town => town.Settlement?.LocationComplex?.GetLocationWithId("arena") != null && !town.IsUnderSiege)
            .ToList();

        if (towns.Count == 0)
        {
            ScheduleNextEvent(FollowupRumorMinDays, FollowupRumorMaxDays);
            return;
        }

        _eventSerial++;
        Town town = ForceLagetaTesting
            ? towns.FirstOrDefault(candidate => candidate.Settlement?.StringId == TestSettlementId) ?? towns[MBRandom.RandomInt(towns.Count)]
            : towns[MBRandom.RandomInt(towns.Count)];
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
            QueueActiveRumorNotification();
        }
    }

    private void ShowDuelRumorInquiry(string settlementName)
    {
        InformationManager.ShowInquiry(
            new InquiryData(
                "Duel Rumour",
                $"A duel rumour reaches your party.\n\n{_activeDuelistName} is accepting challengers near {settlementName} at {_activeGroundDescription}.\n\nVisit {settlementName} before the rumour fades.",
                true,
                false,
                "Acknowledge",
                string.Empty,
                null,
                null,
                string.Empty,
                0f,
                null,
                null,
                null),
            true,
            false);
    }

    private void EnsureActiveRumorQuestAndPrompt()
    {
        if (string.IsNullOrEmpty(_activeSettlementId) ||
            string.IsNullOrEmpty(_activeDuelistName) ||
            string.IsNullOrEmpty(_activeGroundDescription))
        {
            return;
        }

        Settlement? settlement = Settlement.Find(_activeSettlementId);
        if (settlement == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(_activeQuestId))
        {
            QueueActiveRumorNotification();
        }
    }

    private void QueueActiveRumorNotification()
    {
        _pendingRumorNotification = true;
        _rumorNotificationDelaySeconds = 2f;
    }

    private void ShowPendingRumorNotification()
    {
        if (string.IsNullOrEmpty(_activeSettlementId))
        {
            _pendingRumorNotification = false;
            return;
        }

        Settlement? settlement = Settlement.Find(_activeSettlementId);
        if (settlement == null)
        {
            _pendingRumorNotification = false;
            return;
        }

        if (string.IsNullOrEmpty(_activeQuestId))
        {
            StartDuelRumorQuest(settlement);
        }

        ShowDuelRumorInquiry(settlement.Name.ToString());
        _pendingRumorNotification = false;
        _rumorNotificationDelaySeconds = 0f;
    }

    private void StartDuelRumorQuest(Settlement settlement)
    {
        if (string.IsNullOrEmpty(_activeSettlementId) ||
            string.IsNullOrEmpty(_activeDuelistName) ||
            string.IsNullOrEmpty(_activeGroundDescription))
        {
            return;
        }

        CompleteActiveDuelQuest(ActiveEventClearReason.Cancelled);

        _activeQuestId = $"dc_duel_rumor_quest_{_eventSerial}";
        int daysRemaining = Math.Max(1, (int)Math.Ceiling(_activeEventExpiresDay - CampaignTime.Now.ToDays));
        DuelRumorQuest quest = new(
            _activeQuestId,
            settlement,
            _activeDuelistName!,
            _activeGroundDescription!,
            daysRemaining);
        quest.StartQuest();
    }

    private void CompleteActiveDuelQuest(ActiveEventClearReason reason)
    {
        if (string.IsNullOrEmpty(_activeQuestId) || Campaign.Current?.QuestManager == null)
        {
            return;
        }

        QuestBase? quest = Campaign.Current.QuestManager.Quests.FirstOrDefault(candidate =>
            candidate.StringId == _activeQuestId && candidate.IsOngoing);
        if (quest == null)
        {
            return;
        }

        switch (reason)
        {
            case ActiveEventClearReason.Succeeded:
                quest.CompleteQuestWithSuccess();
                break;
            case ActiveEventClearReason.Expired:
                quest.CompleteQuestWithTimeOut();
                break;
            default:
                quest.CompleteQuestWithCancel();
                break;
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

    private void ClearActiveEvent(ActiveEventClearReason reason = ActiveEventClearReason.Cancelled)
    {
        CompleteActiveDuelQuest(reason);
        _activeSettlementId = null;
        _activeDuelistName = null;
        _activeTemplateId = null;
        _activeRareItemId = null;
        _activeGroundDescription = null;
        _activeHeroId = null;
        _activeQuestId = null;
        _pendingRumorNotification = false;
        _activeTextVariant = 0;
        _activeEventExpiresDay = 0;
        _isGauntletActive = false;
        _gauntletRound = 0;
        _gauntletRoundOneTemplateId = null;
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

    private enum ActiveEventClearReason
    {
        Cancelled,
        Expired,
        Succeeded
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
