using System;
using System.Linq;
using BirdGame.Core;
using FriendShapedDistributable;
using Godot;
using SecretPlan.Generated;
using SecretPlanCore.Configuration;
using SecretPlanGodot.ConfigEditor;
using SecretPlanGodot.Core;

namespace FriendShapedPlugin.ConfigEditor;

public partial class ConfigReferenceEditor : FieldEditor
{
    private readonly CachedAncestor<ConfigEditor> _editor = new();
    private readonly CachedNode<Button> _exploreButton = new("ExploreButton");
    private readonly CachedNode<Label> _label = new("SizeProxy/SizeProxyDriver/PathLabel");
    private readonly CachedNode<Button> _linkButton = new("SizeProxy/Button");
    private readonly CachedAncestor<ConfigInstancePane> _pane = new();

    private readonly ParentCore _parentCore = new();

    private readonly CachedPackedScene<ConfigSearchPopup> _popup =
        new("res://FriendShapedPlugin/ConfigEditor/Scenes/SearchConfigPopup.tscn");

    private ConfigField? _field;

    private Label Label => _label.Get(this);
    private Button LinkButton => _linkButton.Get(this);
    private CoreState CoreState => _parentCore.State(this);
    private Button ExploreButton => _exploreButton.Get(this);

    public override void Initialize(ConfigField field)
    {
        _field = field;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        var configId = LinkedConfigId;

        var instanceUntyped = ConfigServer.Instance.GetInstanceUntyped(configId);
        var instanceInfo = instanceUntyped?.InstanceInfo;

        if (instanceInfo.HasValue)
        {
            Label.Text = instanceInfo.Value.Name;
        }
        else
        {
            if (configId == 0)
            {
                Label.Text = ConfigEditorConstants.EmptyFieldText;
            }
            else
            {
                Label.Text = $"UNKNOWN: {configId}";
            }
        }

        var isTypeHidden = false;
        if (_field != null)
        {
            var configType = ConfigEnumTypeChecker.GetConfigTypeFromEnumType(_field.AssociatedType);
            isTypeHidden = configType == null || FilteredConfigServer.IsTypeHidden(configType);
        }

        LinkButton.Disabled = isTypeHidden;
        ExploreButton.Disabled = isTypeHidden || !instanceInfo.HasValue;
    }

    private uint LinkedConfigId
    {
        get
        {
            var value = _field?.GetValue();

            uint configId = 0;

            if (value != null)
            {
                configId = (uint)value;
            }

            return configId;
        }
    }

    public override void _EnterTree()
    {
        LinkButton.Pressed += LinkButtonPressed;
        ExploreButton.Pressed += ExploreButtonPressed;
        ConfigServer.Instance.EditorConfigChanged += OnConfigChanged;
    }

    public override void _ExitTree()
    {
        LinkButton.Pressed -= LinkButtonPressed;
        ExploreButton.Pressed -= ExploreButtonPressed;
        ConfigServer.Instance.EditorConfigChanged -= OnConfigChanged;
    }

    private void OnConfigChanged(uint uid)
    {
        if (LinkedConfigId == 0)
        {
            return;
        }

        if (uid == LinkedConfigId)
        {
            UpdateDisplay();
        }
    }

    private void ExploreButtonPressed()
    {
        if (_field == null)
        {
            return;
        }

        var value = _field.GetValue();
        if (value == null)
        {
            return;
        }

        var parentPane = _pane.GetOrNull(this);
        var childIndex = parentPane?.GetIndex() + 1;
        _editor.GetOrNull(this)?.OpenPaneForConfig((uint)value, childIndex);
    }

    private void LinkButtonPressed()
    {
        if (_field == null)
        {
            return;
        }

        var value = _field.GetValue();
        if (value == null)
        {
            return;
        }

        var startingUid = (uint)value;

        var configType = ConfigEnumTypeChecker.GetConfigTypeFromEnumType(_field.AssociatedType);

        if (configType != null)
        {
            var typeId = ConfigServer.Instance.TypeIdFromType(configType);
            var allPossibleResults = ConfigServer.Instance.GetAllInstances()
                .Where(config => ConfigServer.Instance.IsTypeIdAssignableTo(typeId, config.InstanceInfo.TypeId))
                .Select(a => a.Uid()).ToList();

            CoreState.PopupManager.OpenPopup(_popup, this, LinkButton)
                .Initialize(OnSelected, startingUid, allPossibleResults,
                    uid => ConfigEditorConstants.UidToConfigName(uid, false), true);
        }
    }

    private void OnSelected(uint configId)
    {
        if (_field == null)
        {
            return;
        }

        _field.SetValue(Enum.ToObject(_field.AssociatedType, configId));
        UpdateDisplay();
    }
}