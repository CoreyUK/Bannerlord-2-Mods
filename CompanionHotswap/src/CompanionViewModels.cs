using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace CompanionHotswap;

public sealed class CompanionItemVM : ViewModel
{
    private readonly Agent _agent;
    private readonly Action<Agent> _swapCallback;
    private string _name = string.Empty;
    private string _keyLabel = string.Empty;
    private CharacterImageIdentifierVM? _portrait;
    private bool _isCurrentlyControlled;
    private bool _isPlayerHero;
    private bool _isDead;

    public CompanionItemVM(Agent agent, int index, bool isCurrentlyControlled, bool isPlayerHero, Action<Agent> swapCallback)
    {
        _agent = agent;
        _swapCallback = swapCallback;
        _name = agent.Name;
        _keyLabel = string.Format("[{0}]", index + 1);
        _isCurrentlyControlled = isCurrentlyControlled;
        _isPlayerHero = isPlayerHero;
        _isDead = !CompanionSwapBehavior.CanBeControlled(agent, Mission.Current?.PlayerTeam);

        if (agent.Character != null)
        {
            _portrait = new CharacterImageIdentifierVM(CharacterCode.CreateFrom(agent.Character));
        }
    }

    [DataSourceProperty]
    public string Name { get => _name; set => SetField(ref _name, value, nameof(Name)); }

    [DataSourceProperty]
    public string KeyLabel { get => _keyLabel; set => SetField(ref _keyLabel, value, nameof(KeyLabel)); }

    [DataSourceProperty]
    public CharacterImageIdentifierVM? Portrait { get => _portrait; set => SetField(ref _portrait, value, nameof(Portrait)); }

    [DataSourceProperty]
    public bool IsCurrentlyControlled { get => _isCurrentlyControlled; set => SetField(ref _isCurrentlyControlled, value, nameof(IsCurrentlyControlled)); }

    [DataSourceProperty]
    public bool IsPlayerHero { get => _isPlayerHero; set => SetField(ref _isPlayerHero, value, nameof(IsPlayerHero)); }

    [DataSourceProperty]
    public bool IsDead { get => _isDead; set => SetField(ref _isDead, value, nameof(IsDead)); }

    public void ExecuteSwap()
    {
        _swapCallback?.Invoke(_agent);
    }
}

public sealed class CompanionRosterVM : ViewModel
{
    private MBBindingList<CompanionItemVM> _companions = new();

    [DataSourceProperty]
    public MBBindingList<CompanionItemVM> Companions { get => _companions; set => SetField(ref _companions, value, nameof(Companions)); }

    public void Refresh(List<Agent> agents, Agent? currentAgent, Action<Agent> swapCallback)
    {
        _companions.Clear();
        for (int i = 0; i < agents.Count; i++)
        {
            Agent agent = agents[i];
            _companions.Add(new CompanionItemVM(agent, i, agent == currentAgent, i == 0, swapCallback));
        }
    }
}

public sealed class CompanionMarkersVM : ViewModel
{
    private readonly Slot[] _slots = new Slot[8];

