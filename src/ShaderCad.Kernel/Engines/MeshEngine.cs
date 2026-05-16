using System.Collections.Generic;
using System.Numerics;
using g3;
using ShaderCad.Core.Diagnostics;
using ShaderCad.Core.Geometry;
using ShaderCad.Core.Models;

namespace ShaderCad.Kernel.Engines;

/// <summary>
/// Geometry3Sharpを用いたメッシュベースのCADエンジン。
/// Core層からのビルド要求を受け、DMesh3を生成します。
/// </summary>
public class MeshEngine : IMeshEngine, ShaderCad.Core.Geometry.IMeshBuilder
{
    private readonly List<DMesh3> _generatedMeshes = new();

    /// <summary>
    /// 生成されたメッシュのリスト（描画層へ渡すため）
    /// </summary>
    public IReadOnlyList<DMesh3> GeneratedMeshes => _generatedMeshes;

    public CadResult Initialize()
    {
        return CadResult.Success();
    }

    public CadResult Rebuild(CadNode rootNode)
    {
        _generatedMeshes.Clear();
        BuildNodeRecursive(rootNode);
        return CadResult.Success();
    }

    private void BuildNodeRecursive(CadNode node)
    {
        // コンポーネントの検証と形状ビルド
        foreach (var comp in node.Components)
        {
            if (comp.Validate().IsSuccess)
            {
                comp.BuildGeometry(this);
            }
        }

        // 子ノードへ再帰
        foreach (var child in node.Children)
        {
            BuildNodeRecursive(child);
        }
    }

    // --- IMeshBuilder の実装 ---

    public void AddSphere(double radius, Vector3 center)
    {
        // Geometry3SharpのSphereジェネレータを使用
        var generator = new Sphere3Generator_NormalizedCube
        {
            Radius = radius,
            EdgeVertices = 32 // 分割数を上げて滑らかな球にする
        };
        generator.Generate();
        var mesh = generator.MakeDMesh();

        // 指定座標に移動
        MeshTransforms.Translate(mesh, new Vector3d(center.X, center.Y, center.Z));
        
        // 法線の計算
        MeshNormals.QuickCompute(mesh);

        _generatedMeshes.Add(mesh);
    }

    public void AddCube(Vector3 size, Vector3 center)
    {
        var generator = new GridBox3Generator
        {
            Box = new Box3d(new Vector3d(center.X, center.Y, center.Z), new Vector3d(size.X / 2, size.Y / 2, size.Z / 2)),
            EdgeVertices = 1
        };
        generator.Generate();
        var mesh = generator.MakeDMesh();
        MeshNormals.QuickCompute(mesh);
        
        _generatedMeshes.Add(mesh);
    }
}
