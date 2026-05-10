# Multi Source Blend Demo

単一レイヤーに `input-system` slug の `InputSystemAdapterBinding` (controller / keyboard 双方の Trigger Action を受ける) を配置してウェイトブレンドの振る舞いを確認するとともに、Vector2 入力 (左スティック / WASD) で両目を同時駆動するアナログ操作 (目線) の挙動を目視するための PlayMode 専用サンプルです。

## 同梱物

| ファイル | 役割 |
|---------|------|
| `MultiSourceBlendDemo.unity` | Animator / FacialController / HUD を結線済みの Scene (新アーキテクチャでは scene 上に追加 MonoBehaviour Extension は不要) |
| `MultiSourceBlendDemoCharacter.asset` | `FacialCharacterProfileSO`（`_adapterBindings` に `InputSystemAdapterBinding` を保持。表情 5 種 (smile / anger / surprise / lipsync_a + 目線) と Trigger1〜10 + Look (Vector2) のバインディングを内蔵） |
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
  - `FacialController`（SO フィールドに `MultiSourceBlendDemoCharacter` が結線済み）
- **Multi Source Blend Demo HUD** — OnGUI ウィジェットを担う GO
  - `MultiSourceBlendDemoHUD`（`_facialController` は Character の FacialController を参照、`_inputSourceSlug = "input-system"` で slug 駆動）
- Main Camera / Directional Light

> 新アーキテクチャでは `_adapterBindings` 内の `InputSystemAdapterBinding` が `FacialController.Initialize` 時に per-FC `LifetimeScope` 上で自動的に `OnStart` を実行し、`InputActionAsset.Instantiate` + `ActionMap.Enable` + Expression / Analog バインディングの結線を行います。scene 上に Extension MonoBehaviour を追加する必要はありません。

### 2. モデルを Character の子に配置

FBX / VRM / prefab いずれでも可。以下の BlendShape 名を持つ SkinnedMeshRenderer を持つモデルを推奨します。

| Expression | 必要な BlendShape 名 |
|-----------|-------------------|
| smile | `笑い`, `口角上げ` |
| anger | `怒り`, `左眉下げ`, `右眉下げ` |
| surprise | `びっくり`, `▲` |
| lipsync_a | `あ` |
| blink_overlay | `まばたき`（default overlay として使われる） |
| smile_closed_eye | `笑い`, `ウィンク`, `ウィンク右`（overlay suppress デモ用の目閉じ笑顔） |

BlendShape 名が合致しないモデルを使う場合は、`MultiSourceBlendDemoCharacter` SO の Inspector を開き、Expression セクションで BlendShape 名をモデルに合わせて編集してください。SO の保存時に裏で `StreamingAssets/.../profile.json` が自動エクスポートされ、ランタイムへ反映されます。

モデル配置手順:

1. Hierarchy の **Character** を選択
2. モデル (FBX/VRM) の prefab を Hierarchy の **Character** に **ドラッグ&ドロップ**（子として配置）
3. SkinnedMeshRenderer は `FacialController.Skinned Mesh Renderers` が空なら子から自動検索されます

### 3. Play

Play モードに入ります。画面左上に HUD が表示されます。

- `Input weight` スライダーで `input-system` ソースの重みを 0〜1 で調整
- `入力源` ブロックで smile / anger / surprise / lipsync_a の **On / Off** をトリガー
- **コントローラ / キーボード** いずれの入力でも同じ `input-system` ソースに集約され、Action 名 (Trigger1〜4) → Expression ID マッピングで発火します
- **キーボード 1 / 2 / 3 / 4** キーでも `input-system` 経由で smile / anger / surprise / lipsync_a が発火します（`InputSystemAdapterBinding` の `ExpressionBindings` で Action 名 → Expression ID をマッピング）
- **左スティック** または **WASD** で `Look` (Vector2) を入力すると、SO の Inspector で目線アナログ表情に設定した両目ボーン (主) や BlendShape (オプション) が同時に駆動されます。
  - 目線ボーンは「参照モデルから自動設定」ボタンで、ボーン名・初期回転に加えて世界の上下/左右軸が各目ボーンの local 空間でどの方向に対応するかを自動算出します。モデルを差替えたら必ず再実行してください。
  - 「可動範囲 (角度制限)」 foldout で上方向/下方向/外側/内側の最大角度を設定できます。例えば「向かって左に視線を送るとき、向かって左の眼は外側、向かって右の眼は内側の値で動く」非対称制限により、両目を完全同角度で動かすことによる違和感を回避できます。
