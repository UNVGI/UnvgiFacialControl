# OscReceiverDemo

`OscAdapterBinding` の受信専用サンプルです。`OscReceiverDemo.unity` を開いて Play すると、`127.0.0.1:9000` で `/avatar/parameters/{name}` 形式の OSC を受信し、最小構成の `FacialCharacterProfileSO` から生成した手続きメッシュへ BlendShape 値を反映します。

## 含まれるもの

| ファイル | 役割 |
|---|---|
| `OscReceiverDemo.unity` | 受信専用サンプル Scene |
| `OscReceiverDemoProfile.asset` | `OscAdapterBinding` と 1 Layer の入力源だけを持つ最小 `FacialCharacterProfileSO` |
| `OscReceiverDemoBootstrap.cs` | 実行時に最小 `SkinnedMeshRenderer` を作り、Scene と profile を結線するサンプルコード |
| `OscReceiverOptions.json` | Scene 内の受信設定と同じ内容の `OscReceiverOptionsDto` サンプル |

## 受信内容

- listen endpoint: `127.0.0.1:9000`
- BlendShape: `Joy`, `Blink`, `MouthOpen`, `BrowUp`
- Gaze: `/avatar/parameters/eye_lookX` / `/avatar/parameters/eye_lookY`
- staleness: 1 秒で base に戻す
- bundle mode: atomic swap

`OscReceiverOptions.json` は手動設定やドキュメント確認用のサンプルです。Play で使われる設定は `OscReceiverDemoProfile.asset` の `OscAdapterBinding` に同じ値として保存されています。

## 確認手順

1. `OscReceiverDemo.unity` を開きます。
2. 必要に応じて `OscReceiverDemoProfile.asset` の `OSC` listen port を送信側に合わせます。
3. Play します。
4. `OscOutputDemo` などの送信側から `127.0.0.1:9000` に送ると、受信側の手続きメッシュの BlendShape が更新されます。

`Samples~/OscReceiverDemo` を編集した場合は、同じ変更を `Assets/Samples/OscReceiverDemo` にも同期してください。
