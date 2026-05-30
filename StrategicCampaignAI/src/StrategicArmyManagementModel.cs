using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.GameComponents;
using TaleWorlds.Localization;

namespace StrategicCampaignAI;

public sealed class StrategicArmyManagementModel : DefaultArmyManagementCalculationModel
{
    private static readonly TextObject SupplyLineText = new("{=SCAI_SUPPLY_LINES}Overextended supply lines");

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
