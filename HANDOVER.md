# HANDOVER (2026-04-21)

## 今回やったこと

- `/kiro:spec-run layer-input-source-blending` で全 47 リーフタスク（1.1〜10.9）を順次バッチ実行
- 各タスクを `claude -p` の子セッションで TDD 実装 → UnityTestRunner → 自動コミット
- 全 47 タスクが OK で完了、47 件の commit を `feature/hidano/generate-prototype` に追加
- `/kiro:validate-impl layer-input-source-blending` を実行して **GO 判定** を取得
- EditMode **1045/1045 Passed**（editmode-10-8.xml）／ PlayMode 個別 suite 全 Passed

## 決定事項

- `claude -p` のサブセッションは `--max-turns 200` で実行（60 では TDD タスクが完結しない）
- Bash の 10 分タイムアウト上限のため `run_in_background: true` で起動し完了通知を待つパターンを採用
- 子セッションへのプロンプトは single-quoted で渡す（タスクタイトル内のバックティックを保護）
- `tasks.md` の checkbox 更新はスキップ（git log を ground truth とする）
- Feature は validation で GO 判定、preview リリース準備フェーズへ進行可

## 捨てた選択肢と理由

- **`--max-turns 60`**: ファイル作成 + テスト実行 + コミットで使い切る。TDD タスクには不足
- **Bash foreground で 30 分タイムアウト**: Bash ツール上限は 600000ms（10 分）のため物理的に不可
- **Monitor ツールで出力ファイルを polling**: `grep` 利用が permission で拒否、シンプルな until-loop も拒否
- **手動で未コミット状態から 1.1 を commit して continue**: ユーザが選択肢 1（リトライ）を採用
- **TaskCreate/TaskUpdate による進捗追跡**: 47 タスクの機械的実行には過剰。commit history と OK/FAIL 出力で十分

## ハマりどころ

- 初回 1.1 が max-turns 60 で打ち切り。ファイル生成済みだが未コミット状態で残った（再実行で解消）
- 7.2（JSON parser）で foreground の 10 分 timeout に接触。harness が自動で background 化したが、それ以降は自発的に `run_in_background: true` で起動する運用に切替
- 子セッションが task 1.1 の scope 外（`ITimeProvider.cs`）を先行作成（後続 2.2 の担当範囲）。依存関係上やむを得ず、検証で problem なし
- `sleep` チェインや `tail` / `grep` を Bash 経由で使おうとして permission 拒否が頻発。Read / Grep の専用ツール経由が必須
- タスクタイトルに `「`、バックティック、日本語全角記号が含まれ shell escape が複雑化（single-quote で統一）

## 学び

- nested `claude -p` は `unset CLAUDECODE &&` + `echo "" |` + `--enable-auto-mode --verbose` が安定パターン
- プロジェクトは hooks による自動 commit が稼働（test-results の .xml は hooks が随時 Add/Delete）
- タスク間依存が `(P)` マーカーで明示されていない場合でも、子セッションは最短経路で先行実装しがち。スコープ境界を prompt に強く明記しても完全抑止は難しい
- 47 タスク連続実行で FAIL ゼロは spec 品質と TDD の粒度設計が機能している証拠
- `validate-impl-agent` は test-results artifact と commit history を合わせて検証する。artifact を残しておくと検証が高速化

## 次にやること

優先度高:
- **PlayMode 全体回帰の実行**: `Tests/PlayMode` を batchmode で一括実行して `playmode-full.xml` を残す（最後の完全 run は task 6.2 時点 = 247 Passed。以降は個別 suite のみ）
- **tasks.md の checkbox 更新**: 全 47 タスクを `- [x]` に一括更新（`/kiro:spec-status` の整合性確保）
- **preview リリース準備**: CHANGELOG のエントリを preview.N に束ね、`package.json` の version を bump、タグ付与

優先度中:
- **`FacialProfileSO_InputSourcesView` の自動テスト追加**: design どおり手動テスト前提だが、repaint O(1) を回帰から守るため将来補助テストを追加する余地
- **D-12（controller + keyboard 同時押しの BlendShape 加算が意図挙動）の Tooltip 追加**: Inspector View に Help Box で明示

優先度低:
- ランタイムの PlayMode Performance を実機（セルフホストランナー）で再計測

## 関連ファイル

仕様 / ドキュメント:
- `.kiro/specs/layer-input-source-blending/{requirements,design,tasks,research}.md`
- `FacialControl/Packages/com.hidano.facialcontrol/CHANGELOG.md`
- `docs/migration-guide.md`

新規実装の主要ファイル:
- `Runtime/Domain/Interfaces/{IInputSource,ITimeProvider}.cs`
- `Runtime/Domain/Models/{InputSourceId,LayerSourceWeightEntry,InputSourceType}.cs`
- `Runtime/Domain/Services/{LayerInputSourceAggregator,LayerInputSourceRegistry,LayerInputSourceWeightBuffer,ValueProviderInputSourceBase,ExpressionTriggerInputSourceBase}.cs`
- `Runtime/Adapters/InputSources/{Controller,Keyboard}ExpressionInputSource.cs`
- `Runtime/Adapters/InputSources/{Osc,LipSync}InputSource.cs`
- `Runtime/Adapters/InputSources/{InputSourceFactory,UnityTimeProvider}.cs`
- `Runtime/Adapters/Json/Dto/{InputSourceDto,InputSourceOptionsDto,OscOptionsDto,ExpressionTriggerOptionsDto,LipSyncOptionsDto}.cs`
- `Editor/Inspector/FacialProfileSO_InputSourcesView.cs`
- `Tests/Shared/ManualTimeProvider.cs` + asmdef

変更ファイル（非破壊）:
- `Runtime/Adapters/OSC/OscDoubleBuffer.cs`（WriteTick 追加）
- `Runtime/Adapters/Json/{SystemTextJsonParser,JsonSchemaDefinition}.cs`（inputSources 必須化）
- `Runtime/Adapters/ScriptableObject/InputBindingProfileSO.cs`（InputSourceCategory 追加）
- `Runtime/Application/UseCases/LayerUseCase.cs`（Aggregator 委譲、API 非破壊）
- `Runtime/Adapters/Playable/FacialController.cs`（weight API 公開）

テスト成果物:
- `FacialControl/test-results/editmode-10-8.xml`（1045 Passed、最新の完全 EditMode run）
- `FacialControl/test-results/playmode-10-5.xml`（10 体 × 60 FPS Performance Passed）
- `FacialControl/test-results/playmode-6.2.xml`（247 Passed、最後の完全 PlayMode run）
