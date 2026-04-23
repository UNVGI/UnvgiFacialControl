# HANDOVER (2026-04-23)

## 今回やったこと

### サブパッケージ分離（preview.1 → preview.2 破壊的変更）

コアパッケージ `com.hidano.facialcontrol` が `com.unity.inputsystem` と `com.hidano.uosc` に hard-required している現状を解消し、3 パッケージ構成に再編。

- **新パッケージ作成**:
  - `com.hidano.facialcontrol.osc` — OSC 関連実装（`OscReceiver` / `OscSender` / `OscDoubleBuffer` / `OscMappingTable` / `OscReceiverPlayable` / `OscInputSource` / `OscOptionsDto`）+ `OscRegistration` ヘルパー + `OscFacialControllerExtension` MonoBehaviour
  - `com.hidano.facialcontrol.input` — InputSystem 関連実装（`InputSystemAdapter` / `FacialInputBinder` / `Controller/Keyboard ExpressionInputSource` / `InputBindingProfileSO` / `ExpressionTriggerOptionsDto` / `InputBinding`）+ `InputRegistration` ヘルパー + `InputFacialControllerExtension` MonoBehaviour
- **コア API 改修**:
  - `InputSourceFactory.RegisterReserved<TOptions>(...)` を新設（公式サブパッケージ向け予約 id 登録 API）
  - `InputSourceFactory` コンストラクタを `lipSyncProvider` のみに簡素化（OSC / Controller / Keyboard 直接登録を削除）
  - `IFacialControllerExtension` インターフェース追加 — `FacialController` 初期化時に同 GameObject 上の拡張へ `ConfigureFactory` で `InputSourceFactory` 登録機会を渡す
  - `FacialController` から `_oscSendPort` / `_oscReceivePort` SerializeField を削除（OSC ポート設定は `OscReceiver` / `OscSender` 側に移管）
- **ファイル移動**: 25+ ファイルを `git mv` で移動（履歴保存）。テスト 12 ファイルもサブパッケージへ移動。`MultiSourceBlendDemo` サンプルは `com.hidano.facialcontrol.input/Samples~/` へ
- **コア依存削除**:
  - `Runtime/Adapters/Hidano.FacialControl.Adapters.asmdef` から `Unity.InputSystem` / `uOSC.Runtime` 参照を削除
  - `Editor/Hidano.FacialControl.Editor.asmdef` から `Unity.InputSystem` 参照を削除
  - `package.json` の `dependencies` から `com.unity.inputsystem` / `com.hidano.uosc` を削除（`com.hidano.scene-view-style-camera-controller` のみ残る）
  - バージョン: `0.1.0-preview.1` → `0.2.0-preview.2`
- **manifest.json 更新**: `com.hidano.facialcontrol.osc` / `.input` を file: 参照で追加
- **テスト互換維持**: コア `Tests.EditMode` / `Tests.PlayMode` asmdef にサブパッケージ `Hidano.FacialControl.Osc` / `.Input` への参照を追加。`InputSourceFactoryTests.CreateFactory` ヘルパーで `OscRegistration.Register` + `InputRegistration.Register` を呼ぶよう更新

### README / CHANGELOG / 新パッケージ README 更新

- `Packages/com.hidano.facialcontrol/README.md` — 「クリーンアーキテクチャ」→「レイヤード設計」、「GC アロケーションゼロ」→「定常状態でゼロを目標」、「ロックフリー」→「`Interlocked.Exchange` ベースの非ブロッキング」、`MultiCharacterPerformanceTests` で 10 キャラ動作検証済みを明記。サブパッケージ章追加
- `Packages/com.hidano.facialcontrol.osc/README.md` — 新規作成
- `Packages/com.hidano.facialcontrol.input/README.md` — 新規作成
- `Packages/com.hidano.facialcontrol/CHANGELOG.md` — `[0.2.0-preview.2]` セクション追加（Added / Changed / Removed / Migration Guide）

## 決定事項

- **サブパッケージの境界** (Plan: `~/.claude/plans/groovy-plotting-wolf.md`):
  - Domain の値オブジェクト (`OscMapping` / `OscConfiguration` / `InputSourceType` / `InputSourceId` / `InputSourceDeclaration`) はコアに残置（純データ・uOSC/InputSystem 非依存）
  - `IInputSource` / `ExpressionTriggerInputSourceBase` / `ValueProviderInputSourceBase` などの汎用抽象もコアに残置
  - `LipSyncInputSource` はコアに残置（外部ライブラリ依存ゼロのため）
  - `InputBinding` 値オブジェクト → input サブパッケージへ移動（`InputBindingProfileSO`/`FacialInputBinder` 専用と確認）
