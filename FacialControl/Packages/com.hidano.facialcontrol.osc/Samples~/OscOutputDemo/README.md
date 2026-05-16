# OscOutputDemo

`OscSenderAdapterBinding` の送信側サンプルです。`OscOutputDemo.unity` を開き、お手持ちのキャラモデルを Scene に置いた状態で Play すると、モデルの全 BlendShape 値と Gaze Vector2 が OSC bundle として送信されます。

## 同梱されているもの

| ファイル | 役割 |
|---|---|
| `OscOutputDemo.unity` | `FacialController` と `OscOutputDemoProfile` を結線済みの最小 Scene |
| `OscOutputDemoProfile.asset` | `OscOutputDemoSignalBinding`（sin 波 demo 信号源）と `OscSenderAdapterBinding`（OSC 送信）を結線済みの `FacialCharacterProfileSO` |
| `OscOutputDemoBootstrap.cs` | `Application.runInBackground = true` を有効化する最小 helper（ウィンドウ非フォーカス時の OSC 送信用） |
| `OscSenderOptions.json` | Scene 内設定と同等の `OscSenderOptionsDto` サンプル（参考用） |

> キャラモデル (FBX / VRM / prefab) は同梱していません。お手持ちのものを用意してください。

## 送信される内容

- VRChat 形式 endpoint: `127.0.0.1:9000` (`/avatar/parameters/{BlendShape 名}`)
- ARKit / PerfectSync 形式 endpoint: `127.0.0.1:9001` (`/ARKit/{BlendShape 名}`)
- BlendShape: **モデルが持つ全 BlendShape を自動送信**（`BlendShape Names (Optional Filter)` を空にしてあるため）
- Gaze: **Profile の `Gaze Configs` で宣言された `expressionId` を自動送信**（`Gaze Expression Ids (Optional Filter)` を空にしてあるため。既定では `eye_look` 1 種類が定義されている）
- ループバック抑制: 有効（同一プロセス内の受信を抑止）
- heartbeat: 5 秒周期で `/_facialcontrol/blendshape_names` を送出（受信側の名前一覧整合性検査用）

## 手順

1. **シーンを開く**: Project ウィンドウで `OscOutputDemo.unity` をダブルクリック。Hierarchy に `Character / Main Camera / Directional Light` が並びます。
2. **モデルを置く**: お手持ちのキャラモデルの prefab を Hierarchy の **`Character` の子**にドラッグして配置します。`FacialController` は子の `SkinnedMeshRenderer` を自動探索するため、特別な結線は不要です。
3. **endpoint を必要に応じて変更**: 別 PC や別アプリへ送るときは `OscOutputDemoProfile.asset` を選択し、Inspector の **`OSC Sender` → `Endpoints`** 内の `endpoint` / `port` を変更します。
4. **Play**: 受信側で `/avatar/parameters/{各 BlendShape 名}` と `/avatar/parameters/eye_lookX` / `eye_lookY`、または `/ARKit/...` 系メッセージが届くことを確認します。

## subset 配信したい場合

「200 個ある BlendShape のうち口形状 30 個だけ送信したい」のようなケースのみ、`OscOutputDemoProfile.asset` → `OSC Sender` → `BlendShape Names (Optional Filter)` に名前を列挙してください。空のままなら全送信が既定動作です。

Gaze 側も同様に subset 配信したい時のみ `Gaze Expression Ids (Optional Filter)` を明示します。

## トラブルシューティング

- **送信されない**: Hierarchy の `Character` 配下にモデルの `SkinnedMeshRenderer` が居るか確認。`FacialController` の `Skinned Mesh Renderers` が空のとき、子 GameObject から自動探索します。
- **モデルの BlendShape が動かない**: モデルに BlendShape が定義されているか、Mesh Inspector で確認。BlendShape を持たない rigid mesh では Sender が値を送出しません（heartbeat のみ送出）。
- **同一プロセスで受信側 (`OscReceiverDemo`) も動かしたい**: `OSC Sender` の `Suppress Loopback` を ✗ OFF にしてください（既定は ON で、同一プロセス内 receiver と同じ port の送信を抑止します）。
