# ShaderCad 完成度分析：Godot / PlayCanvas / FreeCAD との比較

> 調査日: 2026-05-17  
> 対象コード: `src/` 以下の実装を直接参照して比較

---

## 1. コンポーネント・エンティティシステム

### ShaderCad（現状）

```csharp
// CadComponent.cs — 基底クラス
public abstract class CadComponent {
    public CadNode Node { get; internal set; }
    public bool IsEnabled { get; set; } = true;
    public virtual void OnAttached() {}
    public virtual CadResult Validate() => CadResult.Success();
    public virtual void BuildGeometry(IMeshBuilder builder) {}
}

// SphereComponent.cs — 実装例
[CadParameter(Min = 0.001, Group = "Dimensions")]
public double Radius { get; set; } = 1.0;
```

### Godot（EditorInspector 方式）

```cpp
// editor_inspector.cpp — プロパティはすべて PropertyInfo で定義
void EditorInspector::update_tree() {
    // PropertyInfo リストをスキャンし動的にウィジェットを生成
    for (const PropertyInfo &E : plist) {
        EditorProperty *editor = ...
        editor->connect("property_changed", ...)
    }
}
```

### PlayCanvas（属性定義方式）

```javascript
// Classic script方式
MyScript.attributes.add('radius', {
    type: 'number', default: 1.0, min: 0.001, title: 'Radius'
});
// Modern ESM方式 — JSDocアノテーション
/** @attribute */
radius = 1.0;
```

### 比較表

| 機能 | ShaderCad | Godot | PlayCanvas | 完成度 |
|------|-----------|-------|------------|--------|
| コンポーネント基底クラス | ✅ `CadComponent` | ✅ `Object` 派生 | ✅ `Script` 派生 | **80%** |
| プロパティのメタデータ | ✅ `[CadParameter]` 属性 | ✅ `PropertyInfo` + Export | ✅ `@attribute` JSDoc | **85%** |
| 型に応じたエディタUI生成 | ⚠️ `double` のみ | ✅ 全型 (Color/Vec3 等) | ✅ 全型 | **30%** |
| IsEnabled / 有効化トグル | ✅ あり | ✅ あり | ✅ あり | **90%** |
| コンポーネントの動的追加・削除 | ❌ ハードコード | ✅ UIから操作可 | ✅ UIから操作可 | **0%** |
| 複数コンポーネント同時アタッチ | ✅ 設計上可能 | ✅ あり | ✅ あり | **70%** |

---

## 2. Inspector / プロパティエディタ

### ShaderCad（現状）

```csharp
// InspectorViewModel.cs — リフレクション駆動
var props = type.GetProperties(...)
    .Where(p => p.GetCustomAttribute<CadParameterAttribute>() != null);
foreach (var p in props) {
    if (p.PropertyType == typeof(double))
        Properties.Add(new DoublePropertyViewModel(...));
    // string / Vector3 は未実装
}
```

### Godot との差分

Godot の `EditorInspector` は：
- `Vector2/3`, `Color`, `Enum`, `Resource`, `NodePath` など **20種以上**の型に対応
- プロパティをグループ/カテゴリにネスト表示
- プロパティの **Undo/Redo** 対応（`EditorUndoRedoManager`）
- カスタム `EditorInspectorPlugin` で拡張可能

### FreeCAD との差分

FreeCAD の **Task Panel** は：
- Qt ベースの専用ダイアログ（各フィーチャごとに独自UIを定義）
- 入力値の **単位変換**（mm/inch/etc）
- **拘束ソルバ** との双方向バインディング

### 比較表

| 機能 | ShaderCad | Godot | FreeCAD | 完成度 |
|------|-----------|-------|---------|--------|
| 動的プロパティ生成 | ✅ リフレクション | ✅ PropertyInfo | ✅ Qt動的 | **75%** |
| 対応型の数 | ⚠️ double のみ | ✅ 20+ 型 | ✅ 10+ 型 | **10%** |
| グループ/カテゴリ表示 | ⚠️ 属性定義済みだが未表示 | ✅ あり | ✅ あり | **20%** |
| Undo/Redo | ❌ なし | ✅ あり | ✅ あり | **0%** |
| 単位変換 | ❌ なし | ❌ なし | ✅ あり | **0%** |
| カスタムエディタ拡張 | ❌ なし | ✅ プラグイン | ✅ あり | **0%** |

---

## 3. 3Dビューポート・レンダリング

### ShaderCad（現状）

```csharp
// SilkViewportControl.cs — 現在の描画パイプライン
// - グリッド（XZ平面、20x20）
// - Y軸ライン（白、軸識別なし）
// - メッシュ（Phong照明、ライト固定）
// - Orbit Camera（Azimuth/Elevation/Distance）
```

### Godot との差分

Godot の `Node3DEditorViewport` は：
- **マルチビューポート**（Top/Front/Side/Perspective の4分割）
- **選択ハイライト**（アウトライン描画）
- **スナップ機能**（グリッドスナップ、角度スナップ）
- **フライモード**（WASD + 右クリック）
- **Gizmo矢印**でTransform直接操作

