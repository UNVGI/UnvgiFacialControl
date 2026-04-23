# FacialControl

3D キャラクターの表情をリアルタイムに制御する Unity 向けライブラリ（開発者向けアセット）。

## 概要

FacialControl は、VTuber 配信用フェイシャルキャプチャ連動や GUI エディタでの Expression 作成支援を主なユースケースとする、Unity エンジニア向けの表情制御パッケージです。

### 主な機能

- **マルチレイヤー表情制御** — 感情・リップシンク・目などのレイヤーで表情を同時管理。排他モード（LastWins / Blend）をレイヤー単位で設定可能
- **スムーズな表情遷移** — 線形補間・イージング・カスタムカーブによる Expression 間の遷移。遷移割り込み時も Persistent NativeArray 再利用で追加アロケーションなし
- **ARKit / PerfectSync 対応** — ARKit 52 ブレンドシェイプと PerfectSync の自動検出・Expression 自動生成
- **Editor 拡張** — Inspector カスタマイズ（プロファイル管理を統合）、BlendShape スライダー付き Expression 作成ツール（UI Toolkit）
- **JSON ファーストの永続化** — 表情設定は JSON で管理。ビルド後も差し替え可能。ScriptableObject は JSON への参照ポインター
- **リップシンク連携** — 外部プラグイン（uLipSync 等）からの入力を受け付ける `ILipSyncProvider` インターフェース
- **OSC ネットワーク通信** — 別パッケージ `com.hidano.facialcontrol.osc` で提供。VRChat 互換 OSC（UDP + uOsc）による BlendShape 送受信。受信は `Interlocked.Exchange` ベースの非ブロッキングダブルバッファ
- **入力デバイス対応** — 別パッケージ `com.hidano.facialcontrol.input` で提供。InputSystem 経由のコントローラ / キーボード入力と `InputBindingProfileSO` + `FacialInputBinder` によるキーコンフィグ永続化

## 動作要件

- Unity 6000.3 以降
- Animator コンポーネントが設定された 3D キャラクターモデル（FBX）
- BlendShape を持つ SkinnedMeshRenderer

## パッケージ構成

FacialControl は機能別に 3 パッケージで提供されます。コアパッケージは表情制御の本体機能のみを含み、OSC 配信・InputSystem 入力は用途に応じて任意で追加します。

| パッケージ | 役割 | 必須依存 |
|---|---|---|
| `com.hidano.facialcontrol` | コア（表情遷移・レイヤーブレンド・JSON プロファイル・Editor 拡張・LipSync I/F） | Unity 6000.3+ |
| `com.hidano.facialcontrol.osc` | OSC 送受信アダプタ（VRChat / ARKit 互換） | コア + `com.hidano.uosc` |
| `com.hidano.facialcontrol.input` | InputSystem 経由のキーバインド・コントローラ入力 | コア + `com.unity.inputsystem` |

サブパッケージは同 GameObject 上の `OscFacialControllerExtension` / `InputFacialControllerExtension` MonoBehaviour 経由で `FacialController` に接続されます。

## インストール

### npm レジストリ経由（推奨）

`Packages/manifest.json` に以下を追加してください（必要なサブパッケージのみ追加 OK）。

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
        "com.hidano.facialcontrol": "0.2.0-preview.2",
        "com.hidano.facialcontrol.osc": "0.1.0-preview.2",
        "com.hidano.facialcontrol.input": "0.1.0-preview.2"
    }
}
```

### ローカルインストール

リポジトリをクローンして `Packages/` ディレクトリに配置する場合:

```json
{
    "dependencies": {
        "com.hidano.facialcontrol": "file:com.hidano.facialcontrol",
        "com.hidano.facialcontrol.osc": "file:com.hidano.facialcontrol.osc",
        "com.hidano.facialcontrol.input": "file:com.hidano.facialcontrol.input"
    }
}
```

## クイックスタート

1. メニュー **FacialControl** → **新規プロファイル作成** からダイアログを開き、命名規則プリセット（VRM / ARKit）を選んで作成
   - `FacialProfileSO` アセットとプロファイル JSON が自動生成され、`smile` / `angry` / `blink` の雛形 Expression も含まれます
2. キャラクターの GameObject に **FacialController** を追加し、生成された `FacialProfileSO` を割り当て
3. **キーボード／コントローラ入力を使う場合**: `com.hidano.facialcontrol.input` を導入し、同 GameObject に **Input Facial Extension** を追加。`InputBindingProfileSO` を作成して `FacialInputBinder` に割り当て（キー `1`/`2`/`3` で発動）
4. **OSC を使う場合**: `com.hidano.facialcontrol.osc` を導入し、同 GameObject に **OSC Facial Extension** + `OscReceiver` / `OscSender` を追加
5. Play して動作確認。BlendShape 名の微調整は `FacialProfileSO` Inspector か **Expression 作成** ウィンドウから（ドロップダウン候補あり）

```csharp
// スクリプトから Expression を切り替える場合
_facialController.Activate(expression);
_facialController.Deactivate(expression);
```

詳細は [クイックスタートガイド](Documentation~/quickstart.md) を参照してください。JSON の直接編集など高度な運用も同ガイドでカバーしています。

## アーキテクチャ

レイヤード設計（Domain / Application / Adapters）を採用し、Assembly Definition で依存方向を強制しています。Unity ランタイム依存（PlayableAPI / InputSystem / uOSC 等）は Adapters 層に集約し、Domain 層は `Unity.Collections`（NativeArray）にのみ依存します（`UnityEngine.dll` には依存しません）。

```
Runtime/
├── Domain/         # 値オブジェクト・抽象 I/F・純ドメインサービス
├── Application/    # ユースケース
└── Adapters/       # Unity API / 外部ライブラリへの実装
Editor/             # Editor 拡張（UI Toolkit）
```

表情制御は PlayableGraph + AnimationStream API ベースで、既存の Animator と共存可能です。

## パフォーマンス

- 定常状態（レイヤー数・BlendShape 数固定時）で毎フレーム GC アロケーションゼロを目標に設計（構成変更時のみ内部バッファを再確保）
- 遷移割り込み時も `LayerPlayable` の `_snapshotBuffer` / `_targetBuffer`（Persistent NativeArray）を再利用し追加アロケーションなし
- OSC 受信は `Interlocked.Exchange` ベースの非ブロッキングダブルバッファで lock 不使用
- OSC 送信は uOsc クライアント内部のワーカースレッドで非同期化（本ライブラリ側はメインスレッド非ブロック）
- PlayMode テスト `MultiCharacterPerformanceTests`（10 キャラクター × ARKit 52 BlendShape）で同時制御の動作を検証

## ドキュメント

- [クイックスタートガイド](Documentation~/quickstart.md)
- [JSON スキーマリファレンス](Documentation~/json-schema.md)

## 依存パッケージ（コアのみ）

| パッケージ | バージョン | 用途 |
|-----------|-----------|------|
| `com.hidano.scene-view-style-camera-controller` | 1.0.0 | Editor プレビューウィンドウのシーンビュー操作 |

OSC / InputSystem 依存は対応サブパッケージにのみ含まれます。表情切替を API から呼ぶだけのユーザーは、コア単体で導入可能（uOSC・InputSystem 不要）。

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
