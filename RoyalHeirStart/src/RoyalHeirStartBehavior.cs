using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Core.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.Localization;
using TaleWorlds.ObjectSystem;

namespace RoyalHeirStart;

public sealed class RoyalHeirStartBehavior : CampaignBehaviorBase
{
    private const float PromptDelaySeconds = 1.5f;
    private const string RoyalCourtMenuId = "rhs_royal_court";
    private const int MaxKingdomOptions = 10;

    private bool _shouldOfferRoyalStart;
    private bool _royalStartApplied;
    private bool _readyNotificationShown;
    private bool _summonsPopupShown;
    private bool _promptOpen;
    private string? _preservedPlayerBannerCode;
    private uint _preservedPlayerClanColor;
    private uint _preservedPlayerClanColor2;
    private int _bannerRestoreTicksRemaining;
    private float _elapsedSinceSessionStart;

    public override void RegisterEvents()
    {
        RoyalHeirStartLog.Write("RegisterEvents.");
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.OnAfterSessionLaunchedEvent.AddNonSerializedListener(this, OnAfterSessionLaunched);
        CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
    }

    public override void SyncData(IDataStore dataStore)
    {
        dataStore.SyncData("rhs_should_offer_royal_start", ref _shouldOfferRoyalStart);
        dataStore.SyncData("rhs_royal_start_applied", ref _royalStartApplied);
        dataStore.SyncData("rhs_ready_notification_shown", ref _readyNotificationShown);
        dataStore.SyncData("rhs_summons_popup_shown", ref _summonsPopupShown);
        dataStore.SyncData("rhs_preserved_player_banner_code", ref _preservedPlayerBannerCode);
        dataStore.SyncData("rhs_preserved_player_clan_color", ref _preservedPlayerClanColor);
        dataStore.SyncData("rhs_preserved_player_clan_color2", ref _preservedPlayerClanColor2);
        dataStore.SyncData("rhs_banner_restore_ticks_remaining", ref _bannerRestoreTicksRemaining);
    }

    private void OnNewGameCreated(CampaignGameStarter starter)
    {
        RoyalHeirStartLog.Write("OnNewGameCreated.");
        _shouldOfferRoyalStart = true;
        _royalStartApplied = false;
        _readyNotificationShown = false;
        _summonsPopupShown = false;
        _promptOpen = false;
        _elapsedSinceSessionStart = 0f;
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        RoyalHeirStartLog.Write($"OnSessionLaunched: loadingType={Campaign.Current?.CampaignGameLoadingType.ToString() ?? "null"}");
        AddMenus(starter);

        if (!_royalStartApplied &&
            Campaign.Current != null &&
            (Campaign.Current.CampaignGameLoadingType == Campaign.GameLoadingType.NewCampaign ||
             Campaign.Current.CampaignGameLoadingType == Campaign.GameLoadingType.Tutorial))
        {
            _shouldOfferRoyalStart = true;
        }

        _promptOpen = false;
        _elapsedSinceSessionStart = 0f;
    }

    private void OnAfterSessionLaunched(CampaignGameStarter starter)
    {
        RoyalHeirStartLog.Write($"OnAfterSessionLaunched: canOffer={CanOfferRoyalStart()}");
        if (!_royalStartApplied && CanOfferRoyalStart())
        {
            _shouldOfferRoyalStart = true;
        }
    }

