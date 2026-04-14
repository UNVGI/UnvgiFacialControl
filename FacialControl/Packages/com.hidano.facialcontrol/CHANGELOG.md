# Changelog

すべての変更は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) の形式に準拠し、[セマンティックバージョニング](https://semver.org/lang/ja/) に従います。

## [0.1.0-preview.1] - Unreleased

初回プレリリース。

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
- デフォルト InputAction Asset

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

#### サンプル
- `Assets/Samples/SampleFacialProfileSO.asset` — サンプル専用の最小 `FacialProfileSO`
- `Assets/StreamingAssets/FacialControl/sample_profile.json` — まばたき Expression（固定 GUID）を含む最小 JSON
- `Assets/Samples/SampleInputBinding.asset` — `Trigger1` をまばたき Expression にバインドする `InputBindingProfileSO`

#### テンプレート
- `default_profile.json` — デフォルト 3 レイヤー + 基本 Expression（default, blink, gaze_follow, gaze_camera）
- `default_config.json` — VRChat プリセットの OSC 設定
- デフォルト InputAction Asset（Xbox コントローラの LT/RT バインディング含む）

#### ドキュメント
- 全公開 API の XML コメント
- クイックスタートガイド（`Documentation~/quickstart.md`）
- JSON スキーマリファレンス（`Documentation~/json-schema.md`）

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

#### Domain 層
- `ARKitDetector` の検出仕様を見直し、完全一致判定を修正

### Breaking Changes

- `InputSystemAdapter` を `MonoBehaviour` から純粋 C# クラス（`IDisposable`）へ変更
  - 移行方法: GameObject へのアタッチを止め、`new InputSystemAdapter(facialController)` でインスタンスを生成する
  - 終了処理は `OnDisable()` の代わりに `Dispose()` を呼び出す
  - シーン内でキーコンフィグを扱う場合は新設の `FacialInputBinder` コンポーネントを使用する

### Removed

- `ProfileManagerWindow`（Inspector 統合により不要）
- プロファイル情報のレイヤー数・Expression 数の冗長な表示
- JSON 読み込みボタン（Inspector 表示時の自動読み込みで代替）
- クローン作成ボタン（Unity 標準のアセット複製で代替）
- `Assets/Samples/TestExpressionToggle.cs`（`FacialInputBinder` + `SampleInputBinding.asset` への移行により不要）
