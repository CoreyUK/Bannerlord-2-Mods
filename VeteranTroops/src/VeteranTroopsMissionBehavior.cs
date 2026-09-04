using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace VeteranTroops;

/// <summary>
/// Applies a stack's veterancy to its soldiers as they spawn. Effects are
/// deliberately defensive (more hit points, higher starting morale) so the mod
/// changes how long veterans stand rather than how hard they hit.
/// </summary>
public sealed class VeteranTroopsMissionBehavior : MissionBehavior
{
    private readonly Dictionary<string, VeteranTier> _tierCache = new();
    private VeteranTroopsBehavior? _behavior;
    private VeteranTroopsSettings _settings = new();

    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    public override void AfterStart()
    {
        base.AfterStart();
        _behavior = VeteranTroopsBehavior.Instance;
        _settings = _behavior?.Settings ?? VeteranTroopsSettings.Effective;
    }

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);

        if (_behavior == null || agent == null || !agent.IsHuman || agent.IsMainAgent)
        {
            return;
        }

        if (agent.Character is not CharacterObject character || character.IsHero)
        {
            return;
        }

        // Only troops fielded by the player's own party carry memory in this version.
        if (agent.Origin?.BattleCombatant is not PartyBase party || party != PartyBase.MainParty)
        {
            return;
        }

        VeteranTier tier = GetTier(character);
        int steps = (int)tier;
        if (steps <= 0)
        {
            return;
        }

        float healthMultiplier = 1f + (steps * _settings.HealthBonusPercentPerTier / 100f);
        if (healthMultiplier > 1f)
        {
            agent.BaseHealthLimit *= healthMultiplier;
            agent.HealthLimit *= healthMultiplier;
            agent.Health *= healthMultiplier;
        }

        float moraleBonus = steps * _settings.MoraleBonusPerTier;
        if (moraleBonus > 0f)
        {
            agent.ChangeMorale(moraleBonus);
        }
    }

    private VeteranTier GetTier(CharacterObject character)
    {
        if (_tierCache.TryGetValue(character.StringId, out VeteranTier cached))
        {
            return cached;
        }

        VeteranTier tier = _behavior!.GetTier(character);
        _tierCache[character.StringId] = tier;
        return tier;
    }
}