- **キーバインディング** の動作モードは「Hold (押下中のみ ON)」が既定。リリースで OFF に戻ります。トグル動作 (押すたびに ON/OFF 切替) が必要なバインディングは Inspector で「動作モード」を `Toggle` に変更してください。
- **左トリガー (LT)** で smile を Analog 駆動 (押し量に応じて smile 全体の BlendShape をスケール、`input:analog-expression` 経路)。
- **右トリガー (RT)** は **Overlay モード** で `blink` slot を駆動します。active 表情の `overlays.blink` を解決し、無ければ profile の `defaultOverlays.blink` (= `blink_overlay`) にフォールバックします。これがユーザー要望「ボタン操作の表情に対し、追加で Trigger を押すと目だけ閉じる、ただし表情ごとに違う最終形にしたい」を解決する仕組みです。
  - 例 1: Trigger1 で smile を ON にした上で RT を引くと、`overlay` レイヤー (priority=1) に `blink_overlay` が立ち、`emotion` レイヤー (smile) の眉・口角は ContributeMask off で貫通、`まばたき` だけが lerp で 0→1 に補間されます。
  - 例 2: Trigger5 で `smile_closed_eye` を ON にした状態で RT を引くと、Expression 側の `overlays.blink = ""`（明示 suppress）が効き overlay は発火しません。「すでに目を閉じている表情で RT を引いても二重に閉じない」を実現します。
  - 各表情ごとの「目閉じ最終形」を別 Expression として用意し、SO Inspector の Expression エントリの `overlays` リストに `slot:blink, expressionId:<その表情用 *_blink>` を登録するとコンテキスト連動が可能。`expressionId` を空にすると当該 slot を suppress 扱いにできます。

参考: 「smile (普通) Hold + RT 全押し」では smile に overlays.blink が宣言されていないため、profile-level default である `blink_overlay` が発火して `まばたき` だけが lerp で立ち上がります。

### トラブルシューティング

- **HUD が表示されない**: Play モードに入っているか確認。`Application.runInBackground` を HUD.Awake で true に設定しているので Game View 非 focus でも動きます
- **BlendShape が動かない**: SO の Expression セクションの BlendShape 名とモデルの BlendShape 名が完全一致しているか確認（日本語・特殊記号の扱いに注意）
- **`input-system は profile.inputSources に未宣言`**: `StreamingAssets/FacialControl/MultiSourceBlendDemoCharacter/profile.json` が `Assets/StreamingAssets/...` 配下に展開されているか、SO の `_layers[].inputSources` に `input-system` slug が宣言されているか、`_adapterBindings` 内の `InputSystemAdapterBinding.Slug` が一致しているかを確認
- **FacialController 初期化前**: SO に StreamingAssets パス相当の JSON が無く、SO のフィールドも空の場合は初期化されません。Inspector で SO の状態を確認してください

## カスタマイズ

### 独自プロファイルに差し替える

`MultiSourceBlendDemoCharacter` SO の Inspector で Expression セクションを直接編集するか、`Tools / FacialControl / Force Export Selected Character SO` メニューから StreamingAssets への即時反映が可能です。SO 自体を別の `FacialCharacterProfileSO` アセットに差し替えるだけで設定一式が切り替わります。

## 参考

- `Documentation~/quickstart.md`: コア機能のセットアップフロー全体
- `Documentation~/json-schema.md`: プロファイル JSON スキーマ詳細 (上級者向け、通常運用では Inspector のみで完結)
