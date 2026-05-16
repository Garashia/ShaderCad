using System;
using System.Collections.Generic;
using System.Linq;

namespace ShaderCad.Core.Diagnostics;

/// <summary>
/// CADの評価結果のエラーレベル
/// </summary>
public enum CadErrorLevel
{
    Error,   // B-rep破綻、位相エラーなど（保存やエクスポートを阻止）
    Warning, // 製造性エラー（薄肉、鋭角など）
    Info,    // 推奨されない設計
    Hint,    // 設計のヒント
    Success  // 問題なし
}

/// <summary>
/// 個々の診断結果
/// </summary>
public record CadDiagnostic(
    CadErrorLevel Level,
    string Message,
    System.Numerics.Vector3? Location = null,
    string? Recommendation = null
);

/// <summary>
/// コンポーネントやソルバーの検証結果
/// </summary>
public class CadResult
{
    private readonly List<CadDiagnostic> _diagnostics = new();

    public bool IsSuccess => !_diagnostics.Any(d => d.Level == CadErrorLevel.Error);
    public IReadOnlyList<CadDiagnostic> Diagnostics => _diagnostics;

    public void AddDiagnostic(CadDiagnostic diagnostic)
    {
        _diagnostics.Add(diagnostic);
    }

    public static CadResult Success() => new CadResult();

    public static CadResult FromError(string message, System.Numerics.Vector3? location = null)
    {
        var result = new CadResult();
        result.AddDiagnostic(new CadDiagnostic(CadErrorLevel.Error, message, location));
        return result;
    }
}
