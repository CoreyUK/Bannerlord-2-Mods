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

    public override TextObject Title => Localize("{=dc_rumor_quest_title}Duel Rumour: {SETTLEMENT}", ("SETTLEMENT", _settlement.Name));

    public override bool IsRemainingTimeHidden => true;

    protected override void OnStartQuest()
    {
        AddTrackedObject(_settlement);
        AddLog(
            Localize(
                "{=dc_rumor_quest_start}A dangerous champion named {DUELIST} is accepting challengers at {DUEL_GROUND}. Travel to {SETTLEMENT} before the rumour fades.",
                ("DUELIST", _duelistName),
                ("DUEL_GROUND", _groundDescription),
                ("SETTLEMENT", _settlement.Name)),
            false);
    }

    protected override void OnCompleteWithSuccess()
    {
        AddLog(Localize("{=dc_rumor_quest_success}{DUELIST} has been defeated. The duel rumour is settled.", ("DUELIST", _duelistName)), false);
    }

    protected override void OnTimedOut()
    {
        AddLog(
            Localize(
                "{=dc_rumor_quest_timeout}The rumour at {SETTLEMENT} has faded. {DUELIST} has moved on.",
                ("SETTLEMENT", _settlement.Name),
                ("DUELIST", _duelistName)),
            false);
    }

    public override void OnCanceled()
    {
        AddLog(Localize("{=dc_rumor_quest_cancelled}The duel rumour at {SETTLEMENT} is no longer active.", ("SETTLEMENT", _settlement.Name)), false);
    }

    protected override void SetDialogs()
    {
    }

    protected override void InitializeQuestOnGameLoad()
    {
    }

    private static TextObject Localize(string text, params (string Key, object Value)[] variables)
    {
        TextObject textObject = new(text);
        foreach ((string key, object value) in variables)
        {
            textObject.SetTextVariable(key, value as TextObject ?? new TextObject(value.ToString() ?? string.Empty));
        }

        return textObject;
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