    private void AddMenus(CampaignGameStarter starter)
    {
        starter.AddGameMenu(
            RoyalCourtMenuId,
            "{RHS_ROYAL_COURT_TEXT}",
            OnRoyalCourtMenuInit,
            GameMenu.MenuOverlayType.SettlementWithBoth);

        for (int i = 0; i < MaxKingdomOptions; i++)
        {
            int optionIndex = i;
            starter.AddGameMenuOption(
                RoyalCourtMenuId,
                $"rhs_select_kingdom_{i}",
                $"{{RHS_KINGDOM_OPTION_{i}}}",
                args => RoyalCourtKingdomCondition(args, optionIndex),
                args => SelectRoyalCourtKingdom(optionIndex),
                false,
                i);
        }

        starter.AddGameMenuOption(
            RoyalCourtMenuId,
            "rhs_refuse_crown",
            "{=rhs_refuse_crown}Refuse the crown",
            args =>
            {
                args.optionLeaveType = GameMenuOption.LeaveType.Leave;
                return true;
            },
            args =>
            {
                _royalStartApplied = true;
                _shouldOfferRoyalStart = false;
                GameMenu.SwitchToMenu("town");
            },
            true,
            MaxKingdomOptions);

        AddRoyalClaimOption(starter, "town", "rhs_town_claim_crown");
        AddRoyalClaimOption(starter, "village", "rhs_village_claim_crown");
        AddRoyalClaimOption(starter, "castle", "rhs_castle_claim_crown");
    }

    private static void AddRoyalClaimOption(CampaignGameStarter starter, string sourceMenu, string optionId)
    {
        starter.AddGameMenuOption(
            sourceMenu,
            optionId,
            "{=rhs_claim_crown}Answer the summons of the royal court",
            RoyalClaimCondition,
            RoyalClaimConsequence,
            false,
            0);
    }

    private static bool RoyalClaimCondition(MenuCallbackArgs args)
    {
        RoyalHeirStartBehavior? behavior = Campaign.Current?.CampaignBehaviorManager.GetBehavior<RoyalHeirStartBehavior>();
        if (behavior == null || behavior._royalStartApplied || !behavior.CanOfferRoyalStart())
        {
            return false;
        }

        args.optionLeaveType = GameMenuOption.LeaveType.Submenu;
        return true;
    }

    private static void RoyalClaimConsequence(MenuCallbackArgs args)
    {
        RoyalHeirStartBehavior? behavior = Campaign.Current?.CampaignBehaviorManager.GetBehavior<RoyalHeirStartBehavior>();
        behavior?.ShowRoyalStartInquiry();
    }

    private void OnTick(float dt)
    {
        if (_bannerRestoreTicksRemaining > 0)
        {
            RestorePlayerBanner();
            _bannerRestoreTicksRemaining--;
        }

        if (_royalStartApplied || _promptOpen || !CanOfferRoyalStart())
        {
            return;
        }

        _elapsedSinceSessionStart += dt;
        if (_elapsedSinceSessionStart < PromptDelaySeconds)
        {
            return;
        }

        if (_shouldOfferRoyalStart || IsEarlyUnclaimedStart())
        {
            ShowRoyalSummonsNotice();
            _shouldOfferRoyalStart = false;
        }
    }

    private void ShowRoyalSummonsNotice()
    {
        if (_summonsPopupShown || _promptOpen)
        {
            return;
        }

        _promptOpen = true;
        _summonsPopupShown = true;

        var inquiry = new InquiryData(
            "Royal Court Summons",
            "A sealed message arrives from the high courts. The ruler has died after a sudden illness, and your bloodline has been named in the succession.\n\nVisit any settlement and answer the summons of the royal court to issue your response.",
            true,
            false,
            "Understood",
            string.Empty,
            () =>
            {
                _promptOpen = false;
                ShowReadyNotification();
            },
            null,
            string.Empty,
            0f,
            null,
            null,
            null);

        InformationManager.ShowInquiry(inquiry, true, false);
    }

    private void ShowReadyNotification()
    {
        if (_readyNotificationShown)
        {
            return;
        }

        _readyNotificationShown = true;
        InformationManager.DisplayMessage(new InformationMessage(
            "Royal summons received. Visit any settlement to claim a crown.",
            new Color(0.72f, 0.58f, 0.22f, 1f)));
    }

