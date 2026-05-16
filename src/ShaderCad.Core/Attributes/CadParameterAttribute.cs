using System;

namespace ShaderCad.Core.Attributes;

/// <summary>
/// UI（Inspector）での編集や、Undo操作の対象となるプロパティを宣言するための属性。
/// Unityにおける [SerializeField] と同等の役割を果たします。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class CadParameterAttribute : Attribute
{
    /// <summary>
    /// パラメータの最小値
    /// </summary>
    public double Min { get; set; } = double.MinValue;

    /// <summary>
    /// パラメータの最大値
    /// </summary>
    public double Max { get; set; } = double.MaxValue;

    /// <summary>
    /// UI上でグループ化するための名称
    /// </summary>
    public string Group { get; set; } = "General";
}
