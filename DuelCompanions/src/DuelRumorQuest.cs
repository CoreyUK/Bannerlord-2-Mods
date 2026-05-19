using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace DuelCompanions;

internal sealed class DuelRumorQuest : QuestBase
{
    [SaveableField(1)]
    private readonly Settlement _settlement;

    [SaveableField(2)]
    private readonly string _duelistName;

    [SaveableField(3)]
    private readonly string _groundDescription;

    public DuelRumorQuest(
        string questId,
        Settlement settlement,
        string duelistName,
        string groundDescription,
        int daysRemaining)
        : base(questId, Hero.MainHero, CampaignTime.Never, 0)
    {
        _settlement = settlement;
        _duelistName = duelistName;
        _groundDescription = groundDescription;
    }

    public override TextObject Title => new($"Duel Rumour: {_settlement.Name}");

    public override bool IsRemainingTimeHidden => true;

    protected override void OnStartQuest()
    {
        AddTrackedObject(_settlement);
        AddLog(
            new TextObject(
                $"A dangerous champion named {_duelistName} is accepting challengers at {_groundDescription}. Travel to {_settlement.Name} before the rumour fades."),
            false);
    }

    protected override void OnCompleteWithSuccess()
    {
        AddLog(new TextObject($"{_duelistName} has been defeated. The duel rumour is settled."), false);
    }

    protected override void OnTimedOut()
    {
        AddLog(new TextObject($"The rumour at {_settlement.Name} has faded. {_duelistName} has moved on."), false);
    }

    public override void OnCanceled()
    {
        AddLog(new TextObject($"The duel rumour at {_settlement.Name} is no longer active."), false);
    }

    protected override void SetDialogs()
    {
    }

    protected override void InitializeQuestOnGameLoad()
    {
    }
}

internal sealed class DuelCompanionsSaveableTypeDefiner : SaveableTypeDefiner
{
    public DuelCompanionsSaveableTypeDefiner()
        : base(7243100)
    {
    }

    protected override void DefineClassTypes()
    {
        AddClassDefinition(typeof(DuelRumorQuest), 1, null);
    }
}
