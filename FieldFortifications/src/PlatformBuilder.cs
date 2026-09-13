using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace FieldFortifications;

/// <summary>
/// A raised archer platform: a deck and a ramp tiled from the game's modular wooden floor piece, on wooden posts,
/// built from props scaled to exact sizes, with a matching walkable navmesh (NavMeshPrefabs/ff_platform.bin) so AI
/// archers can climb it.
/// The platform's local frame is x right, y forward toward the enemy, z up; the ramp descends toward the
/// player's line. The dimensions here must match the ones the navmesh file was generated with.
/// </summary>
internal static class PlatformBuilder
{
    public const float DeckWidth = 12f, DeckDepth = 8f, DeckHeight = 2.5f, RampWidth = 12f, RampLength = 7f;
    /// <summary>The ramp runs this far past the deck edge, tucked under the deck, so the two always meet.</summary>
    private const float RampOverlap = 0.4f;
    /// <summary>Largest tile the floor piece is stretched to; bigger surfaces are tiled from several.</summary>
    private const float TileTarget = 4f;
    private const float PlankThickness = 0.2f, PostSize = 0.3f;
    public const string PlankPrefab = "module_european_floor_a";
    public const string PostPrefab = "bd_pole_3m";
    public const string NavMeshName = "ff_platform";
    private const int RemoveReason = 94;

    public readonly struct Part
    {
        public readonly string Prefab;
        public readonly MatrixFrame Local;
        public Part(string prefab, MatrixFrame local) { Prefab = prefab; Local = local; }
    }

    private static List<Part>? _parts;
    private static readonly Dictionary<string, (Vec3 size, Vec3 centre)> _measured = new();

