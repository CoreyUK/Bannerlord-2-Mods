using System;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.Screens;

namespace TroopHealthBars;

internal sealed class TroopHealthBarsMissionView : MissionBehavior
{
    private readonly int[] _initialCounts = new int[3];
    private readonly int[] _aliveCounts = new int[3];
    private GauntletLayer? _layer;
    private GauntletMovieIdentifier? _movie;
    private TroopHealthBarsVM? _dataSource;
    private MissionScreen? _missionScreen;
    private float _refreshTimer;
    private float _attachTimer;
    private bool _attachFailed;

    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    public override void OnRemoveBehavior()
    {
        DetachLayer();
        base.OnRemoveBehavior();
    }

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);

        if (TryGetTrackedCategory(agent, out TroopCategory category))
        {
            _initialCounts[(int)category]++;
        }
    }

    public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow killingBlow)
    {
        base.OnAgentRemoved(affectedAgent, affectorAgent, agentState, killingBlow);
        RefreshCounts();
    }

    public override void OnAgentTeamChanged(Team prevTeam, Team newTeam, Agent agent)
    {
        base.OnAgentTeamChanged(prevTeam, newTeam, agent);
        RefreshCounts();
    }

    public override void OnMissionTick(float dt)
    {
        try
        {
            if (_layer == null && !_attachFailed)
            {
                _attachTimer += dt;
                if (_attachTimer >= 2f)
                {
                    AttachLayer();
                }
            }

            _refreshTimer += dt;
            if (_refreshTimer >= 0.5f)
            {
                _refreshTimer = 0f;
                RefreshCounts();
            }
        }
        catch
        {
            _attachFailed = true;
            DetachLayer();
        }
    }

    private void RefreshCounts()
    {
        RefreshCountsWithoutUi();
    }

    private void RefreshCountsWithoutUi()
    {
        Array.Clear(_aliveCounts, 0, _aliveCounts.Length);

        foreach (Agent agent in Mission.Agents)
        {
            if (agent != null && TryGetTrackedCategory(agent, out TroopCategory category) && IsAlive(agent))
            {
                _aliveCounts[(int)category]++;
            }
        }

        if (_dataSource == null)
        {
            return;
        }

        for (int i = 0; i < _initialCounts.Length; i++)
        {
            _dataSource.SetValues((TroopCategory)i, _aliveCounts[i], _initialCounts[i]);
        }
    }

    private void AttachLayer()
    {
        MissionScreen? screen = GetMissionScreen();
        if (screen == null)
        {
            return;
        }

        _missionScreen = screen;
        _dataSource = new TroopHealthBarsVM();
        _layer = new GauntletLayer("TroopHealthBarsLayer", 180, false);
        _movie = _layer.LoadMovie("TroopHealthBars", _dataSource);
        screen.AddLayer(_layer);
        RefreshCounts();
    }

    private void DetachLayer()
    {
        if (_layer != null)
        {
            try
            {
                if (_movie != null)
                {
                    _layer.ReleaseMovie(_movie);
                }

                _missionScreen?.RemoveLayer(_layer);
            }
            catch
            {
            }
        }

        _movie = null;
        _layer = null;
        _dataSource = null;
        _missionScreen = null;
    }

    private MissionScreen? GetMissionScreen()
    {
        if (_missionScreen != null)
        {
            return _missionScreen;
        }

        foreach (MissionBehavior behavior in Mission.MissionBehaviors)
        {
            if (behavior is MissionView view && view.MissionScreen != null)
            {
                return view.MissionScreen;
            }
        }

        return null;
    }

    private bool TryGetTrackedCategory(Agent agent, out TroopCategory category)
    {
        category = TroopCategory.Infantry;

        if (agent == null || !agent.IsHuman || agent.IsHero || agent.Team == null || !agent.Team.IsPlayerAlly || agent.Character == null)
        {
            return false;
        }

        BasicCharacterObject character = agent.Character;
        FormationClass formationClass = character.DefaultFormationClass;

        if (formationClass == FormationClass.HorseArcher || (character.IsMounted && character.IsRanged))
        {
            category = TroopCategory.Mounted;
            return true;
        }

        if (formationClass == FormationClass.Cavalry || formationClass == FormationClass.LightCavalry || formationClass == FormationClass.HeavyCavalry || character.IsMounted)
        {
            category = TroopCategory.Mounted;
            return true;
        }

        if (formationClass == FormationClass.Ranged || formationClass == FormationClass.Skirmisher || character.IsRanged)
        {
            category = TroopCategory.Archer;
            return true;
        }

        category = TroopCategory.Infantry;
        return true;
    }

    private static bool IsAlive(Agent agent)
    {
        return agent.IsActive() && agent.Health > 0f && !agent.IsRunningAway;
    }
}
