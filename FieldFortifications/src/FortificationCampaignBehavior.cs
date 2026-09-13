using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Localization;

namespace FieldFortifications;

/// <summary>Adds the priced fortification options to the pre-battle encounter menu, field battles only.</summary>
public sealed class FortificationCampaignBehavior : CampaignBehaviorBase
{
    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.MapEventEnded.AddNonSerializedListener(this, OnMapEventEnded);
    }

    public override void SyncData(IDataStore store)
    {
        bool barricades = FortificationState.Barricades, ballista = FortificationState.Ballista, mangonel = FortificationState.Mangonel;
        bool arrows = FortificationState.Arrows, tower = FortificationState.Tower;
        store.SyncData("ff_barricades", ref barricades);
        store.SyncData("ff_ballista", ref ballista);
        store.SyncData("ff_mangonel", ref mangonel);
        store.SyncData("ff_arrows", ref arrows);
        store.SyncData("ff_tower", ref tower);
        FortificationState.Barricades = barricades;
        FortificationState.Ballista = ballista;
        FortificationState.Mangonel = mangonel;
        FortificationState.Arrows = arrows;
        FortificationState.Tower = tower;
    }

    private void OnSessionLaunched(CampaignGameStarter starter)
    {
        FortificationSettings settings = FortificationSettings.Load();
        AddOption(starter, "ff_barricades", 1,
            "{=ff_opt_barricades}Raise barricades (" + settings.BarricadeCost + "{GOLD_ICON})",
            "{=ff_tip_barricades}A line of spiked barricades in front of your infantry. You place it during deployment. Foot soldiers must go around or hack through; horses that charge it are impaled.",
            settings.BarricadeCost, () => FortificationState.Barricades, () => FortificationState.Barricades = true);
        AddOption(starter, "ff_ballista", 2,
            "{=ff_opt_ballista}Set up a ballista (" + settings.BallistaCost + "{GOLD_ICON})",
            "{=ff_tip_ballista}A ballista with " + settings.EngineAmmo + " bolts, placed during deployment. Your archers crew it.",
            settings.BallistaCost, () => FortificationState.Ballista, () => FortificationState.Ballista = true);
        AddOption(starter, "ff_mangonel", 3,
            "{=ff_opt_mangonel}Set up a catapult (" + settings.MangonelCost + "{GOLD_ICON})",
            "{=ff_tip_mangonel}A mangonel with " + settings.EngineAmmo + " stones, placed during deployment. Your archers crew it.",
            settings.MangonelCost, () => FortificationState.Mangonel, () => FortificationState.Mangonel = true);
        AddOption(starter, "ff_arrows", 4,
            "{=ff_opt_arrows}Stock arrows (" + settings.ArrowsCost + "{GOLD_ICON})",
            "{=ff_tip_arrows}Two barrels of arrows placed during deployment. Archers running low walk over and refill; you can too.",
            settings.ArrowsCost, () => FortificationState.Arrows, () => FortificationState.Arrows = true);
        AddOption(starter, "ff_tower", 5,
            "{=ff_opt_tower}Build an archer platform (" + settings.TowerCost + "{GOLD_ICON})",
            "{=ff_tip_tower}A raised timber deck with a ramp at the back, placed during deployment. Order archers onto it once the battle starts.",
            settings.TowerCost, () => FortificationState.Tower, () => FortificationState.Tower = true);
    }

    private static void AddOption(CampaignGameStarter starter, string id, int index, string text, string tooltip, int cost,
        System.Func<bool> isBought, System.Action buy)
    {
        starter.AddGameMenuOption("encounter", id, text,
            args =>
            {
                if (CurrentFieldBattle() == null) return false;
                args.optionLeaveType = GameMenuOption.LeaveType.DefendAction;
                if (isBought())
                {
                    args.IsEnabled = false;
                    args.Tooltip = new TextObject("{=ff_tip_done}Your men have already seen to this.");
                }
                else if (Hero.MainHero.Gold < cost)
                {
                    args.IsEnabled = false;
                    args.Tooltip = new TextObject("{=ff_tip_gold}You need {AMOUNT} denars.").SetTextVariable("AMOUNT", cost);
                }
                else
                {
                    args.Tooltip = new TextObject(tooltip);
                }
                return true;
            },
            args =>
            {
                if (isBought() || Hero.MainHero.Gold < cost) return;
                GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, null, cost, false);
                buy();
                Campaign.Current.GameMenuManager.RefreshMenuOptions(Campaign.Current.CurrentMenuContext);
            },
            false, index, false, null);
    }

    private static MapEvent? CurrentFieldBattle()
    {
        MapEvent? battle = PlayerEncounter.Battle ?? MobileParty.MainParty?.MapEvent;
        return battle != null && battle.IsFieldBattle ? battle : null;
    }

    private static void OnMapEventEnded(MapEvent mapEvent)
    {
        if (mapEvent.IsPlayerMapEvent) FortificationState.Clear();
    }
}
