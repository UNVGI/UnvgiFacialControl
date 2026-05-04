# Changelog

すべての変更は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) の形式に準拠し、[セマンティックバージョニング](https://semver.org/lang/ja/) に従います。

## [Unreleased]

### ⚠ BREAKING CHANGES

- `GazeExpressionConfig` の BlendShape 経路を 4 string field (`leftEyeXBlendShape` / `leftEyeYBlendShape` / `rightEyeXBlendShape` / `rightEyeYBlendShape`) から 4 AnimationClip 参照 (`lookLeftClip` / `lookRightClip` / `lookUpClip` / `lookDownClip`) に置換。Vector2 入力の +X / -X / +Y / -Y がそれぞれ LookRight / LookLeft / LookUp / LookDown clip に対応する。既存 SO の旧 BS string 値はロード時に黙って無視される。
- 目線ボーンの runtime 反映経路を `AnalogBonePoseProvider` (Euler 加算) から専用 `GazeBonePoseProvider` (quaternion 合成 + 可動範囲制限) に切替えた。`FacialCharacterSO.BuildAnalogProfileFromGazeConfigs` は `BonePose` の `AnalogBindingEntry` を生成しなくなり、目線ボーンの軸変換と角度クランプは `GazeBonePoseProvider` が担う。**既存 SO は 目線ボーン foldout の「参照モデルから自動設定」を再実行**して新しい yaw/pitch 軸と可動角を再構築する必要がある。
- `ExpressionBindingEntry` の既定動作モードが `Toggle` から `Hold` に変更。`triggerMode` field を追加し、未指定時は押している間だけ表情が ON、リリースで OFF になる。**既存 SO の挙動が変化する**ため、これまで通りトグル動作を維持したいバインディングは Inspector で動作モードを `Toggle` に戻す。

### Added

- `Hidano.FacialControl.Domain.Models.TriggerMode` (`Hold` / `Toggle`) と `Hidano.FacialControl.Domain.Models.InputBinding.TriggerMode` フィールドを新設し、ボタン押下時の挙動を選択可能にした。
- `ExpressionBindingEntry` に `triggerMode` フィールドを追加し、`FacialCharacterSOInspector` のキーバインディング行に動作モード選択 UI (EnumField) を追加した。
- `GazeExpressionConfig` に左右目それぞれの yaw/pitch local 軸 (`leftEyeYawAxisLocal` / `leftEyePitchAxisLocal` / `rightEyeYawAxisLocal` / `rightEyePitchAxisLocal`) と、可動範囲制限フィールド (`lookUpAngle` / `lookDownAngle` / `outerYawAngle` / `innerYawAngle`) を追加した。
- `Hidano.FacialControl.InputSystem.Adapters.Bone.GazeBonePoseProvider` を新設し、quaternion 合成で世界の上下/左右軸基準に目線ボーン rotation を計算するようにした。`FacialCharacterInputExtension` が GazeConfig から構築・Tick 駆動する。
- `GazeExpressionConfig` に 4 系統 AnimationClip フィールドと、Editor で焼き付けた sample 配列 `lookLeftSamples` / `lookRightSamples` / `lookUpSamples` / `lookDownSamples` (`List<GazeBlendShapeSampleEntry>`) を追加。runtime はこの sample 配列から `AnalogBindingEntry` を構築する。
- `GazeBlendShapeSampleEntry` 型 (`blendShapeName: string` / `weight: float`) を新設。
- `Hidano.FacialControl.InputSystem.Editor.Sampling.GazeClipBlendShapeSampler` (Editor 専用) — 4 系統 clip の time=0 における BlendShape weight を抽出する `AnimationUtility` ベースのヘルパ。
- `FacialCharacterSOAutoExporter.SampleGazeClipsIntoConfigs` — SO 保存時に 4 系統 clip を sample して上記 sample 配列に永続化する経路を追加。
- `FacialCharacterSOInspector` の Gaze BS セクションを 4 ObjectField (AnimationClip) に置換。同セクションの `BuildGazeClipField` ヘルパと、UIElements name 定数 (`ExpressionRowGazeLookLeftClipName` 等) を新設。
- `FacialCharacterSOInspector` の目線ボーン foldout に「参照モデルから自動設定」ボタンを追加。`_referenceModel` の Animator から Humanoid Avatar マッピング優先 (LeftEye / RightEye)、不在時は名前検索 (`*eye*`) で目ボーンを解決し、`Transform.name` と現在の `localEulerAngles` を初期回転として書き込む。同ボタンは新たに、参照モデル時点の親 Transform を介して「世界の上下/左右軸が当該目ボーンの local 空間でどの方向に対応するか」を算出し yaw/pitch 軸として保存する。
- `FacialCharacterSOInspector` のアナログ表情行に「可動範囲 (角度制限)」 foldout を追加。上方向/下方向/外側 (yaw)/内側 (yaw) の 4 角度を Slider で編集できる。

### Changed

- `Adapters.Input.InputSystemAdapter.BindExpression` に `TriggerMode` を受ける overload を追加し、Hold モードでは `started` / `canceled`、Toggle モードでは `performed` を購読する分岐を実装。既存の 2-arg overload は後方互換のため `Toggle` で固定。
- `FacialCharacterInputExtension.BindAllExpressions` がバインディングごとの `TriggerMode` を `InputSystemAdapter` に伝播するようになった。
- `FacialCharacterInputExtension` が GazeConfig がある場合に `GazeBonePoseProvider` を構築・LateUpdate で Apply / Teardown 時に Dispose するようになった。`AnalogBonePoseProvider` は引き続き非目線ボーンや legacy `_analogBindings` 経路で使用される。
- `FacialCharacterSO.BuildAnalogProfileFromGazeConfigs` を 4 sample list 経路に変更し、各 BS の keyframe weight を `AnalogBindingEntry.Scale` に、+X/-X/+Y/-Y 振り分けを `AnalogBindingDirection` (Positive / Negative) に設定して emit する。目線ボーン制御は `GazeBonePoseProvider` に移管したため `BonePose` 系の entry は emit しなくなった。
- `Adapters.Json.Dto.AnalogBindingEntryDto` の scale / direction 追加に伴い JSON round-trip も更新（core パッケージ側）。
- `GazeExpressionConfig` の可動角デフォルト値を Sample 想定値に揃えた: `lookDownAngle` 12° → 9°、`outerYawAngle` 30° → 15°（`lookUpAngle` 15° / `innerYawAngle` 18° は据え置き）。既存 SO で値を明示保存していなければ次回保存時に新デフォルトが書き込まれる。

### Fixed

- `GazeBonePoseProvider` で目線入力 (Vector2) が上下左右とも反転して目ボーンに反映されていた問題を修正。Unity の `Quaternion.AngleAxis` 左手系規約と参照モデル自動取得の yaw/pitch 軸 (+Y / +X) の組み合わせで生じていた符号反転をコード側で吸収し、InputActionAsset の Invert processor に頼らずとも入力 +X = 視線右、+Y = 視線上 で動作するようにした。

### Removed

- `FacialCharacterSO.AppendBoneBinding` private ヘルパを撤去 (目線ボーンは `GazeBonePoseProvider` で扱うため)。
- `FacialCharacterSOInspector.BuildGazeBlendShapeField` (TextField 版) を削除。
- 旧定数 `ExpressionRowGazeLeftXName` / `ExpressionRowGazeLeftYName` / `ExpressionRowGazeRightXName` / `ExpressionRowGazeRightYName` を削除。

## [0.1.0-preview.2] - 2026-05-01

### ⚠ BREAKING CHANGES

> 本リリースは spec `inspector-and-data-model-redesign` に基づく InputSystem 連携の全面改修を含みます。**v0.1.0-preview.1 以前の `FacialCharacterSO` / `*.inputactions` / Scene / Prefab はそのままでは動作しません**。アップグレード前に必ず [`Migration Guide v0.x → v1.0`](Documentation~/MIGRATION-v0.x-to-v1.0.md) の手順で資産を変換してください（Req 10.1, 10.6）。

#### MonoBehaviour / Inspector の撤去

- `KeyboardExpressionInputSource` / `ControllerExpressionInputSource` MonoBehaviour — `ExpressionInputSourceAdapter` 1 個（`[DisallowMultipleComponent]`）に統合
- `ExpressionBindingEntry.Category` field — `InputDeviceCategorizer.Categorize(InputBinding.path)` で自動分類
- `InputSourceCategory` enum — 参照ゼロ確認のうえ撤去（OSC 別 spec で再導入の余地）
- `FacialCharacterSO.GetExpressionBindings(InputSourceCategory category)` — 引数なし版に縮退（adapter 側が dispatch を吸収）
- `FacialCharacterInputExtension.BindCategory` の二重ループ — 1 ループに統合

#### アナログバインディングの撤去

- `AnalogBindingEntry.Mapping`（`AnalogMappingFunction` 埋め込み） — `*.inputactions` 内の processors 文字列に移行
- `AnalogBindingEntrySerializable` を `inputActionRef + targetIdentifier + targetAxis` の 3 field に縮退
- `AnalogMappingFunctionSerializable` を物理削除

### Added

- `ExpressionInputSourceAdapter` MonoBehaviour — Keyboard / Controller を統合した入力源 adapter。内部に keyboard / controller 用の 2 つの sink を composition で保持し、`InputAction.bindings[0].path` を `InputDeviceCategorizer` で分類して dispatch。未認識 device は Controller 側 + `Debug.LogWarning` 1 回（Req 7.5）、unsupported processor 検知時は raw value 続行 + warning（Req 6.6）。per-frame 0-alloc 維持（Req 11.3）
- `Adapters.Input.InputDeviceCategorizer` — `Categorize(string bindingPath, out bool wasFallback)` 静的メソッド。`<Keyboard>` prefix → Keyboard、`<Gamepad>` / `<Joystick>` / `<XRController>` / `<Pen>` / `<Touchscreen>` → Controller、未認識は Controller fallback。`string.StartsWith(prefix, StringComparison.Ordinal)` で 0-alloc
- 6 種カスタム InputProcessor:
  - `analogDeadZone`（`min` / `max` の `float` field、abs ≤ min は 0、それ以外は `Sign(value) * Clamp01(normalized)`）
  - `analogScale`（`factor: float`、`value * factor`）
  - `analogOffset`（`offset: float`、`value + offset`）
  - `analogClamp`（`min` / `max: float`、`Mathf.Clamp(value, min, max)`）
  - `analogInvert`（`-value`）
  - `analogCurve`（`preset: int`、0=Linear, 1=EaseIn, 2=EaseOut, 3=EaseInOut。Custom Hermite は v2.1 以降）
- `Adapters.Processors.AnalogProcessorRegistration` — `[InitializeOnLoad]` + `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` で 6 processor を `InputSystem.RegisterProcessor` 一括登録
- `Documentation~/MIGRATION-v0.x-to-v1.0.md` — v0.x → v1.0 手動移行ガイド（Schema 差分一覧 / Step-by-Step 手順 / AnimationClip メタデータ運搬規約 / 既知の制限）

### Changed

- `InputSystemAdapter.BindExpression(action, expression)` の入口で `InputDeviceCategorizer.Categorize(action.bindings[0].path)` を呼んで分岐
- 同梱サンプル（`Multi Source Blend Demo` / `Analog Binding Demo`）を schema v2.0 / processors 文字列ベースで再生成

### Removed

- `Adapters.InputSources.KeyboardExpressionInputSource` / `ControllerExpressionInputSource` MonoBehaviour（`.meta` 含む）
- `Adapters.ScriptableObject.ExpressionBindingEntry.Category` field
- `Adapters.ScriptableObject.Serializable.AnalogMappingFunctionSerializable`

## [0.1.0-preview.1] - Unreleased

初回プレリリース。`com.hidano.facialcontrol` の Unity InputSystem 連携アダプタとして提供。

### Added

- `InputSystemAdapter` — InputAction Asset との連携（Button / Value 両対応）
- `KeyboardExpressionInputSource` / `ControllerExpressionInputSource`（予約 id `keyboard-expr` / `controller-expr`）
- `InputBindingProfileSO`（旧形式）— InputActionAsset / ActionMap / バインディングペアの永続化
- `FacialInputBinder`（MonoBehaviour）— `InputBindingProfileSO` 読み込み + Action ↔ Expression バインド
- `InputFacialControllerExtension` — `InputSourceFactory.RegisterReserved` 経由でトリガー入力源を登録
- `InputBindingProfileSOEditor`（UI Toolkit Inspector）
- `Runtime/Adapters/Input/FacialControlDefaultActions.inputactions`（Xbox コントローラ LT/RT バインディング含む）
- `FacialCharacterSO`（統合 SO）と `FacialCharacterInputExtension`
- 同梱サンプル `Multi Source Blend Demo` / `Analog Binding Demo`
