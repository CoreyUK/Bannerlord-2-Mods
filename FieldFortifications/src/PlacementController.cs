using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace FieldFortifications;

/// <summary>
/// Lets the player position the bought works during deployment. Every item starts as a ghost at its default spot
/// and as a card above the hint line. Clicking a card (or pressing the place key) picks the item up: its ghost
/// follows the cursor over the terrain, the arrow keys rotate it, the confirm key fixes it, the cancel key drops it
/// back where it was. Whatever is never placed keeps its default spot.
/// </summary>
public sealed class PlacementController
{
    public enum Kind { BarricadeLine, Ballista, Mangonel, ArrowBarrels, ArcherTower }

    public sealed class Item
    {
        public Kind Kind;
        public string Name = "";
        public string Icon = "";
        public Vec2 Position;
        public Vec2 Forward;
        public bool Placed;
        public readonly List<GameEntity> Ghosts = new();
    }

    private const int GhostRemoveReason = 92;
    private const uint ColourValid = 0xFFFFFFFF;
    private const uint ColourInvalid = 0xFFFF4040;
    private const string MovieName = "FieldFortificationsPlacement";

    private readonly Mission _mission;
    private readonly FortificationSettings _settings;
    private readonly Vec2 _anchor;
    private readonly List<Item> _items = new();
    private readonly PlacementPanelVM _panel = new();

    private GauntletLayer? _layer;
    private GauntletMovieIdentifier? _movie;
    private int _active = -1;
    private Vec2 _restorePosition, _restoreForward;
    private float _angle;
    private bool _cursorValid;

    public IReadOnlyList<Item> Items => _items;

    public PlacementController(Mission mission, FortificationSettings settings, Vec2 anchor)
    {
        _mission = mission;
        _settings = settings;
        _anchor = anchor;
    }

    public void AddItem(Kind kind, string name, string icon, Vec2 position, Vec2 forward)
    {
        var item = new Item { Kind = kind, Name = name, Icon = icon, Position = position, Forward = forward };
        CreateGhosts(item);
        UpdateGhostFrames(item, true);
        _items.Add(item);
    }

