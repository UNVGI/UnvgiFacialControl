# FacialControl

3D キャラクターの表情をリアルタイムに制御する Unity 向けライブラリ（開発者向けアセット）。

## 概要

FacialControl は、VTuber 配信用フェイシャルキャプチャ連動や GUI エディタでの Expression 作成支援を主なユースケースとする、Unity エンジニア向けの表情制御パッケージです。

### 主な機能

- **マルチレイヤー表情制御** — 感情・リップシンク・目などのレイヤーで表情を同時管理。排他モード（LastWins / Blend）をレイヤー単位で設定可能
- **スムーズな表情遷移** — 線形補間・イージング・カスタムカーブによる Expression 間の遷移。遷移中の割り込みにも GC フリーで対応
- **OSC ネットワーク通信** — VRChat 互換の OSC（UDP + uOsc）による BlendShape データの送受信。ダブルバッファリングで低遅延
- **ARKit / PerfectSync 対応** — ARKit 52 ブレンドシェイプと PerfectSync の自動検出・Expression 自動生成
- **Editor 拡張** — Inspector カスタマイズ（プロファイル管理を統合）、BlendShape スライダー付き Expression 作成ツール（UI Toolkit）
- **JSON ファーストの永続化** — 表情設定は JSON で管理。ビルド後も差し替え可能。ScriptableObject は JSON への参照ポインター
- **入力デバイス対応** — InputSystem によるコントローラ / キーボードからの Expression トリガー
- **キーコンフィグの永続化**（`InputBindingProfileSO` + `FacialInputBinder`）— Action と Expression のバインディングを ScriptableObject として保存し、シーンに配置するだけでキーコンフィグを復元。詳細は [クイックスタートガイド](Documentation~/quickstart.md) を参照
- **リップシンク連携** — 外部プラグイン（uLipSync 等）からの入力を受け付ける `ILipSyncProvider` インターフェース

## 動作要件

- Unity 6000.3 以降
- Animator コンポーネントが設定された 3D キャラクターモデル（FBX）
- BlendShape を持つ SkinnedMeshRenderer

## インストール

### npm レジストリ経由（推奨）

`Packages/manifest.json` に以下を追加してください。

```json
{
    "scopedRegistries": [
        {
            "name": "npmjs",
            "url": "https://registry.npmjs.org",
            "scopes": [
                "com.hidano"
            ]
        }
    ],
    "dependencies": {
        "com.hidano.facialcontrol": "0.1.0-preview.1"
    }
}
```

### ローカルインストール

リポジトリをクローンして `Packages/` ディレクトリに配置する場合:

```json
{
    "dependencies": {
        "com.hidano.facialcontrol": "file:com.hidano.facialcontrol"
    }
}
```

## クイックスタート

1. メニュー **FacialControl** → **新規プロファイル作成** からダイアログを開き、命名規則プリセット（VRM / ARKit）を選んで作成
   - `FacialProfileSO` アセットとプロファイル JSON が自動生成され、`smile` / `angry` / `blink` の雛形 Expression も含まれます
2. キャラクターの GameObject に **FacialController** を追加し、生成された `FacialProfileSO` を割り当て
3. `InputBindingProfileSO` を作成して `FacialInputBinder` に割り当て（キー `1`/`2`/`3` で発動）
4. Play して動作確認。BlendShape 名の微調整は `FacialProfileSO` Inspector か **Expression 作成** ウィンドウから（ドロップダウン候補あり）

```csharp
// スクリプトから Expression を切り替える場合
_facialController.Activate(expression);
_facialController.Deactivate(expression);
```

詳細は [クイックスタートガイド](Documentation~/quickstart.md) を参照してください。JSON の直接編集など高度な運用も同ガイドでカバーしています。

## アーキテクチャ

クリーンアーキテクチャ（Domain / Application / Adapters）を採用。Unity 依存を Adapters 層に封じ込め、各レイヤーは Assembly Definition で依存方向を強制しています。

```
Runtime/
├── Domain/         # ドメインロジック（Unity 非依存）
├── Application/    # ユースケース
└── Adapters/       # Unity 依存の実装（PlayableAPI, OSC, JSON, Input）
Editor/             # Editor 拡張（UI Toolkit）
```

表情制御は PlayableGraph + AnimationStream API ベースで、既存の Animator と共存可能です。

## パフォーマンス

- 毎フレーム処理での GC アロケーションゼロ
- 遷移割り込み時も NativeArray 再利用で GC フリー
- OSC 受信はダブルバッファリング（ロックフリー）、送信は別スレッドで非同期実行
- 10 体以上のキャラクター同時制御を想定した設計

## ドキュメント

- [クイックスタートガイド](Documentation~/quickstart.md)
- [JSON スキーマリファレンス](Documentation~/json-schema.md)

## 依存パッケージ

| パッケージ | バージョン | 用途 |
|-----------|-----------|------|
| `com.unity.inputsystem` | 1.17.0 | 入力デバイスの動的切り替え |
| `com.hidano.uosc` | 1.0.0 | OSC（UDP）通信 |

## 対応フォーマット

| フォーマット | 対応状況 |
|-------------|----------|
| FBX | 対応 |
| VRM | 今後対応予定 |

## 既知の制限とロードマップ

### Addressables（AssetBundle）対応

現状 preview.1 では、プロファイル JSON は `StreamingAssets/FacialControl/` 配下のファイルシステムから直接読み込む設計です。これにより以下の制約があります。

- **キャラクター Prefab と表情プロファイルを 1 セットで Addressables 配信しにくい**
  - `FacialController` / `FacialProfileSO` 自体は Addressables 対応可能ですが、SO が指す JSON が StreamingAssets 前提のため、キャラクター配信とセットにできません
- **プラットフォーム別の読み込み経路差**
  - Windows PC を主ターゲットとしています。Android / WebGL 等では StreamingAssets アクセスに専用 API が必要で、現状未対応です

### preview.2 以降の予定

**`IProfileJsonLoader` 抽象化の導入**: プロファイル JSON のロード経路を差し替え可能なインターフェースで抽象化し、以下をサポート予定です。

- StreamingAssets（現状、デフォルト）
- TextAsset 参照（Addressables グループに同梱可能）
- Addressables アドレス直接指定（リモート配信・追加表情パック）

既存のデフォルト経路（StreamingAssets）は維持するため、現在の設定から破壊的変更は発生しません。

## ライセンス

[MIT License](LICENSE.md)
