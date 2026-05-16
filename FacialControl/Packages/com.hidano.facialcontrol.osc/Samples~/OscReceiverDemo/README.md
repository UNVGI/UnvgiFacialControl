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

## BlendShape 名を変更したいとき

`OscReceiverDemoProfile.asset` 内の **`OSC` → `Mappings`** が Single Source of Truth。Bootstrap はここから `mode = Normal_BlendShape` の entry を抽出して `expressionId` を BlendShape 名として手続きメッシュに登録する（`OscReceiverDemoBootstrap.ResolveBlendShapeNames`）。Bootstrap 側に名前リストを別途設定する必要はない。

新しい BlendShape を 1 つ追加するには `Mappings` に 1 行追加するだけで OK:
- `mode = Normal_BlendShape`
- `expressionId = 追加したい BlendShape 名`
- `addressPattern = /avatar/parameters/{追加したい BlendShape 名}`（VRChat 互換）

送信側（`OscOutputDemoProfile`）の `OSC Sender` の `BlendShape Mapping Names` も同名で揃える必要がある。揃わないと heartbeat consistency checker が警告を出して該当 BlendShape を skip する。

`Samples~/OscReceiverDemo` を編集した場合は、同じ変更を `Assets/Samples/OscReceiverDemo` にも同期してください。
