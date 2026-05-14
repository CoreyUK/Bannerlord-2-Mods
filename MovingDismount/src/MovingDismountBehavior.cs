using System;
using System.Reflection;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace MovingDismount;

public sealed class MovingDismountBehavior : MissionBehavior
{
    private const float StumbleSpeed = 4f;
    private const float FallSpeed = 8f;
    private const float HardFallSpeed = 13f;
    private const float MaxForcedDismountSpeed = 100f;
    private const float DismountSlideDuration = 0.32f;

    private static readonly MethodInfo? SetMountAgentMethod = GetPrivateAgentMethod("SetMountAgent");
    private static readonly MethodInfo? OnDismountMethod = GetPrivateAgentMethod("OnDismount");

    private Agent? _pendingDismountAgent;
    private Agent? _pendingMountAgent;
    private Agent? _unlockAgent;
    private Agent? _slideAgent;
    private Vec3 _slideStartPosition;
    private Vec3 _slideEndPosition;
    private float _pendingSpeed;
    private float _slideSpeed;
    private float _slideStartTime;
    private float _slideEndTime;
    private float _forceDismountTime;
    private float _unlockTime;
    private float _nextDismountRequestTime;
    private float _suppressDismountUntilTime;
    private float _lastDamageTime = -1f;

    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);

        Agent? player = Agent.Main;
        if (TryContinueDismountSlide(player))
        {
            return;
        }

        TryUnlockForcedDismount(player);

        if (!IsValidMountedPlayer(player))
        {
            ClearPendingDismount();
            return;
        }

        TryForcePendingDismount(player!);
        if (_pendingDismountAgent != null)
        {
            return;
        }

        if (!IsDismountInputPressed())
        {
            return;
        }

        float now = Mission.Current?.CurrentTime ?? 0f;
        if (now < _nextDismountRequestTime || now < _suppressDismountUntilTime)
        {
            return;
        }

        Agent? mount = player!.MountAgent;
        if (mount == null)
        {
            return;
        }

        _pendingDismountAgent = player;
        _pendingMountAgent = mount;
        _pendingSpeed = GetHorizontalSpeed(mount);
        _forceDismountTime = now + 0.18f;
        _nextDismountRequestTime = now + 0.08f;

        player.Mount(mount);
    }

    public override void OnAgentDismount(Agent agent)
    {
        base.OnAgentDismount(agent);

        if (_pendingDismountAgent == null ||
            !ReferenceEquals(agent, _pendingDismountAgent) ||
            _pendingSpeed < StumbleSpeed)
        {
            ClearPendingDismount();
            return;
        }

        ApplyLandingConsequence(agent, _pendingSpeed);
        ClearPendingDismount();
    }

    public override void OnAgentMount(Agent agent)
    {
        base.OnAgentMount(agent);

        if (agent.IsMainAgent)
        {
            float now = Mission.Current?.CurrentTime ?? 0f;
            _suppressDismountUntilTime = now + 0.45f;
            _nextDismountRequestTime = now + 0.45f;
            ClearPendingDismount();
        }
    }

    private static bool IsValidMountedPlayer(Agent? agent)
    {
        return agent != null &&
               agent.IsActive() &&
               agent.IsHuman &&
               agent.IsMainAgent &&
               agent.HasMount &&
               agent.MountAgent != null;
    }

    private bool IsDismountInputPressed()
    {
        IInputContext? input = Mission?.InputManager;
        return input != null &&
               (input.IsGameKeyPressed(CombatHotKeyCategory.Action) ||
                input.IsKeyPressed(InputKey.F));
    }

    private static float GetHorizontalSpeed(Agent mount)
    {
        Vec3 velocity = mount.GetAverageRealGlobalVelocity();
        return velocity.AsVec2.Length;
    }

    private void TryForcePendingDismount(Agent player)
    {
        if (_pendingDismountAgent == null ||
            _pendingMountAgent == null ||
            !ReferenceEquals(player, _pendingDismountAgent) ||
            !ReferenceEquals(player.MountAgent, _pendingMountAgent))
        {
            return;
        }

        float now = Mission.Current?.CurrentTime ?? 0f;
        if (now < _forceDismountTime)
        {
            return;
        }

        Agent mount = _pendingMountAgent;
        float speed = _pendingSpeed;

        ClearPendingDismount();

        if (SetMountAgentMethod == null || OnDismountMethod == null)
        {
            return;
        }

        SetMountAgentMethod.Invoke(player, new object?[] { null });
        OnDismountMethod.Invoke(player, new object[] { mount });
        StartDismountSlide(player, GetDismountLandingPosition(player, mount), speed);
    }

    private void StartDismountSlide(Agent agent, Vec3 endPosition, float speed)
    {
        float now = Mission.Current?.CurrentTime ?? 0f;
        _slideAgent = agent;
        _slideStartPosition = agent.Position;
        _slideEndPosition = endPosition;
        _slideSpeed = speed;
        _slideStartTime = now;
        _slideEndTime = now + DismountSlideDuration;

        agent.ClearTargetFrame();
        agent.ClearTargetZ();
        agent.DisableScriptedMovement();
        agent.DisableScriptedCombatMovement();
        agent.MovementInputVector = Vec2.Zero;
    }

    private bool TryContinueDismountSlide(Agent? agent)
    {
        if (_slideAgent == null || agent == null || !ReferenceEquals(agent, _slideAgent))
        {
            return false;
        }

        float now = Mission.Current?.CurrentTime ?? 0f;
        if (now >= _slideEndTime)
        {
            agent.TeleportToPosition(_slideEndPosition);
            _unlockAgent = agent;
            _unlockTime = now + 0.2f;
            ApplyLandingConsequence(agent, _slideSpeed);
            ClearDismountSlide();
            return false;
        }

        float progress = (_slideEndTime <= _slideStartTime)
            ? 1f
            : MBMath.ClampFloat((now - _slideStartTime) / (_slideEndTime - _slideStartTime), 0f, 1f);
        progress = progress * progress * (3f - (2f * progress));

        Vec3 position = Lerp(_slideStartPosition, _slideEndPosition, progress);
        agent.TeleportToPosition(position);
        agent.MovementInputVector = Vec2.Zero;
        return true;
    }

    private void TryUnlockForcedDismount(Agent? agent)
    {
        if (_unlockAgent == null ||
            agent == null ||
            !ReferenceEquals(agent, _unlockAgent) ||
            (Mission.Current?.CurrentTime ?? 0f) < _unlockTime)
        {
            return;
        }

        _unlockAgent = null;
        _unlockTime = 0f;

        agent.ClearTargetFrame();
        agent.ClearTargetZ();
        agent.DisableScriptedMovement();
        agent.DisableScriptedCombatMovement();
        agent.ClearHandInverseKinematics();
        agent.ResetGuard();
        agent.MovementInputVector = Vec2.Zero;

        ActionIndexCache none = ActionIndexCache.act_none;
        agent.SetActionChannel(
            0,
            none,
            true,
            AnimFlags.amf_priority_continue | AnimFlags.anf_restart,
            0f,
            1f,
            -0.2f,
            0.2f,
            0f,
            false,
            -0.2f,
            0,
            true);
    }

    private static Vec3 GetDismountLandingPosition(Agent rider, Agent mount)
    {
        Vec3 position = mount.Position;
        Vec3 forward = mount.GetAverageRealGlobalVelocity();

        if (forward.AsVec2.Length < 0.1f)
        {
            forward = rider.LookDirection;
        }

        Vec2 direction = forward.AsVec2;
        if (direction.Normalize() < 0.1f)
        {
            direction = new Vec2(0f, 1f);
        }

        Vec2 left = new(-direction.y, direction.x);
        position.x += left.x * 1.15f;
        position.y += left.y * 1.15f;
        position.z += 0.1f;

        return position;
    }

    private static Vec3 Lerp(Vec3 from, Vec3 to, float amount)
    {
        return new Vec3(
            from.x + ((to.x - from.x) * amount),
            from.y + ((to.y - from.y) * amount),
            from.z + ((to.z - from.z) * amount),
            from.w + ((to.w - from.w) * amount));
    }

    private void ApplyLandingConsequence(Agent agent, float speed)
    {
        float now = Mission.Current?.CurrentTime ?? 0f;
        if (now - _lastDamageTime < 0.25f)
        {
            return;
        }

        float damage = CalculateFallDamage(speed);
        if (damage > 0f && agent.Health > 1f)
        {
            agent.Health = Math.Max(1f, agent.Health - damage);
            _lastDamageTime = now;
        }

        agent.MakeVoice(
            speed >= FallSpeed ? SkinVoiceManager.VoiceType.Pain : SkinVoiceManager.VoiceType.Grunt,
            SkinVoiceManager.CombatVoiceNetworkPredictionType.NoPrediction);

        if (speed >= FallSpeed)
        {
            agent.SetCurrentActionSpeed(0, 0.35f);
            agent.SetCurrentActionSpeed(1, 0.35f);
        }

        InformationManager.DisplayMessage(new InformationMessage(GetLandingMessage(speed)));
    }

    private static float CalculateFallDamage(float speed)
    {
        if (speed < StumbleSpeed)
        {
            return 0f;
        }

        if (speed < FallSpeed)
        {
            return 5f + ((speed - StumbleSpeed) * 2.5f);
        }

        if (speed < HardFallSpeed)
        {
            return 15f + ((speed - FallSpeed) * 5f);
        }

        return 40f + (Math.Min(speed, MaxForcedDismountSpeed) - HardFallSpeed) * 4f;
    }

    private static string GetLandingMessage(float speed)
    {
        if (speed >= HardFallSpeed)
        {
            return "You hit the ground hard after jumping from the saddle.";
        }

        if (speed >= FallSpeed)
        {
            return "You stumble after dismounting at speed.";
        }

        return "You stumble after dismounting from the moving horse.";
    }

    private static MethodInfo? GetPrivateAgentMethod(string name)
    {
        return typeof(Agent).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
    }

    private void ClearPendingDismount()
    {
        _pendingDismountAgent = null;
        _pendingMountAgent = null;
        _pendingSpeed = 0f;
        _forceDismountTime = 0f;
    }

    private void ClearDismountSlide()
    {
        _slideAgent = null;
        _slideStartPosition = default;
        _slideEndPosition = default;
        _slideSpeed = 0f;
        _slideStartTime = 0f;
        _slideEndTime = 0f;
    }
}
