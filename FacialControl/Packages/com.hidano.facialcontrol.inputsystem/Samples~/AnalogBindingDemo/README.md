# Analog Binding Demo

`FacialAnalogInputBinder` を介して、アナログ入力 (右スティック / ARKit `jawOpen` / OSC float) を BonePose と BlendShape にリアルタイム駆動するサンプル。離散トリガー側の `FacialInputBinder` と並走する構成を確認するための PlayMode 専用サンプルです (Req 11.1〜11.6)。

## 同梱物

| ファイル | 役割 |
|---------|------|
| `AnalogBindingDemo.unity` | `FacialController` + `FacialAnalogInputBinder` + `FacialInputBinder` を併置した Scene |
| `AnalogBindingProfile.asset` | `AnalogInputBindingProfileSO` (右スティック → eye Euler + jawOpen → mouth-open BS) |
| `analog_binding_demo.json` | SO の JSON 表現 (Req 11.4)。Inspector の Import/Export ボタンから差し替え可能 |
| `AnalogBindingDemoHUD.cs` | アナログ入力値 / BonePose 出力 / BlendShape 出力を OnGUI で可視化する HUD |
| `README.md` | 本ファイル |

モデル (prefab / FBX / VRM) はライセンスの都合で同梱しません。ユーザー自身で用意してください。

## 必要な Humanoid rig

`AnalogBindingProfile` は以下の rig 構造に依存します。実機モデルではこれらの bone / BlendShape 名が一致している必要があります (Req 11.6)。

| ターゲット種別 | 必要な識別子 |
|------------|-----------|
| BonePose | `LeftEye`, `RightEye` (Humanoid の eye bones) |
| BlendShape | `jawOpen` (ARKit / PerfectSync 互換) |

> Humanoid アバターでない場合は `AnalogInputBindingProfileSOEditor` の **Humanoid 自動割当** ボタンが disabled になります。手動で `targetIdentifier` を rig に合わせて書き換えてください。

## セットアップ手順

### 1. Scene を開く

Package Manager で本サンプルを Import すると `Assets/Samples/FacialControl InputSystem/<version>/Analog Binding Demo/AnalogBindingDemo.unity` に配置されます。これをダブルクリックで開いてください。

Scene には既に以下の GameObject が存在します。

- **Character** — モデルを配置するルート GameObject
  - `Animator`
  - `FacialController`
  - `InputFacialControllerExtension`
  - `AnalogBlendShapeRegistration` (FacialAnalogInputBinder.OnEnable 時に自動付与される)
- **Facial Analog Input Binder** — アナログ入力を結線する GO
  - `FacialAnalogInputBinder` (`_profile` に AnalogBindingProfile.asset、`_actionAsset` に Analog ActionMap を持つ InputActionAsset)
- **Facial Input Binder** — 離散トリガー (`InputBindingProfileSO`) と並走する GO
  - `FacialInputBinder`
- **Analog Binding Demo HUD**
  - `AnalogBindingDemoHUD`
- Main Camera / Directional Light

### 2. モデルを Character の子に配置

Humanoid rig を持つモデル (FBX / VRM / prefab) を Character の子にドラッグ & ドロップしてください。

- LeftEye / RightEye の bone が rig 上で `LeftEye` / `RightEye` という名前で存在すること、または `AnalogInputBindingProfileSOEditor` の **Humanoid 自動割当** ボタンで Humanoid muscle 経由で解決できること
- SkinnedMeshRenderer の sharedMesh に `jawOpen` BlendShape が存在すること

### 3. Play

Play モードに入ります。画面左上に HUD が表示されます。

- **右スティック** で LeftEye / RightEye の Euler が変化することを確認
- **OSC** で `/avatar/parameters/jawOpen` または ARKit `/ARKit/jawOpen` を送信すると mouth-open BlendShape が動作 (uOsc サーバを Scene 上に配置している前提)

### トラブルシューティング

- **HUD が表示されない**: Play モードに入っているか、`Application.runInBackground` が有効か確認
- **eye が動かない**: `targetIdentifier` (LeftEye/RightEye) が rig の bone 名と完全一致しているか、Humanoid 自動割当ボタンを試す
- **`jawOpen` が動かない**: SkinnedMeshRenderer の sharedMesh に該当 BlendShape が存在するか確認

## カスタマイズ

### 独自 binding プロファイルへ差し替える

1. `AnalogBindingProfile.asset` を Inspector で開き、**Import JSON...** ボタンから別 JSON を読み込む
2. 別の `AnalogInputBindingProfileSO` を新規作成し、`FacialAnalogInputBinder._profile` を差し替える

### OSC で動作確認する

`com.hidano.facialcontrol.osc` パッケージの `OscReceiver` を Scene に配置し、`FacialAnalogInputBinder.RegisterExternalSource("arkit_jaw_open", new ArKitOscAnalogSource(...))` 等で外部ソースを登録してください。

## 参考

- `Documentation~/quickstart.md`: コア機能のセットアップフロー全体
- `.kiro/specs/analog-input-binding/`: 本機能の仕様書
