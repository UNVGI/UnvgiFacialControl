# Multi Source Blend Demo

同一レイヤーに `controller-expr` と `keyboard-expr` の 2 つの ExpressionTrigger 入力源を並置してウェイトブレンドの振る舞いを確認するとともに、Vector2 入力 (左スティック / WASD) で両目を同時駆動するアナログ操作 (目線) の挙動を目視するための PlayMode 専用サンプルです。

## 同梱物

| ファイル | 役割 |
|---------|------|
| `MultiSourceBlendDemo.unity` | Animator / FacialController / Extension / HUD を結線済みの Scene |
| `MultiSourceBlendDemoCharacter.asset` | `FacialCharacterSO`（schema v2.0、表情 5 種 (smile / anger / surprise / lipsync_a + 目線) と Trigger1〜10 + Look (Vector2) のバインディングを内蔵） |
| `MultiSourceBlendDemoActions.inputactions` | Expression 用 Trigger1〜10 + 目線アナログ操作用 Look (Vector2) を 1 つの ActionMap "Expression" にまとめた InputActionAsset |
| `Smile.anim` / `Anger.anim` / `Surprise.anim` / `Lipsync_A.anim` | 各表情の AnimationClip。`FacialControlMeta_Set` AnimationEvent で transitionDuration / transitionCurvePreset を内蔵 |
| `MultiSourceBlendDemoHUD.cs` | Weight スライダー + 各ソースごとの TriggerOn/Off ボタン + 目線アナログ操作関連 BlendShape の現在値を表示する HUD コンポーネント |
| `StreamingAssets/FacialControl/MultiSourceBlendDemoCharacter/profile.json` | 表情定義 (FacialController が起動時に自動探索) |
| `README.md` | 本ファイル |

モデル (prefab / FBX / VRM) はライセンスの都合で同梱しません。ユーザー自身で用意してください。

## セットアップ手順

### 1. Scene を開く

Package Manager で本サンプルを Import すると以下に展開されます。

- `Assets/Samples/FacialControl InputSystem/<version>/Multi Source Blend Demo/MultiSourceBlendDemo.unity`
- `Assets/Samples/FacialControl InputSystem/<version>/Multi Source Blend Demo/MultiSourceBlendDemoCharacter.asset`
- `Assets/Samples/FacialControl InputSystem/<version>/Multi Source Blend Demo/StreamingAssets/FacialControl/MultiSourceBlendDemoCharacter/profile.json`

`StreamingAssets/...` 配下の JSON は **Project ルート直下の `Assets/StreamingAssets/`** に手動で配置し直す必要があります。Sample import 後、フォルダごと `Assets/StreamingAssets/FacialControl/MultiSourceBlendDemoCharacter/` にコピー (または移動) してください。これにより `FacialController` の OnEnable で `Application.streamingAssetsPath/FacialControl/{SO 名}/profile.json` が自動探索されます。

`MultiSourceBlendDemo.unity` をダブルクリックで開いてください。Scene には既に以下の GameObject が存在します。

- **Character** — モデルを配置するルート GameObject
  - `Animator`（FacialController の `RequireComponent`）
  - `FacialController`（`Character SO` に `MultiSourceBlendDemoCharacter` が結線済み）
  - `InputFacialControllerExtension`（`controller-expr` / `keyboard-expr` 予約 ID を InputSourceFactory に登録）
  - `FacialCharacterInputExtension`（SO の `_inputActionAsset` から ActionMap を Instantiate/Enable し、Expression / Analog バインディングを結線）
- **Multi Source Blend Demo HUD** — OnGUI ウィジェットを担う GO
  - `MultiSourceBlendDemoHUD`（`_facialController` は Character の FacialController を参照）
- Main Camera / Directional Light

> `InputFacialControllerExtension` と `FacialCharacterInputExtension` はいずれも `[RequireComponent(typeof(FacialController))]` 相当の前提を持ち、Character GameObject に同居している必要があります。

### 2. モデルを Character の子に配置

FBX / VRM / prefab いずれでも可。以下の BlendShape 名を持つ SkinnedMeshRenderer を持つモデルを推奨します。

| Expression | 必要な BlendShape 名 |
|-----------|-------------------|
| smile | `笑い`, `口角上げ` |
| anger | `怒り`, `左眉下げ`, `右眉下げ` |
| surprise | `びっくり`, `▲` |
| lipsync_a | `あ` |

BlendShape 名が合致しないモデルを使う場合は、`MultiSourceBlendDemoCharacter` SO の Inspector を開き、Expression セクションで BlendShape 名をモデルに合わせて編集してください。SO の保存時に裏で `StreamingAssets/.../profile.json` が自動エクスポートされ、ランタイムへ反映されます。

モデル配置手順:

1. Hierarchy の **Character** を選択
2. モデル (FBX/VRM) の prefab を Hierarchy の **Character** に **ドラッグ&ドロップ**（子として配置）
3. SkinnedMeshRenderer は `FacialController.Skinned Mesh Renderers` が空なら子から自動検索されます

### 3. Play

Play モードに入ります。画面左上に HUD が表示されます。

- `Controller weight` / `Keyboard weight` スライダーで各ソースの重みを 0〜1 で調整
- `Controller 入力源` ブロックで smile / anger / surprise / lipsync_a の **On / Off** をトリガー
- `Keyboard 入力源` ブロックで同様にトリガー
- 両ソースを同時にトリガーすると、モデルの BlendShape が加重和で合成される様子を確認できる
- **キーボード 1 / 2 / 3 / 4** キーでも `keyboard-expr` 経由で smile / anger / surprise / lipsync_a が発火します（SO の `ExpressionBindings` セクションで Action 名 → Expression ID をマッピング）
- **左スティック** または **WASD** で `Look` (Vector2) を入力すると、SO の Inspector で目線アナログ表情に設定した両目ボーン (主) や BlendShape (オプション) が同時に駆動されます。ボーンパス・BlendShape 名はモデルに合わせて Inspector で編集してください

### トラブルシューティング

- **HUD が表示されない**: Play モードに入っているか確認。`Application.runInBackground` を HUD.Awake で true に設定しているので Game View 非 focus でも動きます
- **BlendShape が動かない**: SO の Expression セクションの BlendShape 名とモデルの BlendShape 名が完全一致しているか確認（日本語・特殊記号の扱いに注意）
- **`controller-expr は profile.inputSources に未宣言`**: `StreamingAssets/FacialControl/MultiSourceBlendDemoCharacter/profile.json` が `Assets/StreamingAssets/...` 配下に展開されているか、SO の `_layers[].inputSources` に両ソースが宣言されているかを確認
- **FacialController 初期化前**: SO に StreamingAssets パス相当の JSON が無く、SO のフィールドも空の場合は初期化されません。Inspector で SO の状態を確認してください

## カスタマイズ

### 独自プロファイルに差し替える

`MultiSourceBlendDemoCharacter` SO の Inspector で Expression セクションを直接編集するか、`Tools / FacialControl / Force Export Selected Character SO` メニューから StreamingAssets への即時反映が可能です。SO 自体を別の `FacialCharacterSO` アセットに差し替えるだけで設定一式が切り替わります。

## 参考

- `Documentation~/quickstart.md`: コア機能のセットアップフロー全体
- `Documentation~/json-schema.md`: プロファイル JSON スキーマ詳細 (上級者向け、通常運用では Inspector のみで完結)
