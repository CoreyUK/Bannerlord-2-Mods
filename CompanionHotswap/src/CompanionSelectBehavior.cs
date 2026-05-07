using System;
using System.Collections.Generic;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.ImageIdentifiers;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.ScreenSystem;

namespace CompanionHotswap;

public sealed class CompanionSelectBehavior : MissionBehavior
{
    private const int TimeSlowId = 98765;
    private const float CanvasW = 1920f;
    private const float CanvasH = 1080f;
    private const float MarkerW = 62f;
    private const float MarkerH = 72f;
    private const float ClickRadiusSq = 0.0009f;

    private CompanionSwapBehavior? _swapBehavior;
    private bool _isMenuOpen;
    private int _menuOpenTicks;
    private GauntletLayer? _portraitLayer;
    private CompanionRosterVM? _portraitVM;
    private GauntletLayer? _markerLayer;
    private CompanionMarkersVM? _markerVM;
    private Mission.TimeSpeedRequest _timeSlow;
    private bool _timeSlowCreated;
    private MissionScreen? _missionScreen;

    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    public override void OnMissionTick(float dt)
    {
        try
        {
            if (Mission.Current == null)
            {
                return;
            }

            _swapBehavior ??= Mission.Current.GetMissionBehavior<CompanionSwapBehavior>();
            if (_swapBehavior == null)
            {
                return;
            }

            bool isMenuKeyDown = Input.IsKeyDown((InputKey)29) || Input.IsKeyDown((InputKey)157);
            if (isMenuKeyDown && !_isMenuOpen)
            {
                OpenMenu();
            }
            else if (!isMenuKeyDown && _isMenuOpen)
            {
                CloseMenu();
            }

            if (_isMenuOpen && _markerVM != null)
            {
                _menuOpenTicks++;
                if (_menuOpenTicks > 1)
                {
                    UpdateMarkers();
                    if (Input.IsKeyPressed((InputKey)224))
                    {
                        HandleMarkerClick();
                    }
                }
            }
        }
        catch
        {
        }
    }

    public override void OnRemoveBehavior()
    {
        if (_isMenuOpen)
        {
            CloseMenu();
        }

        base.OnRemoveBehavior();
    }

    private MissionScreen? GetMissionScreen()
    {
        if (_missionScreen != null)
        {
            return _missionScreen;
        }

        if (Mission.Current == null)
        {
            return null;
        }

        foreach (MissionBehavior behavior in Mission.Current.MissionBehaviors)
        {
            if (behavior is MissionView view && view.MissionScreen != null)
            {
                _missionScreen = view.MissionScreen;
                return _missionScreen;
            }
        }

        return null;
    }

    private void OpenMenu()
    {
        MissionScreen? screen = GetMissionScreen();
        if (screen == null || Mission.Current?.PlayerTeam == null || _swapBehavior == null)
        {
            return;
        }

        _isMenuOpen = true;
        _menuOpenTicks = 0;
        if (!_timeSlowCreated)
        {
            _timeSlow = new Mission.TimeSpeedRequest(0.15f, TimeSlowId);
            _timeSlowCreated = true;
        }

        Mission.Current.AddTimeSpeedRequest(_timeSlow);

        _portraitVM = new CompanionRosterVM();
        _portraitVM.Refresh(_swapBehavior.GetCompanionAgents(), Mission.Current.MainAgent, agent => _swapBehavior.SwapToAgent(agent));
        _portraitLayer = new GauntletLayer("CompanionSelect", 200, false);
        _portraitLayer.InputRestrictions.SetInputRestrictions(true, InputUsageMask.All);
        _portraitLayer.LoadMovie("CompanionSelect", _portraitVM);
        screen.AddLayer(_portraitLayer);

        _markerVM = new CompanionMarkersVM();
        List<Agent> agents = _swapBehavior.GetCompanionAgents();
        int count = Math.Min(agents.Count, 8);
        for (int i = 0; i < count; i++)
        {
            Agent agent = agents[i];
            if (agent?.Character == null)
            {
                continue;
            }

            try
            {
                _markerVM.SetPortrait(i, new CharacterImageIdentifierVM(CharacterCode.CreateFrom(agent.Character)));
            }
            catch
            {
            }
        }

        _markerLayer = new GauntletLayer("CompanionMarkers", 201, false);
        _markerLayer.LoadMovie("CompanionMarkers", _markerVM);
        screen.AddLayer(_markerLayer);
    }

