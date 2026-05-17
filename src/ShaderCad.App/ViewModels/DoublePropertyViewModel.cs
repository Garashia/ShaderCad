using System;
using System.Reflection;

namespace ShaderCad.App.ViewModels;

/// <summary>
/// double型のプロパティを編集するためのViewModel
/// リフレクションを用いて実際のオブジェクトの値を読み書きします
/// </summary>
public class DoublePropertyViewModel : PropertyViewModelBase
{
    private readonly object _target;
    private readonly PropertyInfo _propertyInfo;

    public double Min { get; }
    public double Max { get; }

    public double Value
    {
        get => (double)_propertyInfo.GetValue(_target)!;
        set
        {
            // バリデーション（Min/Max）を適用
            var clamped = Math.Clamp(value, Min, Max);
            _propertyInfo.SetValue(_target, clamped);
            OnPropertyChanged();
        }
    }

    public DoublePropertyViewModel(object target, PropertyInfo propertyInfo, string displayName, double min, double max) 
        : base(displayName)
    {
        _target = target;
        _propertyInfo = propertyInfo;
        Min = min;
        Max = max;
    }
}
