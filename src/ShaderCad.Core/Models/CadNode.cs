using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ShaderCad.Core.Diagnostics;

namespace ShaderCad.Core.Models;

/// <summary>
/// CADの要素（パーツやアセンブリの単位）を表すエンティティクラス。
/// UnityのGameObjectに相当します。
/// </summary>
public class CadNode
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; set; } = "New Node";
    
    // 配置（ローカル座標系）
    public Matrix4x4 Transform { get; set; } = Matrix4x4.Identity;

    private readonly List<CadNode> _children = new();
    public IReadOnlyList<CadNode> Children => _children;

    private readonly List<CadComponent> _components = new();
    public IReadOnlyList<CadComponent> Components => _components;

    /// <summary>
    /// コンポーネントを追加します
    /// </summary>
    public T AddComponent<T>() where T : CadComponent, new()
    {
        var component = new T { Node = this };
        _components.Add(component);
        component.OnAttached();
        return component;
    }

    /// <summary>
    /// 指定された型のコンポーネントを取得します
    /// </summary>
    public T? GetComponent<T>() where T : CadComponent
    {
        return _components.OfType<T>().FirstOrDefault();
    }

    /// <summary>
    /// 自身および子ノードのバリデーションを実行します
    /// </summary>
    public CadResult ValidateAll()
    {
        var result = new CadResult();
        
        foreach (var comp in _components.Where(c => c.IsEnabled))
        {
            var compResult = comp.Validate();
            foreach (var diag in compResult.Diagnostics)
            {
                result.AddDiagnostic(diag);
            }
        }

        foreach (var child in _children)
        {
            var childResult = child.ValidateAll();
            foreach (var diag in childResult.Diagnostics)
            {
                result.AddDiagnostic(diag);
            }
        }

        return result;
    }
}
