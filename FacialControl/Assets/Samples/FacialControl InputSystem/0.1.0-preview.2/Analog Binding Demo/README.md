# Analog Binding Demo

新統合 SO (`FacialCharacterSO`) のアナログバインディングを介して、アナログ入力 (左スティック / ARKit `jawOpen` / OSC float) を BonePose と BlendShape にリアルタイム駆動するサンプル。1 SO + Scene 上のコンポーネント 1 個 (FacialController) という最小結線で動かせる構成を確認するための PlayMode 専用サンプルです。

## 同梱物

| ファイル | 役割 |
|---------|------|
| `AnalogBindingDemo.unity` | `FacialController` + `InputFacialControllerExtension` + `FacialCharacterInputExtension` + HUD を結線済みの Scene |
| `AnalogBindingDemoActions.inputactions` | 左スティック X/Y と ARKit `jawOpen` 用の `InputActionAsset`。値変換は InputAction の `processors` 文字列に埋め込み |
| `AnalogBindingDemoCharacter.asset` | `FacialCharacterSO`（schemaVersion 2.0、`_analogBindings` は `inputActionRef + targetIdentifier + targetAxis` の 3 フィールドのみ） |
| `AnalogBindingDemoHUD.cs` | アナログ入力値 / BonePose 出力 / BlendShape 出力を OnGUI で可視化する HUD |
| `StreamingAssets/FacialControl/AnalogBindingDemoCharacter/profile.json` | 最小プロファイル定義 (schemaVersion 2.0、FacialController が起動時に自動探索) |
| `StreamingAssets/FacialControl/AnalogBindingDemoCharacter/analog_bindings.json` | アナログバインディングの正規化 JSON 表現 (3 フィールド schema、SO の Inspector 編集時に AutoExporter が同期) |
| `README.md` | 本ファイル |

モデル (prefab / FBX / VRM) はライセンスの都合で同梱しません。ユーザー自身で用意してください。

## InputAction processors の役割 (mapping field 撤去)

Phase 3.5 (inspector-and-data-model-redesign spec) で Domain `AnalogBindingEntry` から `Mapping` field（旧 `AnalogMappingFunction`）を撤去しました。代わりに dead-zone / scale / offset / curve / invert / clamp の値変換は **InputAction 側の processors 文字列**で完結させます (Decision 4 / Req 6.4 / 13.3)。

本サンプルの `AnalogBindingDemoActions.inputactions` は次の processor チェーンを宣言しています（`com.hidano.facialcontrol.inputsystem` パッケージが `InputSystem.RegisterProcessor` で登録する 6 種のうち 4 種を使用）。

| Action | processors |
|--------|-----------|
| `LeftStickX` | `analogDeadZone(min=0.1,max=1)` → `analogScale(factor=45)` → `analogClamp(min=-45,max=45)` |
| `LeftStickY` | `analogDeadZone(min=0.1,max=1)` → `analogInvert` → `analogScale(factor=30)` → `analogClamp(min=-30,max=30)` |
| `ArkitJawOpen` | `analogScale(factor=100)` → `analogClamp(min=0,max=100)` |

カスタム processor の名前定数は `Hidano.FacialControl.Adapters.Processors.AnalogProcessorRegistration.ProcessorNames` から参照可能です。

## 必要な Humanoid rig

`AnalogBindingDemoCharacter` の `_analogBindings` は以下の rig 構造に依存します。実機モデルではこれらの bone / BlendShape 名が一致している必要があります。

| ターゲット種別 | 必要な識別子 |
|------------|-----------|
| BonePose | `LeftEye`, `RightEye` (Humanoid の eye bones) |
| BlendShape | `jawOpen` (ARKit / PerfectSync 互換) |

> Humanoid アバターでない場合は SO の Inspector で `targetIdentifier` を rig に合わせて手動編集してください。

## セットアップ手順

### 1. Scene を開く

Package Manager で本サンプルを Import すると以下に展開されます。

- `Assets/Samples/FacialControl InputSystem/<version>/Analog Binding Demo/AnalogBindingDemo.unity`
- `Assets/Samples/FacialControl InputSystem/<version>/Analog Binding Demo/AnalogBindingDemoActions.inputactions`
- `Assets/Samples/FacialControl InputSystem/<version>/Analog Binding Demo/AnalogBindingDemoCharacter.asset`
- `Assets/Samples/FacialControl InputSystem/<version>/Analog Binding Demo/StreamingAssets/FacialControl/AnalogBindingDemoCharacter/profile.json`
- `Assets/Samples/FacialControl InputSystem/<version>/Analog Binding Demo/StreamingAssets/FacialControl/AnalogBindingDemoCharacter/analog_bindings.json`

