# Changelog

すべての変更は [Keep a Changelog](https://keepachangelog.com/ja/1.1.0/) の形式に準拠し、[セマンティックバージョニング](https://semver.org/lang/ja/) に従います。

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
