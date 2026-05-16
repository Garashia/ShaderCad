using System;
using ShaderCad.Core.Diagnostics;
using ShaderCad.Core.Geometry;

namespace ShaderCad.Core.Models;

/// <summary>
/// すべてのCAD機能（形状定義、拘束、属性）の基底クラス。
/// UnityのMonoBehaviourに相当します。
/// </summary>
public abstract class CadComponent
{
    /// <summary>
    /// このコンポーネントがアタッチされているノード
    /// </summary>
    public CadNode Node { get; internal set; } = null!;

    /// <summary>
    /// コンポーネントが有効かどうか
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// ノードにアタッチされた際に呼ばれます
    /// </summary>
    public virtual void OnAttached() { }

    /// <summary>
    /// ノードからデタッチされた際に呼ばれます
    /// </summary>
    public virtual void OnDetached() { }

    /// <summary>
    /// コンポーネントの妥当性を検証します
    /// </summary>
    public virtual CadResult Validate() => CadResult.Success();

    /// <summary>
    /// ジオメトリビルダーを用いて、自身の形状を構築します
    /// </summary>
    public virtual void BuildGeometry(IMeshBuilder builder) { }

    // 将来的に IGizmoRenderer などのインターフェースを引数に取る DrawGizmos メソッドを追加予定
    // public virtual void DrawGizmos(IGizmoRenderer renderer) {}
}
