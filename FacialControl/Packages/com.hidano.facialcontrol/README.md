# com.hidano.facialcontrol

3D キャラクターの表情をリアルタイムに制御する Unity 向けコアパッケージ。

本パッケージは表情制御本体のみを提供する。OSC 送受信や InputSystem 経由の入力は別パッケージ (`com.hidano.facialcontrol.osc` / `com.hidano.facialcontrol.inputsystem`) として分離されており、本コア単体で導入する場合は `com.unity.inputsystem` / `com.hidano.uosc` への依存は発生しない。

## 主な機能

- **マルチレイヤー表情制御** — 感情・リップシンク・目などのレイヤーで表情を同時管理。排他モード (LastWins / Blend) をレイヤー単位で設定可能
- **スムーズな表情遷移** — 線形補間・イージング・カスタムカーブによる Expression 間の遷移。遷移割り込み時も Persistent NativeArray 再利用で追加アロケーションなし
- **ARKit / PerfectSync 対応** — ARKit 52 ブレンドシェイプと PerfectSync の自動検出・Expression 自動生成
- **Inspector 完結型 UX** — `FacialCharacterProfileSO` の UI Toolkit カスタム Inspector で表情・レイヤー・Renderer Paths・Adapter Bindings を 1 アセット内で編集。長文 README を読まずに設定可能 (各セクションに HelpBox + Tooltip)
- **JSON 自動エクスポート** — Editor 編集時に `StreamingAssets/FacialControl/{SO 名}/profile.json` を自動同期。ビルド後の差し替えに対応しつつ、ユーザーが JSON のパスを書く必要はない
- **リップシンク連携 I/F** — 外部プラグイン (uLipSync 等) からの入力を受け付ける `ILipSyncProvider` インターフェース
- **Adapter Binding 拡張点** — `[FacialAdapterBinding(displayName: "...")]` 属性付きの `AdapterBindingBase` 派生を定義するだけで、core Inspector の Add ドロップダウンに自動列挙される。`OnStart(in AdapterBuildContext ctx)` 内で `ctx.InputSourceRegistry.Register(slug, source)` を呼び、binding 自身が VContainer per-FC `LifetimeScope` に配線される (scene 上に追加 MonoBehaviour 不要)

## 動作要件

- Unity 6000.3 以降
- Animator を持つ 3D キャラクターモデル (FBX)
- BlendShape を持つ SkinnedMeshRenderer

## インストール

本パッケージは [VContainer](https://github.com/hadashiA/VContainer) (`jp.hadashikick.vcontainer`) に依存する。VContainer は npmjs / Unity Asset Store に publish されていないため、利用側で OpenUPM 等の scoped registry をあらかじめ追加しておく必要がある。`Packages/manifest.json` に以下のように追記する (他のサブパッケージや scopedRegistries の追加分はリポジトリルートの README を参照)。

```json
{
    "scopedRegistries": [
        {
            "name": "OpenUPM",
            "url": "https://package.openupm.com",
            "scopes": [
                "jp.hadashikick.vcontainer"
            ]
        }
    ],
    "dependencies": {
        "jp.hadashikick.vcontainer": "1.16.6",
        "com.hidano.facialcontrol": "0.1.0-preview.1"
    }
}
```

> **Note**: VContainer のバージョンは本パッケージ作成時点で動作確認済みのものを記載している。後方互換性のあるパッチアップデートは利用側で自由に上げてよい。

## クイックスタート

1. Project ウィンドウで **Create** → **FacialControl** → **Facial Character Profile** から `FacialCharacterProfileSO` を作成
2. SO の Inspector で Layers / Expressions / Renderer Paths を編集 (各 Foldout セクションに使い方の HelpBox あり)
3. Inspector 末尾の **Adapter Bindings** セクションで **Add** ボタンから必要な binding を追加 (例: `Input System` / `OSC` / `ARKit / PerfectSync`)
4. 各 binding の inline UI で `InputActionAsset` / endpoint / port / blendshape マッピング 等を設定
5. キャラクターの GameObject に **FacialController** を Add Component し、SO フィールドに作成した SO を結線
6. Play で動作確認

```csharp
// スクリプトから Expression を切り替える場合
_facialController.Activate(expression);
_facialController.Deactivate(expression);
```

詳細は [Documentation~/quickstart.md](Documentation~/quickstart.md) を参照。

## アーキテクチャ

レイヤード設計 (Domain / Application / Adapters) を採用し、Assembly Definition で依存方向を強制する。Unity ランタイム依存 (PlayableAPI 等) は Adapters 層に集約し、Domain 層は `Unity.Collections` (NativeArray) にのみ依存する (`UnityEngine.dll` には依存しない)。

```
Runtime/
├── Domain/         # 値オブジェクト・抽象 I/F・純ドメインサービス
├── Application/    # ユースケース
└── Adapters/       # Unity API / PlayableGraph 実装
Editor/             # Editor 拡張 (UI Toolkit)
```

表情制御は PlayableGraph + AnimationStream API ベースで、既存の Animator と共存可能。

## パフォーマンス

- 定常状態 (レイヤー数・BlendShape 数固定時) で毎フレーム GC アロケーションゼロを目標に設計 (構成変更時のみ内部バッファを再確保)
- 遷移割り込み時も `LayerPlayable` の `_snapshotBuffer` / `_targetBuffer` (Persistent NativeArray) を再利用し追加アロケーションなし
- PlayMode テスト `MultiCharacterPerformanceTests` (10 キャラクター × ARKit 52 BlendShape) で同時制御の動作を検証

## ドキュメント

- [クイックスタートガイド](Documentation~/quickstart.md)
- [JSON スキーマリファレンス (上級者向け)](Documentation~/json-schema.md)

## 依存パッケージ

| パッケージ | バージョン | 用途 | 取得元 |
|-----------|-----------|------|--------|
| `jp.hadashikick.vcontainer` | 1.16.6 以上 | per-FC LifetimeScope による DI 解決 | OpenUPM (scoped registry を `Packages/manifest.json` に追加) |
| `com.hidano.scene-view-style-camera-controller` | 1.0.0 | Editor プレビューウィンドウのシーンビュー操作 | npm (npmjs.com) |

## 対応フォーマット

| フォーマット | 対応状況 |
|-------------|----------|
| FBX | 対応 |
| VRM | 今後対応予定 |

## ライセンス

[MIT License](LICENSE.md)
