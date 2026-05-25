# OscReceiverDemo

`OscReceiverAdapterBinding` の受信専用サンプルです。`OscReceiverDemo.unity` を開き、お手持ちのキャラモデルを Scene に置いた状態で Play すると、`127.0.0.1:9000` で受信した `/avatar/parameters/{BlendShape 名}` 系 OSC メッセージをモデルの BlendShape に反映します。

## 同梱されているもの

| ファイル | 役割 |
|---|---|
| `OscReceiverDemo.unity` | `FacialController` と `OscReceiverDemoProfile` を結線済みの最小 Scene |
| `OscReceiverDemoProfile.asset` | `OscReceiverAdapterBinding`（OSC 受信）を結線済みの `FacialCharacterProfileSO`。Layer は 1 つだけ（OSC 入力を素通し） |
| `OscReceiverDemoBootstrap.cs` | `Application.runInBackground = true` を有効化する最小 helper |
| `OscReceiverOptions.json` | Scene 内設定と同等の `OscReceiverOptionsDto` サンプル（参考用） |

> キャラモデル (FBX / VRM / prefab) は同梱していません。お手持ちのものを用意してください。

## 受信内容

- listen endpoint: `127.0.0.1:9000`
- BlendShape: `OscReceiverDemoProfile.asset` の `OSC` → `Mappings` で定義されたアドレス → BlendShape 名のマッピング（既定では `Joy / Blink / MouthOpen / BrowUp` の 4 種類のサンプル）
- Gaze: VRChat 形式 X/Y 2 メッセージから Vector2 を復元する `Gaze_VRChat_XY` mode entry が必要（既定では `eye_look` が登録済み）
- staleness: 1 秒受信が途絶えると base 表情へ復帰
- bundle mode: atomic swap

## 手順

1. **シーンを開く**: Project ウィンドウで `OscReceiverDemo.unity` をダブルクリック。Hierarchy に `Character / Main Camera / Directional Light` が並びます。
2. **モデルを置く**: お手持ちのキャラモデルの prefab を Hierarchy の **`Character` の子**にドラッグして配置します。
3. **`Mappings` をモデルに合わせる（重要）**: `OscReceiverDemoProfile.asset` を Inspector で開き、**`OSC` → `Mappings`** に並ぶエントリの `expressionId` をモデル実装の BlendShape 名に書き換えます。`Joy / Blink / MouthOpen / BrowUp` というサンプル名はあくまでプレースホルダで、ほとんどのモデルでは異なる名前（`笑い` / `怒り` 等の日本語名や `eyeBlink_L` のような英語名）になっています。
4. **listen port を必要に応じて変更**: 別 port で受けたいとき、`OscReceiverDemoProfile.asset` の `OSC` → `_endpoint` / `_port` を変更します。
5. **Play**: 送信側（`OscOutputDemo` 等）から `127.0.0.1:9000` に向けて OSC を送ると、モデルの BlendShape が更新されます。

## 将来の改善

現状、受信側 `Mappings` の `expressionId` 列挙は手作業です。送信側 heartbeat (`/_facialcontrol/blendshape_names`) を活用した「受信側 `Mappings` 自動生成」は別 spec で計画されており、それが入ると `Mappings` を空のままで送信側に流れる全 BlendShape を自動的にローカル mesh に貼り付けられるようになります。

## トラブルシューティング

- **何も動かない**: Hierarchy の `Character` 配下にモデルの `SkinnedMeshRenderer` が居るか確認。`Mappings` の `expressionId` がモデル実装の BlendShape 名と一致しているか確認（不一致のときは heartbeat 整合性検査が警告ログを出します）。
- **送信側と同居して動かしたい**: `OscOutputDemo` 側の `OSC Sender` で `Suppress Loopback` を ✗ OFF にする必要があります。
