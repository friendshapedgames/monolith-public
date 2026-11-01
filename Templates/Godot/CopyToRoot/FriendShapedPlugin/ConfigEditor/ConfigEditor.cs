using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BirdGame.Core;
using FriendShapedDistributable;
using Godot;
using SecretPlan.Generated;
using SecretPlanCore.Configuration;
using SecretPlanCore.Core;
using SecretPlanGodot.ConfigEditor;
using SecretPlanGodot.Configuration;
using SecretPlanGodot.Core;
using SecretPlanGodot.Serialization;

namespace FriendShapedPlugin.ConfigEditor;

public partial class ConfigEditor : Control
{
    private readonly CachedPackedScene<CommandSearchPopup> _commandPalette =
        new("res://FriendShapedPlugin/ConfigEditor/Scenes/SearchCommandPopup.tscn");

    private readonly CachedPackedScene<ConfigSearchPopup> _configSearchPopup =
        new("res://FriendShapedPlugin/ConfigEditor/Scenes/SearchConfigPopup.tscn");

    private readonly HashSet<uint> _dirtyUids = new();

    private readonly CachedPackedScene<NewConfigNamePopup> _nameNewConfigPopup =
        new("res://FriendShapedPlugin/ConfigEditor/Scenes/NewConfigNamePopup.tscn");

    private readonly CachedNode<Button> _openButton = new("Stack/Scroll/MainContainer/Margin/OpenButton");

    private readonly CachedPackedScene<ConfigInstancePane> _panePrefab =
        new("res://FriendShapedPlugin/ConfigEditor/Scenes/ConfigInstancePane.tscn");

    private readonly Dictionary<uint, ConfigInstancePane> _panesLookup = new();
    private readonly CachedNode<Control> _panesRoot = new("Stack/Scroll/MainContainer/Panes");
    private readonly ParentCore _parentCore = new();

    private readonly CachedNode<ScrollContainer> _scrollContainer = new("Stack/Scroll");
    private readonly InputEventKey _shiftTabInputEvent = new() { PhysicalKeycode = Key.Tab, ShiftPressed = true };

    private readonly CachedPackedScene<StringSearchPopup> _stringSearchPopup =
        new("res://FriendShapedPlugin/ConfigEditor/Scenes/SearchStringPopup.tscn");

    private readonly InputEventKey _tabInputEvent = new() { PhysicalKeycode = Key.Tab };

    private readonly Queue<Action> _workTasks = new();
    private float _flushTimer;

    private Button OpenButton => _openButton.Get(this);
    private CoreState CoreState => _parentCore.State(this);

    private Control PanesRoot => _panesRoot.Get(this);

    private ScrollContainer ScrollContainer => _scrollContainer.Get(this);

    public override void _Ready()
    {
        CoreState.MouseLock.BottomLevelSetting = Input.MouseModeEnum.Visible;
        _panesRoot.Get(this).QueueFreeAllChildren();
    }

    public override void _EnterTree()
    {
        OpenButton.Pressed += OpenButtonPressed;
        CoreState.NavigationSystem.SetGamepadSupport(false);

        InputMap.ActionAddEvent("ui_focus_next", _tabInputEvent);
        InputMap.ActionAddEvent("ui_focus_prev", _shiftTabInputEvent);
    }

    public override void _ExitTree()
    {
        OpenButton.Pressed -= OpenButtonPressed;
        CoreState.NavigationSystem.SetGamepadSupport(true);

        InputMap.ActionEraseEvent("ui_focus_next", _tabInputEvent);
        InputMap.ActionEraseEvent("ui_focus_prev", _shiftTabInputEvent);
    }

