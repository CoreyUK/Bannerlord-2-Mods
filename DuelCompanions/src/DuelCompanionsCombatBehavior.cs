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

    /// <summary>The carried-over health for saving: the ratio, or a negative value when there is none.</summary>
    public static float SavedGauntletPlayerHealthRatio
    {
        get => _hasGauntletPlayerHealth ? _gauntletPlayerHealthRatio : -1f;
        set
        {
            _hasGauntletPlayerHealth = value > 0f;
            _gauntletPlayerHealthRatio = _hasGauntletPlayerHealth ? MBMath.ClampFloat(value, 0.05f, 1f) : 1f;
        }
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
    // The original tuning, now the Legendary end of the scale. Every boost below is interpolated from an ordinary
    // fighter (power 0) to these values (power 1); see DuelSettings.ChampionPower.
    private const float ChampionDamageAgainstPlayerMultiplier = 4f;
    private const float MinimumChampionExtraDamage = 70f;

    private readonly bool _isGauntlet;
    private readonly float _power;
    private readonly float _damagePower; // power squared: damage is what made the champion unfair, so it falls off fastest
    private readonly bool _smartAi;
    private readonly bool _tactics;
    private readonly float _feintFactor;
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
    private bool _gauntletHealthApplied;

    public DuelCompanionsCombatBehavior(bool isGauntlet)
    {
        _isGauntlet = isGauntlet;
        DuelSettings settings = DuelSettings.Current;
        _power = settings.ChampionPower(isGauntlet);
        _damagePower = _power * _power;
        _smartAi = settings.SmartAi;
        _tactics = settings.DuelTactics;
        _feintFactor = settings.FeintFactor;
    }

    /// <summary>A multiplier whose ordinary value is 1, scaled toward its Legendary value by the champion's power.</summary>
    private float Mul(float legendary) => 1f + (legendary - 1f) * _power;

    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        base.OnAgentBuild(agent, banner);

        if (IsDuelOpponent(agent))
        {
            _duelist = agent;
            ApplyChampionCombatProfile(agent);
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
            // Restore the previous round's wounds once the player's agent exists, before recording this round's health.
            if (!_gauntletHealthApplied)
            {
                DuelCompanionsMissionState.ApplyGauntletPlayerHealthIfNeeded(player);
                _gauntletHealthApplied = true;
            }

            DuelCompanionsMissionState.StoreGauntletPlayerHealth(player);
        }

        float distance = _duelist.Position.Distance(player.Position);
        if (_tactics)
        {
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
        }

        if (_feintFactor > 0f)
        {
            TryForceFeint(_duelist, player, now, distance);
        }

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

        float extraDamage = Math.Max(
            inflictedDamage * (ChampionDamageAgainstPlayerMultiplier - 1f) * _damagePower,
            MinimumChampionExtraDamage * _damagePower);
        if (extraDamage < 0.5f)
        {
            return;
        }

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
        // Easy leaves the champion's decision-making to the game's own AI; only its body is (slightly) boosted.
        if (_smartAi)
        {
            ApplyChampionAi(agent);
        }

        ApplyDuelOnlyChampionStatBuffs(agent);
    }

    private static void ApplyChampionAi(Agent agent)
    {
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
        Set(agent, DrivenProperty.AiDefendWithShieldDecisionChanceValue, 0.55f);
        Set(agent, DrivenProperty.AiAttackingShieldDefenseChance, 0.42f);
        Set(agent, DrivenProperty.AiAttackingShieldDefenseTimer, 0.16f);
        ApplyMeleeBehaviorPressure(agent);
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

    // The Legendary figures are the original ones (with the original single-duel intensity of 1.45 folded in).
    private void ApplyDuelOnlyChampionStatBuffs(Agent agent)
    {
        Set(agent, DrivenProperty.SwingSpeedMultiplier, Mul(5.95f));
        Set(agent, DrivenProperty.ThrustOrRangedReadySpeedMultiplier, Mul(5.95f));
        Set(agent, DrivenProperty.HandlingMultiplier, Mul(5.4f));
        Set(agent, DrivenProperty.CombatMaxSpeedMultiplier, Mul(3f));
        Set(agent, DrivenProperty.MaxSpeedMultiplier, Mul(2.35f));
        Set(agent, DrivenProperty.MeleeWeaponDamageMultiplierBonus, 3.1f * _damagePower);
        Set(agent, DrivenProperty.KickStunDurationMultiplier, Mul(2.15f));
        Set(agent, DrivenProperty.ShieldBashStunDurationMultiplier, Mul(2.15f));
        if (_smartAi)
        {
            Set(agent, DrivenProperty.AiAttackCalculationMaxTimeFactor, 0.05f);
            Set(agent, DrivenProperty.AIHoldingReadyMaxDuration, 0.35f);
        }
    }

    private static void ApplyMeleeBehaviorPressure(Agent agent)
    {
        agent.SetAIBehaviorParams(HumanAIComponent.AISimpleBehaviorKind.Melee, 1000f, 1000f, 0f, 0f, 0f);
        agent.HumanAIComponent?.OverrideBehaviorParams(HumanAIComponent.AISimpleBehaviorKind.Melee, 1000f, 1000f, 0f, 0f, 0f);
    }

    private void ApplyCloseRangePunishProfile(Agent agent)
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
        Set(agent, DrivenProperty.KickStunDurationMultiplier, Mul(2.4f));
        Set(agent, DrivenProperty.ShieldBashStunDurationMultiplier, Mul(2.4f));
        ApplyMeleeBehaviorPressure(agent);
        Set(agent, DrivenProperty.SwingSpeedMultiplier, Mul(5.4f));
        Set(agent, DrivenProperty.ThrustOrRangedReadySpeedMultiplier, Mul(5.4f));
        Set(agent, DrivenProperty.HandlingMultiplier, Mul(5.8f));
        Set(agent, DrivenProperty.CombatMaxSpeedMultiplier, Mul(3.25f));
        Set(agent, DrivenProperty.MaxSpeedMultiplier, Mul(2.55f));
        Set(agent, DrivenProperty.MeleeWeaponDamageMultiplierBonus, 2.65f * _damagePower);
    }

    private void ApplyPressureProfile(Agent agent)
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
        Set(agent, DrivenProperty.SwingSpeedMultiplier, Mul(5f));
        Set(agent, DrivenProperty.ThrustOrRangedReadySpeedMultiplier, Mul(5f));
        Set(agent, DrivenProperty.HandlingMultiplier, Mul(5.65f));
        Set(agent, DrivenProperty.CombatMaxSpeedMultiplier, Mul(3.15f));
        Set(agent, DrivenProperty.MaxSpeedMultiplier, Mul(2.5f));
        Set(agent, DrivenProperty.MeleeWeaponDamageMultiplierBonus, 2.35f * _damagePower);
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
        agent.SetCurrentActionSpeed(0, Mul(5.8f));
        agent.SetCurrentActionSpeed(1, Mul(5.8f));
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

        if (_tactics && IsPlayerDefending(playerAction))
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

        float chance = (IsPlayerDefending(playerAction) ? 0.98f : (_isGauntlet ? 0.68f : 0.82f)) * _feintFactor;
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
        agent.SetCurrentActionSpeed(1, Mul(5.2f));
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
        agent.SetCurrentActionSpeed(1, Mul(5.5f));
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

    private void ApplyActiveActionSpeedBoost(Agent agent)
    {
        if (_power < 0.01f)
        {
            return;
        }

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
            agent.SetCurrentActionSpeed(0, Mul(5.2f));
            agent.SetCurrentActionSpeed(1, Mul(5.2f));
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
