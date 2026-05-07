using System.Collections.Generic;
using System.Linq;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace CompanionHotswap;

public sealed class CompanionSwapBehavior : MissionBehavior
{
    private Agent? _playerHeroAgent;

    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        if (_playerHeroAgent == null && agent.IsMainAgent)
        {
            _playerHeroAgent = agent;
        }
    }

    public override void OnMissionTick(float dt)
    {
        if (_playerHeroAgent == null && Mission.Current?.MainAgent != null)
        {
            _playerHeroAgent = Mission.Current.MainAgent;
        }

        Agent? mainAgent = Mission.Current?.MainAgent;
        if (IsPlayerControlledHeroOnPlayerTeam(mainAgent))
        {
            ApplyPlayerCommandAuthority(mainAgent!, forceManualControl: false);
        }
    }

    public override void OnAgentControllerSetToPlayer(Agent agent)
    {
        base.OnAgentControllerSetToPlayer(agent);

        if (IsPlayerControlledHeroOnPlayerTeam(agent))
        {
            ApplyPlayerCommandAuthority(agent, forceManualControl: false);
        }
    }

    public void SwapToAgent(Agent agent)
    {
        Mission? mission = Mission.Current;
        if (mission == null || agent == null || !agent.IsActive() || agent == mission.MainAgent)
        {
            return;
        }

        Agent? previous = mission.MainAgent;
        if (previous != null)
        {
            previous.Controller = AgentControllerType.AI;
        }

        mission.SetPlayerCanTakeControlOfAnotherAgentWhenDead();
        agent.Controller = AgentControllerType.Player;
        ApplyPlayerCommandAuthority(agent, forceManualControl: true);
    }

    public List<Agent> GetCompanionAgents()
    {
        List<Agent> agents = new();
        Mission? mission = Mission.Current;
        Team? playerTeam = mission?.PlayerTeam;
        if (playerTeam == null)
        {
            return agents;
        }

        if (_playerHeroAgent != null && _playerHeroAgent.IsActive())
        {
            agents.Add(_playerHeroAgent);
        }

        agents.AddRange(playerTeam.ActiveAgents
            .Where(agent => agent.IsHero && agent != _playerHeroAgent && agent.IsActive())
            .OrderBy(agent => agent.Name));

        return agents;
    }

    private static bool IsPlayerControlledHeroOnPlayerTeam(Agent? agent)
    {
        Mission? mission = Mission.Current;
        return mission?.PlayerTeam != null
            && agent != null
            && agent.IsHero
            && agent.IsActive()
            && agent.Team == mission.PlayerTeam;
    }

    private static void ApplyPlayerCommandAuthority(Agent agent, bool forceManualControl)
    {
        Team? team = agent.Team;
        if (team == null)
        {
            return;
        }

        if (!team.IsPlayerGeneral || team.IsPlayerSergeant)
        {
            team.SetPlayerRole(isPlayerGeneral: true, isPlayerSergeant: false);
        }

        team.GeneralAgent = agent;
        team.PlayerOrderController.Owner = agent;

        foreach (Formation formation in team.FormationsIncludingSpecialAndEmpty)
        {
            if (formation == null)
            {
                continue;
            }

            if (formation.PlayerOwner != agent)
            {
                formation.PlayerOwner = agent;
            }

            if (forceManualControl)
            {
                formation.SetControlledByAI(false, false);
            }
        }
    }
}
