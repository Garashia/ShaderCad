using System.Numerics;
using ShaderCad.Core.Attributes;
using ShaderCad.Core.Diagnostics;
using ShaderCad.Core.Geometry;
using ShaderCad.Core.Models;

namespace ShaderCad.Core.Components.Primitives;

/// <summary>
/// 球体を生成するプリミティブコンポーネント。
/// 形状のデータ（半径）を保持し、Kernel層に対して球の生成を依頼します。
/// </summary>
public class SphereComponent : CadComponent
{
    /// <summary>
    /// 球の半径
    /// </summary>
    [CadParameter(Min = 0.001, Group = "Dimensions")]
    public double Radius { get; set; } = 1.0;

    /// <summary>
    /// 値の妥当性を検証します
    /// </summary>
    public override CadResult Validate()
    {
        if (Radius <= 0)
        {
            // エラー箇所として、このノードの現在座標を渡す（Gizmos描画用）
            var pos = this.Node?.Transform.Translation ?? Vector3.Zero;
            return CadResult.FromError("球の半径は0より大きい必要があります。", pos);
        }

        return CadResult.Success();
    }

    /// <summary>
    /// ジオメトリビルダーに球の生成を依頼します
    /// </summary>
    public override void BuildGeometry(IMeshBuilder builder)
    {
        // ノードのローカル座標系原点に球を生成
        builder.AddSphere(this.Radius, Vector3.Zero);
    }
}