    private void OnRoyalCourtMenuInit(MenuCallbackArgs args)
    {
        MBTextManager.SetTextVariable(
            "RHS_ROYAL_COURT_TEXT",
            "A courier from the royal court finds you at the gates. The ruler has died after a sudden illness. Courtiers reveal that you are the lost heir and ask which realm will recognize your claim.");
    }

    private static bool RoyalCourtKingdomCondition(MenuCallbackArgs args, int optionIndex)
    {
        RoyalHeirStartBehavior? behavior = Campaign.Current?.CampaignBehaviorManager.GetBehavior<RoyalHeirStartBehavior>();
        if (behavior == null || behavior._royalStartApplied || !behavior.CanOfferRoyalStart())
        {
            return false;
        }

        List<KingdomStartOption> options = BuildKingdomOptions();
        if (optionIndex < 0 || optionIndex >= options.Count)
        {
            return false;
        }

        KingdomStartOption option = options[optionIndex];
        MBTextManager.SetTextVariable($"RHS_KINGDOM_OPTION_{optionIndex}", $"{option.Title}: {option.Hint}");
        args.optionLeaveType = GameMenuOption.LeaveType.Continue;
        return true;
    }

    private void SelectRoyalCourtKingdom(int optionIndex)
    {
        List<KingdomStartOption> options = BuildKingdomOptions();
        if (optionIndex < 0 || optionIndex >= options.Count)
        {
            GameMenu.SwitchToMenu("town");
            return;
        }

        ApplyRoyalStart(options[optionIndex]);
        GameMenu.SwitchToMenu("town");
    }

    private bool CanOfferRoyalStart()
    {
        return Hero.MainHero != null &&
               Clan.PlayerClan != null &&
               MobileParty.MainParty != null &&
               Clan.PlayerClan.Kingdom == null;
    }

    private static bool IsEarlyUnclaimedStart()
    {
        if (Campaign.Current == null || Clan.PlayerClan == null)
        {
            return false;
        }

        if (Campaign.Current.CampaignGameLoadingType == Campaign.GameLoadingType.NewCampaign ||
            Campaign.Current.CampaignGameLoadingType == Campaign.GameLoadingType.Tutorial)
        {
            return true;
        }

        return Clan.PlayerClan.Tier <= 1 && CampaignTime.Now.ToDays <= 5d;
    }

    private void ShowRoyalStartInquiry()
    {
        RoyalHeirStartLog.Write("ShowRoyalStartInquiry.");
        List<KingdomStartOption> options = BuildKingdomOptions();
        RoyalHeirStartLog.Write($"Kingdom options: {options.Count}");
        if (options.Count == 0)
        {
            _shouldOfferRoyalStart = false;
            InformationManager.DisplayMessage(new InformationMessage("Royal Heir Start could not find any kingdoms to inherit."));
            return;
        }

        List<InquiryElement> elements = options
            .Select(option => new InquiryElement(option, option.Title, new BannerImageIdentifier(option.Kingdom.Banner, true), true, option.Hint))
            .ToList();

        _promptOpen = true;
        _shouldOfferRoyalStart = true;
        var inquiry = new MultiSelectionInquiryData(
            "A Crown Without an Heir",
            "The ruler has died after a sudden illness. Courtiers reveal that you are the lost heir and ask which realm will recognize your claim.",
            elements,
            true,
            1,
            1,
            "Accept the crown",
            "Refuse",
            OnKingdomSelected,
            OnKingdomSelectionClosed,
            string.Empty,
            false);

        MBInformationManager.ShowMultiSelectionInquiry(inquiry, true, false);
    }

    private void OnKingdomSelected(List<InquiryElement> selectedElements)
    {
        _promptOpen = false;
        KingdomStartOption? selectedOption = selectedElements.FirstOrDefault()?.Identifier as KingdomStartOption;
        if (selectedOption == null)
        {
            return;
        }

        ApplyRoyalStart(selectedOption);
    }

