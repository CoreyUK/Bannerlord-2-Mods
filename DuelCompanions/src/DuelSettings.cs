using System;
using System.Globalization;
using System.IO;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace DuelCompanions;

/// <summary>
/// Player-tunable values read from Modules/DuelCompanions/settings.txt, when a campaign loads and again before every
/// duel so edits apply without restarting. A missing file or key means the default below.
/// </summary>
internal sealed class DuelSettings
{
    public enum Level { Easy, Normal, Hard, Legendary }

    public Level Difficulty = Level.Normal;
    public bool ScaleWithSkill = true;
    public int DefeatGoldPercent = 10;
    public int DefeatGoldMax = 20000;
    public int DefeatMorale = 25;
    public int DefeatWeaponChance = 20;
    public int DuelReward = 6000;
    public int GauntletReward = 15000;
    public int RumorNearestTowns = 12;

    public static DuelSettings Current = new();

    public static string FilePath => System.IO.Path.Combine(BasePath.Name, "Modules", "DuelCompanions", "settings.txt");

    public static void Reload() => Current = Load();

    private static DuelSettings Load()
    {
        var settings = new DuelSettings();
        try
        {
            if (!File.Exists(FilePath)) return settings;
            foreach (string raw in File.ReadAllLines(FilePath))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim().ToLowerInvariant();
                string value = line.Substring(eq + 1).Trim();
                bool Flag() => value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
                int Int(int fallback, int max = int.MaxValue) =>
                    int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n >= 0 ? Math.Min(n, max) : fallback;
                switch (key)
                {
                    case "difficulty":
                        if (Enum.TryParse(value, true, out Level level)) settings.Difficulty = level;
                        break;
                    case "scale_with_skill": settings.ScaleWithSkill = Flag(); break;
                    case "defeat_gold_percent": settings.DefeatGoldPercent = Int(settings.DefeatGoldPercent, 100); break;
                    case "defeat_gold_max": settings.DefeatGoldMax = Int(settings.DefeatGoldMax); break;
                    case "defeat_morale": settings.DefeatMorale = Int(settings.DefeatMorale, 100); break;
                    case "defeat_weapon_chance": settings.DefeatWeaponChance = Int(settings.DefeatWeaponChance, 100); break;
                    case "duel_reward": settings.DuelReward = Int(settings.DuelReward); break;
                    case "gauntlet_reward": settings.GauntletReward = Int(settings.GauntletReward); break;
                    case "rumor_nearest_towns": settings.RumorNearestTowns = Int(settings.RumorNearestTowns); break;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.Print($"[DuelCompanions] Could not read {FilePath}: {ex.Message}");
        }
        return settings;
    }

    /// <summary>Denars lost on a defeat: a share of the player's purse, capped.</summary>
    public int DefeatGoldLoss(int playerGold) =>
        Math.Max(0, Math.Min(DefeatGoldMax, (int)((long)playerGold * DefeatGoldPercent / 100)));

    /// <summary>
    /// How strong a champion is for the current player, from 0 (an ordinary fighter) to 1 (the original, near-unbeatable
    /// tuning). Rises with the player's best melee skill when <see cref="ScaleWithSkill"/> is on, so a new character
    /// meets a gentler champion than a veteran does. Gauntlet opponents are a little weaker, since there are three.
    /// </summary>
    public float ChampionPower(bool gauntlet)
    {
        float power = Difficulty switch
        {
            Level.Easy => 0.1f,
            Level.Normal => 0.35f,
            Level.Hard => 0.65f,
            _ => 1f
        };

        if (ScaleWithSkill && Hero.MainHero != null)
        {
            int best = Math.Max(Hero.MainHero.GetSkillValue(DefaultSkills.OneHanded),
                       Math.Max(Hero.MainHero.GetSkillValue(DefaultSkills.TwoHanded), Hero.MainHero.GetSkillValue(DefaultSkills.Polearm)));
            float t = MBMath.ClampFloat((best - 30f) / 220f, 0f, 1f);
            power *= 0.6f + 0.4f * t;
        }

        return gauntlet ? power * 0.85f : power;
    }

    /// <summary>
    /// The champion's AI behaviour for the difficulty. Easy uses the game's own AI; Normal sharpens its defence;
    /// Hard and Legendary add the scripted duel tactics (forced feints, footwork, counter-attacks).
    /// </summary>
    public bool SmartAi => Difficulty != Level.Easy;
    public bool DuelTactics => Difficulty >= Level.Hard;

    public float FeintFactor => Difficulty switch
    {
        Level.Easy => 0f,
        Level.Normal => 0.25f,
        Level.Hard => 0.55f,
        _ => 1f
    };
}
