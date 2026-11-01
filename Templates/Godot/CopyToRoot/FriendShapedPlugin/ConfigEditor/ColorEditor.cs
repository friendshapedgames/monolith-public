using Godot;
using SecretPlanGodot.ConfigEditor;
using SecretPlanGodot.Configuration;
using SecretPlanGodot.Core;

namespace FriendShapedPlugin.ConfigEditor;

public partial class ColorEditor : FieldEditor
{
    private readonly CachedNode<ColorPicker> _colorPicker = new("VBoxContainer/ColorPicker");
    private readonly CachedNode<ColorRect> _colorRect = new("ColorRect");
    private readonly CachedNode<HSlider> _alphaSlider = new("VBoxContainer/AlphaSlider/HSlider");
    private readonly CachedNode<Control> _sliderRoot = new("VBoxContainer/AlphaSlider");
    private ConfigField? _configField;

    public override void Initialize(ConfigField configField)
    {
        _configField = configField;

        if (_configField.GetValue() is not ISerializedColor color)
        {
            return;
        }

        _colorPicker.Get(this).Color = color.ToColor();
        _alphaSlider.Get(this).Value = color.ToColor().A;
        Refresh();

        _colorPicker.Get(this).ColorChanged += ColorChanged;
        _alphaSlider.Get(this).ValueChanged += AlphaSliderChanged;

        if (configField.GetValue() is SerializedColorRgba)
        {
            _sliderRoot.Get(this).Visible = true;
            _colorPicker.Get(this).EditAlpha = true;
        }
        
        if (configField.GetValue() is SerializedColorRgb)
        {
            _sliderRoot.Get(this).Visible = false;
            _colorPicker.Get(this).EditAlpha = false;
        }
    }

    private void AlphaSliderChanged(double value)
    {
        ColorChanged(_colorPicker.Get(this).Color);
    }

    private void ColorChanged(Color color)
    {
        if (_configField == null)
        {
            return;
        }

        color.A = (float)_alphaSlider.Get(this).Value;

        if (_configField.GetValue() is SerializedColorRgba)
        {
            _configField.SetValue(SerializedColorRgba.FromColor(color));
        }

        if (_configField.GetValue() is SerializedColorRgb)
        {
            _configField.SetValue(SerializedColorRgb.FromColor(color));
        }

        Refresh();
    }

    private void Refresh()
    {
        if (_configField == null)
        {
            LocalClient.Error("Color Editor was not initialized");
            return;
        }

        if (_configField.GetValue() is not ISerializedColor color)
        {
            return;
        }

        _colorRect.Get(this).Color = color.ToColor();
    }
}