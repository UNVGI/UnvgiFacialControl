# Changelog

すべての変更は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) の形式に準拠し、[セマンティックバージョニング](https://semver.org/lang/ja/) に従います。

## [0.1.0-preview.2] - 2026-05-01

### ⚠ BREAKING CHANGES

> 本リリースは spec `inspector-and-data-model-redesign` に基づく Domain モデル / 中間 JSON schema / InputSystem 連携の全面改修を含みます。**v0.1.0-preview.1 以前の `FacialCharacterSO` / JSON / Scene / Prefab はロードできません**。アップグレード前に必ず [`Migration Guide v0.x → v1.0`](../com.hidano.facialcontrol.inputsystem/Documentation~/MIGRATION-v0.x-to-v1.0.md) の手順で資産を変換してください（Req 10.1, 10.6）。

#### 中間 JSON schema 破壊

- `schemaVersion` を `"2.0"` に bump。`SystemTextJsonParser` は `schemaVersion != "2.0"` を `Debug.LogError` + `InvalidOperationException` で拒否（schema v1.0 ロード不可）
- `expressions[]` を `id / name / layer / layerOverrideMask: List<string> / snapshot: ExpressionSnapshotDto` の snapshot table 形式に再構成。旧 `transitionDuration / transitionCurve / blendShapeValues / layerSlots` field を撤去
- top-level `rendererPaths[]` を新設し、各 Expression snapshot の `rendererPaths[]` がそのサブセットであることを保証

#### Domain モデルの撤去

- `Hidano.FacialControl.Domain.Models.LayerSlot` — `LayerOverrideMask` (`[Flags] enum : int`、32 bit) に置換
- `Hidano.FacialControl.Domain.Models.BonePose` / `BonePoseEntry` — `BoneSnapshot` (`ReadOnlyMemory<BoneSnapshot>`) に統合
- `Hidano.FacialControl.Domain.Models.FacialProfile.BonePoses` プロパティ — Expression snapshot 経路に一元化
- `Hidano.FacialControl.Domain.Models.AnalogMappingFunction` — InputAction Asset の processors チェーンに置換
- `Hidano.FacialControl.Domain.Services.AnalogMappingEvaluator` — 上記に伴い物理削除
- `Hidano.FacialControl.Domain.Models.Expression` の独立 field（`TransitionDuration / TransitionCurve / BlendShapeValues / LayerSlots`） — `SnapshotId` 参照に集約
- `TransitionCurve` enum — `TransitionCurvePreset` enum (`Linear=0 / EaseIn=1 / EaseOut=2 / EaseInOut=3`) に置換

#### InputSystem 連携の撤去

- `KeyboardExpressionInputSource` / `ControllerExpressionInputSource` MonoBehaviour — `ExpressionInputSourceAdapter` 1 個に統合（`com.hidano.facialcontrol.inputsystem` パッケージで管理）
- `ExpressionBindingEntry.Category` field — `InputDeviceCategorizer.Categorize(bindingPath)` で自動分類
- `InputSourceCategory` enum — 参照ゼロ確認のうえ撤去（OSC 別 spec で再導入の余地）
- `FacialCharacterSO.GetExpressionBindings(InputSourceCategory category)` — 引数なし版に縮退
- `AnalogBindingEntry.Mapping` field — `*.inputactions` 内の processors 文字列に移行

### Added

#### Domain 層

- `LayerOverrideMask`（`[Flags] enum : int`、32 bit）— レイヤーオーバーライド bit マスク
- `BlendShapeSnapshot`（`readonly struct`、`RendererPath / Name / Value`）
- `BoneSnapshot`（`readonly struct`、`BonePath / Position(X,Y,Z) / Euler(X,Y,Z) / Scale(X,Y,Z)`）
- `ExpressionSnapshot`（`Id / TransitionDuration / TransitionCurvePreset / BlendShapes / Bones / RendererPaths`、防御コピー + `ReadOnlyMemory<T>` 公開）
- `TransitionCurvePreset` enum（`Linear=0 / EaseIn=1 / EaseOut=2 / EaseInOut=3`）
- `Domain.Services.ExpressionResolver` — `TryResolve(snapshotId, Span<float> blendShapeOutput, Span<BoneSnapshot> boneOutput)` の 0-alloc preallocated 解決経路

#### Adapters / Editor 層

- `Editor/Sampling/IExpressionAnimationClipSampler` interface と `AnimationClipExpressionSampler` 実装 — `AnimationUtility.GetCurveBindings` / `GetEditorCurve(...).Evaluate(0f)` で AnimationClip → `ExpressionSnapshot` をサンプリング
- AnimationEvent 経由のメタデータ運搬規約（予約 functionName `FacialControlMeta_Set`、key `transitionDuration` / `transitionCurvePreset`）
- 中間 JSON schema v2.0 DTO（`ExpressionSnapshotDto` / `BlendShapeSnapshotDto` / `BoneSnapshotDto`）
- `FacialCharacterSOAutoExporter` の `OnWillSaveAssets` 経路 — 200ms 超で `EditorUtility.DisplayProgressBar` 発火、サンプリング失敗時は当該 SO の save abort + `Debug.LogError`
- `FacialCharacterSOInspector` UI Toolkit 全面改修 — AnimationClip ObjectField / Layer DropdownField / LayerOverrideMask MaskField / read-only RendererPath summary、validation エラー時の HelpBox + Save 無効化、Guid 自動採番、AnimationClip 名からの Name 派生

### Changed

- `Domain.Models.AnalogBindingEntry` を `SourceId / SourceAxis / TargetKind / TargetIdentifier / TargetAxis` の 5 field に縮退（Mapping は Adapters 側 `InputActionReference` の processors に移管）
- `BonePoseComposer` の入力型を `BoneSnapshot` 経路に統一
- `ExpressionCreatorWindow` を AnimationClip ベイク経路に改修（`AnimationUtility.SetEditorCurve` + `SetAnimationEvents`）

### Removed

- `Domain.Models.LayerSlot`、`Domain.Models.BonePose`、`Domain.Models.BonePoseEntry`、`Domain.Models.AnalogMappingFunction`、`Domain.Services.AnalogMappingEvaluator`
- `FacialProfile.BonePoses` プロパティ
- 旧 `BonePoseDto` / `BonePoseEntryDto` / `LayerSlotDto` / `AnalogMappingFunctionSerializable`

## [0.1.0-preview.1] - Unreleased

初回プレリリース。3 パッケージ構成（コア + `com.hidano.facialcontrol.osc` + `com.hidano.facialcontrol.inputsystem`）で提供。

### サブパッケージ構成

- 新パッケージ **`com.hidano.facialcontrol.osc`** — OSC 関連実装（`OscReceiver` / `OscSender` / `OscDoubleBuffer` / `OscMappingTable` / `OscReceiverPlayable` / `OscInputSource` / `OscOptionsDto`）+ `OscRegistration` ヘルパー + `OscFacialControllerExtension` MonoBehaviour
- 新パッケージ **`com.hidano.facialcontrol.inputsystem`** — InputSystem 関連実装（`InputSystemAdapter` / `FacialInputBinder` / `ControllerExpressionInputSource` / `KeyboardExpressionInputSource` / `InputBindingProfileSO` / `ExpressionTriggerOptionsDto` / `InputBinding`）+ `InputRegistration` ヘルパー + `InputFacialControllerExtension` MonoBehaviour
- コア `Hidano.FacialControl.Adapters` は `Unity.InputSystem` / `uOSC.Runtime` 非参照。表情切替を API から呼ぶだけのユーザーは uOSC・InputSystem インストール不要
- `IFacialControllerExtension` (`Hidano.FacialControl.Adapters.Playable`) — `FacialController` 初期化時に同 GameObject の拡張から `InputSourceFactory` に追加登録するための I/F
- `InputSourceFactory.RegisterReserved<TOptions>(...)` — 予約 id を含む任意 id の登録 API（公式サブパッケージ向け）



### Added

#### Domain 層
- `FacialProfile`、`Expression`、`BlendShapeMapping`、`LayerDefinition`、`LayerSlot` ドメインモデル
- `ExclusionMode`（LastWins / Blend）、`TransitionCurveType` enum
- `TransitionCurve` 構造体（Linear / EaseIn / EaseOut / EaseInOut / Custom）
- `TransitionCalculator` — 遷移カーブ評価サービス
- `ExclusionResolver` — LastWins クロスフェード / Blend 加算排他ロジック
- `LayerBlender` — レイヤー優先度ベースのウェイトブレンドと layerSlots オーバーライド
- `ARKitDetector` — ARKit 52 / PerfectSync の完全一致検出とレイヤーグルーピング
- `IJsonParser`、`IProfileRepository`、`ILipSyncProvider`、`IBlinkTrigger` インターフェース
- `FacialControlConfig`、`FacialState`、`FacialOutputData` 構造体
- `FacialProfile.RendererPaths` — Renderer パスの保持（JSON / SO 双方で同期）
- `InputBinding`（readonly struct）— ActionName と ExpressionId を値ベースで保持する Domain 層の入力バインディングモデル

#### Application 層
- `ProfileUseCase` — プロファイル読み込み・再読み込み・Expression 取得
- `ExpressionUseCase` — Expression のアクティブ化・非アクティブ化
- `LayerUseCase` — レイヤーウェイト更新とブレンド出力計算
- `ARKitUseCase` — ARKit / PerfectSync 検出と Expression・OSC マッピング自動生成

#### Adapters 層
- `SystemTextJsonParser` — System.Text.Json ベースの JSON パース / シリアライズ
- `FileProfileRepository` — ファイルシステムからのプロファイル読み書き
- `NativeArrayPool` — GC フリーの NativeArray プール管理
- `AnimationClipCache` — LRU 方式の AnimationClip キャッシュ
- `PropertyStreamHandleCache` — BlendShape → PropertyStreamHandle キャッシュ
- `LayerPlayable`（ScriptPlayable）— NativeArray ベースの補間計算と排他モード処理
- `FacialControlMixer`（ScriptPlayable）— レイヤーウェイトブレンドと最終出力統合
- `PlayableGraphBuilder` — FacialProfile からの PlayableGraph 構築
- `OscDoubleBuffer` — ロックフリーのダブルバッファリング
- `OscReceiver` / `OscSender` — uOsc ベースの OSC 送受信
- `OscReceiverPlayable` — PlayableGraph への OSC 受信統合
- `OscMappingTable` — OSC アドレスと BlendShape のマッピング管理
- `FacialProfileSO`（ScriptableObject）— JSON への参照ポインター（RendererPaths・使用モデル参照を含む）
- `FacialProfileMapper` — FacialProfile ⟷ FacialProfileSO 変換（RendererPaths 同期対応）
- `FacialController`（MonoBehaviour）— メインコンポーネント（Activate / Deactivate / LoadProfile / ReloadProfile）
- `InputSystemAdapter` — InputAction Asset との連携（Button / Value 両対応）
- `InputBindingProfileSO`（ScriptableObject）— InputActionAsset・ActionMap 名・バインディングペア（Action ⟷ ExpressionId）を永続化するアセット
- `FacialInputBinder`（MonoBehaviour）— `InputBindingProfileSO` を読み込み、`InputSystemAdapter` 経由で Action と Expression をバインドするシーン配置用コンポーネント
- `Runtime/Adapters/Input/FacialControlDefaultActions.inputactions` — デフォルト InputAction Asset（Xbox コントローラの LT/RT バインディング含む）

#### Editor 拡張
- `FacialControllerEditor` — FacialController の Inspector カスタマイズ
- `FacialProfileSOEditor` — FacialProfileSO の Inspector カスタマイズ（プロファイル管理を Inspector に統合）
  - Expression の一覧表示・検索フィルタ・追加/削除（Unity 標準 List UI）
  - BlendShape の Weight 値編集・追加/削除・検索付きドロップダウン
  - レイヤー一覧表示・インライン編集・ドラッグ順序による優先度設定
  - JSON インポート / エクスポート・JSON 上書き保存
  - 使用モデル指定と RendererPaths 自動検出
- UI Toolkit スタイル共通定義
- `ExpressionCreatorWindow` — BlendShape スライダーでリアルタイムプレビューしながら Expression 作成
- `PreviewRenderUtility` ラッパー（カメラ / ライティング / RenderTexture 管理）
- `ARKitDetectorWindow` — ARKit / PerfectSync 自動検出 Editor UI
- `InputBindingProfileSOEditor` — UI Toolkit ベースの Inspector。ActionMap / Action / Expression ドロップダウンの自動列挙とバインディング行の追加・削除をサポート
- MenuItem `FacialControl/新規プロファイル作成` — `ProfileCreationDialog` を開き GUI だけで `FacialProfileSO` + JSON を生成（JSON 手書き不要の GUI ファースト導線）
- `ProfileCreationData.NamingConvention`（VRM / ARKit / None）と `BuildSampleExpressions()` — 選択した命名規則に合わせて `smile` / `angry` / `blink` の雛形 Expression を自動生成
- `Editor/Common/BlendShapeNameProvider.cs` — 参照モデル（`GameObject` / `FacialProfileSO`）から BlendShape 名を収集する Editor 共通ユーティリティ
- `ARKitEditorService.MergeIntoExistingProfile()` — ARKit / PerfectSync 検出結果を既存 `FacialProfileSO` にマージ（`ARKitDetectorWindow` の UI から呼び出し可能）

#### サンプル
- `Samples~/MultiSourceBlendDemo` — 同一レイヤーに `controller-expr` + `keyboard-expr` を並置し、ウェイトブレンドの挙動を OnGUI HUD で目視確認する PlayMode サンプル。Scene (`MultiSourceBlendDemo.unity`) / FacialProfileSO / InputBindingProfileSO / JSON プロファイル / HUD スクリプト / README を同梱し、ユーザーは Scene を開いて Character の子にモデルを配置するだけで動作する（モデルはライセンスの都合で同梱しない）

#### テンプレート
- `Templates/default_profile.json` — デフォルト 3 レイヤー + 基本 Expression（default, blink, gaze_follow, gaze_camera）

#### ドキュメント
- 全公開 API の XML コメント
- クイックスタートガイド（`Documentation~/quickstart.md`）
- JSON スキーマリファレンス（`Documentation~/json-schema.md`）
- `README.md` に「既知の制限とロードマップ」節を新設（Addressables 対応方針 / preview.2 以降の `IProfileJsonLoader` 抽象化計画）

### Changed

#### Editor 拡張
- プロファイル管理機能を `ProfileManagerWindow` から `FacialProfileSOEditor`（Inspector）に統合
- JSON ファイルパスを読み取り専用表示に変更（インポート機能で代替）
- Expression の追加/削除を Unity 標準 List UI に置き換え
- BlendShape 選択ドロップダウンに検索入力ボックスを追加
- レイヤー優先度をドラッグ順序で設定し、数値はラベル表示のみに変更
- 「参照モデル」の表記を「使用モデル」に統一
- 使用モデルセクションを Inspector 最上部に移動
- JSON ファイルセクションを Inspector 最下部に移動
- 使用モデルに Hierarchy 上の GameObject をアタッチ可能に変更
- ARKit 検出ツールの JSON 保存後に `AssetDatabase.Refresh` を実行するよう修正
- JSON インポートで JsonFilePath 未設定時もファイル選択ダイアログを表示するよう改善
- `FacialProfileSOEditor` / `ExpressionCreatorWindow` の BlendShape 名入力を TextField から検索付きドロップダウン（`BlendShapeNameProvider` 連携）に変更してタイポ耐性を向上
- `ARKitDetectorWindow` に既存 `FacialProfileSO` へのマージ UI を追加（`MergeIntoExistingProfile()` 連携）

#### Domain 層
- `ARKitDetector` の検出仕様を見直し、完全一致判定を修正

#### ドキュメント
- `Documentation~/quickstart.md` を GUI ファースト手順に全面刷新（プロファイル作成ダイアログ → 使用モデル指定 → キーコンフィグ設定の順序）
- `README.md` のクイックスタート節を 4 ステップに刷新（GUI ファースト導線へ）

### Breaking Changes

> 本節の破壊的変更は preview 段階の破壊的変更ポリシー（`docs/requirements.md` の FR-001「表情プロファイル管理」内の「後方互換性: preview 段階では破壊的変更を許容」規定）に基づく。移行手順の詳細は [`docs/migration-guide.md`](../../../docs/migration-guide.md) を参照。

- `InputSystemAdapter` を `MonoBehaviour` から純粋 C# クラス（`IDisposable`）へ変更
  - 移行方法: GameObject へのアタッチを止め、`new InputSystemAdapter(facialController)` でインスタンスを生成する
  - 終了処理は `OnDisable()` の代わりに `Dispose()` を呼び出す
  - シーン内でキーコンフィグを扱う場合は新設の `FacialInputBinder` コンポーネントを使用する

- FacialProfile JSON の `layers[].inputSources` を**必須フィールド化**し、暗黙の `legacy` フォールバックを廃止
  - `inputSources` が欠落または空配列のレイヤーは `FormatException` でロードが失敗する
  - 予約 ID: `osc` / `lipsync` / `controller-expr` / `keyboard-expr` / `input`。サードパーティ拡張は `x-` プレフィックスを使用
  - 移行方法: 既存プロファイル JSON の各レイヤーに `"inputSources": [{ "id": "controller-expr", "weight": 1.0 }]` 等の配列を明示的に追記する（同梱サンプルの移行例は `docs/migration-guide.md` および `StreamingAssets/FacialControl/*_profile.json` を参照）
  - 根拠: `docs/requirements.md` FR-001（preview 段階の破壊的変更許容）、および spec `layer-input-source-blending` の D-5 / R3.2 / R7.3 / R7.4

- 予約 ID `legacy` の廃止
  - 旧バージョンで `legacy` を用いて Expression パイプライン全体を 1 本の入力源として温存していた挙動は削除された
  - 移行方法: Expression 駆動のみを行うレイヤーは `inputSources: [{ "id": "controller-expr", "weight": 1.0 }]`（および必要に応じて `keyboard-expr`）へ置き換える
  - 根拠: spec `layer-input-source-blending` の D-1 / D-5 / D-6 / R1.7

- `InputBindingProfileSO` に `InputSourceCategory` フィールドを追加、既定値は `Controller`
  - 既存 Asset は本フィールドを持たないため、Unity のシリアライズ機構により初回ロード時に既定値 `Controller` が暗黙的に付与される
  - キーボード専用バインディングの Asset は `Keyboard` に明示変更しないと `ControllerExpressionInputSource` 側へトリガーが流れる挙動変更となるため、**全ての既存 Asset を Project ビューで一度レビューして Category を再設定する必要がある**
  - 手順の詳細は [`docs/migration-guide.md`](../../../docs/migration-guide.md) を参照
  - 根拠: spec `layer-input-source-blending` の R5.1 / R5.7 / R7.4

### Removed

- `ProfileManagerWindow`（Inspector 統合により不要）
- プロファイル情報のレイヤー数・Expression 数の冗長な表示
- JSON 読み込みボタン（Inspector 表示時の自動読み込みで代替）
- クローン作成ボタン（Unity 標準のアセット複製で代替）
- `Assets/Samples/TestExpressionToggle.cs`（`FacialInputBinder` + `com.hidano.facialcontrol.inputsystem/Samples~/MultiSourceBlendDemo/MultiSourceBlendDemoInputBinding.asset` への移行により不要）
