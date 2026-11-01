using FriendShapedDistributable;
using Godot;
using SecretPlanCore.Configuration;
using SecretPlanCore.Core;
using SecretPlanGodot.Core;

namespace FriendShapedPlugin.ConfigEditor;

public partial class ConfigInstanceTitleDisplay : Control
{
    private readonly CachedNode<Label> _infoLabel = new("InstanceInfo");
    private readonly CachedNode<Label> _nameLabel = new("InstanceName");
    private uint _instanceId;

    public void Initialize(Config instance)
    {
        _instanceId = instance.InstanceInfo.InstanceId;
        var instanceInfo = instance.InstanceInfo;
        
        Refresh(instanceInfo.InstanceId);
    }

    private void Refresh(uint uid)
    {
        if (_instanceId != uid)
        {
            return;
        }

        var instance = ConfigServer.Instance.GetInstanceUntyped(uid);

        if (instance != null)
        {
            var instanceInfo = instance.InstanceInfo;

            _nameLabel.Get(this).Text = instanceInfo.NameWithoutPathOrExtensionOrType();
            var infoText = $"{instanceInfo.TypeId} - {instanceInfo.InstanceId}";
            _infoLabel.Get(this).Text = infoText;
        }
        else
        {
            _nameLabel.Get(this).Text = _instanceId.ToString();
            _infoLabel.Get(this).Text = "???";
        }
    }

    public override void _EnterTree()
    {
        ConfigServer.Instance.EditorConfigChanged += OnConfigChanged;
    }

    public override void _ExitTree()
    {
        ConfigServer.Instance.EditorConfigChanged -= OnConfigChanged;
    }

    private void OnConfigChanged(uint id)
    {
        Refresh(id);
    }
}