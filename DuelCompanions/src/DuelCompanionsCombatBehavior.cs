using System;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace DuelCompanions;

internal static class DuelCompanionsMissionState
{
    private static bool _nextMissionIsDuel;
    private static bool _nextMissionIsGauntlet;
    private static bool _hasGauntletPlayerHealth;
    private static float _gauntletPlayerHealthRatio = 1f;

    public static void ArmNextDuel(bool isGauntlet)
    {
        _nextMissionIsDuel = true;
        _nextMissionIsGauntlet = isGauntlet;
    }

    public static bool TryConsumeNextDuel(out bool isGauntlet)
    {
        isGauntlet = _nextMissionIsGauntlet;
        if (!_nextMissionIsDuel)
        {
            return false;
        }

        _nextMissionIsDuel = false;
        _nextMissionIsGauntlet = false;
        return true;
    }

    public static void ResetGauntletPlayerHealth()
    {
        _hasGauntletPlayerHealth = false;
        _gauntletPlayerHealthRatio = 1f;
    }

    public static void StoreGauntletPlayerHealth(Agent? player)
    {
        if (player == null || !player.IsHuman || player.HealthLimit <= 0f)
        {
            return;
        }

        _hasGauntletPlayerHealth = true;
        _gauntletPlayerHealthRatio = MBMath.ClampFloat(player.Health / player.HealthLimit, 0.05f, 1f);
    }

    public static void ApplyGauntletPlayerHealthIfNeeded(Agent agent)
    {
        if (!_hasGauntletPlayerHealth || !agent.IsMainAgent || agent.HealthLimit <= 0f)
        {
            return;
        }

        agent.Health = MBMath.ClampFloat(agent.HealthLimit * _gauntletPlayerHealthRatio, 1f, agent.HealthLimit);
    }
}

public sealed class DuelCompanionsCombatBehavior : MissionBehavior
{
    private const float ChampionDamageAgainstPlayerMultiplier = 4f;
    private const float MinimumChampionExtraDamage = 70f;

    private readonly bool _isGauntlet;
    private readonly Random _random = new();
    private Agent? _duelist;
    private float _nextRefreshTime;
    private float _nextTacticalThinkTime;
    private float _nextFeintTime;
    private float _nextBaitFeintTime;
    private float _nextClosePunishTime;
    private float _nextGuardReadTime;
    private float _nextStrafeFlipTime;
    private float _nextPressureBurstTime;
    private float _nextAntiSpamPunishTime;
    private float _nextWhiffPunishTime;
    private float _lastChampionExtraDamageTime = -1f;
    private Agent.ActionCodeType _lastPlayerAction;
    private float _playerReleaseStartTime;
    private float _strafeSide = 1f;