    // The game attaches a dynamic navmesh through MissionObject; these members are protected there.
    private static readonly FieldInfo? NavMeshPrefabField = typeof(MissionObject).GetField("NavMeshPrefabName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly FieldInfo? IdStartField = typeof(MissionObject).GetField("DynamicNavmeshIdStart", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly MethodInfo? AttachMethod = typeof(MissionObject).GetMethod("AttachDynamicNavmeshToEntity", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    /// <summary>Every prop making up the platform, with its local frame (including scale) relative to the platform root.</summary>
    public static IReadOnlyList<Part> Parts(Scene scene)
    {
        if (_parts != null) return _parts;
        var parts = new List<Part>();
        float t = PlankThickness;

        // deck, tiled from floor pieces
        int deckAcross = (int)Math.Ceiling(DeckWidth / TileTarget), deckAlong = (int)Math.Ceiling(DeckDepth / TileTarget);
        float deckCellW = DeckWidth / deckAcross, deckCellD = DeckDepth / deckAlong;
        for (int ix = 0; ix < deckAcross; ix++)
        for (int iy = 0; iy < deckAlong; iy++)
        {
            Vec3 centre = new Vec3(-DeckWidth * 0.5f + deckCellW * (ix + 0.5f), deckCellD * (iy + 0.5f), DeckHeight - t * 0.5f);
            parts.Add(new Part(PlankPrefab, BoxFrame(scene, PlankPrefab, new Vec3(deckCellW, deckCellD, t), centre, 0f)));
        }
        // ramp: panels side by side, tilted about x so they rise toward +y. The top surface runs from the ground at
        // y = -RampLength up to the deck's underside at its rear edge, then a little further under the deck, so the
        // ramp tucks under the deck edge and there is never a seam at the top.
        Vec3 bottom = new Vec3(0f, -RampLength, 0f);
        Vec3 top = new Vec3(0f, 0f, DeckHeight - t);
        Vec3 dir = top - bottom;
        float run = dir.Normalize();
        float angle = (float)Math.Atan2(dir.z, dir.y);
        Vec3 normal = new Vec3(0f, -(float)Math.Sin(angle), (float)Math.Cos(angle));
        float length = run + RampOverlap;
        int rampAcross = (int)Math.Ceiling(RampWidth / TileTarget), rampAlong = (int)Math.Ceiling(length / TileTarget);
        float rampCellW = RampWidth / rampAcross, rampCellL = length / rampAlong;
        for (int ix = 0; ix < rampAcross; ix++)
        for (int k = 0; k < rampAlong; k++)
        {
            Vec3 onLine = bottom + dir * (rampCellL * (k + 0.5f)) - normal * (t * 0.5f);
            Vec3 centre = new Vec3(-RampWidth * 0.5f + rampCellW * (ix + 0.5f), onLine.y, onLine.z);
            parts.Add(new Part(PlankPrefab, BoxFrame(scene, PlankPrefab, new Vec3(rampCellW, rampCellL, t), centre, angle)));
        }
        // posts, sunk a little into the ground so they never float on uneven terrain
        const float sink = 0.3f;
        float postHeight = DeckHeight - t + sink;
        float px = DeckWidth * 0.5f - PostSize * 0.5f - 0.15f;
        foreach (float x in new[] { -px, 0f, px })
        foreach (float y in new[] { 0.35f, DeckDepth * 0.5f, DeckDepth - 0.35f })
            parts.Add(new Part(PostPrefab, BoxFrame(scene, PostPrefab, new Vec3(PostSize, PostSize, postHeight), new Vec3(x, y, postHeight * 0.5f - sink), 0f)));
        _parts = parts;
        return parts;
    }

    private static (Vec3 size, Vec3 centre) Measure(Scene scene, string prefab)
    {
        if (_measured.TryGetValue(prefab, out var known)) return known;
        GameEntity? probe = GameEntity.Instantiate(scene, prefab, false, false, "");
        if (probe == null)
        {
            ErrorLog.Write("Prefab " + prefab + " not found; the platform will look wrong.");
            return _measured[prefab] = (new Vec3(1f, 1f, 1f), Vec3.Zero);
        }
        Vec3 min = probe.GetBoundingBoxMin(), max = probe.GetBoundingBoxMax();
        probe.Remove(RemoveReason);
        return _measured[prefab] = (max - min, (min + max) * 0.5f);
    }

    /// <summary>
    /// A frame that turns a prop into a box of the wanted extents at the wanted centre. The prop's axes are paired
    /// with the root axes by size: its longest axis goes to the axis with the largest wanted extent, and so on, then
    /// each is scaled to fit. Finally the box is tilted about the root x axis by <paramref name="tiltAboutX"/>.
    /// </summary>
    private static MatrixFrame BoxFrame(Scene scene, string prefab, Vec3 extents, Vec3 centre, float tiltAboutX)
    {
        (Vec3 size, Vec3 localCentre) = Measure(scene, prefab);
        float[] have = { size.x, size.y, size.z };
        float[] want = { extents.x, extents.y, extents.z };
        int[] haveOrder = { 0, 1, 2 }, wantOrder = { 0, 1, 2 };
        Array.Sort(haveOrder, (a, b) => have[b].CompareTo(have[a]));
        Array.Sort(wantOrder, (a, b) => want[b].CompareTo(want[a]));

        Vec3[] axes = new Vec3[3];
        int thinLocal = haveOrder[2];
        for (int rank = 0; rank < 3; rank++)
        {
            int local = haveOrder[rank], target = wantOrder[rank];
            Vec3 axis = target == 0 ? new Vec3(1f, 0f, 0f) : target == 1 ? new Vec3(0f, 1f, 0f) : new Vec3(0f, 0f, 1f);
            axes[local] = axis * (want[target] / Math.Max(have[local], 0.001f));
        }
        // keep the frame right-handed: flip the thinnest axis if the pairing mirrored it
        if (Vec3.DotProduct(Vec3.CrossProduct(axes[0], axes[1]), axes[2]) < 0f) axes[thinLocal] = -axes[thinLocal];

        var rotation = new Mat3(axes[0], axes[1], axes[2]);
        if (tiltAboutX != 0f)
        {
            rotation.s = RotateAboutX(rotation.s, tiltAboutX);
            rotation.f = RotateAboutX(rotation.f, tiltAboutX);
            rotation.u = RotateAboutX(rotation.u, tiltAboutX);
        }
        // put the prop's bounding-box centre on the wanted centre
        Vec3 mapped = rotation.s * localCentre.x + rotation.f * localCentre.y + rotation.u * localCentre.z;
        return new MatrixFrame(rotation, centre - mapped);
    }

    private static Vec3 RotateAboutX(Vec3 v, float a)
    {
        float c = (float)Math.Cos(a), sn = (float)Math.Sin(a);
        return new Vec3(v.x, v.y * c - v.z * sn, v.y * sn + v.z * c);
    }

    /// <summary>Global frame of part <paramref name="index"/> for a platform whose root sits at <paramref name="root"/>.</summary>
    public static MatrixFrame PartGlobalFrame(Scene scene, int index, in MatrixFrame root)
    {
        MatrixFrame local = Parts(scene)[index].Local;
        return root.TransformToParent(in local);
    }

    /// <summary>Translucent, physics-less props for the placement preview.</summary>
    public static List<GameEntity> CreateGhostParts(Scene scene)
    {
        var ghosts = new List<GameEntity>();
        foreach (Part part in Parts(scene))
        {
            GameEntity? ghost = GameEntity.Instantiate(scene, part.Prefab, false, false, "");
            if (ghost == null) continue;
            try { ghost.SetAlpha(FortificationState.GhostAlpha); } catch { /* cosmetic */ }
            ghosts.Add(ghost);
        }
        return ghosts;
    }

    /// <summary>Builds the real platform: solid props plus the walkable navmesh attached to an invisible root.</summary>
    public static void Build(Mission mission, in MatrixFrame root)
    {
        Scene scene = mission.Scene;
        try
        {
            IReadOnlyList<Part> parts = Parts(scene);
            for (int i = 0; i < parts.Count; i++)
            {
                MatrixFrame frame = root.TransformToParent(parts[i].Local);
                GameEntity? entity = GameEntity.Instantiate(scene, parts[i].Prefab, frame, false);
                if (entity == null) continue;
                entity.SetGlobalFrame(in frame, true);
                // The callback pass is what finalises a placed prop's collision; the props have no scripts of their own.
                entity.CallScriptCallbacks(true);
                entity.SetPhysicsState(true, true);
            }

            // The engine reads navmesh pieces from the Native module folder; make sure ours is there before importing.
            EnsureNavMeshInNative();
            // The navmesh must belong to a mission object: the game looks the owner up when agents and orders touch
            // dynamic navmesh faces. An empty entity with a SynchedMissionObject script is the lightest owner.
            GameEntity rootEntity = GameEntity.CreateEmpty(scene, false, false, false);
            rootEntity.SetGlobalFrame(in root, true);
            rootEntity.CreateAndAddScriptComponent("SynchedMissionObject", false);
            MissionObject? owner = rootEntity.GetFirstScriptOfType<MissionObject>();
            if (owner == null)
            {
                ErrorLog.Write("Archer platform: could not add a SynchedMissionObject to the navmesh root.");
                return;
            }
            NavMeshPrefabField?.SetValue(owner, NavMeshName);
            rootEntity.CallScriptCallbacks(true);
            int faces = rootEntity.GetAttachedNavmeshFaceCount();
            if (faces == 0)
            {
                AttachMethod?.Invoke(owner, null);
                faces = rootEntity.GetAttachedNavmeshFaceCount();
            }
            // OnInit disables the faces when the owner has no physics body; enable them by hand.
            int start = IdStartField != null ? (int)IdStartField.GetValue(owner) : 0;
            if (start > 0) for (int id = start; id < start + 10; id++) scene.SetAbilityOfFacesWithId(id, true);
            if (faces == 0) ErrorLog.Write("Archer platform navmesh did not attach; archers cannot climb it.");
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Archer platform build failed: " + ex);
        }
    }

    private static string ModuleNavMeshFile => System.IO.Path.Combine(BasePath.Name, "Modules", "FieldFortifications", "NavMeshPrefabs", NavMeshName + ".bin");
    private static string NativeNavMeshFile => System.IO.Path.Combine(BasePath.Name, "Modules", "Native", "NavMeshPrefabs", NavMeshName + ".bin");

    private static void EnsureNavMeshInNative()
    {
        try
        {
            if (!File.Exists(ModuleNavMeshFile)) return;
            if (File.Exists(NativeNavMeshFile))
            {
                byte[] a = File.ReadAllBytes(ModuleNavMeshFile), b = File.ReadAllBytes(NativeNavMeshFile);
                if (SameBytes(a, b)) return;
            }
            File.Copy(ModuleNavMeshFile, NativeNavMeshFile, true);
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Could not copy the platform navmesh into Native: " + ex.Message);
        }
    }

    private static bool SameBytes(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }
}
