using System;
using System.Collections.Generic;
using BirdGame.UI;
using DATA_ASSEMBLY.Distributable;
using DATA_ASSEMBLY.DistributableConfig;
using FriendShapedPlugin;
using Godot;
using SecretPlan.Generated;
using SecretPlanCore.Configuration;
using SecretPlanCore.Core;
using SecretPlanGodot.Core;
using SecretPlanGodot.Navigation;
using SecretPlanGodot.Serialization;
using SecretPlanGodot.Testing;
using ClientIdDisplay = FriendShapedPlugin.ClientIdDisplay;
using ConfirmModal = FriendShapedPlugin.ConfirmModal;

namespace FriendShapedDistributable;

public partial class GameCore : Node, ICoreStateProvider
{
    private readonly CachedNode<AudioStreamPlayer> _buttonClickSoundPlayer = new("ClickSound");
    private readonly CachedNode<AudioStreamPlayer> _buttonRolloverSoundPlayer = new("RolloverSound");
    private readonly CachedPackedScene<ConfirmModal> _confirmModal = new("res://Scenes/UI/ConfirmModal.tscn");
    private readonly CachedNode<Node> _currentScene = new("CurrentScene");

    private readonly CachedPackedScene<Node> _firstScene =
        new("res://FriendShapedDistributable/Scenes/StartupScene.tscn");

    private readonly CachedNode<FocusSink> _focusSink = new("FocusSink", () => new FocusSink());
    private readonly CachedNode<LoadingScrim> _loadingScrim = new("LoadingScrim");
    private readonly CachedNode<SceneRoot> _sceneRoot = new("CurrentScene");
    private Vector2 _currentMouseDelta;
    private readonly List<Action> _onDestroy = new();
    private float _saveTimer;
    private Node CurrentSceneRoot => _currentScene.Get(this);
    private LoadingScrim LoadingScrim => _loadingScrim.Get(this);
    public CoreState CoreState { get; } = new();

    public override void _Ready()
    {
        OS.AddLogger(new SecretLogger());

        LocalClient.LogInit();

        // create debug UI
        AddChild(new ClientIdDisplay());
        AddChild(new FramerateDisplay());

        // read config table
        CommonSerializationConstants.PopulateConfigServer(true);

        LocalClient.Print("Running preload steps");
        ConfigServer.Instance.DoPreload();
        LocalClient.Print("Done with preload (some might be running in the background)");

#if DEBUG
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            TestHelpers.RunAllTestsInAssembly(assembly);
        }
#endif

        // setup settings
        var settings = CoreState.SerializedState.Settings;
        settings.ValueChanged += OnSettingChanged;
        settings.InvokeChangedForAllSettings();

        // setup own interconnections
        CoreState.LoadingStatus.Changed += UpdateLoadingScrim;
        CoreState.SerializedState.Settings.SaveDataActionRequested += SaveDataActionData;
        CoreState.SerializedState.Settings.GetLogsRequested += CoreState.GetLogs;
        CoreState.SerializedState.Settings.OpenSaveDataRequested += CoreState.OpenSaveData;
        CoreState.LoadSceneRequested += (cachedPackedScene, action) =>
            _sceneRoot.Get(this).LoadScene(cachedPackedScene, action);

        // godot connections
        GetWindow().FocusEntered += OnWindowGainedFocus;
        GetWindow().FocusExited += OnWindowLostFocus;
        GetViewport().GuiFocusChanged += OnFocusedControlChanged;

        // initial cleanup
        LoadingScrim.Visible = false;
        UpdateLoadingScrim();
        ClearCurrentScene();
        CurrentSceneRoot.CallDeferred(Node.MethodName.AddChild, _firstScene.LoadAndInstantiate());

        // navigation
        CoreState.NavigationSystem.SetGamepadSupport(true);
        var flexibleUiNavigator = (CoreState.NavigationSystem.UiNavigator as FlexibleUiNavigator)!;
        CoreState.NavigationSystem.FocusState.Setup(new BoolProvider(() => CoreState.Debug.LogUiNavigation),
            GetViewport());
        flexibleUiNavigator.Setup(new BoolProvider(() => CoreState.Debug.LogUiNavigation), GetViewport(),
            () => _buttonRolloverSoundPlayer.Get(this).Play());
        CoreState.NavigationSystem.FocusState.SetNavigationOwner(_focusSink.Get(this));

        CoreState.SerializedState.Settings.AllowConsole = true;

        // var callable = ControllerIconsBridge.ConnectInputTypeChanged(OnInputChanged);
        // _onDestroy.Add(() => { ControllerIconsBridge.DisconnectInputTypeChanged(callable); });

