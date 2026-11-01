using System;
using Godot;
using SecretPlanGodot.Core;

namespace FriendShapedDistributable;

/// <summary>
///     Hand-written C# bridge to the Controller Icons GDScript autoload (addons/controller_icons/ControllerIcons.gd).
///     Only wraps the public API we actually use. Calls go through Godot's Call() since the plugin isn't in ClassDB.
/// </summary>
public static class ControllerIconsBridge
{
    // Mirrors ControllerIcons.gd's InputType enum
    public enum InputType
    {
        KeyboardMouse = 0,
        Controller = 1
    }

    // Mirrors ControllerSettings.gd's Devices enum
    public enum Device
    {
        None = -1,
        Luna,
        Ouya,
        Ps3,
        Ps4,
        Ps5,
        Stadia,
        Steam,
        Switch,
        Joycon,
        Xbox360,
        XboxOne,
        XboxSeries,
        SteamDeck
    }

    public static readonly StringName InputTypeChangedSignal = new("input_type_changed");

    private static readonly StringName ParsePathMethod = new("parse_path");
    private static readonly StringName ParseEventMethod = new("parse_event");
    private static readonly StringName GetLastInputTypeMethod = new("get_last_input_type");
    private static readonly StringName GetJoypadTypeMethod = new("get_joypad_type");
    private static readonly StringName IconTexturePathProperty = new("path");

    private static Node? _instance;
    private static GDScript? _iconTextureScript;

    private static GDScript IconTextureScript => _iconTextureScript ??=
        SecretResourceLoader.LoadTypedConfident<GDScript>("res://addons/controller_icons/objects/ControllerIconTexture.gd");

    /// <summary>
    ///     Creates a ControllerIconTexture for an input action (eg: "flip") or joypad/key path. The texture listens to the
    ///     ControllerIcons autoload itself, so it swaps icons automatically when the player switches devices.
    ///     Assign it anywhere a Texture2D goes (TextureRect.Texture, Button.Icon, Sprite2D.Texture, etc.)
    /// </summary>
    public static Texture2D CreateIconTexture(StringName path)
    {
        var texture = IconTextureScript.New().As<Texture2D>();
        // Setting path triggers the texture's own reload, no manual refresh needed
        texture.Set(IconTexturePathProperty, path);
        return texture;
    }

    /// <summary>
    ///     Re-point an existing ControllerIconTexture (eg: one created via CreateIconTexture, or authored in a scene)
    /// </summary>
    public static void SetIconTexturePath(Texture2D iconTexture, StringName path)
    {
        iconTexture.Set(IconTexturePathProperty, path);
    }

    /// <summary>
    ///     The live autoload node. Re-resolved if it was freed (eg: between editor play sessions).
    /// </summary>
    public static Node Instance
    {
        get
        {
            if (_instance == null || !GodotObject.IsInstanceValid(_instance))
            {
                var tree = (SceneTree)Engine.GetMainLoop();
                _instance = tree.Root.GetNode(StringNameCache.ControllerIcons.ToString());
            }

            return _instance;
        }
    }

    public static InputType LastInputType => (InputType)Instance.Call(GetLastInputTypeMethod).AsInt32();

    /// <summary>
    ///     Icon for an input action (eg: "ui_accept") or joypad/key path (eg: "joypad/a", "key/space"),
    ///     using the last input type and controller. Null if no icon matches.
    /// </summary>
    public static Texture2D? ParsePath(string path)
    {
        // Only pass the path so the GDScript defaults (last input type / last controller) kick in
        return Instance.Call(ParsePathMethod, path).AsGodotObject() as Texture2D;
    }

    public static Texture2D? ParseEvent(InputEvent inputEvent)
    {
        return Instance.Call(ParseEventMethod, inputEvent).AsGodotObject() as Texture2D;
    }

    /// <summary>
    ///     Controller family of the last-used controller (or a specific device index)
    /// </summary>
    public static Device GetJoypadType(int? controller = null)
    {
        var result = controller.HasValue
            ? Instance.Call(GetJoypadTypeMethod, controller.Value)
            : Instance.Call(GetJoypadTypeMethod);
        return (Device)result.AsInt32();
    }

    /// <summary>
    ///     Subscribe to input type changes (keyboard/mouse vs controller).
    ///     Keep the returned Callable and pass it to DisconnectInputTypeChanged when the listener goes away.
    /// </summary>
    public static Callable ConnectInputTypeChanged(Action<InputType, int> handler)
    {
        var callable = Callable.From((int inputType, int controller) => handler((InputType)inputType, controller));
        Instance.Connect(InputTypeChangedSignal, callable);
        return callable;
    }

    public static void DisconnectInputTypeChanged(Callable callable)
    {
        if (Instance.IsConnected(InputTypeChangedSignal, callable))
        {
            Instance.Disconnect(InputTypeChangedSignal, callable);
        }
    }
}