### FreeCAD との差分

FreeCAD の `3D View`（Coin3D/OpenInventor）は：
- **ステップ検出**（エッジ/面/点の選択）
- **寸法表示（Measurement）**
- **断面ビュー**
- **モデルの影（AmbientOcclusion近似）**
- **測定ツール**（距離/角度/面積）

### 比較表

| 機能 | ShaderCad | Godot Editor | FreeCAD | 完成度 |
|------|-----------|-------------|---------|--------|
| 基本メッシュ描画 | ✅ OpenGL | ✅ Vulkan/GL | ✅ Coin3D | **70%** |
| Orbit カメラ | ✅ 球面座標 | ✅ あり | ✅ あり | **80%** |
| グリッド | ✅ XZプレーン | ✅ あり | ✅ あり | **60%** |
| XYZ軸 Gizmo | ⚠️ 白線のみ | ✅ 色付き矢印 | ✅ 色付き矢印 | **20%** |
| 面/エッジ選択 | ❌ なし | ✅ あり | ✅ あり | **0%** |
| Transform Gizmo | ❌ なし | ✅ あり | ✅ あり | **0%** |
| 複数ライト | ❌ 固定1点 | ✅ 動的 | ✅ あり | **30%** |
| フライモード | ❌ なし | ✅ あり | ❌ なし | **0%** |
| ワイヤーフレーム | ❌ なし | ✅ あり | ✅ あり | **0%** |

---

## 4. シーン管理・アーキテクチャ

### ShaderCad（現状）

```csharp
// CadNode.cs — ノード階層
public class CadNode {
    public string Name { get; set; }
    public Matrix4x4 Transform { get; set; }
    public IReadOnlyList<CadNode> Children;
    public IReadOnlyList<CadComponent> Components;
    public T AddComponent<T>() where T : CadComponent, new() { ... }
    public T? GetComponent<T>() { ... }
}
```

### Godot との差分（SceneTree）

Godot は：- **シーンのシリアライズ**（`.tscn` / `.tres` ファイル保存）- **シグナル/接続システム**（コンポーネント間通信）- **GDScript / C# スクリプト** による行動定義- **プレハブ（PackedScene）** の概念

### FreeCAD との差分（Document/Feature ツリー）

FreeCAD は：- **Document** 単位の保存（`.FCStd` / `.STEP` エクスポート）- **パラメータ参照**（スケッチの寸法 → ボディのパッド厚）- **フィーチャーツリー**（順序付き履歴）- **制約ソルバー**（Sketcher の拘束システム）

### 比較表

| 機能 | ShaderCad | Godot | FreeCAD | 完成度 |
|------|-----------|-------|---------|--------|
| ノード階層 | ✅ `CadNode` | ✅ `Node` | ✅ `Feature` | **70%** |
| シーンの保存/読み込み | ❌ なし | ✅ `.tscn` | ✅ `.FCStd` | **0%** |
| Undo/Redo | ❌ なし | ✅ あり | ✅ あり | **0%** |
| シーンツリーUI | ❌ なし | ✅ あり | ✅ あり | **0%** |
| パラメータ参照/依存関係 | ❌ なし | ❌ なし | ✅ あり（CAD特有）| **0%** |
| ファイルI/O (STEP等) | ❌ なし | ❌ なし | ✅ あり | **0%** |

---

## 5. 総合評価

```
ShaderCad 完成度レーダー（Godot/FreeCAD を 100% とした場合）

レンダリングパイプライン  ████████░░  70%
コンポーネントシステム    ███████░░░  65%
Inspector UI             ███░░░░░░░  25%
シーン管理               ██░░░░░░░░  20%
Gizmo・選択システム      ██░░░░░░░░  15%
ファイル保存/I/O         ░░░░░░░░░░   0%
Undo/Redo               ░░░░░░░░░░   0%
```

### 強み（ShaderCad が優れている/同等な点）
- **属性ベースの Inspector 生成**は Godot の Export アノテーションと同等の設計思想
- **クリーンアーキテクチャ**（Core/Kernel/Renderer 分離）は FreeCAD の App/Gui 分離と同じ考え方
- **Geometry3Sharp カーネル**の採用は OpenCASCADE の軽量代替として正しい選択

### 次の優先実装（インパクト順）

| 優先度 | 機能 | 難易度 | 効果 |
|--------|------|--------|------|
| 🔴 高 | XYZ軸 色付き Gizmo | 低 | 視覚的完成度 UP |
| 🔴 高 | シーンツリーUI（追加/削除）| 中 | CAD感 UP |
| 🟡 中 | Inspector の型拡張（int/string/Vector3）| 中 | 実用性 UP |
| 🟡 中 | Box/Cylinder コンポーネント | 低 | コンテンツ UP |
| 🟠 低 | Undo/Redo | 高 | 品質 UP |
| 🟠 低 | ファイル保存（JSON/STEP）| 高 | 実用性 UP |