        GameSpecificStuff();
    }

    private void OnInputChanged(ControllerIconsBridge.InputType inputType, int controllerIndex)
    {
        LocalClient.Print("Input changed: " + inputType + " " + controllerIndex);
        // todo: do something with CoreState.NavigationSystem.CurrentNavigationMode 
    }

    private void GameSpecificStuff()
    {
        
    }

    public override void _ExitTree()
    {
        foreach (var item in _onDestroy)
        {
            item.Invoke();
        }
        _onDestroy.Clear();
        
        foreach (var config in ConfigServer.Instance.GetAllInstancesOfType<IConfigWithExtraResource>())
        {
            foreach (var resource in config.GetOwnedResources())
            {
                resource.Dispose();
            }
        }
    }

    private void OnWindowLostFocus()
    {
        CoreState.IsGameInFocus = false;
        OnWindowFocusChanged();
    }

    private void OnWindowGainedFocus()
    {
        CoreState.IsGameInFocus = true;
        OnWindowFocusChanged();
    }

    private void ClearCurrentScene()
    {
        foreach (var child in CurrentSceneRoot.GetChildren())
        {
            child.QueueFree();
        }
    }

    private void SaveDataActionData(Action<SaveFile> action, LocalizationTableIds message)
    {
        CoreState.PopupManager.OpenPopup(_confirmModal, null, null).Initialize(
            CoreState.TranslatedFromId(message), false,
            () => { action(CoreState.SerializedState.SaveFile); });
    }

    private void UpdateLoadingScrim()
    {
        LoadingScrim.SetState(CoreState.LoadingStatus.ShouldShowLoadingScrim);
    }

    private void OnFocusedControlChanged(Control newlyFocusedControl)
    {
        if (CoreState.Debug.LogUiNavigation)
        {
            LocalClient.Print($"NAVI: Godot focus {newlyFocusedControl.Name}");
        }

        CoreState.NavigationSystem.UiNavigator.OnFocusChanged(newlyFocusedControl);
    }

    private void OnSettingChanged(string settingName)
    {
    }

    private void OnWindowFocusChanged()
    {
        CoreState.MouseLock.OnChanged();
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (inputEvent is InputEventJoypadButton or InputEventJoypadMotion)
        {
            CoreState.NavigationSystem.SetNavigationMode(NavigationMode.Gamepad);
        }

        var isNavigation = inputEvent.IsActionPressed(StringNameCache.UiUp) ||
                           inputEvent.IsActionPressed(StringNameCache.UiDown) ||
                           inputEvent.IsActionPressed(StringNameCache.UiLeft) ||
                           inputEvent.IsActionPressed(StringNameCache.UiRight)
                           || inputEvent.IsActionPressed(StringNameCache.UiAccept);

        if (inputEvent is InputEventKey key && isNavigation)
        {
            CoreState.NavigationSystem.SetNavigationMode(NavigationMode.Keyboard);
        }

        if (inputEvent is InputEventMouseButton mouseButton)
        {
            var hoveredControl = GetViewport().GuiGetHoveredControl();
            var shouldPlaySound = hoveredControl.IsValidAndNotQueuedForDeletion() &&
                                  (hoveredControl.FocusMode != Control.FocusModeEnum.None || hoveredControl is Button)
                                  && !CoreState.MouseLock.IsMouseLocked();

            if (mouseButton.ButtonIndex == MouseButton.Left && !inputEvent.IsPressed() && shouldPlaySound)
            {
                _buttonClickSoundPlayer.Get(this).Play();
            }
        }
        else
        {
            var focusOwner = GetViewport().GuiGetFocusOwner();
            if (inputEvent.IsActionPressed(StringNameCache.UiAccept) && focusOwner != null)
            {
                if (focusOwner is not FocusSink)
                {
                    _buttonClickSoundPlayer.Get(this).Play();
                }
            }
        }

        if (inputEvent is InputEventMouseMotion mouseMotionEvent)
        {
            if (Input.MouseMode == Input.MouseModeEnum.Captured)
            {
                _currentMouseDelta = mouseMotionEvent.Relative;
            }
            else
            {
                // we only set input mode to mouse if the mouse is not captured because otherwise we'd be thrashing between the two all the time during gameplay.
                CoreState.NavigationSystem.SetNavigationMode(NavigationMode.Mouse);
            }
        }
    }

    public override void _Process(double delta)
    {
        if (CoreState.SerializedState.Settings.GarbageCollectEveryFrame)
        {
            GC.Collect();
        }

        CoreState.NavigationSystem.UiNavigator.UpdateHoverAndFocus(CoreState.NavigationSystem.CurrentNavigationMode);

        if (CoreState.SerializedState.SaveFileDirty && _saveTimer < 0f)
        {
            CoreState.SerializedState.SaveSaveFile();
            _saveTimer = 5f;
        }

        if (!GetWindow().HasFocus())
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
        else
        {
            var mouseLockState = CoreState.MouseLock.DesiredState;

            if (CoreState.NavigationSystem.CurrentNavigationMode == NavigationMode.Mouse)
            {
                Input.MouseMode = mouseLockState;
            }
            else
            {
                if (mouseLockState == Input.MouseModeEnum.Visible)
                {
                    Input.MouseMode = Input.MouseModeEnum.Hidden;
                }
            }
        }
    }
}