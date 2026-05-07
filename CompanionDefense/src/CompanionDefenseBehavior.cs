using System;
using System.Collections.Generic;
using System.Linq;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace CompanionDefense;

public sealed class CompanionDefenseBehavior : CampaignBehaviorBase
{
    private const float RepeatNotificationCooldownHours = 12f;
    private readonly Dictionary<string, CampaignTime> _recentNotifications = new();

    public override void RegisterEvents()
    {
        CampaignEvents.VillageBeingRaided.AddNonSerializedListener(this, OnVillageBeingRaided);
        CampaignEvents.OnSiegeEventStartedEvent.AddNonSerializedListener(this, OnSiegeEventStarted);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    private void OnVillageBeingRaided(Village village)
    {
        Settlement villageSettlement = village.Settlement;
        if (villageSettlement == null || !villageSettlement.IsVillage)
        {
            return;
        }

        TryOfferDefenseOrder(villageSettlement, "raid");
    }

    private void OnSiegeEventStarted(SiegeEvent siegeEvent)
    {
        Settlement? settlement = siegeEvent?.BesiegedSettlement;
        if (settlement == null || !settlement.IsFortification)
        {
            return;
        }

        TryOfferDefenseOrder(settlement, "siege");
    }

    private void TryOfferDefenseOrder(Settlement target, string threatType)
    {
        if (!IsOwnedByPlayerClan(target) || IsRecentlyNotified(target, threatType))
        {
            return;
        }

        List<MobileParty> eligibleParties = GetEligibleCompanionParties(target);
        if (eligibleParties.Count == 0)
        {
            InformationManager.DisplayMessage(new InformationMessage(
                $"Companion Defense: {target.Name} is under {threatType}, but no companion-led clan party can respond."));
            return;
        }

        MarkNotified(target, threatType);
        ShowCompanionSelection(target, threatType, eligibleParties);
    }

    private static bool IsOwnedByPlayerClan(Settlement settlement)
    {
        Clan playerClan = Clan.PlayerClan;
        if (settlement.OwnerClan == playerClan)
        {
            return true;
        }

        if (settlement.Village?.Bound?.OwnerClan == playerClan)
        {
            return true;
        }

        return false;
    }

    private static List<MobileParty> GetEligibleCompanionParties(Settlement target)
    {
        return Clan.PlayerClan.WarPartyComponents
            .Select(component => component.MobileParty)
            .Where(party => party != null)
            .Where(party => party.LeaderHero != null && party.LeaderHero.IsPlayerCompanion)
            .Where(party => party.IsActive && !party.IsMainParty && party.MapEvent == null)
            .Where(party => party.CurrentSettlement == null || party.CurrentSettlement != target)
            .OrderBy(party => party.GetPosition2D.DistanceSquared(target.GetPosition2D))
            .ToList();
    }

    private void ShowCompanionSelection(Settlement target, string threatType, List<MobileParty> eligibleParties)
    {
        List<InquiryElement> elements = eligibleParties
            .Select(party =>
            {
                string distance = MathF.Sqrt(party.GetPosition2D.DistanceSquared(target.GetPosition2D)).ToString("0.0");
                string text = $"{party.LeaderHero.Name} - {party.MemberRoster.TotalManCount} troops - {distance} away";
                return new InquiryElement(party, text, null);
            })
            .ToList();

        string title = "Companion Defense";
        string text = $"{target.Name} is under {threatType}. Send a message to a companion party to defend it?";
        var inquiry = new MultiSelectionInquiryData(
            title,
            text,
            elements,
            true,
            1,
            1,
            "Send",
            "Ignore",
            selected => OnCompanionSelected(target, threatType, selected),
            _ => { },
            string.Empty,
            false);

        MBInformationManager.ShowMultiSelectionInquiry(inquiry, true, false);
    }

    private static void OnCompanionSelected(Settlement target, string threatType, List<InquiryElement> selected)
    {
        if (selected.Count == 0 || selected[0].Identifier is not MobileParty party)
        {
            return;
        }

        party.SetPartyObjective(MobileParty.PartyObjective.Defensive);
        party.SetMoveDefendSettlement(target, true, MobileParty.NavigationType.Default);
        party.Ai.SetDoNotMakeNewDecisions(true);
        party.Ai.DisableForHours(24);

        InformationManager.DisplayMessage(new InformationMessage(
            $"Message sent: {party.LeaderHero.Name}'s party is heading to defend {target.Name}."));
    }

    private bool IsRecentlyNotified(Settlement target, string threatType)
    {
        string key = GetNotificationKey(target, threatType);
        if (!_recentNotifications.TryGetValue(key, out CampaignTime lastNotification))
        {
            return false;
        }

        return lastNotification.ElapsedHoursUntilNow < RepeatNotificationCooldownHours;
    }

    private void MarkNotified(Settlement target, string threatType)
    {
        _recentNotifications[GetNotificationKey(target, threatType)] = CampaignTime.Now;
    }

    private static string GetNotificationKey(Settlement target, string threatType)
    {
        return $"{target.StringId}:{threatType}";
    }
}
