namespace StrategicCampaignAI145;

internal sealed class StrategicFactionStatus
{
    public float OwnedStrength { get; set; }
    public float EnemyStrength { get; set; }
    public int OwnedFortifications { get; set; }
    public int ThreatenedFortifications { get; set; }
    public int ActiveWars { get; set; }
    public int RaidedVillages { get; set; }
    public StrategicWarGoal WarGoal { get; set; } = StrategicWarGoal.BorderWar;
    public bool WantsPeace { get; set; }

    public bool IsExhausted
    {
        get
        {
            return ActiveWars > 0 &&
                   (EnemyStrength > OwnedStrength * 1.35f ||
                    ThreatenedFortifications >= OwnedFortifications / 3 ||
                    RaidedVillages >= 3);
        }
    }
}

