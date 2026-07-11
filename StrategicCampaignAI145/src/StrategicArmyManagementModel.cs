using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace StrategicCampaignAI145;

public sealed class StrategicArmyManagementModel : DefaultArmyManagementCalculationModel
{
    private static readonly TextObject CooldownText = new("{=AEF_COOLDOWN}Recovering from a recent defeat or army dispersal.");
    private static readonly TextObject StrengthText = new("{=AEF_STRENGTH}Party must recover before joining another army.");
    private static readonly TextObject SupplyLineText = new("{=SCAI_SUPPLY_LINES}Overextended supply lines");

    public override bool CanLordCreateArmy(MobileParty mobileParty, out MBList<MobileParty> possibleArmyMembers)
    {
        bool canCreate = base.CanLordCreateArmy(mobileParty, out possibleArmyMembers);
        if (!canCreate)
        {
            return false;
        }

        if (mobileParty.LeaderHero == null ||
            StrategicAiState.IsArmyCreationOnCooldown(mobileParty.LeaderHero) ||
            !StrategicAiHelpers.IsRecoveredEnoughToLeadArmy(mobileParty))
        {
            return false;
        }

        for (int i = possibleArmyMembers.Count - 1; i >= 0; i--)
        {
            MobileParty member = possibleArmyMembers[i];
            if (member.LeaderHero == null ||
                StrategicAiState.IsArmyCreationOnCooldown(member.LeaderHero) ||
                !StrategicAiHelpers.IsRecoveredEnoughToJoinArmy(member))
            {
                possibleArmyMembers.RemoveAt(i);
            }
        }

        return possibleArmyMembers.Count >= StrategicAiTuning.MinimumArmyMemberParties &&
               GetProspectiveArmyStrength(mobileParty, possibleArmyMembers) >= StrategicAiTuning.MinimumProspectiveArmyStrength;
    }

    public override bool CheckPartyEligibility(MobileParty party, out TextObject explanation)
    {
        bool eligible = base.CheckPartyEligibility(party, out explanation);
        if (!eligible)
        {
            return false;
        }

        if (party.LeaderHero != null && StrategicAiState.IsArmyCreationOnCooldown(party.LeaderHero))
        {
            explanation = CooldownText;
            return false;
        }

        if (!StrategicAiHelpers.IsRecoveredEnoughToJoinArmy(party))
        {
            explanation = StrengthText;
            return false;
        }

        return true;
    }

    private static float GetProspectiveArmyStrength(MobileParty leader, MBList<MobileParty> members)
    {
        float strength = leader.Party.EstimatedStrength;
        for (int i = 0; i < members.Count; i++)
        {
            strength += members[i].Party.EstimatedStrength;
        }

        return strength;
    }

    public override ExplainedNumber CalculateDailyCohesionChange(Army army, bool includeDescriptions = false)
    {
        ExplainedNumber result = base.CalculateDailyCohesionChange(army, includeDescriptions);

        if (StrategicAiState.GetEnemyTerritoryDays(army) > StrategicAiTuning.SupplyGraceDays)
        {
            result.Add(StrategicAiTuning.DeepTerritoryCohesionPenalty, includeDescriptions ? SupplyLineText : null);
        }

        return result;
    }
}

