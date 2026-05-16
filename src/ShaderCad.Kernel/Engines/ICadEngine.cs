using System;
using ShaderCad.Core.Models;
using ShaderCad.Core.Diagnostics;

namespace ShaderCad.Kernel.Engines;

/// <summary>
/// CADカーネル（B-Repまたはメッシュ）を抽象化する基底インターフェース
/// </summary>
public interface ICadEngine
{
    CadResult Initialize();
    CadResult Rebuild(CadNode rootNode);
}

/// <summary>
/// フェーズ1用：Geometry3Sharpなどを用いたメッシュベースの演算エンジン
/// </summary>
public interface IMeshEngine : ICadEngine
{
    // メッシュ固有の演算メソッド（ブーリアン、オフセットなど）を将来追加
}

/// <summary>
/// フェーズ2用：OpenCASCADEなどのB-Repベース演算エンジン
/// </summary>
public interface IBRepEngine : ICadEngine
{
    // B-Rep固有の演算メソッド（フィレット、スイープなど）を将来追加
}
