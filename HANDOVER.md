# HANDOVER (2026-04-23)

## 今回やったこと

- README レビュー中に出た Q1/Q2/Q3 に回答
  - Q1: プロファイル JSON 役割とテンプレート位置付け → GUI ファースト化方針決定
  - Q2: FacialProfileSO = JSON への参照ポインター（現状で OK、アクションなし）
  - Q3: Addressables 対応パターン A〜E 比較 → preview.1 は A 継続、preview.2 以降で E（`IProfileJsonLoader` 抽象化）
- Q1 実装 10 タスク完了（計画ファイル: `~/.claude/plans/a-e-q1-twinkly-raven.md`）
  - `ProfileCreationDialog` に MenuItem `FacialControl/新規プロファイル作成` 追加
  - `ProfileCreationData` に `NamingConvention` enum（VRM / ARKit / None）と `BuildSampleExpressions()` 追加
  - ダイアログ UI に命名規則プリセット + 雛形 Expression チェックボックス
  - `Editor/Common/BlendShapeNameProvider.cs` 新規作成（参照モデルから BlendShape 名収集）
  - `FacialProfileSOEditor` / `ExpressionCreatorWindow` を BlendShape ドロップダウン化
  - `ARKitDetectorWindow` + `ARKitEditorService.MergeIntoExistingProfile()` で既存 SO マージ機能追加
  - 新規テスト 27 件（`SampleExpressionsTests.cs` + `BlendShapeNameProviderTests.cs`）全 pass
  - `Documentation~/quickstart.md` を GUI ファースト順序に全面刷新
  - `README.md` のクイックスタート 4 ステップ刷新 + 「既知の制限とロードマップ」節新設
- テスト結果: 1081 総数、1080 pass、1 flaky（`Pipeline_AggregateAndBlendLoop_AllocatesZeroManagedMemory`）

## 決定事項

- **Q1 対応スコープ**: MenuItem 結線 + 雛形 Expression + BlendShape ドロップダウン化（2 Editor）+ ARKit マージ のフルスコープを preview.1 に含める
- **雛形 Expression の初期値ポリシー**: `ProfileCreationData` のデフォルトは `IncludeSampleExpressions = false` / `Naming = None`。ダイアログ UI 側でのみ VRM + true を初期値として提示。→ `CreateDefault()` を使う既存テストの互換性を維持
- **命名規則プリセット**: VRM（`Fcl_ALL_Joy`）/ ARKit（`mouthSmile_L` 等）/ None の 3 択固定
- **Addressables 対応ロードマップ**: preview.2 以降で `IProfileJsonLoader` I/F を導入し、StreamingAssets / TextAsset / Addressables の 3 実装を opt-in 可能にする。既存 StreamingAssets 経路はデフォルトとして維持
- **並列 Agent 運用ルール**: 完了直後に必ず `Unity_ReadConsole` でコンパイルエラー 0 を確認する。Agent の `[Tool result missing due to internal error]` は失敗として扱い再実行

## 捨てた選択肢と理由

- **Q3 パターン C（SO に JSON 文字列埋め込み）**: 不採用。「JSON ファーストの永続化」ポリシーと真逆で、外部エディタ編集や単独配布・Git diff 性が崩れる
- **Q3 パターン D 単独採用（Addressables 強制）**: 不採用。ミニマル利用者に Addressables 依存を強制したくない。E の I/F 抽象化で opt-in 提供する
- **ProfileCreationDialog で「空プロファイル作成」のみ案**: 不採用。雛形 3 件（smile/angry/blink）自動生成で「作成 → Play して即試す」導線を作る方が GUI ファースト価値が高い
- **Q1 スコープを「MenuItem 結線 + README 並べ替え」のみに限定する案**: 不採用。タイポ耐性 UI 改善（BlendShape ドロップダウン）を合わせて入れないと GUI ファースト化の効果が半減する
- **batchmode でテスト実行**: Unity Editor 起動中は競合で失敗。`TestRunnerApi` + ファイルポーリング経路に切り替え
- **`ExpressionCreatorWindow` の TextField → PopupField 置換**: 対象 TextField が存在しなかった（元 UI は slider 方式）。「参照モデル自動解決 + 候補プロバイダフック」という別実装に変更

## ハマりどころ