    private void OnKingdomSelectionClosed(List<InquiryElement> selectedElements)
    {
        _promptOpen = false;
        _royalStartApplied = true;
        _shouldOfferRoyalStart = false;
        InformationManager.DisplayMessage(new InformationMessage("You refused the crown and began as a free adventurer."));
    }

    private void ApplyRoyalStart(KingdomStartOption option)
    {
        Kingdom kingdom = option.Kingdom;
        Clan playerClan = Clan.PlayerClan;
        Hero player = Hero.MainHero;
        Hero oldRuler = kingdom.Leader;
        PreservePlayerBanner(playerClan);

        playerClan.Culture = kingdom.Culture;
        playerClan.IsNoble = true;
        playerClan.AddRenown(option.RenownBonus);
        ChangeClanInfluenceAction.Apply(playerClan, option.InfluenceBonus);
        player.ChangeHeroGold(option.GoldBonus);

        if (playerClan.Kingdom != kingdom)
        {
            ChangeKingdomAction.ApplyByJoinToKingdom(playerClan, kingdom, CampaignTime.Now, true);
        }

        ChangeRulingClanAction.Apply(kingdom, playerClan);
        RestorePlayerBanner();

        Settlement? seat = PickRoyalSeat(kingdom, oldRuler);
        if (seat != null && seat.OwnerClan != playerClan)
        {
            ChangeOwnerOfSettlementAction.ApplyByDefault(player, seat);
            RestorePlayerBanner();
        }

        AddStartingTroops(option);
        TryRetireOldRuler(oldRuler, player);
        RestorePlayerBanner();
        _bannerRestoreTicksRemaining = 300;

        _royalStartApplied = true;
        _shouldOfferRoyalStart = false;

        string seatText = seat == null ? string.Empty : $" {seat.Name} has been granted to your clan as the royal seat.";
        InformationManager.DisplayMessage(new InformationMessage($"You have inherited {kingdom.Name}. {option.Summary}{seatText}"));
    }

    private void PreservePlayerBanner(Clan playerClan)
    {
        _preservedPlayerBannerCode = playerClan.Banner.BannerCode;
        _preservedPlayerClanColor = playerClan.Color;
        _preservedPlayerClanColor2 = playerClan.Color2;
    }

    private void RestorePlayerBanner()
    {
        if (Clan.PlayerClan == null || string.IsNullOrEmpty(_preservedPlayerBannerCode))
        {
            return;
        }

        Clan.PlayerClan.Color = _preservedPlayerClanColor;
        Clan.PlayerClan.Color2 = _preservedPlayerClanColor2;
        Clan.PlayerClan.Banner = new Banner(_preservedPlayerBannerCode);

        if (Clan.PlayerClan.Kingdom != null)
        {
            Clan.PlayerClan.Kingdom.Banner = new Banner(_preservedPlayerBannerCode);
        }
    }

    private static List<KingdomStartOption> BuildKingdomOptions()
    {
        return Kingdom.All
            .Where(kingdom => !kingdom.IsEliminated && !kingdom.IsMinorFaction && !kingdom.IsRebelClan && kingdom.Culture != null)
            .OrderBy(kingdom => kingdom.Name.ToString())
            .Select(CreateOption)
            .ToList();
    }

