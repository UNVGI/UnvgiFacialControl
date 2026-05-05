# FacialControl OSC

`com.hidano.facialcontrol` の OSC（VRChat / ARKit 互換）配信アダプタ。

## 概要

このパッケージは FacialControl コアに OSC 入出力機能を追加します。

- **OscAdapterBinding**: `FacialCharacterProfileSO` の `_adapterBindings` に追加するだけで OSC 受信ソケットの確保・helper MonoBehaviour の AddComponent・`InputSourceRegistry` への登録までを 1 binding で完結する `AdapterBindingBase` 具象（`displayName: "OSC"`）
- **ArKitOscAdapterBinding**: ARKit / PerfectSync の OSC 受信を 1 binding にまとめた具象（`displayName: "ARKit / PerfectSync"`）
- **OscReceiverHost / OscSenderHost**: binding 配下で `AddComponent` される helper MonoBehaviour。uOSC サーバー / クライアントをラップし、受信データを `OscDoubleBuffer`（`Interlocked.Exchange` ベースの非ブロッキングダブルバッファ）に書き込む
- **OscInputSource**: OSC 受信値を `IInputSource` として供給する実装。`OscAdapterBinding` 内部で構築され、`InputSourceRegistry.Register(slug, source)` 経由で各レイヤーから参照される

## 依存パッケージ

| パッケージ | バージョン | 用途 |
|---|---|---|
| `com.hidano.facialcontrol` | 0.1.0-preview.1 以降 | コア機能 |
| `com.hidano.uosc` | 1.0.0 | OSC（UDP）通信 |

## 使い方

1. `com.hidano.facialcontrol` と本パッケージを `Packages/manifest.json` に追加
2. キャラクターの GameObject に `FacialController` を追加し、`FacialCharacterProfileSO` を結線
3. `FacialCharacterProfileSO` Inspector の **Adapter Bindings** セクションで Add ドロップダウンから `OSC`（および必要に応じて `ARKit / PerfectSync`）を追加
4. 各 binding の inline UI で endpoint / port / blendshape マッピングを設定する。受信ソケットと helper MonoBehaviour の確保は `OnStart` 内で binding 自身が行う
5. レイヤー側の `inputSources` リストに `{ slug = "<binding に設定した slug>", weight = 1.0 }` を追加すると OSC 受信値がそのレイヤーに合流する。同一レイヤーに InputSystem 由来の trigger や analog などを並置すれば加重和ブレンディングが効く

## OSC アドレス形式

- VRChat 互換: `/avatar/parameters/{BlendShapeName}`
- ARKit 互換: `/ARKit/{BlendShapeName}`
- カスタムマッピングは `config.json` の `osc.mapping` 配列で定義

詳細は [JSON スキーマリファレンス](../com.hidano.facialcontrol/Documentation~/json-schema.md) を参照。

## ライセンス

[MIT License](../com.hidano.facialcontrol/LICENSE.md)
