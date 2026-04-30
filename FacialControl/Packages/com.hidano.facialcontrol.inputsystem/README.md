# FacialControl InputSystem

`com.hidano.facialcontrol` の Unity InputSystem 連携アダプタ。

## 概要

このパッケージは FacialControl コアに **InputActionAsset 1 個 + キャラクター SO 1 個** で完結する入力結線を提供します。長文の README を読ませず、SO Inspector の Foldout セクションだけで設定が完結する UX を目指しています。

- **FacialCharacterSO**: キャラクター単位の統合 ScriptableObject。InputActionAsset / キーバインディング (Action ↔ Expression) / アナログバインディング (連続値 → BlendShape / BonePose) / レイヤー / Expression / BonePose を 1 アセットに集約
- **FacialCharacterInputExtension**: `IFacialControllerExtension` 実装の MonoBehaviour。`FacialController` と同じ GameObject に配置すると SO の入力結線とアナログ入力源登録を自動実施
- **ControllerExpressionInputSource** (予約 id `controller-expr`) / **KeyboardExpressionInputSource** (予約 id `keyboard-expr`): Expression をトリガーする `IInputSource` 実装
- **InputFacialControllerExtension**: コア機能のトリガー入力源 (controller-expr / keyboard-expr) を `InputSourceFactory.RegisterReserved` 経由で登録する MonoBehaviour
- **JSON 自動エクスポート (Editor 専用)**: SO 保存時に `StreamingAssets/FacialControl/{SO 名}/profile.json` および `analog_bindings.json` を AssetModificationProcessor 経由で自動更新

## 依存パッケージ

| パッケージ | バージョン | 用途 |
|---|---|---|
| `com.hidano.facialcontrol` | 0.1.0-preview.1 以降 | コア機能 |
| `com.unity.inputsystem` | 1.17.0 | 入力デバイスの動的切り替え |

## 使い方

1. `com.hidano.facialcontrol` と本パッケージを `Packages/manifest.json` に追加
2. Project ウィンドウで **Create** → **FacialControl** → **Facial Character** から `FacialCharacterSO` を作成
3. SO Inspector の各 Foldout セクション (入力 / キーバインディング / アナログ / レイヤー / Expression / BonePose / デバッグ) で設定を埋める
4. キャラクターの GameObject に `FacialController` を追加し、SO を `Character SO` フィールドに結線
5. 同 GameObject に `FacialCharacterInputExtension` を追加
6. (任意) `InputFacialControllerExtension` も同 GameObject に追加するとコア標準のトリガー入力源 (controller-expr / keyboard-expr) も登録される

```csharp
// 手動でコード配線する場合 (コアから直接トリガー入力源を登録)
var factory = new InputSourceFactory();
InputRegistration.Register(factory, blendShapeNames, ExclusionMode.LastWins);
```

## サンプル

- **Multi Source Blend Demo** — 同一レイヤーに `controller-expr` と `keyboard-expr` を並置して加重和ブレンドを目視確認するサンプル。Scene / `FacialCharacterSO` / InputActionAsset / HUD を同梱、ユーザーはモデルを Scene の Character の子に配置するだけで動作。Package Manager から Import 可能
- **Analog Binding Demo** — 右スティック等の連続値を BlendShape / BonePose 軸に写像するアナログ結線サンプル

各サンプルの詳細は `Samples~/<sample-name>/README.md` を参照。

## ライセンス

[MIT License](../com.hidano.facialcontrol/LICENSE.md)
