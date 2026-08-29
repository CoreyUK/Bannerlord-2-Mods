using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace StrategicCampaignAI145;

public sealed class StrategicArmyManagementModel : DefaultArmyManagementCalculationModel
{
    private static readonly TextObject CooldownText = new("{=SCAI_COOLDOWN}Recovering from a recent defeat or army dispersal.");
    private static readonly TextObject StrengthText = new("{=SCAI_STRENGTH}Party must recover before joining another army.");
    private static readonly TextObject SupplyLineText = new("{=SCAI_SUPPLY_LINES}Overextended supply lines");

    public override bool CanLordCreateArmy(MobileParty mobileParty, out MBList<MobileParty> possibleArmyMembers)
    {
        if (!base.CanLordCreateArmy(mobileParty, out possibleArmyMembers))
        {
            return false;
        }

        // The player decides for themselves whether to raise an army.
        if (StrategicAiHelpers.IsPlayerControlled(mobileParty))
        {
            return true;
        }

        try
        {
            if (mobileParty.LeaderHero == null ||
                StrategicAiState.IsArmyCreationOnCooldown(mobileParty.LeaderHero) ||
                !StrategicAiHelpers.IsRecoveredEnoughToLeadArmy(mobileParty))
            {
                return false;
            }

            if (mobileParty.MapFaction is Kingdom kingdom &&
                kingdom.Armies != null &&
                kingdom.Armies.Count >= StrategicAiHelpers.GetAllowedArmyCount(kingdom))
            {
                return false;
            }

            if (possibleArmyMembers == null)
            {
                return false;
            }

            for (int i = possibleArmyMembers.Count - 1; i >= 0; i--)
            {
                MobileParty member = possibleArmyMembers[i];
                if (member == null ||
                    member.LeaderHero == null ||
                    StrategicAiState.IsArmyCreationOnCooldown(member.LeaderHero) ||
                    !StrategicAiHelpers.IsRecoveredEnoughToJoinArmy(member))
                {
                    possibleArmyMembers.RemoveAt(i);
                }
            }

            return possibleArmyMembers.Count >= StrategicAiTuning.MinimumArmyMemberParties &&
                   GetProspectiveArmyStrength(mobileParty, possibleArmyMembers) >= StrategicAiTuning.MinimumProspectiveArmyStrength;
        }
        catch (Exception)
        {
            // If our gating throws, defer to vanilla rather than blocking armies.
            return true;
        }
    }

    public override bool CheckPartyEligibility(MobileParty party, out TextObject explanation)
    {
        if (!base.CheckPartyEligibility(party, out explanation))
        {
            return false;
        }

        if (StrategicAiHelpers.IsPlayerControlled(party))
        {
            return true;
        }

        try
        {
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
        }
        catch (Exception)
        {
            return true;
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

        try
        {
            if (StrategicAiState.GetEnemyTerritoryDays(army) > StrategicAiTuning.SupplyGraceDays)
            {
                result.Add(StrategicAiTuning.DeepTerritoryCohesionPenalty, includeDescriptions ? SupplyLineText : null);
            }
        }
        catch (Exception)
        {
            // Keep the vanilla cohesion result.
        }

        return result;
    }
}
