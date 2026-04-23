# FacialControl OSC

`com.hidano.facialcontrol` の OSC（VRChat / ARKit 互換）配信アダプタ。

## 概要

このパッケージは FacialControl コアに OSC 入出力機能を追加します。

- **OscReceiver**: uOSC サーバーをラップし、受信データを `OscDoubleBuffer`（`Interlocked.Exchange` ベースの非ブロッキングダブルバッファ）に書き込む
- **OscSender**: uOSC クライアントをラップし、BlendShape 値を OSC アドレスに送信する
- **OscInputSource**: 予約 id `osc` の `IInputSource` 実装。受信データを `FacialController` の入力源として接続
- **OscFacialControllerExtension**: `FacialController` と同じ GameObject に配置するだけで OSC 入力をフックする MonoBehaviour

## 依存パッケージ

| パッケージ | バージョン | 用途 |
|---|---|---|
| `com.hidano.facialcontrol` | 0.1.0-preview.1 以降 | コア機能 |
| `com.hidano.uosc` | 1.0.0 | OSC（UDP）通信 |

## 使い方

1. `com.hidano.facialcontrol` と本パッケージを `Packages/manifest.json` に追加
2. キャラクターの GameObject に `FacialController` を追加
3. 同 GameObject に `OscReceiver`（受信）／`OscSender`（送信）を追加し、ポート設定
4. 同 GameObject に **OSC Facial Extension** (`OscFacialControllerExtension`) を追加
   - Receiver / Sender 参照は同 GameObject から自動取得される
5. プロファイル JSON の `layers[*].inputSources` に `{"id": "osc", "weight": 1.0}` を含めると OSC 受信値がそのレイヤーに合流する

```csharp
// 手動でコード配線する場合
var factory = new InputSourceFactory();
OscRegistration.Register(factory, oscReceiver.Buffer, new UnityTimeProvider());
```

## OSC アドレス形式

- VRChat 互換: `/avatar/parameters/{BlendShapeName}`
- ARKit 互換: `/ARKit/{BlendShapeName}`
- カスタムマッピングは `config.json` の `osc.mapping` 配列で定義

詳細は [JSON スキーマリファレンス](../com.hidano.facialcontrol/Documentation~/json-schema.md) を参照。

## ライセンス

[MIT License](../com.hidano.facialcontrol/LICENSE.md)
