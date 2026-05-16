using ShaderCad.Core.Diagnostics;
using ShaderCad.Core.Models;

namespace ShaderCad.Core.Solvers;

/// <summary>
/// 幾何拘束ソルバーなどのアルゴリズムを差し替えるためのStrategyインターフェース
/// </summary>
public interface ISolverStrategy
{
    /// <summary>
    /// 指定されたノード（およびその子孫）の拘束を計算・解決します
    /// </summary>
    /// <param name="rootNode">対象のルートノード</param>
    /// <returns>解決結果やエラー（過拘束など）を含むCadResult</returns>
    CadResult Solve(CadNode rootNode);
}
