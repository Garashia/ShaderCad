using System.Numerics;

namespace ShaderCad.Core.Diagnostics;

/// <summary>
/// CADカーネルやコンポーネントが、デバッグ目的で画面上に線や点を描画するためのインターフェース。
/// 実際の描画処理（OpenGL呼び出しなど）は ShaderCad.Renderer 側で実装します。
/// </summary>
public interface IGizmoRenderer
{
    /// <summary>
    /// 指定された色で線分を描画します。
    /// </summary>
    void DrawLine(Vector3 start, Vector3 end, Vector4 color);

    /// <summary>
    /// 指定された色で点を描画します。
    /// </summary>
    void DrawPoint(Vector3 position, Vector4 color, float size = 1.0f);

    /// <summary>
    /// 指定されたバウンディングボックス（AABB）のワイヤーフレームを描画します。
    /// </summary>
    void DrawWireCube(Vector3 center, Vector3 size, Vector4 color);

    /// <summary>
    /// 現在のフレームに描画されたGizmosをクリアします。
    /// </summary>
    void Clear();
}
