# com.hidano.facialcontrol

3D キャラクターの表情をリアルタイムに制御する Unity 向けコアパッケージ。

本パッケージは表情制御本体のみを提供する。OSC 送受信や InputSystem 経由の入力は別パッケージ（`com.hidano.facialcontrol.osc` / `com.hidano.facialcontrol.inputsystem`）として分離されており、本コア単体で導入する場合は `com.unity.inputsystem` / `com.hidano.uosc` への依存は発生しない。

## 主な機能

- **マルチレイヤー表情制御** — 感情・リップシンク・目などのレイヤーで表情を同時管理。排他モード（LastWins / Blend）をレイヤー単位で設定可能
- **スムーズな表情遷移** — 線形補間・イージング・カスタムカーブによる Expression 間の遷移。遷移割り込み時も Persistent NativeArray 再利用で追加アロケーションなし
- **ARKit / PerfectSync 対応** — ARKit 52 ブレンドシェイプと PerfectSync の自動検出・Expression 自動生成
- **Editor 拡張** — Inspector カスタマイズ（プロファイル管理を統合）、BlendShape スライダー付き Expression 作成ツール（UI Toolkit）
- **JSON ファーストの永続化** — 表情設定は JSON で管理。ビルド後も差し替え可能。ScriptableObject は JSON への参照ポインター
- **リップシンク連携 I/F** — 外部プラグイン（uLipSync 等）からの入力を受け付ける `ILipSyncProvider` インターフェース
- **サブパッケージ拡張点** — `IFacialControllerExtension` インターフェースと `InputSourceFactory.RegisterReserved<TOptions>(...)` で、追加の入力源（OSC / InputSystem 等）を同 GameObject 上の MonoBehaviour 経由で組み込める

## 動作要件

- Unity 6000.3 以降
- Animator コンポーネントが設定された 3D キャラクターモデル（FBX）
- BlendShape を持つ SkinnedMeshRenderer

## インストール

`Packages/manifest.json` に以下を追加する（他のサブパッケージや scopedRegistries の設定はリポジトリルートの README を参照）。

```json
{
    "dependencies": {
        "com.hidano.facialcontrol": "0.1.0-preview.1"
    }
}
```

## クイックスタート

1. メニュー **FacialControl** → **新規プロファイル作成** からダイアログを開き、命名規則プリセット（VRM / ARKit）を選んで作成（`FacialProfileSO` + プロファイル JSON が自動生成され、`smile` / `angry` / `blink` の雛形 Expression が含まれる）
2. キャラクターの GameObject に **FacialController** を追加し、生成された `FacialProfileSO` を割り当て
3. Play して動作確認。BlendShape 名の微調整は `FacialProfileSO` Inspector か **Expression 作成** ウィンドウから

```csharp
// スクリプトから Expression を切り替える場合
_facialController.Activate(expression);
_facialController.Deactivate(expression);
```

詳細は [Documentation~/quickstart.md](Documentation~/quickstart.md) を参照。

## アーキテクチャ

レイヤード設計（Domain / Application / Adapters）を採用し、Assembly Definition で依存方向を強制する。Unity ランタイム依存（PlayableAPI 等）は Adapters 層に集約し、Domain 層は `Unity.Collections`（NativeArray）にのみ依存する（`UnityEngine.dll` には依存しない）。

```
Runtime/
├── Domain/         # 値オブジェクト・抽象 I/F・純ドメインサービス
├── Application/    # ユースケース
└── Adapters/       # Unity API / PlayableGraph 実装
Editor/             # Editor 拡張（UI Toolkit）
```

表情制御は PlayableGraph + AnimationStream API ベースで、既存の Animator と共存可能。

## パフォーマンス

- 定常状態（レイヤー数・BlendShape 数固定時）で毎フレーム GC アロケーションゼロを目標に設計（構成変更時のみ内部バッファを再確保）
- 遷移割り込み時も `LayerPlayable` の `_snapshotBuffer` / `_targetBuffer`（Persistent NativeArray）を再利用し追加アロケーションなし
- PlayMode テスト `MultiCharacterPerformanceTests`（10 キャラクター × ARKit 52 BlendShape）で同時制御の動作を検証

## ドキュメント

- [クイックスタートガイド](Documentation~/quickstart.md)
- [JSON スキーマリファレンス](Documentation~/json-schema.md)

## 依存パッケージ

| パッケージ | バージョン | 用途 |
|-----------|-----------|------|
| `com.hidano.scene-view-style-camera-controller` | 1.0.0 | Editor プレビューウィンドウのシーンビュー操作 |

## 対応フォーマット

| フォーマット | 対応状況 |
|-------------|----------|
| FBX | 対応 |
| VRM | 今後対応予定 |

## ライセンス

[MIT License](LICENSE.md)