    /// <summary>Puts the card panel on screen. Call once after the items are added.</summary>
    public void ShowPanel()
    {
        try
        {
            if (ScreenManager.TopScreen is not MissionScreen screen) return;
            _panel.Cards.Clear();
            for (int i = 0; i < _items.Count; i++)
                _panel.Cards.Add(new PlacementCardVM(i, _items[i].Name, _items[i].Icon, card => Begin(card.Index)));
            _panel.Hint = $"Click a card or press {_settings.PlaceKey} to pick up a work. Arrow keys rotate, {_settings.ConfirmKey} places, {_settings.CancelKey} cancels.";
            RefreshCards();
            _layer = new GauntletLayer(MovieName, 150, false);
            _movie = _layer.LoadMovie(MovieName, _panel);
            _layer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.Mouse);
            screen.AddLayer(_layer);
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Placement card panel failed; keys still work: " + ex);
            _layer = null;
        }
    }

    public void Tick(float dt)
    {
        if (_items.Count == 0) return;
        if (Input.IsKeyPressed(_settings.PlaceKey)) Advance(restoreCurrent: true);
        if (_active < 0) return;
        Item item = _items[_active];

        if (Input.IsKeyPressed(_settings.CancelKey))
        {
            Restore(item);
            _active = -1;
            RefreshCards();
            Say("Placement cancelled.");
            return;
        }

        float turn = 0f;
        if (Input.IsKeyDown(_settings.RotateLeftKey)) turn += 1f;
        if (Input.IsKeyDown(_settings.RotateRightKey)) turn -= 1f;
        if (turn != 0f) _angle += turn * FortificationState.PlacementTurnSpeed * dt;
        item.Forward = Rotate(_restoreForward, _angle);

        if (TryCursorGround(out Vec2 ground)) item.Position = ground;
        _cursorValid = IsValid(item);
        UpdateGhostFrames(item, _cursorValid);

        if (Input.IsKeyPressed(_settings.ConfirmKey) || Input.IsKeyPressed(InputKey.NumpadEnter))
        {
            if (!_cursorValid)
            {
                Say(item.Kind == Kind.BarricadeLine || item.Kind == Kind.ArcherTower
                    ? item.Name + " must sit outside your deployment area, in front of your lines, and out of the enemy's."
                    : "Cannot place there: too far from your line or inside the enemy's ground.");
                return;
            }
            item.Placed = true;
            Say(item.Name + " placed.");
            Advance(restoreCurrent: false);
        }
    }

    /// <summary>Removes every ghost and the card panel. Call before the real works are spawned.</summary>
    public void Dispose()
    {
        foreach (Item item in _items) RemoveGhosts(item);
        _active = -1;
        try
        {
            if (_layer != null)
            {
                if (_movie != null) _layer.ReleaseMovie(_movie);
                if (ScreenManager.TopScreen is MissionScreen screen) screen.RemoveLayer(_layer);
            }
        }
        catch (Exception ex) { ErrorLog.Write("Placement card panel removal failed: " + ex); }
        _layer = null;
        _movie = null;
    }

    /// <summary>Moves on to the next unplaced item. A skipped item goes back where it was; a confirmed one stays.</summary>
    private void Advance(bool restoreCurrent)
    {
        int start = _active;
        if (_active >= 0)
        {
            if (restoreCurrent) Restore(_items[_active]);
            else UpdateGhostFrames(_items[_active], true);
            _active = -1;
        }
        for (int step = 1; step <= _items.Count; step++)
        {
            int next = (start + step) % _items.Count;
            if (_items[next].Placed && start >= 0) continue;
            if (next == start) break;
            Begin(next);
            return;
        }
        RefreshCards();
        Say("Placement finished. Click a card or press " + _settings.PlaceKey + " to move something.");
    }

    private void Restore(Item item)
    {
        item.Position = _restorePosition;
        item.Forward = _restoreForward;
        UpdateGhostFrames(item, true);
    }

    private void Begin(int index)
    {
        if (index < 0 || index >= _items.Count || _active == index) return;
        // Switching items mid-placement by clicking a card: the previous one goes back where it was.
        if (_active >= 0) Restore(_items[_active]);
        _active = index;
        Item item = _items[index];
        item.Placed = false;
        _restorePosition = item.Position;
        _restoreForward = item.Forward;
        _angle = 0f;
        RefreshCards();
        Say($"Placing: {item.Name}. {_settings.RotateLeftKey}/{_settings.RotateRightKey} rotate, {_settings.ConfirmKey} places, {_settings.PlaceKey} skips, {_settings.CancelKey} cancels.");
    }

    private void RefreshCards()
    {
        for (int i = 0; i < _panel.Cards.Count && i < _items.Count; i++)
        {
            PlacementCardVM card = _panel.Cards[i];
            card.IsSelected = i == _active;
            card.Status = i == _active ? "Placing" : _items[i].Placed ? "Placed" : "Default";
        }
    }

    private bool TryCursorGround(out Vec2 ground)
    {
        ground = Vec2.Zero;
        if (ScreenManager.TopScreen is not MissionScreen screen) return false;
        Scene scene = _mission.Scene;
        float reach = FortificationState.MaxPlacementDistance * 3f;

        // The mission screen takes a normalised screen point; pixels and the game's own order flag are fallbacks.
        foreach (Vec2 point in new[] { Input.MousePositionRanged, Input.MousePositionPixel })
        {
            screen.ScreenPointToWorldRay(point, out Vec3 begin, out Vec3 end);
            if (scene.RayCastForClosestEntityOrTerrain(begin, end, out float _, out Vec3 hit, 0.01f, BodyFlags.CommonFocusRayCastExcludeFlags)
                && hit.AsVec2.Distance(_anchor) < reach)
            {
                ground = hit.AsVec2;
                return true;
            }
        }
        Vec3 flag = screen.GetOrderFlagPosition();
        if (flag.IsValid && flag.AsVec2.Distance(_anchor) < reach)
        {
            ground = flag.AsVec2;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Anything must stay within reach of the line and out of the enemy's deployment ground. Barricades must also
    /// sit wholly outside the player's own deployment ground: their blocker navmesh disables the terrain under them,
    /// and troops deployed there would be relocated by the engine when the battle starts.
    /// </summary>
    private bool IsValid(Item item)
    {
        Vec2 position = item.Position;
        if (position.Distance(_anchor) > FortificationState.MaxPlacementDistance) return false;
        IMissionDeploymentPlan plan = _mission.DeploymentPlan;
        Team? enemy = _mission.PlayerEnemyTeam;
        if (enemy != null && plan.HasDeploymentBoundaries(enemy) && plan.IsPositionInsideDeploymentBoundaries(enemy, in position)) return false;
        if (item.Kind != Kind.BarricadeLine && item.Kind != Kind.ArcherTower) return true;
        Team? player = _mission.PlayerTeam;
        if (player == null || !plan.HasDeploymentBoundaries(player)) return true;
        for (int i = 0; i < GhostCount(item.Kind); i++)
        {
            Placement(item, i, out Vec2 at, out Vec2 _);
            Vec2 behind = at - item.Forward * FortificationState.BoundaryMargin;
            if (plan.IsPositionInsideDeploymentBoundaries(player, in at) || plan.IsPositionInsideDeploymentBoundaries(player, in behind)) return false;
        }
        return true;
    }

    private void CreateGhosts(Item item)
    {
        Scene scene = _mission.Scene;
        if (item.Kind == Kind.ArcherTower)
        {
            item.Ghosts.AddRange(PlatformBuilder.CreateGhostParts(scene));
            return;
        }
        int count = GhostCount(item.Kind);
        string prefab = item.Kind switch
        {
            Kind.Ballista => FortificationState.BallistaPrefab,
            Kind.Mangonel => FortificationState.MangonelPrefab,
            Kind.ArrowBarrels => FortificationState.ArrowBarrelPrefab,
            Kind.ArcherTower => PlatformBuilder.PlankPrefab,
            _ => FortificationState.SegmentPrefab,
        };
        for (int i = 0; i < count; i++)
        {
            GameEntity? ghost = null;
            try { ghost = GameEntity.Instantiate(scene, prefab, false, false, ""); }
            catch (Exception ex) { ErrorLog.Write("Ghost " + prefab + " failed: " + ex.Message); }
            if (ghost == null) continue;
            try { ghost.SetAlpha(FortificationState.GhostAlpha); } catch { /* not every mesh supports alpha */ }
            item.Ghosts.Add(ghost);
        }
    }

    private static void RemoveGhosts(Item item)
    {
        foreach (GameEntity ghost in item.Ghosts)
        {
            try { ghost.Remove(GhostRemoveReason); } catch { /* already gone */ }
        }
        item.Ghosts.Clear();
    }

    private void UpdateGhostFrames(Item item, bool valid)
    {
        uint colour = valid ? ColourValid : ColourInvalid;
        Scene scene = _mission.Scene;
        if (item.Kind == Kind.ArcherTower)
        {
            Placement(item, 0, out Vec2 centre, out Vec2 forward);
            MatrixFrame root = GroundFrame(scene, centre, forward);
            for (int i = 0; i < item.Ghosts.Count; i++)
            {
                MatrixFrame frame = PlatformBuilder.PartGlobalFrame(scene, i, in root);
                item.Ghosts[i].SetGlobalFrame(in frame, true);
                try { item.Ghosts[i].SetFactorColor(colour); } catch { /* cosmetic */ }
            }
            return;
        }
        for (int i = 0; i < item.Ghosts.Count; i++)
        {
            Placement(item, i, out Vec2 at, out Vec2 facing);
            MatrixFrame frame = GroundFrame(scene, at, facing);
            item.Ghosts[i].SetGlobalFrame(in frame, true);
            try { item.Ghosts[i].SetFactorColor(colour); } catch { /* cosmetic */ }
        }
    }

    public static int GhostCount(Kind kind) => kind switch
    {
        Kind.BarricadeLine => FortificationState.SegmentCount,
        Kind.ArrowBarrels => FortificationState.ArrowBarrelCount,
        _ => 1,
    };

    /// <summary>
    /// World position and facing for segment <paramref name="index"/> of an item. Most prop meshes' own forward
    /// points at their builders, so the frame faces back toward the player's line; the siege tower drives forward
    /// toward the enemy, so it keeps the item's forward.
    /// </summary>
    public void Placement(Item item, int index, out Vec2 at, out Vec2 facing)
    {
        Vec2 forward = item.Forward;
        switch (item.Kind)
        {
            case Kind.BarricadeLine:
            {
                float halfSpan = (FortificationState.SegmentCount - 1) * FortificationState.SegmentPitch * 0.5f;
                at = item.Position + forward.LeftVec() * (index * FortificationState.SegmentPitch - halfSpan);
                break;
            }
            case Kind.ArrowBarrels:
            {
                float halfSpan = (FortificationState.ArrowBarrelCount - 1) * FortificationState.ArrowBarrelPitch * 0.5f;
                at = item.Position + forward.LeftVec() * (index * FortificationState.ArrowBarrelPitch - halfSpan);
                break;
            }
            default:
                at = item.Position;
                break;
        }
        facing = item.Kind == Kind.ArcherTower ? forward : -forward;
    }

    public static MatrixFrame GroundFrame(Scene scene, Vec2 at, Vec2 facing)
    {
        float z = scene.GetGroundHeightAtPosition(at.ToVec3(1000f), BodyFlags.CommonCollisionExcludeFlagsForAgent);
        Vec3 f = facing.ToVec3(0f);
        Mat3 rotation = Mat3.CreateMat3WithForward(in f);
        rotation.OrthonormalizeAccordingToForwardAndKeepUpAsZAxis();
        return new MatrixFrame(rotation, at.ToVec3(z));
    }

    private static Vec2 Rotate(Vec2 v, float degrees)
    {
        double r = degrees * Math.PI / 180.0;
        float c = (float)Math.Cos(r), s = (float)Math.Sin(r);
        return new Vec2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    private static void Say(string text) => InformationManager.DisplayMessage(new InformationMessage(text));
}
