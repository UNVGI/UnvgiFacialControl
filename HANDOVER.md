# HANDOVER (2026-04-23)

## 今回やったこと

### README 正直化修正

- 「クリーンアーキテクチャ」→「レイヤード設計」（Domain に OSC/Input/ARKit 名が漏れている現実を反映）
- 「GC アロケーションゼロ」→「定常状態でゼロを目標」（`EnsureBuffers` の再確保ロジックに合わせる）
- 「ロックフリー」→「`Interlocked.Exchange` ベースの非ブロッキング」
- 「想定」→「`MultiCharacterPerformanceTests` で 10 キャラ動作検証済み」
- 依存パッケージ表に `com.hidano.scene-view-style-camera-controller` を追加

### サブパッケージ分離（preview.1 内で実施 / publish 前のためバージョンバンプなし）

3 パッケージ構成に再編。コアから `com.unity.inputsystem` / `com.hidano.uosc` 強制依存を排除。

- 新パッケージ **`com.hidano.facialcontrol.osc`** — OSC 関連実装（`OscReceiver` / `OscSender` / `OscDoubleBuffer` / `OscMappingTable` / `OscReceiverPlayable` / `OscInputSource` / `OscOptionsDto`）+ `OscRegistration` ヘルパー + `OscFacialControllerExtension` MonoBehaviour
- 新パッケージ **`com.hidano.facialcontrol.inputsystem`** — InputSystem 関連実装（`InputSystemAdapter` / `FacialInputBinder` / `Controller/Keyboard ExpressionInputSource` / `InputBindingProfileSO` / `ExpressionTriggerOptionsDto` / `InputBinding`）+ `InputRegistration` ヘルパー + `InputFacialControllerExtension` MonoBehaviour
- コア API:
  - `IFacialControllerExtension` インターフェース新設（`Hidano.FacialControl.Adapters.Playable`）— `FacialController` 初期化時に同 GameObject の拡張から `InputSourceFactory` に追加登録するための I/F
  - `InputSourceFactory.RegisterReserved<TOptions>(...)` 公開 API 新設（公式サブパッケージ向け予約 id 登録）
  - `InputSourceFactory` コンストラクタを `lipSyncProvider` のみに簡素化（OSC/Controller/Keyboard 直接登録を削除）
  - `FacialController` から `_oscSendPort` / `_oscReceivePort` SerializeField + 不要になった `s_sharedTimeProvider` を削除
  - `FacialControllerEditor` から OSC ポート設定セクション削除
  - `FacialProfileSO_InputSourcesView` の `InputBindingProfileSO` 警告ロジック削除（input 依存切離のため）
- ファイル移動: 25+ ファイルを `git mv` で履歴保存付き移動。テスト 12 ファイル + `MultiSourceBlendDemo` サンプルもサブパッケージへ
- コア依存削除: `package.json` / `Adapters` asmdef / `Editor` asmdef から `com.unity.inputsystem` / `com.hidano.uosc` 系を全て削除
- テスト互換維持: コア `Tests.EditMode` / `Tests.PlayMode` asmdef にサブパッケージ参照を追加。`InputSourceFactoryTests.CreateFactory` ヘルパーで `OscRegistration.Register` + `InputRegistration.Register` を呼ぶよう更新

### `Hidano.FacialControl.Input` → `Hidano.FacialControl.InputSystem` リネーム

- パッケージ: `com.hidano.facialcontrol.input` → `com.hidano.facialcontrol.inputsystem`
- アセンブリ + .asmdef ファイル名 4 種を `Hidano.FacialControl.InputSystem*` に
- C# namespace: `Hidano.FacialControl.Input` → `Hidano.FacialControl.InputSystem`
- 参照側 (`manifest.json`, `packages-lock.json`, コア Tests asmdef, テストファイル using) を一括更新
- ドキュメント (CHANGELOG / README / quickstart / HANDOVER / サブパッケージ description) も統一

### namespace 衝突解消

`Hidano.FacialControl.InputSystem` namespace と `UnityEngine.InputSystem.InputSystem` 静的クラスが衝突 → CS0234 を 3 か所で発生。
テストコード内で `UnityEngine.InputSystem.InputSystem.AddDevice<>()` と完全修飾して解消。

