using System;
using System.Collections.Generic;
using System.Reflection;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace FieldFortifications;

/// <summary>
/// Builds the paid-for works once the player finishes deploying: a spiked barricade line, a ballista, a mangonel.
/// During deployment the works are ghosts the player can position (see <see cref="PlacementController"/>).
/// Afterwards it hurts horses that charge into the spikes and keeps a crewless mangonel loaded.
/// </summary>
public sealed class FortificationMissionBehavior : MissionBehavior
{
    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    private readonly struct Barricade
    {
        public readonly Vec2 Centre, Forward, Lateral;
        public Barricade(Vec2 centre, Vec2 forward) { Centre = centre; Forward = forward; Lateral = forward.LeftVec(); }
    }

    private bool _initialised, _spawned;
    private FortificationSettings _settings = new();
    private PlacementController? _placement;
    private readonly List<Barricade> _barricades = new();
    private readonly Dictionary<int, float> _spikeCooldown = new();
    private readonly List<RangedSiegeWeapon> _engines = new();
    private readonly Dictionary<RangedSiegeWeapon, float> _reloadStuckSince = new();

    // Protected on MissionObject / RangedSiegeWeapon; the engine sets them from prefab XML, we set them from code.
    private static readonly FieldInfo? NavMeshPrefabField = typeof(MissionObject).GetField("NavMeshPrefabName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly MethodInfo? AttachNavMeshMethod = typeof(MissionObject).GetMethod("AttachDynamicNavmeshToEntity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly FieldInfo? DefaultSideField = typeof(RangedSiegeWeapon).GetField("DefaultSide", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly FieldInfo? StartingAmmoField = typeof(RangedSiegeWeapon).GetField("StartingAmmoCount", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly MethodInfo? SetStartAmmoMethod = typeof(RangedSiegeWeapon).GetMethod("SetStartAmmo", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly FieldInfo? MissileItemIdField = typeof(RangedSiegeWeapon).GetField("MissileItemID", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly FieldInfo? LoadedMissileField = typeof(RangedSiegeWeapon).GetField("_loadedMissileItem", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly PropertyInfo? StateProperty = typeof(RangedSiegeWeapon).GetProperty("State", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly MethodInfo? UpdateAmmoMeshMethod = typeof(RangedSiegeWeapon).GetMethod("UpdateAmmoMesh", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        if (_spawned)
        {
            if (_barricades.Count > 0) TickSpikes();
            if (_engines.Count > 0) TickAutoReload();
            return;
        }
        if (!_initialised)
        {
            Initialise();
            return;
        }
        if (Mission.IsDeploymentFinished || Mission.Mode != MissionMode.Deployment)
        {
            SpawnAll();
            return;
        }
        try { _placement?.Tick(dt); }
        catch (Exception ex) { ErrorLog.Write("Placement tick failed: " + ex); }
    }

    public override void OnRemoveBehavior()
    {
        base.OnRemoveBehavior();
        _placement?.Dispose();
        _placement = null;
    }

    /// <summary>Waits for the deployment plan, then sets up every bought item as a ghost at its default spot.</summary>
    private void Initialise()
    {
        Mission mission = Mission;
        Team? player = mission.PlayerTeam;
        if (player == null) return;
        if (!mission.DeploymentPlan.IsPlanMade(player)) return;
        if (!mission.IsFieldBattle)
        {
            _initialised = true;
            _spawned = true;
            return;
        }

        mission.GetFormationSpawnFrame(player, FormationClass.Infantry, false, out WorldPosition spawn, out Vec2 direction, true);
        if (!spawn.IsValid) return;
        _initialised = true;

        _settings = FortificationSettings.Load();
        FortificationSettings.Current = _settings;
        SiegeAiPatches.BarrelPoints.Clear();
        Vec2 forward = direction.Normalized();
        Vec2 lateral = forward.LeftVec();
        float halfSpan = (FortificationState.SegmentCount - 1) * FortificationState.SegmentPitch * 0.5f;
        bool useBoundary = mission.DeploymentPlan.HasDeploymentBoundaries(player);

        float Distance(float lateralOffset)
        {
            Vec2 origin = spawn.AsVec2 + lateral * lateralOffset;
            return useBoundary ? DistanceToBoundary(mission, player, origin, forward) : FortificationState.DistanceInFront;
        }

        _placement = new PlacementController(mission, _settings, spawn.AsVec2);
        int lines = FortificationState.Count(FortificationState.Work.Barricades);
        int ballistas = FortificationState.Count(FortificationState.Work.Ballista);
        int mangonels = FortificationState.Count(FortificationState.Work.Mangonel);
        int stockpiles = FortificationState.Count(FortificationState.Work.Arrows);
        int platforms = FortificationState.Count(FortificationState.Work.Tower);

        // Barricade lines sit side by side across the front; everything else hangs off the ends of that front.
        float frontHalfWidth = halfSpan + Math.Max(0, lines - 1) * FortificationState.LinePitch * 0.5f;
        for (int n = 0; n < lines; n++)
        {
            float lineOffset = (n - (lines - 1) * 0.5f) * FortificationState.LinePitch;
            // The line is one straight piece, so use the furthest boundary distance of its segments: none may start inside.
            float lineDistance = 0f;
            for (int i = 0; i < FortificationState.SegmentCount; i++)
                lineDistance = Math.Max(lineDistance, Distance(lineOffset + i * FortificationState.SegmentPitch - halfSpan));
            _placement.AddItem(PlacementController.Kind.BarricadeLine, Numbered("Barricades", n, lines), FortificationState.BarricadeIcon,
                spawn.AsVec2 + lateral * lineOffset + forward * lineDistance, forward);
        }
        for (int n = 0; n < ballistas; n++)
        {
            float offset = frontHalfWidth + FortificationState.EngineFlankOffset + n * FortificationState.EnginePitch;
            _placement.AddItem(PlacementController.Kind.Ballista, Numbered("Ballista", n, ballistas), FortificationState.BallistaIcon,
                spawn.AsVec2 + lateral * offset + forward * Distance(offset), forward);
        }
        for (int n = 0; n < mangonels; n++)
        {
            float offset = -(frontHalfWidth + FortificationState.EngineFlankOffset + n * FortificationState.EnginePitch);
            _placement.AddItem(PlacementController.Kind.Mangonel, Numbered("Catapult", n, mangonels), FortificationState.MangonelIcon,
                spawn.AsVec2 + lateral * offset + forward * Distance(offset), forward);
        }
        for (int n = 0; n < stockpiles; n++)
        {
            // Behind the infantry line, inside the deployment area, where the archers stand.
            float offset = (n - (stockpiles - 1) * 0.5f) * FortificationState.ArrowsPitch;
            _placement.AddItem(PlacementController.Kind.ArrowBarrels, Numbered("Arrows", n, stockpiles), FortificationState.ArrowsIcon,
                spawn.AsVec2 + lateral * offset - forward * FortificationState.ArrowsBehindLine, forward);
        }
        for (int n = 0; n < platforms; n++)
        {
            float offset = frontHalfWidth + FortificationState.TowerFlankOffset + ballistas * FortificationState.EnginePitch + n * (PlatformBuilder.DeckWidth + 4f);
            _placement.AddItem(PlacementController.Kind.ArcherTower, Numbered("Platform", n, platforms), FortificationState.TowerIcon,
                spawn.AsVec2 + lateral * offset + forward * Distance(offset), forward);
        }

        // No deployment phase (a later round, or reinforcements): build straight away at the defaults.
        if (mission.IsDeploymentFinished || mission.Mode != MissionMode.Deployment)
        {
            SpawnAll();
            return;
        }
        _placement.ShowPanel();
    }

    private static string Numbered(string name, int index, int total) => total > 1 ? name + " " + (index + 1) : name;

    /// <summary>Turns every ghost into the real thing, wherever it ended up.</summary>
    private void SpawnAll()
    {
        _spawned = true;
        Mission mission = Mission;
        Team? player = mission.PlayerTeam;
        PlacementController? placement = _placement;
        _placement = null;
        if (placement == null || player == null) return;
        placement.Dispose();

        foreach (PlacementController.Item item in placement.Items)
        {
            switch (item.Kind)
            {
                case PlacementController.Kind.BarricadeLine:
                    for (int i = 0; i < FortificationState.SegmentCount; i++)
                    {
                        placement.Placement(item, i, out Vec2 at, out Vec2 facing);
                        if (SpawnSegment(mission, player, at, facing)) _barricades.Add(new Barricade(at, item.Forward));
                    }
                    break;
                case PlacementController.Kind.Ballista:
                case PlacementController.Kind.Mangonel:
                {
                    placement.Placement(item, 0, out Vec2 at, out Vec2 facing);
                    string prefab = item.Kind == PlacementController.Kind.Ballista ? FortificationState.BallistaPrefab : FortificationState.MangonelPrefab;
                    SpawnEngine(mission, player, prefab, at, facing);
                    break;
                }
                case PlacementController.Kind.ArrowBarrels:
                    for (int i = 0; i < FortificationState.ArrowBarrelCount; i++)
                    {
                        placement.Placement(item, i, out Vec2 at, out Vec2 facing);
                        SpawnArrowBarrel(mission, player, at, facing);
                    }
                    break;
                case PlacementController.Kind.ArcherTower:
                {
                    placement.Placement(item, 0, out Vec2 at, out Vec2 facing);
                    MatrixFrame root = PlacementController.GroundFrame(mission.Scene, at, facing);
                    PlatformBuilder.Build(mission, in root);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Walks forward from the spawn line until it leaves the player's deployment boundary, then adds a margin so the
    /// works sit just outside it and can never overlap a deployment slot. Falls back to the fixed distance if the
    /// boundary is never left or the origin is already outside it.
    /// </summary>
    private static float DistanceToBoundary(Mission mission, Team team, Vec2 origin, Vec2 forward)
    {
        IMissionDeploymentPlan plan = mission.DeploymentPlan;
        if (!plan.IsPositionInsideDeploymentBoundaries(team, in origin)) return FortificationState.DistanceInFront;
        for (float d = FortificationState.MinDistanceInFront; d <= FortificationState.MaxDistanceInFront; d += 1f)
        {
            Vec2 probe = origin + forward * d;
            if (!plan.IsPositionInsideDeploymentBoundaries(team, in probe))
                return d + FortificationState.BoundaryMargin;
        }
        return FortificationState.DistanceInFront;
    }

    private static bool SpawnSegment(Mission mission, Team owner, Vec2 at, Vec2 facing)
    {
        try
        {
            MatrixFrame frame = PlacementController.GroundFrame(mission.Scene, at, facing);
            GameEntity? entity = GameEntity.Instantiate(mission.Scene, FortificationState.SegmentPrefab, frame, false);
            if (entity == null)
            {
                ErrorLog.Write("Prefab " + FortificationState.SegmentPrefab + " not found.");
                return false;
            }
            DestructableComponent? destructable = entity.GetFirstScriptOfType<DestructableComponent>();
            if (destructable != null) destructable.BattleSide = owner.Side;
            entity.CallScriptCallbacks(true);
            destructable ??= entity.GetFirstScriptOfType<DestructableComponent>();
            if (destructable != null)
            {
                NavMeshPrefabField?.SetValue(destructable, FortificationState.SegmentNavMeshPrefab);
                AttachNavMeshMethod?.Invoke(destructable, null);
            }
            return true;
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Barricade spawn failed: " + ex);
            return false;
        }
    }

    private void SpawnEngine(Mission mission, Team owner, string prefab, Vec2 at, Vec2 facing)
    {
        try
        {
            MatrixFrame frame = PlacementController.GroundFrame(mission.Scene, at, facing);
            GameEntity? entity = GameEntity.Instantiate(mission.Scene, prefab, frame, false);
            if (entity == null)
            {
                ErrorLog.Write("Prefab " + prefab + " not found.");
                return;
            }

            RangedSiegeWeapon? weapon = entity.GetFirstScriptOfTypeRecursive<RangedSiegeWeapon>();
            if (weapon != null)
            {
                DefaultSideField?.SetValue(weapon, owner.Side);
                StartingAmmoField?.SetValue(weapon, _settings.EngineAmmo);
            }
            entity.CallScriptCallbacks(true);
            weapon ??= entity.GetFirstScriptOfTypeRecursive<RangedSiegeWeapon>();
            if (weapon == null)
            {
                ErrorLog.Write(prefab + " has no RangedSiegeWeapon script.");
                return;
            }

            // Re-assert the side after OnInit in case the game's default-side logic overrode it, and top up ammo.
            DefaultSideField?.SetValue(weapon, owner.Side);
            if (weapon.AmmoCount < _settings.EngineAmmo) SetStartAmmoMethod?.Invoke(weapon, new object[] { _settings.EngineAmmo });

            if (_settings.CrewAi)
            {
                DetachmentManager detachments = owner.DetachmentManager;
                if (!detachments.ContainsDetachment(weapon)) detachments.MakeDetachment(weapon);
                PickCrewFormation(owner)?.JoinDetachment(weapon);
                weapon.SetForcedUse(true);
            }
            else
            {
                weapon.SetIsDisabledForAI(true);
            }
            _engines.Add(weapon);
        }
        catch (Exception ex)
        {
            ErrorLog.Write(prefab + " spawn failed: " + ex);
        }
    }

    /// <summary>
    /// An arrow barrel is a usable machine; registering it as a detachment of the archers' formation lets men who run
    /// low walk over and refill, exactly as in a siege. The player can use it too.
    /// </summary>
    private static void SpawnArrowBarrel(Mission mission, Team owner, Vec2 at, Vec2 facing)
    {
        try
        {
            MatrixFrame frame = PlacementController.GroundFrame(mission.Scene, at, facing);
            GameEntity? entity = GameEntity.Instantiate(mission.Scene, FortificationState.ArrowBarrelPrefab, frame, false);
            if (entity == null)
            {
                ErrorLog.Write("Prefab " + FortificationState.ArrowBarrelPrefab + " not found.");
                return;
            }
            entity.CallScriptCallbacks(true);
            UsableMachine? barrel = entity.GetFirstScriptOfTypeRecursive<UsableMachine>();
            if (barrel == null)
            {
                ErrorLog.Write("Arrow barrel has no UsableMachine script.");
                return;
            }
            foreach (StandingPoint point in barrel.StandingPoints)
            {
                if (point is StandingPointWithWeaponRequirement withWeapon) withWeapon.SetUsingBattleSide(owner.Side);
                SiegeAiPatches.BarrelPoints.Add(point);
            }
            // Barrels ship without an AI object (only players use them in sieges); the crew assignment code needs one.
            if (barrel.Ai == null) barrel.SetAI(new BarrelAi(barrel));
            DetachmentManager detachments = owner.DetachmentManager;
            if (!detachments.ContainsDetachment(barrel)) detachments.MakeDetachment(barrel);
            PickCrewFormation(owner)?.JoinDetachment(barrel);
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Arrow barrel spawn failed: " + ex);
        }
    }

    /// <summary>The stock machine AI with nothing added: it walks eligible agents to the standing points and back.</summary>
    private sealed class BarrelAi : UsableMachineAIBase
    {
        public BarrelAi(UsableMachine machine) : base(machine) { }
    }

    private static Formation? PickCrewFormation(Team team)
    {
        Formation? ranged = team.GetFormation(FormationClass.Ranged);
        if (ranged != null && ranged.CountOfUnits > 0) return ranged;
        Formation? infantry = team.GetFormation(FormationClass.Infantry);
        if (infantry != null && infantry.CountOfUnits > 0) return infantry;
        foreach (Formation formation in team.FormationsIncludingEmpty)
            if (formation.CountOfUnits > 0) return formation;
        return null;
    }

    /// <summary>Horses that run into a barricade's spiked face take damage scaled by their speed.</summary>
    private void TickSpikes()
    {
        float now = Mission.CurrentTime;
        foreach (Agent agent in Mission.Agents)
        {
            if (!agent.IsMount || !agent.IsActive()) continue;
            Vec2 velocity = agent.MovementVelocity;
            float speed = velocity.Length;
            if (speed < FortificationState.SpikeMinSpeed) continue;
            if (_spikeCooldown.TryGetValue(agent.Index, out float until) && now < until) continue;

            Vec2 position = agent.Position.AsVec2;
            foreach (Barricade barricade in _barricades)
            {
                Vec2 d = position - barricade.Centre;
                float along = Vec2.DotProduct(d, barricade.Forward);
                float across = Vec2.DotProduct(d, barricade.Lateral);
                if (Math.Abs(along) > FortificationState.SegmentHalfDepth || Math.Abs(across) > FortificationState.SegmentHalfWidth) continue;

                float damage = Math.Min(FortificationState.SpikeMaxDamage, FortificationState.SpikeBaseDamage + FortificationState.SpikeDamagePerSpeed * speed);
                Impale(agent, damage, velocity);
                _spikeCooldown[agent.Index] = now + FortificationState.SpikeCooldown;
                break;
            }
        }
    }

    private static void Impale(Agent horse, float damage, Vec2 velocity)
    {
        try
        {
            Vec3 direction = velocity.Normalized().ToVec3(0f);
            Blow blow = new Blow(horse.Index)
            {
                DamageType = DamageTypes.Pierce,
                StrikeType = StrikeType.Thrust,
                AttackType = AgentAttackType.Collision,
                BoneIndex = horse.Monster.HeadLookDirectionBoneIndex,
                VictimBodyPart = BoneBodyPartType.Chest,
                GlobalPosition = horse.GetChestGlobalPosition(),
                BaseMagnitude = damage,
                InflictedDamage = (int)damage,
                SwingDirection = direction,
                Direction = direction,
                DamageCalculated = true,
                BlowFlag = BlowFlags.None,
            };
            blow.WeaponRecord.FillAsMeleeBlow(null, null, -1, -1);
            AttackCollisionData collision = AttackCollisionData.GetAttackCollisionDataForDebugPurpose(
                false, false, false, true, false, false, false, false, false, false, false, false,
                CombatCollisionResult.StrikeAgent, -1, 0, (int)DamageTypes.Pierce, blow.BoneIndex, blow.VictimBodyPart, -1,
                Agent.UsageDirection.AttackLeft, -1, CombatHitResultFlags.NormalHit, 0.5f, 1f, 0f, 0f, 0f, 0f, 0f, 0f,
                Vec3.Up, blow.Direction, blow.GlobalPosition, Vec3.Zero, Vec3.Zero, horse.Velocity, Vec3.Up);
            horse.RegisterBlow(blow, in collision);
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Spike damage failed: " + ex);
        }
    }

    /// <summary>
    /// The mangonel is built to be reloaded by a crew. If nobody reloads it for a while (crew dead, or crews turned
    /// off), the next stone is loaded and the machine returns to idle by itself.
    /// </summary>
    private void TickAutoReload()
    {
        float now = Mission.CurrentTime;
        foreach (RangedSiegeWeapon weapon in _engines)
        {
            if (weapon is not Mangonel) continue;
            RangedSiegeWeapon.WeaponState state = weapon.State;
            bool waiting = state == RangedSiegeWeapon.WeaponState.WaitingBeforeReloading || state == RangedSiegeWeapon.WeaponState.Reloading
                        || state == RangedSiegeWeapon.WeaponState.ReloadingPaused || state == RangedSiegeWeapon.WeaponState.LoadingAmmo;
            if (!waiting)
            {
                _reloadStuckSince.Remove(weapon);
                continue;
            }
            if (!_reloadStuckSince.TryGetValue(weapon, out float since))
            {
                _reloadStuckSince[weapon] = now;
                continue;
            }
            if (now - since < FortificationState.AutoReloadSeconds || weapon.AmmoCount <= 0) continue;
            _reloadStuckSince.Remove(weapon);
            try
            {
                string? missileId = MissileItemIdField?.GetValue(weapon) as string;
                ItemObject? missile = string.IsNullOrEmpty(missileId) ? null : MBObjectManager.Instance.GetObject<ItemObject>(missileId);
                if (missile != null) LoadedMissileField?.SetValue(weapon, missile);
                StateProperty?.SetValue(weapon, RangedSiegeWeapon.WeaponState.Idle);
                UpdateAmmoMeshMethod?.Invoke(weapon, null);
            }
            catch (Exception ex)
            {
                ErrorLog.Write("Mangonel self-reload failed: " + ex);
            }
        }
    }
}