- **一晩放置の原因**: 並列 Agent 3 本が ~4 分で完了したが、完了直後に Unity コンパイル検証を行わず、Agent C が混入させた `Application.streamingAssetsPath` 名前空間 shadow バグ（`using Hidano.FacialControl.Application.UseCases;` による）を翌朝まで検知できなかった
- **Unity の DLL キャッシュ**: ソース編集後 `RequestScriptCompilation(CleanBuildCache)` + `EditorUtility.RequestScriptReload()` でも in-memory の型は古いまま。最終的に Unity 再起動でしか確実に反映できなかった
- **Test assembly 単体では再コンパイルが走らない**: 新規 `.cs` ファイルを追加しても Test DLL の mtime が更新されず、Total 件数が変わらなかった。Unity 再起動で解消
- **NUnit `Assert.Contains`**: 第 2 引数が非ジェネリック `ICollection` 要求のため `HashSet<string>` 非対応。`Assert.IsTrue(set.Contains(...))` または `CollectionAssert.Contains(IEnumerable, object)` で代替
- **Unity MCP `Unity_RunCommand` の namespace 解決**: 生成スクリプトが `Unity.AI.Assistant.Agent.Dynamic.Extension.Editor` 名前空間に包まれるため、`UnityEngine.Application` / `UnityEditor.Compilation.CompilationPipeline` 等は完全修飾しないと shadow される
- **Unity MCP で domain reload ダイアログが出る操作は不可**: DLL 削除や「Restart Required」系は `UNEXPECTED_ERROR: User interactions are not supported` で拒否される
- **Bash の `until ... sleep ... done` パターンがセッション設定で denied**: `Monitor` ツール経由で使う必要あり

## 学び

- 並列 Agent の成功 report はファイル変更の正当性を保証しない。Unity のようなコンパイル依存環境では、Agent 完了 → `Unity_ReadConsole` → 型チェックの 3 段ループが必須
- Unity の `using Namespace;` は `UnityEngine.{Common}` を shadow しうる。`Application` / `Debug` / `Object` / `Random` / `Time` など衝突しやすい名前はエージェントに事前警告しておく
- Unity 6 の MCP 経由では、単体 Asset の ImportAsset + ForceUpdate ではなく、ソースファイルの mtime 更新 + 再起動が最も確実な recompile トリガー
- `CreateDefault()` のようなファクトリメソッドは「古い契約」を保つために property defaults を維持する価値がある。新機能のデフォルトを既存 API に波及させると、既存テスト全体を書き換える必要が出る

## 次にやること

**優先度：高**

- CHANGELOG.md に今回の Q1 変更を追記（MenuItem、雛形 Expression、BlendShape ドロップダウン、ARKit マージ、README ロードマップ節、quickstart.md 刷新）
- クイックスタート手順を実機で上から実行して詰まる箇所がないか検証（GUI ファースト化したので過去の検証は無効）

**優先度：中**

- `Pipeline_AggregateAndBlendLoop_AllocatesZeroManagedMemory` の切り分け（flaky か real regression か）。失敗時の差分は 24576〜32768 byte
- preview.1 リリースステップ: `package.json` バリデーション → uOsc / FacialControl を npmjs.com 公開 → `v0.1.0-preview.1` タグ作成
- `Samples~/MultiSourceBlendDemo/MultiSourceBlendDemoHUD.cs` と `Assets/Samples/MultiSourceBlendDemoHUD.cs` の doc コメント差分解消

**優先度：低（preview.2 以降）**

- `IProfileJsonLoader` I/F 設計 + StreamingAssets / TextAsset / Addressables の 3 実装（Q3 ロードマップ）
- `JsonSchemaDefinition.SampleConfigJson` と旧 `default_config.json` の役割重複棚卸し

## 関連ファイル

### 新規作成
- `FacialControl/Packages/com.hidano.facialcontrol/Editor/Common/BlendShapeNameProvider.cs`
- `FacialControl/Packages/com.hidano.facialcontrol/Tests/EditMode/Editor/SampleExpressionsTests.cs`
- `FacialControl/Packages/com.hidano.facialcontrol/Tests/EditMode/Editor/BlendShapeNameProviderTests.cs`

### 修正
- `FacialControl/Packages/com.hidano.facialcontrol/Editor/Windows/ProfileCreationDialog.cs`
- `FacialControl/Packages/com.hidano.facialcontrol/Editor/Windows/ProfileCreationData.cs`
- `FacialControl/Packages/com.hidano.facialcontrol/Editor/Tools/ExpressionCreatorWindow.cs`
- `FacialControl/Packages/com.hidano.facialcontrol/Editor/Inspector/FacialProfileSOEditor.cs`
- `FacialControl/Packages/com.hidano.facialcontrol/Editor/Windows/ARKitDetectorWindow.cs`
- `FacialControl/Packages/com.hidano.facialcontrol/Editor/Tools/ARKitEditorService.cs`
- `FacialControl/Packages/com.hidano.facialcontrol/Documentation~/quickstart.md`
- `FacialControl/Packages/com.hidano.facialcontrol/README.md`

### 計画・参照
- `~/.claude/plans/a-e-q1-twinkly-raven.md`（Q1 実装プラン）
- `docs/work-procedure.md`（preview.1 チェックリスト L1757-1782）