`StreamingAssets/...` 配下の JSON は **Project ルート直下の `Assets/StreamingAssets/`** に手動で配置し直す必要があります。Sample import 後、フォルダごと `Assets/StreamingAssets/FacialControl/AnalogBindingDemoCharacter/` にコピー (または移動) してください。

`AnalogBindingDemo.unity` をダブルクリックで開いてください。Scene には既に以下の GameObject が存在します。

- **Character** — モデルを配置するルート GameObject
  - `Animator`
  - `FacialController`（`Character SO` に `AnalogBindingDemoCharacter` が結線済み）
  - `InputFacialControllerExtension`（`controller-expr` / `keyboard-expr` 予約 ID 登録）
  - `FacialCharacterInputExtension`（SO の `_analogBindings` から analog source / BonePose provider を構築）
- **Analog Binding Demo HUD**
  - `AnalogBindingDemoHUD`（`_facialController` は Character の FacialController を参照）
- Main Camera / Directional Light

### 2. モデルを Character の子に配置

Humanoid rig を持つモデル (FBX / VRM / prefab) を Character の子にドラッグ & ドロップしてください。

- LeftEye / RightEye の bone が rig 上で `LeftEye` / `RightEye` という名前で存在すること
- SkinnedMeshRenderer の sharedMesh に `jawOpen` BlendShape が存在すること

### 3. Play

Play モードに入ります。画面左上に HUD が表示されます。

- **左スティック** で LeftEye / RightEye の Euler が変化することを確認 (`analogDeadZone` → `analogScale` → `analogClamp` 経由で値変換される)
- **OSC** で `/avatar/parameters/jawOpen` または ARKit `/ARKit/jawOpen` を送信すると mouth-open BlendShape が動作 (uOsc サーバを Scene 上に配置している前提)

### トラブルシューティング

- **HUD が表示されない**: Play モードに入っているか、`Application.runInBackground` が有効か確認
- **eye が動かない**: SO Inspector の `_analogBindings` で `targetIdentifier` (LeftEye/RightEye) が rig の bone 名と完全一致しているか確認
- **`jawOpen` が動かない**: SkinnedMeshRenderer の sharedMesh に該当 BlendShape が存在するか確認
- **`analog*` processor が解決できない警告**: `AnalogProcessorRegistration` の `[InitializeOnLoad]` 経路が動作しているか確認 (Editor の Domain Reload が走った直後に自動登録される)

## カスタマイズ

### 独自 binding プロファイルへ差し替える

1. `AnalogBindingDemoCharacter` SO を Inspector で開き、`_analogBindings` セクションを直接編集する
2. SO の保存時に裏で `StreamingAssets/.../analog_bindings.json` が自動エクスポートされ、ランタイムへ反映されます

### 値変換パラメータをカスタマイズする

`AnalogBindingDemoActions.inputactions` を Inspector で開き、各 Action の `processors` 文字列を編集してください。利用可能な processor は次の 6 種です（パッケージ初期化時に登録）。

| 登録名 | パラメータ | 役割 |
|-------|----------|------|
| `analogDeadZone` | `min`, `max` (float) | 入力絶対値 ≤ min を 0、その上を min..max で正規化 |
| `analogScale` | `factor` (float) | 入力に factor を乗じる |
| `analogOffset` | `offset` (float) | 入力に offset を加える |
| `analogClamp` | `min`, `max` (float) | 入力を `[min, max]` にクランプ |
| `analogCurve` | `preset` (int) | 0=Linear / 1=EaseIn / 2=EaseOut / 3=EaseInOut |
| `analogInvert` | (なし) | 符号反転 |

旧 `AnalogMappingFunction` (deadzone / scale / offset / curve / invert / clamp) を保持していたユーザーは Migration Guide (`Documentation~/MIGRATION-v0.x-to-v1.0.md`) の対応表を参照してください。

### OSC で動作確認する

`com.hidano.facialcontrol.osc` パッケージの `OscReceiver` を Scene に配置してください。`arkit_jaw_open` などの sourceId が外部から push される構成であれば、`FacialCharacterInputExtension` が解決できなかったソースは ActionMap 経由で結線されないため、別途 OSC アダプタ経由での `IAnalogInputSource` 提供が必要です。

## 参考

- `Documentation~/quickstart.md`: コア機能のセットアップフロー全体
- `Documentation~/MIGRATION-v0.x-to-v1.0.md`: 旧 `AnalogMappingFunction` から InputAction processors への移行ガイド
- `.kiro/specs/inspector-and-data-model-redesign/`: 本サンプルが追従する破壊的変更の仕様書
- `.kiro/specs/analog-input-binding/`: 本機能の元仕様書