    public DuelCompanionsCombatBehavior(bool isGauntlet)
    {
        _isGauntlet = isGauntlet;
    }

    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);

        if (IsDuelOpponent(agent))
        {
            _duelist = agent;
            ApplyChampionCombatProfile(agent);
        }
        else if (_isGauntlet)
        {
            DuelCompanionsMissionState.ApplyGauntletPlayerHealthIfNeeded(agent);
        }
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);

        if (_duelist == null || !_duelist.IsActive() || !_duelist.IsAIControlled)
        {
            _duelist = FindDuelOpponent();
        }

        if (_duelist == null)
        {
            return;
        }

        float now = Mission.Current.CurrentTime;
        if (now >= _nextRefreshTime)
        {
            ApplyChampionCombatProfile(_duelist);
            _nextRefreshTime = now + 0.05f;
        }

        Agent? player = Agent.Main;
        if (player == null || !player.IsActive())
        {
            return;
        }

        if (_isGauntlet)
        {
            DuelCompanionsMissionState.StoreGauntletPlayerHealth(player);
        }

        float distance = _duelist.Position.Distance(player.Position);
        RunDuelTacticalLayer(_duelist, player, now, distance);

        if (distance < 1.45f)
        {
            ApplyCloseRangePunishProfile(_duelist);
            TryForceCloseRangePunish(_duelist, now);
        }
        else if (distance > 2.35f)
        {
            ApplyPressureProfile(_duelist);
        }

        TryForceFeint(_duelist, player, now, distance);
        ApplyActiveActionSpeedBoost(_duelist);
    }

    public override void OnAgentHit(
        Agent affectedAgent,
        Agent affectorAgent,
        in MissionWeapon affectorWeapon,
        in Blow blow,
        in AttackCollisionData attackCollisionData)
    {
        base.OnAgentHit(affectedAgent, affectorAgent, affectorWeapon, blow, attackCollisionData);

        ApplyChampionExtraDamageToPlayer(affectorAgent, affectedAgent, Math.Max(attackCollisionData.InflictedDamage, blow.InflictedDamage));
    }

    public override void OnMeleeHit(Agent attacker, Agent victim, bool isCanceled, AttackCollisionData attackCollisionData)
    {
        base.OnMeleeHit(attacker, victim, isCanceled, attackCollisionData);

        if (!isCanceled)
        {
            ApplyChampionExtraDamageToPlayer(attacker, victim, attackCollisionData.InflictedDamage);
        }
    }

    protected override void OnEndMission()
    {
        if (_isGauntlet)
        {
            DuelCompanionsMissionState.StoreGauntletPlayerHealth(Agent.Main);
        }

        base.OnEndMission();
    }

    private Agent? FindDuelOpponent()
    {
        foreach (Agent agent in Mission.AllAgents)
        {
            if (IsDuelOpponent(agent))
            {
                return agent;
            }
        }

        return null;
    }

    private void ApplyChampionExtraDamageToPlayer(Agent? attacker, Agent? victim, int inflictedDamage)
    {
        if (attacker == null ||
            victim == null ||
            inflictedDamage <= 0 ||
            !(victim.IsMainAgent || ReferenceEquals(victim, Agent.Main)) ||
            !(ReferenceEquals(attacker, _duelist) || IsDuelOpponent(attacker)))
        {
            return;
        }

        float now = Mission.Current?.CurrentTime ?? 0f;
        if (now - _lastChampionExtraDamageTime < 0.03f)
        {
            return;
        }

        float extraDamage = Math.Max(inflictedDamage * (ChampionDamageAgainstPlayerMultiplier - 1f), MinimumChampionExtraDamage);
        victim.Health = Math.Max(1f, victim.Health - extraDamage);
        _lastChampionExtraDamageTime = now;
    }

    private static bool IsDuelOpponent(Agent agent)
    {
        if (agent == null || !agent.IsHuman || !agent.IsAIControlled || !agent.IsActive())
        {
            return false;
        }

        BasicCharacterObject? character = agent.Character;
        if (character == null)
        {
            return false;
        }

        string id = character.StringId ?? string.Empty;
        return id.StartsWith("dc_heavy_", StringComparison.Ordinal) ||
               id.StartsWith("dc_event_hero_", StringComparison.Ordinal) ||
               id.StartsWith("dc_gauntlet_", StringComparison.Ordinal);
    }

    private void ApplyChampionCombatProfile(Agent agent)
    {
        float intensity = _isGauntlet ? 1.2f : 1.45f;

        ApplyLegendaryDifficultyOverride(agent);

        Set(agent, DrivenProperty.AIBlockOnDecideAbility, 1f);
        Set(agent, DrivenProperty.AIParryOnDecideAbility, 1f);
        Set(agent, DrivenProperty.AIParryOnAttackAbility, 1f);
        Set(agent, DrivenProperty.AIParryOnAttackingContinueAbility, 1f);
        Set(agent, DrivenProperty.AIRealizeBlockingFromIncorrectSideAbility, 1f);
        Set(agent, DrivenProperty.AIDecideOnRealizeEnemyBlockingAttackAbility, 0.95f);
        Set(agent, DrivenProperty.AIEstimateStunDurationPrecision, 0.95f);

        Set(agent, DrivenProperty.AIAttackOnDecideChance, 1f);
        Set(agent, DrivenProperty.AIDecideOnAttackChance, 1f);
        Set(agent, DrivenProperty.AIAttackOnParryChance, 1f);
        Set(agent, DrivenProperty.AiAttackOnParryTiming, 0.02f);
        Set(agent, DrivenProperty.AiTryChamberAttackOnDecide, 0.72f);
        Set(agent, DrivenProperty.AiDecideOnAttackContinueAction, 1f);
        Set(agent, DrivenProperty.AiDecideOnAttackingContinue, 1f);
        Set(agent, DrivenProperty.AiAttackCalculationMaxTimeFactor, 0.18f);

        Set(agent, DrivenProperty.AIHoldingReadyMaxDuration, 0.55f);
        Set(agent, DrivenProperty.AIHoldingReadyVariationPercentage, 0.95f);
        Set(agent, DrivenProperty.AiRandomizedDefendDirectionChance, 0.02f);

        Set(agent, DrivenProperty.AiMovementDelayFactor, 0f);
        Set(agent, DrivenProperty.AiCheckApplyMovementInterval, 0.01f);
        Set(agent, DrivenProperty.AiCheckCalculateMovementInterval, 0.01f);
        Set(agent, DrivenProperty.AiCheckDecideSimpleBehaviorInterval, 0.01f);
        Set(agent, DrivenProperty.AiCheckDoSimpleBehaviorInterval, 0.01f);
        Set(agent, DrivenProperty.AiMinimumDistanceToContinueFactor, 1.9f);
        Set(agent, DrivenProperty.AiMoveEnemySideTimeValue, 2.1f);

        Set(agent, DrivenProperty.AiKick, 1f);
        Set(agent, DrivenProperty.KickStunDurationMultiplier, 2.15f);
        Set(agent, DrivenProperty.ShieldBashStunDurationMultiplier, 2.15f);
        Set(agent, DrivenProperty.AiDefendWithShieldDecisionChanceValue, 0.55f);
        Set(agent, DrivenProperty.AiAttackingShieldDefenseChance, 0.42f);
        Set(agent, DrivenProperty.AiAttackingShieldDefenseTimer, 0.16f);

        ApplyDuelOnlyChampionStatBuffs(agent, intensity);
    }

    private static void ApplyLegendaryDifficultyOverride(Agent agent)
    {
        Set(agent, DrivenProperty.AIBlockOnDecideAbility, 1f);
        Set(agent, DrivenProperty.AIParryOnDecideAbility, 1f);
        Set(agent, DrivenProperty.AIParryOnAttackAbility, 1f);
        Set(agent, DrivenProperty.AIParryOnAttackingContinueAbility, 1f);
        Set(agent, DrivenProperty.AIRealizeBlockingFromIncorrectSideAbility, 1f);
        Set(agent, DrivenProperty.AIDecideOnRealizeEnemyBlockingAttackAbility, 1f);
        Set(agent, DrivenProperty.AIEstimateStunDurationPrecision, 1f);
        Set(agent, DrivenProperty.AISetNoAttackTimerAfterBeingHitAbility, 1f);
        Set(agent, DrivenProperty.AISetNoAttackTimerAfterBeingParriedAbility, 1f);
        Set(agent, DrivenProperty.AISetNoDefendTimerAfterHittingAbility, 1f);
        Set(agent, DrivenProperty.AISetNoDefendTimerAfterParryingAbility, 1f);
        Set(agent, DrivenProperty.AiAttackCalculationMaxTimeFactor, 0.25f);
        Set(agent, DrivenProperty.AiCheckApplyMovementInterval, 0.01f);
        Set(agent, DrivenProperty.AiCheckCalculateMovementInterval, 0.01f);
        Set(agent, DrivenProperty.AiCheckDecideSimpleBehaviorInterval, 0.01f);
        Set(agent, DrivenProperty.AiCheckDoSimpleBehaviorInterval, 0.01f);
        Set(agent, DrivenProperty.AiMovementDelayFactor, 0f);
        Set(agent, DrivenProperty.AiParryDecisionChangeValue, 0.95f);
        Set(agent, DrivenProperty.AiRaiseShieldDelayTimeBase, 0f);
        Set(agent, DrivenProperty.AiRandomizedDefendDirectionChance, 0f);
        Set(agent, DrivenProperty.UseRealisticBlocking, 1f);
    }

    private static void ApplyDuelOnlyChampionStatBuffs(Agent agent, float intensity)
    {
        Set(agent, DrivenProperty.SwingSpeedMultiplier, 4.1f * intensity);
        Set(agent, DrivenProperty.ThrustOrRangedReadySpeedMultiplier, 4.1f * intensity);
        Set(agent, DrivenProperty.HandlingMultiplier, 5.4f);
        Set(agent, DrivenProperty.CombatMaxSpeedMultiplier, 3f);
        Set(agent, DrivenProperty.MaxSpeedMultiplier, 2.35f);
        Set(agent, DrivenProperty.MeleeWeaponDamageMultiplierBonus, 2.15f * intensity);
        Set(agent, DrivenProperty.AiAttackCalculationMaxTimeFactor, 0.05f);
        Set(agent, DrivenProperty.AIHoldingReadyMaxDuration, 0.35f);
        ApplyMeleeBehaviorPressure(agent);
    }

    private static void ApplyMeleeBehaviorPressure(Agent agent)
    {
        agent.SetAIBehaviorParams(HumanAIComponent.AISimpleBehaviorKind.Melee, 1000f, 1000f, 0f, 0f, 0f);
        agent.HumanAIComponent?.OverrideBehaviorParams(HumanAIComponent.AISimpleBehaviorKind.Melee, 1000f, 1000f, 0f, 0f, 0f);
    }

    private static void ApplyCloseRangePunishProfile(Agent agent)
    {
        Set(agent, DrivenProperty.AiKick, 1f);
        Set(agent, DrivenProperty.AIAttackOnDecideChance, 1f);
        Set(agent, DrivenProperty.AIDecideOnAttackChance, 1f);
        Set(agent, DrivenProperty.AIAttackOnParryChance, 1f);
        Set(agent, DrivenProperty.AiAttackOnParryTiming, 0f);
        Set(agent, DrivenProperty.AiMinimumDistanceToContinueFactor, 2.55f);
        Set(agent, DrivenProperty.AiMoveEnemySideTimeValue, 2.4f);
        Set(agent, DrivenProperty.AiDefendWithShieldDecisionChanceValue, 0.35f);
        Set(agent, DrivenProperty.AiAttackingShieldDefenseChance, 0.25f);
        Set(agent, DrivenProperty.AiAttackingShieldDefenseTimer, 0.08f);
        Set(agent, DrivenProperty.KickStunDurationMultiplier, 2.4f);
        Set(agent, DrivenProperty.ShieldBashStunDurationMultiplier, 2.4f);
        ApplyMeleeBehaviorPressure(agent);
        Set(agent, DrivenProperty.SwingSpeedMultiplier, 5.4f);
        Set(agent, DrivenProperty.ThrustOrRangedReadySpeedMultiplier, 5.4f);
        Set(agent, DrivenProperty.HandlingMultiplier, 5.8f);
        Set(agent, DrivenProperty.CombatMaxSpeedMultiplier, 3.25f);
        Set(agent, DrivenProperty.MaxSpeedMultiplier, 2.55f);
        Set(agent, DrivenProperty.MeleeWeaponDamageMultiplierBonus, 2.65f);
    }

    private static void ApplyPressureProfile(Agent agent)
    {
        Set(agent, DrivenProperty.AIAttackOnDecideChance, 1f);
        Set(agent, DrivenProperty.AIDecideOnAttackChance, 1f);
        Set(agent, DrivenProperty.AiDecideOnAttackContinueAction, 1f);
        Set(agent, DrivenProperty.AiDecideOnAttackingContinue, 1f);
        Set(agent, DrivenProperty.AiMinimumDistanceToContinueFactor, 1.85f);
        Set(agent, DrivenProperty.AiMovementDelayFactor, 0f);
        Set(agent, DrivenProperty.AiDefendWithShieldDecisionChanceValue, 0.35f);
        Set(agent, DrivenProperty.AiAttackingShieldDefenseChance, 0.22f);
        ApplyMeleeBehaviorPressure(agent);
        Set(agent, DrivenProperty.SwingSpeedMultiplier, 5f);
        Set(agent, DrivenProperty.ThrustOrRangedReadySpeedMultiplier, 5f);
        Set(agent, DrivenProperty.HandlingMultiplier, 5.65f);
        Set(agent, DrivenProperty.CombatMaxSpeedMultiplier, 3.15f);
        Set(agent, DrivenProperty.MaxSpeedMultiplier, 2.5f);
        Set(agent, DrivenProperty.MeleeWeaponDamageMultiplierBonus, 2.35f);
    }

    private static void Set(Agent agent, DrivenProperty property, float value)
    {
        agent.SetAgentDrivenPropertyValueFromConsole(property, value);
    }

    private void RunDuelTacticalLayer(Agent agent, Agent player, float now, float distance)
    {
        if (now < _nextTacticalThinkTime)
        {
            return;
        }

        _nextTacticalThinkTime = now + 0.012f;

        Agent.ActionCodeType playerAction = player.GetCurrentActionType(1);
        TrackPlayerAttackWindow(playerAction, now);

        if (now >= _nextStrafeFlipTime)
        {
            _strafeSide = _random.Next(2) == 0 ? -1f : 1f;
            _nextStrafeFlipTime = now + 0.1f + ((float)_random.NextDouble() * 0.18f);
        }

        ApplyMeleeBehaviorPressure(agent);
        ApplyActiveActionSpeedBoost(agent);

        Vec2 movement = BuildDuelMovement(agent, player, distance, playerAction, _strafeSide);
        agent.SetMovementDirection(movement);
        agent.MovementInputVector = movement;

        if (IsPlayerThreateningAttack(playerAction) && now >= _nextGuardReadTime)
        {
            agent.SetWeaponGuard(GetCounterGuard(player.GetAttackDirection()));
            agent.ForceAiBehaviorSelection();
            _nextGuardReadTime = now + 0.035f;

            if (distance < 1.22f && now >= _nextAntiSpamPunishTime)
            {
                ApplyCloseRangePunishProfile(agent);
                ForceCounterAttack(agent, player);
                _nextAntiSpamPunishTime = now + 0.1f;
            }
        }
        else if (IsPlayerDefending(playerAction))
        {
            ApplyPressureProfile(agent);
            if (now >= _nextPressureBurstTime)
            {
                agent.SetWeaponGuard(PickDifferentGuard(player.GetCurrentActionDirection(1)));
                agent.ForceAiBehaviorSelection();
                agent.InvalidateAIWeaponSelections();
                _nextPressureBurstTime = now + 0.055f;
            }
        }
        else if (WasPlayerAttackLikelyWhiffed(playerAction, now, distance) && now >= _nextWhiffPunishTime)
        {
            ApplyPressureProfile(agent);
            ForceCounterAttack(agent, player);
            _nextWhiffPunishTime = now + 0.12f;
        }
        else if (distance > 1.8f && now >= _nextPressureBurstTime)
        {
            ApplyPressureProfile(agent);
            agent.ForceAiBehaviorSelection();
            _nextPressureBurstTime = now + 0.1f;
        }
    }

    private static Vec2 BuildDuelMovement(Agent agent, Agent player, float distance, Agent.ActionCodeType playerAction, float strafeSide)
    {
        Vec2 toPlayer = new(player.Position.x - agent.Position.x, player.Position.y - agent.Position.y);
        float length = toPlayer.Normalize();
        if (length < 0.001f)
        {
            return Vec2.Zero;
        }

        Vec2 strafe = new(-toPlayer.y, toPlayer.x);
        float forwardPressure;
        float strafeWeight;

        if (IsPlayerDefending(playerAction))
        {
            forwardPressure = distance > 0.62f ? 1.95f : 0.35f;
            strafeWeight = 0.18f;
        }
        else if (IsPlayerThreateningAttack(playerAction))
        {
            forwardPressure = distance > 1.75f ? 0.85f : -1.05f;
            strafeWeight = 1.55f;
        }
        else
        {
            forwardPressure = distance > 0.95f ? 1.75f : -0.1f;
            strafeWeight = 0.85f;
        }

        Vec2 movement = (toPlayer * forwardPressure) + (strafe * strafeWeight * strafeSide);
        movement.Normalize();
        return movement;
    }

    private void TrackPlayerAttackWindow(Agent.ActionCodeType playerAction, float now)
    {
        if (playerAction == Agent.ActionCodeType.ReleaseMelee && _lastPlayerAction != Agent.ActionCodeType.ReleaseMelee)
        {
            _playerReleaseStartTime = now;
        }

        _lastPlayerAction = playerAction;
    }

    private bool WasPlayerAttackLikelyWhiffed(Agent.ActionCodeType playerAction, float now, float distance)
    {
        return _lastPlayerAction == Agent.ActionCodeType.ReleaseMelee &&
               playerAction != Agent.ActionCodeType.ReleaseMelee &&
               now - _playerReleaseStartTime > 0.12f &&
               distance > 1.15f;
    }

    private void ForceCounterAttack(Agent agent, Agent player)
    {
        Agent.UsageDirection attack = PickAttackAroundGuard(player.GetCurrentActionDirection(1));
        agent.SetWeaponGuard(attack);
        agent.SetCurrentActionProgress(1, 0f);
        agent.SetCurrentActionSpeed(0, 5.8f);
        agent.SetCurrentActionSpeed(1, 5.8f);
        agent.ForceAiBehaviorSelection();
        agent.InvalidateAIWeaponSelections();
    }

    private static bool IsPlayerThreateningAttack(Agent.ActionCodeType action)
    {
        return action == Agent.ActionCodeType.ReadyMelee ||
               action == Agent.ActionCodeType.ReleaseMelee;
    }

    private static bool IsPlayerDefending(Agent.ActionCodeType action)
    {
        return action == Agent.ActionCodeType.DefendShield ||
               action == Agent.ActionCodeType.DefendForward1h ||
               action == Agent.ActionCodeType.DefendUp1h ||
               action == Agent.ActionCodeType.DefendRight1h ||
               action == Agent.ActionCodeType.DefendLeft1h ||
               action == Agent.ActionCodeType.DefendForward2h ||
               action == Agent.ActionCodeType.DefendUp2h ||
               action == Agent.ActionCodeType.DefendRight2h ||
               action == Agent.ActionCodeType.DefendLeft2h ||
               action == Agent.ActionCodeType.DefendForwardStaff ||
               action == Agent.ActionCodeType.DefendUpStaff ||
               action == Agent.ActionCodeType.DefendRightStaff ||
               action == Agent.ActionCodeType.DefendLeftStaff;
    }

    private static Agent.UsageDirection GetCounterGuard(Agent.UsageDirection attackDirection)
    {
        return attackDirection switch
        {
            Agent.UsageDirection.AttackUp => Agent.UsageDirection.DefendUp,
            Agent.UsageDirection.AttackDown => Agent.UsageDirection.DefendDown,
            Agent.UsageDirection.AttackLeft => Agent.UsageDirection.DefendLeft,
            Agent.UsageDirection.AttackRight => Agent.UsageDirection.DefendRight,
            _ => Agent.UsageDirection.DefendAny
        };
    }

    private void TryForceFeint(Agent agent, Agent player, float now, float distance)
    {
        if (distance > 2.65f)
        {
            return;
        }

        Agent.ActionCodeType actionType = agent.GetCurrentActionType(1);
        Agent.ActionStage actionStage = agent.GetCurrentActionStage(1);
        Agent.ActionCodeType playerAction = player.GetCurrentActionType(1);

        if (IsPlayerDefending(playerAction))
        {
            TryBaitBlockWithFeint(agent, player, now, actionType, actionStage);
        }

        if (now < _nextFeintTime)
        {
            return;
        }

        if (actionType != Agent.ActionCodeType.ReadyMelee ||
            (actionStage != Agent.ActionStage.AttackReady && actionStage != Agent.ActionStage.AttackQuickReady))
        {
            return;
        }

        float progress = agent.GetCurrentActionProgress(1);
        if (progress < 0.08f || progress > 0.82f)
        {
            return;
        }

        float chance = IsPlayerDefending(playerAction) ? 0.98f : (_isGauntlet ? 0.68f : 0.82f);
        if (_random.NextDouble() > chance)
        {
            _nextFeintTime = now + 0.08f;
            return;
        }

        Agent.UsageDirection nextGuard = IsPlayerDefending(playerAction)
            ? PickAttackAroundGuard(player.GetCurrentActionDirection(1))
            : PickDifferentGuard(agent.GetAttackDirection());

        agent.SetWeaponGuard(nextGuard);
        agent.SetCurrentActionProgress(1, 0f);
        agent.SetCurrentActionSpeed(1, 5.2f);
        agent.ForceAiBehaviorSelection();
        agent.InvalidateAIWeaponSelections();
        _nextFeintTime = now + (_isGauntlet ? 0.18f : 0.12f);
    }

    private void TryBaitBlockWithFeint(
        Agent agent,
        Agent player,
        float now,
        Agent.ActionCodeType actionType,
        Agent.ActionStage actionStage)
    {
        if (now < _nextBaitFeintTime ||
            actionType != Agent.ActionCodeType.ReadyMelee ||
            (actionStage != Agent.ActionStage.AttackReady && actionStage != Agent.ActionStage.AttackQuickReady))
        {
            return;
        }

        float progress = agent.GetCurrentActionProgress(1);
        if (progress < 0.12f || progress > 0.55f)
        {
            return;
        }

        agent.SetWeaponGuard(PickAttackAroundGuard(player.GetCurrentActionDirection(1)));
        agent.SetCurrentActionProgress(1, 0f);
        agent.SetCurrentActionSpeed(1, 5.5f);
        agent.ForceAiBehaviorSelection();
        agent.InvalidateAIWeaponSelections();
        _nextBaitFeintTime = now + 0.09f;
        _nextFeintTime = now + 0.06f;
    }

    private void TryForceCloseRangePunish(Agent agent, float now)
    {
        if (now < _nextClosePunishTime)
        {
            return;
        }

        agent.ForceAiBehaviorSelection();
        agent.InvalidateAIWeaponSelections();
        _nextClosePunishTime = now + 0.45f;
    }

    private static void ApplyActiveActionSpeedBoost(Agent agent)
    {
        Agent.ActionCodeType actionType = agent.GetCurrentActionType(1);
        if (actionType == Agent.ActionCodeType.ReadyMelee ||
            actionType == Agent.ActionCodeType.ReleaseMelee ||
            actionType == Agent.ActionCodeType.DefendShield ||
            actionType == Agent.ActionCodeType.DefendForward1h ||
            actionType == Agent.ActionCodeType.DefendUp1h ||
            actionType == Agent.ActionCodeType.DefendRight1h ||
            actionType == Agent.ActionCodeType.DefendLeft1h ||
            actionType == Agent.ActionCodeType.DefendForward2h ||
            actionType == Agent.ActionCodeType.DefendUp2h ||
            actionType == Agent.ActionCodeType.DefendRight2h ||
            actionType == Agent.ActionCodeType.DefendLeft2h)
        {
            agent.SetCurrentActionSpeed(0, 5.2f);
            agent.SetCurrentActionSpeed(1, 5.2f);
        }
    }

    private Agent.UsageDirection PickDifferentGuard(Agent.UsageDirection attackDirection)
    {
        Agent.UsageDirection[] guards =
        {
            Agent.UsageDirection.DefendUp,
            Agent.UsageDirection.DefendDown,
            Agent.UsageDirection.DefendLeft,
            Agent.UsageDirection.DefendRight
        };

        Agent.UsageDirection guard = guards[_random.Next(guards.Length)];
        if ((attackDirection == Agent.UsageDirection.AttackUp && guard == Agent.UsageDirection.DefendUp) ||
            (attackDirection == Agent.UsageDirection.AttackDown && guard == Agent.UsageDirection.DefendDown) ||
            (attackDirection == Agent.UsageDirection.AttackLeft && guard == Agent.UsageDirection.DefendLeft) ||
            (attackDirection == Agent.UsageDirection.AttackRight && guard == Agent.UsageDirection.DefendRight))
        {
            guard = guards[(Array.IndexOf(guards, guard) + 1) % guards.Length];
        }

        return guard;
    }

    private Agent.UsageDirection PickAttackAroundGuard(Agent.UsageDirection guardDirection)
    {
        Agent.UsageDirection[] attacks =
        {
            Agent.UsageDirection.AttackUp,
            Agent.UsageDirection.AttackDown,
            Agent.UsageDirection.AttackLeft,
            Agent.UsageDirection.AttackRight
        };

        Agent.UsageDirection attack = attacks[_random.Next(attacks.Length)];
        if ((guardDirection == Agent.UsageDirection.DefendUp && attack == Agent.UsageDirection.AttackUp) ||
            (guardDirection == Agent.UsageDirection.DefendDown && attack == Agent.UsageDirection.AttackDown) ||
            (guardDirection == Agent.UsageDirection.DefendLeft && attack == Agent.UsageDirection.AttackLeft) ||
            (guardDirection == Agent.UsageDirection.DefendRight && attack == Agent.UsageDirection.AttackRight))
        {
            attack = attacks[(Array.IndexOf(attacks, attack) + 1 + _random.Next(2)) % attacks.Length];
        }

        return attack;
    }
}