    private void CloseMenu()
    {
        if (!_isMenuOpen)
        {
            return;
        }

        _isMenuOpen = false;
        _menuOpenTicks = 0;
        Mission.Current?.RemoveTimeSpeedRequest(TimeSlowId);

        MissionScreen? screen = GetMissionScreen();
        if (_portraitLayer != null)
        {
            _portraitLayer.InputRestrictions.ResetInputRestrictions();
            screen?.RemoveLayer(_portraitLayer);
            _portraitLayer = null;
        }

        _portraitVM = null;

        if (_markerLayer != null)
        {
            screen?.RemoveLayer(_markerLayer);
            _markerLayer = null;
        }

        _markerVM = null;
    }

    private void UpdateMarkers()
    {
        if (Mission.Current == null || _swapBehavior == null || _markerVM == null)
        {
            return;
        }

        List<Agent> agents = _swapBehavior.GetCompanionAgents();
        if (agents.Count == 0)
        {
            _markerVM.ClearSlots();
            return;
        }

        MissionScreen? screen = GetMissionScreen();
        Camera? camera = screen?.CombatCamera;
        if (camera == null)
        {
            return;
        }

        Agent? mainAgent = Mission.Current.MainAgent;
        float screenW = Screen.RealScreenResolutionWidth;
        float screenH = Screen.RealScreenResolutionHeight;
        float scaleX = screenW > 1f ? CanvasW / screenW : 1f;
        float scaleY = screenH > 1f ? CanvasH / screenH : 1f;
        int count = Math.Min(agents.Count, 8);
        int visibleMask = 0;

        for (int i = 0; i < count; i++)
        {
            Agent agent = agents[i];
            if (!agent.IsActive())
            {
                continue;
            }

            Vec3 world = agent.GetEyeGlobalPosition();
            world.z += 0.35f;
            float sx = 0f;
            float sy = 0f;
            float w = 0f;
            MBWindowManager.WorldToScreen(camera, world, ref sx, ref sy, ref w);
            if (w <= 0f)
            {
                continue;
            }

            int x = (int)(sx * scaleX - MarkerW * 0.5f);
            int y = (int)(sy * scaleY - MarkerH);
            x = Math.Max(0, Math.Min((int)(CanvasW - MarkerW), x));
            y = Math.Max(0, Math.Min((int)(CanvasH - MarkerH), y));
            _markerVM.SetSlot(i, x, y, agent == mainAgent);
            visibleMask |= 1 << i;
        }

        for (int i = 0; i < 8; i++)
        {
            if ((visibleMask & (1 << i)) == 0)
            {
                _markerVM.HideSlot(i);
            }
        }
    }

    private void HandleMarkerClick()
    {
        if (Mission.Current == null || _swapBehavior == null)
        {
            return;
        }

        List<Agent> agents = _swapBehavior.GetCompanionAgents();
        if (agents.Count == 0)
        {
            return;
        }

        Camera? camera = GetMissionScreen()?.CombatCamera;
        if (camera == null)
        {
            return;
        }

        float mouseX = Input.MousePositionRanged.X;
        float mouseY = Input.MousePositionRanged.Y;
        float screenW = Screen.RealScreenResolutionWidth;
        float screenH = Screen.RealScreenResolutionHeight;
        int count = Math.Min(agents.Count, 8);

        for (int i = 0; i < count; i++)
        {
            Agent agent = agents[i];
            if (!agent.IsActive())
            {
                continue;
            }

            Vec3 world = agent.GetEyeGlobalPosition();
            world.z += 0.35f;
            float sx = 0f;
            float sy = 0f;
            float w = 0f;
            MBWindowManager.WorldToScreen(camera, world, ref sx, ref sy, ref w);
            if (w <= 0f)
            {
                continue;
            }

            float normalizedX = screenW > 1f ? sx / screenW : sx;
            float normalizedY = screenH > 1f ? sy / screenH : sy;
            float dx = mouseX - normalizedX;
            float dy = mouseY - normalizedY;
            if (dx * dx + dy * dy < ClickRadiusSq)
            {
                _swapBehavior.SwapToAgent(agent);
                return;
            }
        }
    }
}
