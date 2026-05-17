using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using ShaderCad.Core.Attributes;
using ShaderCad.Core.Models;

namespace ShaderCad.App.ViewModels;

/// <summary>
/// 選択されたコンポーネントを解析し、プロパティエディタのリストを生成するViewModel
/// </summary>
public class InspectorViewModel : ViewModelBase
{
    public ObservableCollection<PropertyViewModelBase> Properties { get; } = new();

    private CadComponent? _targetComponent;
    public CadComponent? TargetComponent
    {
        get => _targetComponent;
        set
        {
            if (SetProperty(ref _targetComponent, value))
            {
                RebuildProperties();
            }
        }
    }

    private void RebuildProperties()
    {
        Properties.Clear();
        if (_targetComponent == null) return;

        var type = _targetComponent.GetType();
        
        // [CadParameter] 属性がついたプロパティをすべて取得
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Where(p => p.GetCustomAttribute<CadParameterAttribute>() != null);

        foreach (var p in props)
        {
            var attr = p.GetCustomAttribute<CadParameterAttribute>()!;
            
            // 型に応じた PropertyViewModel を生成（ポリモーフィズム）
            if (p.PropertyType == typeof(double))
            {
                Properties.Add(new DoublePropertyViewModel(_targetComponent, p, p.Name, attr.Min, attr.Max));
            }
            // 今後 string や Vector3 の対応を追加...
        }
    }
}
