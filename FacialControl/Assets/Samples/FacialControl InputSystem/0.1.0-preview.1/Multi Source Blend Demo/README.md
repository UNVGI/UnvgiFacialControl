# Multi Source Blend Demo

同一レイヤーに `controller-expr` と `keyboard-expr` の 2 つの ExpressionTrigger 入力源を並置し、ウェイトブレンドの振る舞いを OnGUI 経由で目視確認するための PlayMode 専用サンプルです。

## 同梱物

| ファイル | 役割 |
|---------|------|
| `MultiSourceBlendDemo.unity` | Animator / FacialController / Extension / InputBinder / HUD を結線済みの Scene |
| `MultiSourceBlendDemoProfile.asset` | `FacialProfileSO`（設計パターン呈示用。HUD が TextAsset 経由で初期化するため空設定） |
| `MultiSourceBlendDemoInputBinding.asset` | `InputBindingProfileSO`（キーボード 1/2/3/4 → smile/angry/surprise/troubled に Bind） |
| `MultiSourceBlendDemoHUD.cs` | Weight スライダー + 各ソースごとの TriggerOn/Off ボタンを表示する HUD コンポーネント |
| `multi_source_blend_demo.json` | 2 source on the same layer + 4 Expression (smile / angry / surprise / troubled) を宣言したプロファイル定義 |
| `README.md` | 本ファイル |

モデル (prefab / FBX / VRM) はライセンスの都合で同梱しません。ユーザー自身で用意してください。

## セットアップ手順

### 1. Scene を開く

Package Manager で本サンプルを Import すると
`Assets/Samples/FacialControl InputSystem/<version>/Multi Source Blend Demo/MultiSourceBlendDemo.unity`
に配置されます。これをダブルクリックで開いてください。

Scene には既に以下の GameObject が存在します。

- **Character** — ルート GameObject（以下のコンポーネント付与済み）
  - `Animator`（FacialController の `RequireComponent`）
  - `FacialController`（`Profile SO` は未設定。HUD が JSON を TextAsset 経由で初期化）
  - `InputFacialControllerExtension`（`controller-expr` / `keyboard-expr` アダプタ登録）
  - `FacialInputBinder`（キーボード入力 → Expression）
  - `MultiSourceBlendDemoHUD`（JSON TextAsset とウィジェット HUD）
- Main Camera / Directional Light

### 2. モデルを Character の子に配置

FBX / VRM / prefab いずれでも可。以下の BlendShape 名を持つ SkinnedMeshRenderer を持つモデルを推奨します。

| Expression | 必要な BlendShape 名 |
|-----------|-------------------|
| smile | `笑い`, `口角上げ` |
| angry | `怒り`, `左眉下げ`, `右眉下げ` |
| surprise | `びっくり`, `▲` |
| troubled | `困る`, `口角下げ` |

BlendShape 名が合致しないモデルを使う場合は、`multi_source_blend_demo.json` 内の `blendShapeValues` の `name` をモデルの BlendShape 名に書き換えてから Scene を再生してください。

モデル配置手順:

1. Hierarchy の **Character** を選択
2. モデル (FBX/VRM) の prefab を Hierarchy の **Character** に **ドラッグ&ドロップ**（子として配置）
3. SkinnedMeshRenderer は `FacialController.Skinned Mesh Renderers` が空なら子から自動検索されます

### 3. Play

Play モードに入ります。画面左上に HUD が表示されます。

- `Controller weight` / `Keyboard weight` スライダーで各ソースの重みを 0〜1 で調整
- `Controller 入力源` ブロックで smile / angry / surprise / troubled の **On / Off** をトリガー
- `Keyboard 入力源` ブロックで同様にトリガー
- 両ソースを同時にトリガーすると、モデルの BlendShape が加重和で合成される様子を確認できる
- **キーボード 1 / 2 / 3 / 4** キーでも `keyboard-expr` 経由で smile / angry / surprise / troubled が発火します（FacialInputBinder 経由）

### トラブルシューティング

- **HUD が表示されない**: Play モードに入っているか確認。`Application.runInBackground` を HUD.Awake で true に設定しているので Game View 非 focus でも動きます
- **BlendShape が動かない**: JSON の `name` とモデルの BlendShape 名が完全一致しているか確認（日本語・特殊記号の扱いに注意）
- **`controller-expr は profile.inputSources に未宣言`**: `MultiSourceBlendDemoHUD` の `Profile Json` フィールドに `multi_source_blend_demo.json` TextAsset が割り当たっているか確認
- **FacialController 初期化前**: HUD.Awake で InitializeWithProfile が呼ばれますが、`Profile Json` が未設定だと起動しません。Inspector で確認してください

## カスタマイズ

### 独自プロファイルに差し替える

`MultiSourceBlendDemoHUD._profileJson` に別の JSON TextAsset を割り当てるだけで差し替え可能です。JSON のスキーマは `Documentation~/json-schema.md` 参照。

### StreamingAssets ベース運用に切り替える

プロダクション運用では `StreamingAssets/FacialControl/` 配下に JSON を配置し、`FacialProfileSO.Json File Path` に相対パスを設定する方式を推奨します。以下の手順で切り替えられます。

1. `multi_source_blend_demo.json` を `Assets/StreamingAssets/FacialControl/multi_source_blend_demo.json` にコピー
2. `MultiSourceBlendDemoProfile.asset` の `Json File Path` に `FacialControl/multi_source_blend_demo.json` を設定
3. Scene 上の `FacialController.Profile SO` に `MultiSourceBlendDemoProfile` をドラッグ
4. `MultiSourceBlendDemoHUD._profileJson` を空にする（HUD の TextAsset bootstrap を無効化）

## 参考

- `Documentation~/quickstart.md`: コア機能のセットアップフロー全体
- `Documentation~/json-schema.md`: プロファイル JSON スキーマ詳細
