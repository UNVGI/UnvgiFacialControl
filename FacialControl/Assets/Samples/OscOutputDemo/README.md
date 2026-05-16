# OscOutputDemo

`OscSenderAdapterBinding` の送信側サンプルです。`OscOutputDemo.unity` を開いて Play すると、手続き生成した最小メッシュの BlendShape 値と `eye_look` の Gaze Vector2 が OSC bundle として送信されます。

## 含まれるもの

| ファイル | 役割 |
|---|---|
| `OscOutputDemo.unity` | 送信側サンプル Scene |
| `OscOutputDemoProfile.asset` | `OscOutputDemoSignalBinding` と `OscSenderAdapterBinding` を結線済みの `FacialCharacterProfileSO` |
| `OscOutputDemoBootstrap.cs` | 実行時に最小 SkinnedMeshRenderer を作り、デモ用 BlendShape / Gaze 入力源を登録するサンプルコード |
| `OscSenderOptions.json` | Scene 内の送信設定と同じ内容の `OscSenderOptionsDto` サンプル |

## 送信内容

- VRChat 形式 endpoint: `127.0.0.1:9000`
- ARKit / PerfectSync 形式 endpoint: `127.0.0.1:9001`
- BlendShape: `Joy`, `Blink`, `MouthOpen`, `BrowUp`
- Gaze: `eye_look`
- ループバック抑制: 有効
- heartbeat: 5 秒

`OscSenderOptions.json` は手動設定やドキュメント確認用のサンプルです。実際に Play で使われる設定は `OscOutputDemoProfile.asset` の `OscSenderAdapterBinding` に同じ値として保存されています。

## 確認手順

1. `OscOutputDemo.unity` を開きます。
2. 必要に応じて `OscOutputDemoProfile.asset` の `OSC Sender` endpoint を受信アプリの IP / port に変更します。
3. Play します。
4. 受信側で `/avatar/parameters/Joy` などの BlendShape と `/avatar/parameters/eye_lookX` / `/avatar/parameters/eye_lookY`、または `/ARKit/eyeLook...` が届くことを確認します。

`Samples~/OscOutputDemo` を編集した場合は、同じ変更を `Assets/Samples/OscOutputDemo` にも同期してください。