### ドキュメント更新

- コア `README.md`: サブパッケージ章 + 依存パッケージ表の刷新
- 新規 `com.hidano.facialcontrol.osc/README.md`
- 新規 `com.hidano.facialcontrol.inputsystem/README.md`
- `Documentation~/quickstart.md`: 3 パッケージ構成での導入手順
- `CHANGELOG.md`: preview.1 内に「サブパッケージ構成」サブセクション統合（preview.2 セクションは削除）

### バージョン revert

publish 前のため `0.2.0-preview.2` / `0.1.0-preview.2` を全て `0.1.0-preview.1` に戻す。

## 決定事項

- **サブパッケージ境界** (Plan: `~/.claude/plans/groovy-plotting-wolf.md`):
  - Domain 値オブジェクト (`OscMapping` / `OscConfiguration` / `InputSourceType` / `InputSourceId` / `InputSourceDeclaration`) はコア残置（純データ・依存ゼロ）
  - 汎用抽象 (`IInputSource` / `ExpressionTriggerInputSourceBase` / `ValueProviderInputSourceBase`) もコア残置
  - `LipSyncInputSource` はコア残置（外部ライブラリ依存ゼロのため）
  - `InputBinding` 値オブジェクト → input サブパッケージ移動（input 専用と確認済）
- **`InputSourceFactory.RegisterReserved` は public API**: 予約 id を含む任意 id 登録可能。将来サブパッケージ追加時も同 API で拡張
- **`OscControllerBlendingIntegrationTests` は input サブパッケージ側**: input asmdef が osc を参照する形
- **拡張接続パターン**: `IFacialControllerExtension` MonoBehaviour を同 GameObject に配置 → `FacialController` が `GetComponents<IFacialControllerExtension>()` で検出 → `ConfigureFactory(factory, profile, blendShapeNames)` で各 extension が `RegisterReserved` を呼ぶ
- **InputSystem namespace 衝突は完全修飾で解消**: namespace `Hidano.FacialControl.InputSystem` を維持しつつ `UnityEngine.InputSystem.InputSystem.AddDevice<>()` と完全修飾
- **バージョン**: publish 前は `0.1.0-preview.1` 維持。複数機能追加でもバンプしない

## 捨てた選択肢と理由

- **`internal RegisterBuiltIn` + `InternalsVisibleTo` でサブパッケージ限定**: 将来サブパッケージ追加時に core の AssemblyInfo 修正が必要になり拡張性低い → `public RegisterReserved` を採用
- **`OscControllerBlendingIntegrationTests` を osc 側 / dev project 側に置く案**: input が controller の主軸なので input 側に置く方が自然
- **`com.hidano.facialcontrol.lipsync` として LipSync を即切り出し**: 外部ライブラリ依存ゼロなので preview スコープを最小化するためコア残置
- **`Hidano.FacialControl.InputSystem` namespace 衝突を using alias で解消**: parent namespace 解決 (`Hidano.FacialControl` → 子 namespace `InputSystem` がヒット) がエイリアスより優先されるため効かず → 完全修飾を採用
- **`Hidano.FacialControl.InputSystem` を別の namespace 名（例: `InputSystemAdapter`）に変更**: パッケージ名と一致させる方が分かりやすく、衝突箇所は 3 か所のみで完全修飾コストが小さいため namespace 維持を採用
- **`OscMappingTable` / `OscDoubleBuffer` をコア残置**: OSC 専用構造で核機能と無関係なため osc サブパッケージへ移動
- **JSON パーサ (`SystemTextJsonParser`) を OSC 知識から分離**: `OscConfigurationDto` は parser 内部 private 型で `OscMapping`/`OscConfiguration` (Domain) を生成するだけなのでコア残置で問題なし
- **`ARKitEditorService` の OSC マッピング生成部分を osc サブパッケージへ抽出**: Domain の `OscMapping`/`OscConfiguration` のみ使用（uOSC 非依存）なのでコア残置で問題なし
- **Library/ScriptAssemblies を削除して強制 Domain Reload**: Unity が "User interactions are not supported" で拒否 → Editor 再起動を user に依頼する形

