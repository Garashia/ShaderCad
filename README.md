# ShaderCad

ShaderCad は、C# (.NET 8) で構築されている次世代の機械設計用コンポーネントベースCADシステムです。
既存のフィーチャーベース（履歴ツリー）CADの限界を克服し、ゲームエンジンのような直感的なコンポーネント管理とGitネイティブな運用を実現することを目指しています。

## 特徴
- **履歴ベースの廃止**: 過去の操作を遡って修正した際の「再構築エラー（崩壊）」を防ぐため、Gitによるバージョン管理とコンポーネント指向（Unityライク）な非破壊モデリングを採用。
- **オブジェクト指向UI**: Avalonia UIのDataTemplate（ポリモーフィズム）を活かした、柔軟で自動生成されるInspectorを備えています。
- **Airspace問題のない統合ビュー**: Silk.NET (OpenGL) をAvaloniaの中にネイティブに組み込み、美しく高速な3Dビューポートを実現しています。
- **マルチカーネル対応**: 形状生成カーネル（バックエンド）の抽象化により、初期はメッシュベース（geometry3Sharp）、将来的にはB-Rep（OpenCASCADE）への切り替えが可能です。

## アーキテクチャ
- `ShaderCad.Core`: CADデータモデルと数学的検証を担う、ライブラリ非依存のクリーンな中核ロジック。
- `ShaderCad.Kernel`: Geometry3Sharp等をラップし、Coreから要求された形状生成を代行する計算層。
- `ShaderCad.Renderer`: Silk.NETを用いたOpenGL描画、および視覚的デバッグ（Gizmos）を担う描画層。
- `ShaderCad.App`: Avaloniaによるウィンドウ管理と、InspectorなどのUIを提供する最上位層。

## ビルドおよび実行手順

このプロジェクトは巨大なOSSをサブモジュールとして含んでいるため、クローン時に必ず `--recursive` を指定してください。

```bash
# 1. リポジトリのクローン（サブモジュールを含む）
git clone --recursive https://github.com/Garashia/ShaderCad.git
cd ShaderCad

# 2. ビルドおよび実行 (.NET 8 SDK が必要です)
dotnet run --project src/ShaderCad.App/ShaderCad.App.csproj
```

## ライセンスと運用ルール
本プロジェクトは基本的に MIT / Boost Software License としますが、一部バックエンド（OpenCASCADE等）がLGPLであるため、動的リンクの強制などGitHub公開時の厳密なルールを設けています。詳しくは `rule.md` を参照してください。