    public void ClearSlots()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            SetVisible(i, false);
        }
    }

    public void HideSlot(int index) => SetVisible(index, false);

    public void SetPortrait(int index, CharacterImageIdentifierVM portrait)
    {
        if (IsValid(index))
        {
            _slots[index].Portrait = portrait;
            OnPropertyChangedWithValue(portrait, $"Slot{index}Portrait");
        }
    }

    public void SetSlot(int index, float x, float y, bool isCurrent)
    {
        if (!IsValid(index))
        {
            return;
        }

        _slots[index].PosX = x;
        _slots[index].PosY = y;
        _slots[index].Current = isCurrent;
        _slots[index].Visible = true;
        OnPropertyChangedWithValue(x, $"Slot{index}PosX");
        OnPropertyChangedWithValue(y, $"Slot{index}PosY");
        OnPropertyChangedWithValue(isCurrent, $"Slot{index}Current");
        OnPropertyChangedWithValue(true, $"Slot{index}Visible");
    }

    private void SetVisible(int index, bool value)
    {
        if (IsValid(index))
        {
            _slots[index].Visible = value;
            OnPropertyChangedWithValue(value, $"Slot{index}Visible");
        }
    }

    private static bool IsValid(int index) => index >= 0 && index < 8;

    private bool GetVisible(int index) => _slots[index].Visible;
    private float GetPosX(int index) => _slots[index].PosX;
    private float GetPosY(int index) => _slots[index].PosY;
    private bool GetCurrent(int index) => _slots[index].Current;
    private CharacterImageIdentifierVM? GetPortrait(int index) => _slots[index].Portrait;

    [DataSourceProperty] public bool Slot0Visible { get => GetVisible(0); set => SetVisible(0, value); }
    [DataSourceProperty] public float Slot0PosX { get => GetPosX(0); set { _slots[0].PosX = value; OnPropertyChangedWithValue(value, nameof(Slot0PosX)); } }
    [DataSourceProperty] public float Slot0PosY { get => GetPosY(0); set { _slots[0].PosY = value; OnPropertyChangedWithValue(value, nameof(Slot0PosY)); } }
    [DataSourceProperty] public bool Slot0Current { get => GetCurrent(0); set { _slots[0].Current = value; OnPropertyChangedWithValue(value, nameof(Slot0Current)); } }
    [DataSourceProperty] public CharacterImageIdentifierVM? Slot0Portrait { get => GetPortrait(0); set { _slots[0].Portrait = value; OnPropertyChangedWithValue(value, nameof(Slot0Portrait)); } }

    [DataSourceProperty] public bool Slot1Visible { get => GetVisible(1); set => SetVisible(1, value); }
    [DataSourceProperty] public float Slot1PosX { get => GetPosX(1); set { _slots[1].PosX = value; OnPropertyChangedWithValue(value, nameof(Slot1PosX)); } }
    [DataSourceProperty] public float Slot1PosY { get => GetPosY(1); set { _slots[1].PosY = value; OnPropertyChangedWithValue(value, nameof(Slot1PosY)); } }
    [DataSourceProperty] public bool Slot1Current { get => GetCurrent(1); set { _slots[1].Current = value; OnPropertyChangedWithValue(value, nameof(Slot1Current)); } }
    [DataSourceProperty] public CharacterImageIdentifierVM? Slot1Portrait { get => GetPortrait(1); set { _slots[1].Portrait = value; OnPropertyChangedWithValue(value, nameof(Slot1Portrait)); } }

    [DataSourceProperty] public bool Slot2Visible { get => GetVisible(2); set => SetVisible(2, value); }
    [DataSourceProperty] public float Slot2PosX { get => GetPosX(2); set { _slots[2].PosX = value; OnPropertyChangedWithValue(value, nameof(Slot2PosX)); } }
    [DataSourceProperty] public float Slot2PosY { get => GetPosY(2); set { _slots[2].PosY = value; OnPropertyChangedWithValue(value, nameof(Slot2PosY)); } }
    [DataSourceProperty] public bool Slot2Current { get => GetCurrent(2); set { _slots[2].Current = value; OnPropertyChangedWithValue(value, nameof(Slot2Current)); } }
    [DataSourceProperty] public CharacterImageIdentifierVM? Slot2Portrait { get => GetPortrait(2); set { _slots[2].Portrait = value; OnPropertyChangedWithValue(value, nameof(Slot2Portrait)); } }

    [DataSourceProperty] public bool Slot3Visible { get => GetVisible(3); set => SetVisible(3, value); }
    [DataSourceProperty] public float Slot3PosX { get => GetPosX(3); set { _slots[3].PosX = value; OnPropertyChangedWithValue(value, nameof(Slot3PosX)); } }
    [DataSourceProperty] public float Slot3PosY { get => GetPosY(3); set { _slots[3].PosY = value; OnPropertyChangedWithValue(value, nameof(Slot3PosY)); } }
    [DataSourceProperty] public bool Slot3Current { get => GetCurrent(3); set { _slots[3].Current = value; OnPropertyChangedWithValue(value, nameof(Slot3Current)); } }
    [DataSourceProperty] public CharacterImageIdentifierVM? Slot3Portrait { get => GetPortrait(3); set { _slots[3].Portrait = value; OnPropertyChangedWithValue(value, nameof(Slot3Portrait)); } }

    [DataSourceProperty] public bool Slot4Visible { get => GetVisible(4); set => SetVisible(4, value); }
    [DataSourceProperty] public float Slot4PosX { get => GetPosX(4); set { _slots[4].PosX = value; OnPropertyChangedWithValue(value, nameof(Slot4PosX)); } }
    [DataSourceProperty] public float Slot4PosY { get => GetPosY(4); set { _slots[4].PosY = value; OnPropertyChangedWithValue(value, nameof(Slot4PosY)); } }
    [DataSourceProperty] public bool Slot4Current { get => GetCurrent(4); set { _slots[4].Current = value; OnPropertyChangedWithValue(value, nameof(Slot4Current)); } }
    [DataSourceProperty] public CharacterImageIdentifierVM? Slot4Portrait { get => GetPortrait(4); set { _slots[4].Portrait = value; OnPropertyChangedWithValue(value, nameof(Slot4Portrait)); } }

    [DataSourceProperty] public bool Slot5Visible { get => GetVisible(5); set => SetVisible(5, value); }
    [DataSourceProperty] public float Slot5PosX { get => GetPosX(5); set { _slots[5].PosX = value; OnPropertyChangedWithValue(value, nameof(Slot5PosX)); } }
    [DataSourceProperty] public float Slot5PosY { get => GetPosY(5); set { _slots[5].PosY = value; OnPropertyChangedWithValue(value, nameof(Slot5PosY)); } }
    [DataSourceProperty] public bool Slot5Current { get => GetCurrent(5); set { _slots[5].Current = value; OnPropertyChangedWithValue(value, nameof(Slot5Current)); } }
    [DataSourceProperty] public CharacterImageIdentifierVM? Slot5Portrait { get => GetPortrait(5); set { _slots[5].Portrait = value; OnPropertyChangedWithValue(value, nameof(Slot5Portrait)); } }

    [DataSourceProperty] public bool Slot6Visible { get => GetVisible(6); set => SetVisible(6, value); }
    [DataSourceProperty] public float Slot6PosX { get => GetPosX(6); set { _slots[6].PosX = value; OnPropertyChangedWithValue(value, nameof(Slot6PosX)); } }
    [DataSourceProperty] public float Slot6PosY { get => GetPosY(6); set { _slots[6].PosY = value; OnPropertyChangedWithValue(value, nameof(Slot6PosY)); } }
    [DataSourceProperty] public bool Slot6Current { get => GetCurrent(6); set { _slots[6].Current = value; OnPropertyChangedWithValue(value, nameof(Slot6Current)); } }
    [DataSourceProperty] public CharacterImageIdentifierVM? Slot6Portrait { get => GetPortrait(6); set { _slots[6].Portrait = value; OnPropertyChangedWithValue(value, nameof(Slot6Portrait)); } }

    [DataSourceProperty] public bool Slot7Visible { get => GetVisible(7); set => SetVisible(7, value); }
    [DataSourceProperty] public float Slot7PosX { get => GetPosX(7); set { _slots[7].PosX = value; OnPropertyChangedWithValue(value, nameof(Slot7PosX)); } }
    [DataSourceProperty] public float Slot7PosY { get => GetPosY(7); set { _slots[7].PosY = value; OnPropertyChangedWithValue(value, nameof(Slot7PosY)); } }
    [DataSourceProperty] public bool Slot7Current { get => GetCurrent(7); set { _slots[7].Current = value; OnPropertyChangedWithValue(value, nameof(Slot7Current)); } }
    [DataSourceProperty] public CharacterImageIdentifierVM? Slot7Portrait { get => GetPortrait(7); set { _slots[7].Portrait = value; OnPropertyChangedWithValue(value, nameof(Slot7Portrait)); } }

    private struct Slot
    {
        public bool Visible;
        public float PosX;
        public float PosY;
        public bool Current;
        public CharacterImageIdentifierVM? Portrait;
    }
}
