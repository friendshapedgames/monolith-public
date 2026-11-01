using System;
using System.Collections.Generic;
using System.Linq;
using FriendShapedDistributable;
using Godot;
using SecretPlanGodot.ConfigEditor;
using SecretPlanGodot.Configuration;
using SecretPlanGodot.Core;

namespace FriendShapedPlugin.ConfigEditor;

public partial class ResourceReferenceLink : Control
{
    private readonly CachedNode<Button> _button = new("SizeProxy/Button");
    private readonly ParentCore _parentCore = new();
    private readonly CachedNode<Label> _pathLabel = new("SizeProxy/SizeProxyDriver/PathLabel");

    private readonly CachedPackedScene<ResourceSearchPopup> _popup =
        new("res://FriendShapedPlugin/ConfigEditor/Scenes/SearchResourcePopup.tscn");

    private ConfigField? _field;
    private Action? _onChanged;
    private Type? _resourceReferenceType;

    public Button Button => _button.Get(this);
    private CoreState CoreState => _parentCore.State(this);

    public void Initialize(ConfigField field, Type resourceReferenceType, Action onChanged)
    {
        _field = field;
        _resourceReferenceType = resourceReferenceType;
        UpdateDisplay();
        _onChanged = onChanged;
    }

    private void UpdateDisplay()
    {
        var (path, status) = GetResourcePathAndStatus();

        var labelText = path;
        
        
        _pathLabel.Get(this).RemoveThemeColorOverride("font_color");
        if (status != ResourceStatus.Ok)
        {
            _pathLabel.Get(this).AddThemeColorOverride("font_color", Colors.Pink);
        }

        if (status == ResourceStatus.PathDoesNotResolve)
        {
            labelText += " (missing!)";
        }
        
        _pathLabel.Get(this).Text = labelText;
        
        _onChanged?.Invoke();
    }

    private enum ResourceStatus
    {
        Ok = 0,
        NotInitialized = 1,
        PathDoesNotResolve = 2,
    }
    
    private (string, ResourceStatus) GetResourcePathAndStatus()
    {
        if (_field == null || _resourceReferenceType == null)
        {
            LocalClient.Error("Resource Reference field was not initialized");
            return ("???", ResourceStatus.NotInitialized);
        }

        var value = _field.GetValue() as IResourceReference ??
                    ResourceReferenceUtilities.CreateResourceReference(_resourceReferenceType, "");

        var path = value.Path;
        
        if (string.IsNullOrWhiteSpace(path))
        {
            path = ConfigEditorConstants.EmptyFieldText;
        }
        else
        {
            if (!SecretResourceLoader.ExistsInAnyContext(path))
            {
                return (path, ResourceStatus.PathDoesNotResolve);
            }
        }

        return (path, ResourceStatus.Ok);
    }

    public override void _EnterTree()
    {
        Button.Pressed += OnButtonPressed;
    }

    public override void _ExitTree()
    {
        Button.Pressed -= OnButtonPressed;
    }

    private void OnButtonPressed()
    {
        if (_field == null)
        {
            return;
        }

        if (_resourceReferenceType == null)
        {
            return;
        }

        var currentPath = (_field.GetValue() as IResourceReference)?.Path ?? string.Empty;

        List<string> allPossibleResults = [];

        var extensions = ResourceReferenceUtilities.GetSupportedFileExtensions(_resourceReferenceType).ToArray();

        foreach (var realPath in GodotUtilities.GetAndCacheAllResFiles(null))
        {
            if (ShouldSkipPath(realPath))
            {
                continue;
            }

            var hasValidExtension = false;
            foreach (var extension in extensions)
            {
                if (realPath.EndsWith(extension))
                {
                    hasValidExtension = true;
                    break;
                }
            }

            if (hasValidExtension)
            {
                if (!realPath.Contains("://"))
            {
                var resPath = $"res://{realPath}";
                allPossibleResults.Add(resPath);
                }
                else
                {
                    allPossibleResults.Add(realPath);
                }
            }
        }

        CoreState.PopupManager.OpenPopup(_popup, this, Button)
            .Initialize(OnSelected, currentPath, allPossibleResults, a => a, true);
    }

    private static bool ShouldSkipPath(string realPath)
    {
        if (realPath.StartsWith(".godot"))
        {
            return true;
        }

        if (realPath.StartsWith("addons"))
        {
            return true;
        }

        if (realPath.EndsWith(".uid"))
        {
            return true;
        }

        if (realPath.EndsWith(".import"))
        {
            return true;
        }

        if (realPath.EndsWith(".cs"))
        {
            return true;
        }

        if (realPath.EndsWith(".gd"))
        {
            return true;
        }

        return false;
    }

    private void OnSelected(string newValue)
    {
        if (_field == null)
        {
            return;
        }

        if (_resourceReferenceType == null)
        {
            return;
        }

        _field.SetValue(ResourceReferenceUtilities.CreateResourceReference(_resourceReferenceType, newValue));
        UpdateDisplay();
    }
}