# Analog Binding Demo

新統合 SO (`FacialCharacterSO`) のアナログバインディングを介して、アナログ入力 (右スティック / ARKit `jawOpen` / OSC float) を BonePose と BlendShape にリアルタイム駆動するサンプル。1 SO + Scene 上のコンポーネント 1 個 (FacialController) という最小結線で動かせる構成を確認するための PlayMode 専用サンプルです。

## 同梱物

| ファイル | 役割 |
|---------|------|
| `AnalogBindingDemo.unity` | `FacialController` + `InputFacialControllerExtension` + `FacialCharacterInputExtension` + HUD を結線済みの Scene |
| `AnalogBindingDemoCharacter.asset` | `FacialCharacterSO`（右スティック → eye Euler + jawOpen → mouth-open BS をアナログバインディングで宣言） |
| `AnalogBindingDemoHUD.cs` | アナログ入力値 / BonePose 出力 / BlendShape 出力を OnGUI で可視化する HUD |
| `StreamingAssets/FacialControl/AnalogBindingDemoCharacter/profile.json` | 最小プロファイル定義 (FacialController が起動時に自動探索) |
| `StreamingAssets/FacialControl/AnalogBindingDemoCharacter/analog_bindings.json` | アナログバインディングの正規化 JSON 表現 (SO の Inspector 編集時に AutoExporter が同期) |
| `README.md` | 本ファイル |

モデル (prefab / FBX / VRM) はライセンスの都合で同梱しません。ユーザー自身で用意してください。

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

- **右スティック** で LeftEye / RightEye の Euler が変化することを確認
- **OSC** で `/avatar/parameters/jawOpen` または ARKit `/ARKit/jawOpen` を送信すると mouth-open BlendShape が動作 (uOsc サーバを Scene 上に配置している前提)

### トラブルシューティング

- **HUD が表示されない**: Play モードに入っているか、`Application.runInBackground` が有効か確認
- **eye が動かない**: SO Inspector の `_analogBindings` で `targetIdentifier` (LeftEye/RightEye) が rig の bone 名と完全一致しているか確認
- **`jawOpen` が動かない**: SkinnedMeshRenderer の sharedMesh に該当 BlendShape が存在するか確認

## カスタマイズ

### 独自 binding プロファイルへ差し替える

1. `AnalogBindingDemoCharacter` SO を Inspector で開き、`_analogBindings` セクションを直接編集する
2. SO の保存時に裏で `StreamingAssets/.../analog_bindings.json` が自動エクスポートされ、ランタイムへ反映されます

### OSC で動作確認する

`com.hidano.facialcontrol.osc` パッケージの `OscReceiver` を Scene に配置してください。`arkit_jaw_open` などの sourceId が外部から push される構成であれば、`FacialCharacterInputExtension` が解決できなかったソースは ActionMap 経由で結線されないため、別途 OSC アダプタ経由での `IAnalogInputSource` 提供が必要です。

## 参考

- `Documentation~/quickstart.md`: コア機能のセットアップフロー全体
- `.kiro/specs/analog-input-binding/`: 本機能の仕様書