## ハマりどころ

- **`Unity_ReadConsole` の Type フィルタ**: Unity の compile error は `Type:"Log"` として記録されることがある。`["Error"]` だけでは見落とす。**`["Error", "Warning", "Log"]` 全タイプで確認必須**
- **C# 親 namespace 解決の優先順位**: `Hidano.FacialControl.X` の中で `InputSystem` を参照すると、親 namespace `Hidano.FacialControl` の子 namespace `InputSystem` が using-alias より優先されてヒットする。alias 効かず完全修飾必須
- **MCP `Unity_RunCommand` の namespace 自動ラップ**: 私のコードが `Unity.AI.Assistant.Agent.Dynamic.Extension.Editor` 名前空間にラップされ、`UnityEditor.Compilation.CompilationPipeline` を `Unity.CompilationPipeline` と誤解釈する。fully-qualified name 明示が必要
- **MCP `AppDomain.CurrentDomain.GetAssemblies()` の見え方**: MCP RunCommand 実行コンテキストでは Editor 主 AppDomain と異なる可能性があり、新規追加アセンブリが loaded 一覧から欠落することがある。テスト実行時の実 AppDomain では正しく load されている
- **`git mv` できないファイル**: `Samples~/` 配下は `.gitignore` の `*~` パターンで untracked のため `git mv` がエラー。プレーン `mv` で対応
- **Editor + Tests asmdef の DLL タイムスタンプ更新遅延**: Adapters/Domain は再コンパイルされるが Editor/Tests が古い DLL のまま AppDomain に残ることがある。Editor 再起動で解消

## 学び

- **サブパッケージ分離時に「拡張点」(`IFacialControllerExtension` 等) を core 側に置く**: サブパッケージは中立な API で接続でき、core は具体実装を知らない
- **`[RequireComponent(typeof(FacialController))]` を extension MonoBehaviour に付ける**: Inspector で関係性が明示される
- **Unity の `git mv`**: asmdef + .meta GUID を保持するが、AssetDB の MonoScript-class binding キャッシュは domain reload まで stale が残る場合がある（Editor 再起動で解消）
- **C# namespace 衝突の根本回避策は「親 namespace に競合する子 namespace を作らない」**: `Hidano.FacialControl.InputSystem` のような命名は `UnityEngine.InputSystem.InputSystem` クラスを参照するコードと衝突する。完全修飾で対処可能だが、命名で回避する方が望ましい場合もある

## 次にやること

### 優先 P0

- **Unity Editor 再起動**: 現状 input サブパッケージのアセンブリが AppDomain にロードされていない（推定 15 件の transient テスト失敗）。再起動で MonoScript binding 更新 + 全アセンブリ load される

### 優先 P1

- **PlayMode テストフル実行**: `Hidano.FacialControl.Tests.PlayMode` + `Osc.Tests.PlayMode` + `InputSystem.Tests.PlayMode` を Editor 再起動後に通す
- **dev プロジェクトで `MultiSourceBlendDemo` シーン動作確認**: controller-expr + keyboard-expr の加重和ブレンドが正常動作するか目視

### 優先 P2

- **コア単体ビルド検証**: 別 Unity プロジェクトに `com.hidano.facialcontrol` のみ `file:` 参照、InputSystem/uOSC 未インストール状態でコンパイル通過確認
- **`docs/work-procedure.md` 反映**: サブパッケージ化タスクの記録
- **`InputSourceFactoryTests` の分割検討**: 現状コア tests に居て osc + inputsystem 参照しているのが歪。各サブパッケージのテスト asmdef へ移動すべきか preview.1 publish 前に判断

### 優先 P3

- 各サブパッケージの個別 README で詳細な使用例を充実させる
- ARKit / VRM 自動検出と OSC マッピング生成のドキュメント化

## 関連ファイル

