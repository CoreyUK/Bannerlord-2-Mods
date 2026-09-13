using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace ManorLord;

/// <summary>Hands the estate state from the campaign to the next mission that opens, and carries the defense result back.</summary>
internal static class ManorDefenseMissionState
{
    private static bool _armed;
    private static bool _defense;
    private static bool? _result;
    private static EstateSnapshot? _estate;

    public static void ArmDefense(EstateSnapshot estate)
    {
        _armed = true;
        _defense = true;
        _result = null;
        _estate = estate;
    }

    public static void ArmEstateVisit(EstateSnapshot estate)
    {
        _armed = true;
        _defense = false;
        _result = null;
        _estate = estate;
    }

    public static bool TryConsume(out bool defense, out EstateSnapshot estate)
    {
        defense = _defense;
        estate = _estate ?? new EstateSnapshot();
        if (!_armed) return false;
        _armed = false;
        _estate = null;
        return true;
    }

    public static void CancelArm()
    {
        _armed = false;
        _estate = null;
    }

    public static void Complete(bool victory) => _result = victory;

    public static bool TryTakeResult(out bool victory)
    {
        if (!_result.HasValue) { victory = false; return false; }
        victory = _result.Value;
        _result = null;
        return true;
    }
}

/// <summary>Decides the outcome of a playable manor defense: victory once every attacker is down while the player still stands.</summary>
public sealed class ManorDefenseMissionBehavior : MissionBehavior
{
    private bool _playerDefeated;
    private bool _enemySeen;
    private bool _enemyRemaining;

    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        _enemyRemaining = false;
        foreach (Agent agent in Mission.AllAgents)
        {
            if (!agent.IsHuman || !agent.IsActive()) continue;
            if (agent.IsMainAgent) _playerDefeated = false;
            if (agent.Team?.Side == BattleSideEnum.Attacker)
            {
                _enemySeen = true;
                _enemyRemaining = true;
            }
        }
        Agent? player = Agent.Main;
        if (player == null || !player.IsActive()) _playerDefeated = true;
    }

    protected override void OnEndMission()
    {
        ManorDefenseMissionState.Complete(_enemySeen && !_enemyRemaining && !_playerDefeated);
        base.OnEndMission();
    }
}