    private void OpenButtonPressed()
    {
        PromptOpen();
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (CoreState.PopupManager.IsPopupOpen)
        {
            return;
        }

        if (inputEvent is InputEventKey inputEventKey && inputEvent.IsPressed() &&
            inputEventKey.IsCommandOrControlPressed())
        {
            if (inputEventKey.KeyLabel == Key.O)
            {
                PromptOpen();
                GetViewport().SetInputAsHandled();
            }

            if (inputEventKey.KeyLabel == Key.N)
            {
                PromptNew();
                GetViewport().SetInputAsHandled();
            }

            if (inputEventKey.KeyLabel == Key.P)
            {
                PromptCommand();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void PromptNew()
    {
        CoreState.PopupManager
            .OpenPopup(_stringSearchPopup, this, null)
            .Initialize(
                PromptNameNewConfigOfType,
                string.Empty,
                FilteredConfigServer.AllTypeIds().ToList(),
                typeId =>
                {
                    if (string.IsNullOrWhiteSpace(typeId))
                    {
                        return string.Empty;
                    }

                    return ConfigServer.Instance.TypeFromId(typeId)?.Name ?? $"(unknown type: {typeId})";
                },
                false);
    }

    private void PromptCommand()
    {
        CoreState.PopupManager
            .OpenPopup(_commandPalette, this, null)
            .Initialize(
                RunCommand,
                CommandPaletteCommand.Empty,
                AllCommands().ToList(),
                command => command.Text,
                false);
    }

    private void RunCommand(CommandPaletteCommand command)
    {
        command.Action();
    }

    private IEnumerable<CommandPaletteCommand> AllCommands()
    {
        yield return new CommandPaletteCommand("Close All Panes", () =>
        {
            foreach (var pane in _panesLookup.Values.ToList())
            {
                if (pane.IsValidAndNotQueuedForDeletion())
                {
                    pane.QueueFree();
                }
            }
        });

        foreach (var typeId in FilteredConfigServer.AllTypeIds())
        {
            yield return new CommandPaletteCommand($"Open All {typeId} Instances", () =>
            {
                foreach (var instance in ConfigServer.Instance.GetAllInstances())
                {
                    if (instance.InstanceInfo.TypeId == typeId)
                    {
                        OpenPaneForConfig(instance.Uid());
                    }
                }
            });
        }
    }

    public void PromptDuplicateConfig(Config originalConfig)
    {
        CoreState.PopupManager
            .OpenPopup(_nameNewConfigPopup, this, null)
            .Initialize($"Name duplicate of {originalConfig.InstanceInfo.Name}", originalConfig.GetType(), this,
                newConfigName =>
                {
                    var instance = ConfigServer.Instance.Duplicate(originalConfig, newConfigName);

                    if (instance == null)
                    {
                        return;
                    }

                    OpenNewConfigInstance(instance);
                });
    }
    
    public void PromptRenameConfig(Config configToRename)
    {
        CoreState.PopupManager
            .OpenPopup(_nameNewConfigPopup, this, null)
            .Initialize($"Rename {configToRename.InstanceInfo.ShortName()}", configToRename.GetType(), this,
                newConfigName =>
                {
                    var fileSystem = configToRename.GetWritableFileSystem();
                    if (fileSystem == null)
                    {
                        if (GameConstants.IsEditor)
                        {
                            fileSystem = GameConstants.EditorProjectRoot;
                        }
                        else
                        {
                            return;
                        }
                    }

                    var success = ConfigServer.Instance.TryRenameInstance(configToRename, newConfigName, fileSystem);
                    if (success)
                    {
                        MarkDirty(configToRename.Uid());
                        if (GameConstants.IsEditor)
                        {
                            ConfigServer.Instance.WriteAllConfigsTxt(GameConstants.EditorProjectRoot);
                        }
                    }
                }, configToRename.InstanceInfo.NameWithoutPathOrExtensionOrType());
    }

    private void OpenNewConfigInstance(Config instance)
    {
        if (!GameConstants.IsEditor)
        {
            // Any new config created by the editor will be dropped in user://Mods
            instance.Origin =
                new ConfigOrigin(
                    CommonSerializationConstants.AppDataFiles.CreateDirectory("Mods"), true);
        }

        OpenPaneForConfig(instance.Uid());
        MarkDirty(instance.Uid());

        if (GameConstants.IsEditor)
        {
            ConfigServer.Instance.WriteAllConfigsTxt(GameConstants.EditorProjectRoot);
        }
    }

    public void PromptNameNewConfigOfType(string typeId)
    {
        var configType = ConfigServer.Instance.TypeFromId(typeId);

        if (configType != null)
        {
            CoreState.PopupManager
                .OpenPopup(_nameNewConfigPopup, this, null)
                .Initialize($"Name new {configType.Name} instance", configType, this, newText =>
                {
                    string? path = null;

                    if (GameConstants.IsEditor)
                    {
                        path = "Config";
                    }
                    
                    var instance = ConfigServer.Instance.CreateInstance(configType, newText, path);
                    if (instance == null)
                    {
                        return;
                    }

                    OpenNewConfigInstance(instance);
                });
        }
    }

    private void PromptOpen()
    {
        CoreState.PopupManager
            .OpenPopup(_configSearchPopup, this, null) // <-- no focus return
            .Initialize(
                uid => OpenPaneForConfig(uid),
                0,
                FilteredConfigServer.GetAllInstances().Select(a => a.Uid()).ToList(),
                uid => ConfigEditorConstants.UidToConfigName(uid, true),
                false);
    }

    public void OpenPaneForConfig(uint uid, int? childIndex = null)
    {
        var foundInstance = _panesLookup.GetValueOrDefault(uid);
        if (foundInstance.IsValidAndNotQueuedForDeletion())
        {
            ScrollContainer.EnsureControlVisible(foundInstance);
            return;
        }

        var paneInstance = _panePrefab.LoadAndInstantiate();

        var config = ConfigServer.Instance.GetInstanceUntyped(uid);

        if (config == null)
        {
            return;
        }

        _panesLookup[uid] = paneInstance;

        AddWork(() =>
        {
            PanesRoot.AddChild(paneInstance);
            if (childIndex.HasValue)
            {
                PanesRoot.MoveChild(paneInstance, childIndex.Value);
            }

            paneInstance.Initialize(config);
        });

        AddWork(() => { ScrollContainer.CallDeferred(ScrollContainer.MethodName.EnsureControlVisible, paneInstance); });
    }

    public override void _Process(double delta)
    {
        if (_workTasks.Count > 0)
        {
            var nextTask = _workTasks.Dequeue();
            nextTask();
        }

        _flushTimer -= (float)delta;
        if (_flushTimer < 0 && _dirtyUids.Count > 0)
        {
            LocalClient.Print($"Saving {_dirtyUids.Count} configs");
            _flushTimer = 1;

            foreach (var uid in _dirtyUids)
            {
                var config = ConfigServer.Instance.GetInstanceUntyped(uid);
                if (config != null)
                {
                    var fileSystem = config.GetWritableFileSystem();

                    if (fileSystem != null)
                    {
                        ConfigServer.Instance.WriteConfig(fileSystem, config);
                    }
                    else
                    {
                        LocalClient.Print(
                            $"{config.InstanceInfo.ShortName()} was NOT saved because it is readonly! Will still be edited for this run.");
                    }

                    if (_panesLookup.TryGetValue(uid, out var value) && value.IsValidAndNotQueuedForDeletion())
                    {
                        value.NotifyCleaned();
                    }
                }
                else
                {
                    LocalClient.Error($"Found edits on {uid} but ConfigServer does not have a config with that id");
                }
            }

            _dirtyUids.Clear();
        }
    }

    private void AddWork(Action action)
    {
        _workTasks.Enqueue(action);
    }

    public void MarkDirty(uint uid)
    {
        _dirtyUids.Add(uid);
    }

    public static FieldEditor CreateEditorForConfigField(ConfigField configField)
    {
        if (configField.IsArray)
        {
            var arrayEditor =
                GodotUtilities.InstantiateScene<ArrayEditor>("res://FriendShapedPlugin/ConfigEditor/Scenes/ArrayFieldEditor.tscn");

            arrayEditor.Initialize(configField);
            return arrayEditor;
        }

        if (ConfigEnumTypeChecker.IsConfigEnumType(configField.AssociatedType))
        {
            var configEditor =
                GodotUtilities.InstantiateScene<ConfigReferenceEditor>(
                    "res://FriendShapedPlugin/ConfigEditor/Scenes/ConfigReferenceLink.tscn");
            configEditor.Initialize(configField);
            return configEditor;
        }

        if (configField.AssociatedType.IsEnum)
        {
            var editor = GodotUtilities.InstantiateScene<EnumEditor>("res://FriendShapedPlugin/ConfigEditor/Scenes/EnumFieldEditor.tscn");
            editor.Initialize(configField);
            return editor;
        }

        if (NumberUtilities.TypeIsNumber(configField.AssociatedType))
        {
            var intEditor =
                GodotUtilities.InstantiateScene<NumberEditor>("res://FriendShapedPlugin/ConfigEditor/Scenes/NumberFieldEditor.tscn");
            intEditor.Initialize(configField);
            return intEditor;
        }

        if (configField.AssociatedType == typeof(string))
        {
            var scene = GodotUtilities.InstantiateScene<StringEditor>(
                "res://FriendShapedPlugin/ConfigEditor/Scenes/StringFieldEditor.tscn");
            scene.Initialize(configField);
            return scene;
        }

        if (configField.AssociatedType == typeof(bool))
        {
            var scene = GodotUtilities.InstantiateScene<BoolEditor>(
                "res://FriendShapedPlugin/ConfigEditor/Scenes/BoolFieldEditor.tscn");
            scene.Initialize(configField);
            return scene;
        }

        // Godot Resource References
        if (configField.AssociatedType == typeof(ResourceReference<Texture2D>))
                {
                    var textureEditor =
                        GodotUtilities.InstantiateScene<TextureReferenceEditor>(
                            "res://FriendShapedPlugin/ConfigEditor/Scenes/TextureReferenceEditor.tscn");
                    textureEditor.Initialize(configField);
                    return textureEditor;
            }

            if (configField.AssociatedType == typeof(ResourceReference<AudioStream>))
            {
                var audioStreamEditor =
                    GodotUtilities.InstantiateScene<AudioStreamReferenceEditor>(
                        "res://FriendShapedPlugin/ConfigEditor/Scenes/AudioStreamReferenceEditor.tscn");
                audioStreamEditor.Initialize(configField);
                return audioStreamEditor;
            }


        if (configField.AssociatedType.IsAssignableTo(typeof(IResourceReference)))
        {
            var anyResourceEditor =
                GodotUtilities.InstantiateScene<AnyResourceReferenceEditor>(
                    "res://FriendShapedPlugin/ConfigEditor/Scenes/AnyResourceReferenceEditor.tscn");
            anyResourceEditor.Initialize(configField);
            return anyResourceEditor;
        }

        var customEditor = configField.AssociatedType.GetCustomAttribute<CustomEditorAttribute>(true);
        if (customEditor != null)
        {
            var cachedPackedScene = customEditor.Scene.CachedPackedScene;
            var scene = cachedPackedScene.LoadAndInstantiate();
            if (scene is FieldEditor editor)
            {
                editor.Initialize(configField);
                return editor;
            }

            LocalClient.Error(
                $"Attempted to use custom editor {cachedPackedScene.Path} for {configField.AssociatedType} but could not cast the resulting scene to a {nameof(FieldEditor)}");

            // since we're not using this scene, free it before we forget about it
            scene.QueueFree();
        }

        // Fallback to ObjectEditor if there's nothing else
        var objectEditor = GodotUtilities.InstantiateScene<ObjectEditor>(
            "res://FriendShapedPlugin/ConfigEditor/Scenes/ObjectFieldEditor.tscn");
        objectEditor.Initialize(configField);
        return objectEditor;
    }

    public readonly record struct CommandPaletteCommand(string Text, Action Action)
    {
        public static readonly CommandPaletteCommand Empty = new(string.Empty, () => { });
    }
}


public static class ConfigExtensions
{
    public static bool IsWritable(this ConfigOrigin origin)
    {
        switch (origin.GetOriginType())
        {
            case ConfigOriginType.BaseGame:
                return GameConstants.IsEditor;
            case ConfigOriginType.LocalModsFolder:
                return true;
            case ConfigOriginType.ExternalWorkshop:
                return false;
        }

        return false;
    }

    public static IFileSystem? GetWritableFileSystem(this Config config)
    {
        if (config.Origin.IsWritable())
        {
            var fileSystem = config.Origin.SourceFileSystem;
            if(GameConstants.IsEditor && fileSystem == null)
            {
                return GameConstants.EditorProjectRoot;
            }
            return fileSystem;
        }

        return null;
    }
    
    public static ConfigOriginType GetOriginType(this ConfigOrigin origin)
    {
        if (origin.IsBaseGameConfig())
        {
            return ConfigOriginType.BaseGame;
        }

        if (origin.IsInLocalModsFolder)
        {
            return ConfigOriginType.LocalModsFolder;
        }

        return ConfigOriginType.ExternalWorkshop;
    }
}


public enum ConfigOriginType
{
    BaseGame,
    LocalModsFolder,
    ExternalWorkshop
}