### コア（変更）
- `FacialControl/Packages/com.hidano.facialcontrol/package.json`
- `FacialControl/Packages/com.hidano.facialcontrol/CHANGELOG.md`
- `FacialControl/Packages/com.hidano.facialcontrol/README.md`
- `FacialControl/Packages/com.hidano.facialcontrol/Documentation~/quickstart.md`
- `FacialControl/Packages/com.hidano.facialcontrol/Runtime/Adapters/Hidano.FacialControl.Adapters.asmdef`
- `FacialControl/Packages/com.hidano.facialcontrol/Editor/Hidano.FacialControl.Editor.asmdef`
- `FacialControl/Packages/com.hidano.facialcontrol/Runtime/Adapters/Playable/IFacialControllerExtension.cs` (新規)
- `FacialControl/Packages/com.hidano.facialcontrol/Runtime/Adapters/Playable/FacialController.cs`
- `FacialControl/Packages/com.hidano.facialcontrol/Runtime/Adapters/InputSources/InputSourceFactory.cs`
- `FacialControl/Packages/com.hidano.facialcontrol/Editor/Inspector/FacialControllerEditor.cs`
- `FacialControl/Packages/com.hidano.facialcontrol/Editor/Inspector/FacialProfileSO_InputSourcesView.cs`
- `FacialControl/Packages/com.hidano.facialcontrol/Tests/EditMode/Hidano.FacialControl.Tests.EditMode.asmdef`
- `FacialControl/Packages/com.hidano.facialcontrol/Tests/PlayMode/Hidano.FacialControl.Tests.PlayMode.asmdef`
- `FacialControl/Packages/com.hidano.facialcontrol/Tests/EditMode/Adapters/InputSources/InputSourceFactoryTests.cs`

### 新規パッケージ
- `FacialControl/Packages/com.hidano.facialcontrol.osc/` 一式
  - `package.json`, `README.md`
  - `Runtime/Hidano.FacialControl.Osc.asmdef`
  - `Runtime/Adapters/OSC/{OscReceiver, OscSender, OscDoubleBuffer, OscMappingTable}.cs`
  - `Runtime/Adapters/Playable/OscReceiverPlayable.cs`
  - `Runtime/Adapters/InputSources/OscInputSource.cs`
  - `Runtime/Adapters/Json/Dto/OscOptionsDto.cs`
  - `Runtime/Registration/OscRegistration.cs` (新規)
  - `Runtime/OscFacialControllerExtension.cs` (新規)
  - `Editor/Hidano.FacialControl.Osc.Editor.asmdef`
  - `Tests/EditMode/Hidano.FacialControl.Osc.Tests.EditMode.asmdef` + 4 ファイル
  - `Tests/PlayMode/Hidano.FacialControl.Osc.Tests.PlayMode.asmdef` + 3 ファイル

- `FacialControl/Packages/com.hidano.facialcontrol.inputsystem/` 一式
  - `package.json`, `README.md`
  - `Runtime/Hidano.FacialControl.InputSystem.asmdef`
  - `Runtime/Adapters/Input/{InputSystemAdapter, FacialInputBinder}.cs`
  - `Runtime/Adapters/InputSources/{Controller, Keyboard}ExpressionInputSource.cs`
  - `Runtime/Adapters/ScriptableObject/InputBindingProfileSO.cs`
  - `Runtime/Adapters/Json/Dto/ExpressionTriggerOptionsDto.cs`
  - `Runtime/Domain/Models/InputBinding.cs`
  - `Runtime/Registration/InputRegistration.cs` (新規)
  - `Runtime/InputFacialControllerExtension.cs` (新規)
  - `Editor/Inspector/InputBindingProfileSOEditor.cs`
  - `Editor/Hidano.FacialControl.InputSystem.Editor.asmdef`
  - `Tests/EditMode/Hidano.FacialControl.InputSystem.Tests.EditMode.asmdef` + 5 ファイル
  - `Tests/PlayMode/Hidano.FacialControl.InputSystem.Tests.PlayMode.asmdef` + 2 ファイル
  - `Samples~/MultiSourceBlendDemo/` 一式

### dev manifest
- `FacialControl/Packages/manifest.json`
- `FacialControl/Packages/packages-lock.json`

### プラン / ドキュメント
- `~/.claude/plans/groovy-plotting-wolf.md` (本セッション開始時のプラン)
- `HANDOVER.md` (このファイル)
