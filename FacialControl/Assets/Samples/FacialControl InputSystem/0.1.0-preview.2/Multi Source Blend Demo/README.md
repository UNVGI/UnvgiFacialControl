# Multi Source Blend Demo

同一レイヤーに `controller-expr` と `keyboard-expr` の 2 つの ExpressionTrigger 入力源を並置し、ウェイトブレンドの振る舞いを OnGUI 経由で目視確認するための PlayMode 専用サンプルです。

## 同梱物

- `MultiSourceBlendDemoHUD.cs` — Weight スライダー + 各ソースごとの TriggerOn/Off ボタンを表示する HUD コンポーネント
- `multi_source_blend_demo.json` — 2 source on the same layer + 5 Expression (smile / angry / surprise / troubled / blink) を宣言したプロファイル定義
- `README.md` (本ファイル)

モデル (prefab / FBX / VRM) はライセンスの都合で同梱しません。ユーザー自身で用意してください。

## セットアップ手順

### 1. JSON プロファイルを StreamingAssets に配置

`multi_source_blend_demo.json` を以下のパスにコピーしてください:

```
Assets/StreamingAssets/FacialControl/multi_source_blend_demo.json
```

`StreamingAssets/FacialControl/` フォルダが存在しない場合は作成。

### 2. FacialProfileSO を作成

Project ウィンドウで右クリック → `Create` → `FacialControl/Facial Profile` で `FacialProfileSO` Asset を作成。Inspector で以下を設定:

- `Json File Path`: `FacialControl/multi_source_blend_demo.json`

### 3. モデルの準備

以下の BlendShape 名を持つ SkinnedMeshRenderer が使えるモデルを用意してください (FBX / VRM 等):

| Expression | 必要な BlendShape 名 |
|-----------|-------------------|
| smile | `笑い`, `口角上げ` |
| angry | `怒り`, `左眉下げ`, `右眉下げ` |
| surprise | `びっくり`, `▲` |
| troubled | `困る`, `口角下げ` |
| blink | `まばたき` |

BlendShape 名が合致しないモデルを使う場合は、`multi_source_blend_demo.json` 内の `blendShapeValues` の `name` をモデルの BlendShape 名に書き換えてください。

### 4. シーンを組み立てる

1. モデルを Scene に配置
2. モデルのルート GameObject に以下のコンポーネントを追加:
   - `Animator` (`FacialController` が `RequireComponent`)
   - `FacialController`
     - `Profile SO`: 手順 2 で作った FacialProfileSO をドラッグ
   - `Multi Source Blend Demo HUD` (本サンプル)
     - `Facial Controller`: 同じ GameObject の FacialController をドラッグ
3. Play

### 5. 動作確認

Play 中、画面左上に HUD が表示されます。

- `Controller weight` / `Keyboard weight` スライダーで各ソースの重みを 0〜1 で調整
- `Controller 入力源` ブロックで smile / angry / surprise / troubled の **On / Off** をトリガー
- `Keyboard 入力源` ブロックで同様にトリガー
- 両ソースを同時にトリガーすると、モデルの BlendShape が加重和で合成される様子を確認できる

### トラブルシューティング

- **HUD が表示されない**: Play モードに入っているか確認。`Application.runInBackground` を HUD.Start で true に設定しているので Game View 非 focus でも動く
- **BlendShape が動かない**: JSON の `name` とモデルの BlendShape 名が完全一致しているか確認 (日本語・特殊記号の扱いに注意)
- **"controller-expr は profile.inputSources に未宣言"**: FacialProfileSO の JSON パスが正しく、プロファイルが load できているか確認

## 参考

- FacialControl README: コア機能の概要と公開 API 一覧
- `docs/migration-guide.md`: preview の破壊的変更と既存アセットの移行手順