    private static KingdomStartOption CreateOption(Kingdom kingdom)
    {
        string kingdomId = kingdom.StringId.ToLowerInvariant();
        string cultureId = kingdom.Culture.StringId.ToLowerInvariant();
        string key = kingdomId.IndexOf("empire", StringComparison.OrdinalIgnoreCase) >= 0 ? "empire" : cultureId;

        return key switch
        {
            "vlandia" => new KingdomStartOption(kingdom, "Vlandia - Banner Knight Retinue", "Gain 6 Banner Knights, 18,000 denars, 140 influence, and 120 renown.", "A hard-hitting cavalry start built for immediate field command.", "vlandian_banner_knight", 6, 18000, 140f, 120f),
            "sturgia" => new KingdomStartOption(kingdom, "Sturgia - Druzhinnik Guard", "Gain 6 Druzhinnik Champions, 15,000 denars, 160 influence, and 130 renown.", "A resilient northern start with elite personal guards.", "druzhinnik_champion", 6, 15000, 160f, 130f),
            "battania" => new KingdomStartOption(kingdom, "Battania - Fian Household", "Gain 8 Fian Champions, 12,000 denars, 130 influence, and 140 renown.", "A deadly archer start that rewards defensive terrain and ambushes.", "battanian_fian_champion", 8, 12000, 130f, 140f),
            "khuzait" => new KingdomStartOption(kingdom, "Khuzait - Khan's Guard Escort", "Gain 6 Khan's Guards, 14,000 denars, 150 influence, and 120 renown.", "A mobile start with elite horse archers and strong campaign speed.", "khuzait_khans_guard", 6, 14000, 150f, 120f),
            "aserai" => new KingdomStartOption(kingdom, "Aserai - Mameluke Treasury", "Gain 6 Mameluke Heavy Cavalry, 22,000 denars, 120 influence, and 110 renown.", "A wealthy start with flexible cavalry and extra financial room.", "aserai_mameluke_heavy_cavalry", 6, 22000, 120f, 110f),
            _ => new KingdomStartOption(kingdom, $"{kingdom.Name} - Imperial Cataphract Corps", "Gain 6 Elite Cataphracts, 16,000 denars, 150 influence, and 130 renown.", "A disciplined imperial start with heavy cavalry and balanced political capital.", "imperial_elite_cataphract", 6, 16000, 150f, 130f)
        };
    }

    private static Settlement? PickRoyalSeat(Kingdom kingdom, Hero oldRuler)
    {
        Settlement? oldRulerTown = kingdom.Settlements
            .Where(settlement => settlement.IsTown && settlement.OwnerClan == oldRuler.Clan)
            .OrderBy(settlement => settlement.Name.ToString())
            .FirstOrDefault();

        if (oldRulerTown != null)
        {
            return oldRulerTown;
        }

        return kingdom.Settlements
            .Where(settlement => settlement.IsTown)
            .OrderBy(settlement => settlement.Name.ToString())
            .FirstOrDefault();
    }

    private static void AddStartingTroops(KingdomStartOption option)
    {
        CharacterObject? troop = MBObjectManager.Instance.GetObject<CharacterObject>(option.TroopId);
        if (troop == null || MobileParty.MainParty?.MemberRoster == null)
        {
            return;
        }

        TroopRoster roster = MobileParty.MainParty.MemberRoster;
        roster.AddToCounts(troop, option.TroopCount, false, 0, 0, true, -1);
    }

    private static void TryRetireOldRuler(Hero oldRuler, Hero player)
    {
        if (oldRuler == null || oldRuler == player || oldRuler.IsDead)
        {
            return;
        }

        KillCharacterAction.ApplyByOldAge(oldRuler, true);
    }

    private sealed class KingdomStartOption
    {
        public KingdomStartOption(Kingdom kingdom, string title, string hint, string summary, string troopId, int troopCount, int goldBonus, float influenceBonus, float renownBonus)
        {
            Kingdom = kingdom;
            Title = title;
            Hint = hint;
            Summary = summary;
            TroopId = troopId;
            TroopCount = troopCount;
            GoldBonus = goldBonus;
            InfluenceBonus = influenceBonus;
            RenownBonus = renownBonus;
        }

        public Kingdom Kingdom { get; }
        public string Title { get; }
        public string Hint { get; }
        public string Summary { get; }
        public string TroopId { get; }
        public int TroopCount { get; }
        public int GoldBonus { get; }
        public float InfluenceBonus { get; }
        public float RenownBonus { get; }
    }
}