- **`InputSourceFactory.RegisterReserved` は public API**: 予約 id を含む任意 id を登録可能。将来サブパッケージ追加（`.lipsync.full` / `.midi` 等）時も同 API を使う
- **`OscControllerBlendingIntegrationTests` は input サブパッケージ側**: input asmdef が osc を参照する形で実装
- **拡張接続パターン**: `IFacialControllerExtension` MonoBehaviour を同 GameObject に配置 → `FacialController` が `GetComponents<IFacialControllerExtension>()` で検出 → `ConfigureFactory(factory, profile, blendShapeNames)` で各 extension が `RegisterReserved` を呼ぶ

## 検証状況

- **コンパイル**: 全 15 アセンブリがクリーンにコンパイル成功（CS エラー 0）
- **アセンブリ依存**: コア `Hidano.FacialControl.Adapters` の参照は `[Domain, Application, Unity.Collections]` のみ — **InputSystem/uOSC 非参照を確認**
- **テスト全体**: EditMode テスト 1066 pass / 15 fail（fail はすべて `InputBindingProfileSOTests` のもの。下記注意点）

## 残課題（要 Editor 再起動）

**Unity Editor の MonoScript バインディング更新**: `InputBindingProfileSO` を `com.hidano.facialcontrol` から `com.hidano.facialcontrol.input` へ asmdef 移動した結果、Unity の MonoScript-to-class マッピングがランタイムキャッシュで古い参照を保持しており、`ScriptableObject.CreateInstance<InputBindingProfileSO>()` が "Instance couldn't be created. The script class needs to derive from ScriptableObject" を吐く。15 件のテスト失敗は全てこの原因。

**対応**: Unity Editor を一度終了 → 再起動すれば解消。または `Library/ScriptAssemblies/` と `Library/Bee/` を削除して再ビルド。

確認済み: `InputBindingProfileSO.cs` の class 定義は正しく `: UnityEngine.ScriptableObject` を継承、コンパイル成功、DLL も `Library/ScriptAssemblies/Hidano.FacialControl.Input.dll` に生成済み。

## ハマりどころ

- **`git mv` できないファイル**: `Samples~/` 配下は `.gitignore` の `*~` パターンで untracked のため `git mv` がエラーで失敗。プレーン `mv` で対応
- **Unity MCP `Unity_RunCommand` の namespace 自動ラップ**: my code が `Unity.AI.Assistant.Agent.Dynamic.Extension.Editor` 名前空間にラップされ、`UnityEditor.Compilation.CompilationPipeline` を `Unity.CompilationPipeline` と誤解釈する。fully-qualified name (`UnityEditor.Compilation.CompilationPipeline`) を明示する必要あり
- **Unity AppDomain.GetAssemblies()**: MCP RunCommand 実行コンテキストでは Editor の主 AppDomain と異なる可能性があり、`Hidano.FacialControl.Input` 等の loaded アセンブリ一覧から欠落。テスト実行時の実 AppDomain では正しく load されている

## 学び

- Unity の `git mv` は asmdef + .meta GUID を保持するが、asset DB の MonoScript-class binding キャッシュは domain reload まで stale が残る場合がある
- サブパッケージ分離時に「拡張点」(`IFacialControllerExtension` 等) を core 側に置けば、サブパッケージは中立な API で接続できる
- `[RequireComponent(typeof(FacialController))]` を extension MonoBehaviour に付けると Inspector でも分かりやすい

## 次にやること

1. **Unity Editor を再起動** → テスト全 pass 確認
2. PlayMode テストもフル実行（`Hidano.FacialControl.Tests.PlayMode` + `Osc.Tests.PlayMode` + `Input.Tests.PlayMode`）
3. Documentation~/quickstart.md 更新（Registration 呼び出し手順追記） — Phase 5 の残タスク
4. dev プロジェクトで `MultiSourceBlendDemo` シーンを開いて動作確認
5. preview.2 タグ前に `docs/work-procedure.md` のサブパッケージ化タスクを反映
