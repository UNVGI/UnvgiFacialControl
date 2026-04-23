# FacialControl InputSystem

`com.hidano.facialcontrol` の Unity InputSystem 連携アダプタ。

## 概要

このパッケージは FacialControl コアに InputSystem 経由のキーボード／コントローラ入力機能を追加します。

- **ControllerExpressionInputSource**: 予約 id `controller-expr`。コントローラ入力で Expression をトリガーする `IInputSource` 実装
- **KeyboardExpressionInputSource**: 予約 id `keyboard-expr`。キーボード入力で Expression をトリガーする `IInputSource` 実装
- **InputBindingProfileSO**: `InputAction` 名と Expression ID のバインディングを ScriptableObject として永続化
- **FacialInputBinder**: `InputBindingProfileSO` を読み込み、`InputSystemAdapter` 経由で `FacialController` を駆動する MonoBehaviour
- **InputFacialControllerExtension**: `FacialController` と同じ GameObject に配置するだけで Controller / Keyboard 入力源を登録する MonoBehaviour

## 依存パッケージ

| パッケージ | バージョン | 用途 |
|---|---|---|
| `com.hidano.facialcontrol` | 0.2.0-preview.2 以降 | コア機能 |
| `com.unity.inputsystem` | 1.17.0 | 入力デバイスの動的切り替え |

## 使い方

1. `com.hidano.facialcontrol` と本パッケージを `Packages/manifest.json` に追加
2. キャラクターの GameObject に `FacialController` を追加
3. 同 GameObject に **Input Facial Extension** (`InputFacialControllerExtension`) を追加
4. キーバインドを Inspector で設定する場合は同 GameObject に `FacialInputBinder` と `InputBindingProfileSO`（プロジェクト内 ScriptableObject）を追加

```csharp
// 手動でコード配線する場合
var factory = new InputSourceFactory();
InputRegistration.Register(factory, blendShapeNames, ExclusionMode.LastWins);
```

## サンプル

`Multi Source Blend Demo` — 同一レイヤーに `controller-expr` と `keyboard-expr` を並置し、OnGUI 経由で加重和ブレンドを目視確認するサンプル。Package Manager から Import 可能。

## ライセンス

[MIT License](../com.hidano.facialcontrol/LICENSE.md)
