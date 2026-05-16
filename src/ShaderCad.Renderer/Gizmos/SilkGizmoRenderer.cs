using System.Numerics;
using ShaderCad.Core.Diagnostics;
using Silk.NET.OpenGL;

namespace ShaderCad.Renderer.Gizmos;

/// <summary>
/// Silk.NET (OpenGL) を用いて、交差判定やデバッグ用の線分を描画するエンジン。
/// Core層の IGizmoRenderer インターフェースを実装します。
/// </summary>
public class SilkGizmoRenderer : IGizmoRenderer
{
    private readonly GL _gl;

    public SilkGizmoRenderer(GL gl)
    {
        _gl = gl;
    }

    public void DrawLine(Vector3 start, Vector3 end, Vector4 color)
    {
        // TODO: ShaderとVBO/VAOを用いたモダンなライン描画処理を実装する
        // （今回はアーキテクチャの骨組みとして定義のみ）
    }

    public void DrawPoint(Vector3 position, Vector4 color, float size = 1.0f)
    {
        // TODO: 点の描画処理
    }

    public void DrawWireCube(Vector3 center, Vector3 size, Vector4 color)
    {
        // TODO: ワイヤーフレームキューブの描画処理
    }

    public void Clear()
    {
        // 描画キューのクリア
    }
}
