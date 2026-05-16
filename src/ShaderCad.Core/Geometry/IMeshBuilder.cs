using System.Numerics;

namespace ShaderCad.Core.Geometry;

/// <summary>
/// Core層のコンポーネントが、Kernel層（geometry3Sharp等）に対して形状生成を依頼するためのインターフェース。
/// （Builderパターン）
/// </summary>
public interface IMeshBuilder
{
    /// <summary>
    /// 球体を追加します。
    /// </summary>
    void AddSphere(double radius, Vector3 center);

    /// <summary>
    /// 立方体（直方体）を追加します。
    /// </summary>
    void AddCube(Vector3 size, Vector3 center);

    // 将来的に AddCylinder, AddExtrude などのメソッドを拡張します
}
