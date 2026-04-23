# HANDOVER (2026-04-23)

## 今回やったこと

- CHANGELOG.md へ Q1 変更を追記
  - 日付を `2026-04-21` → `Unreleased` に戻す（P22-07 ルール準拠）
  - Added（Editor 拡張）: MenuItem `FacialControl/新規プロファイル作成`、`ProfileCreationData.NamingConvention`、`BlendShapeNameProvider`、`ARKitEditorService.MergeIntoExistingProfile()`
  - Changed（Editor 拡張）: BlendShape 名入力の TextField → ドロップダウン化、ARKit マージ UI
  - ドキュメント（新節）: README の「既知の制限とロードマップ」、quickstart.md の GUI ファースト刷新
- `Pipeline_AggregateAndBlendLoop_AllocatesZeroManagedMemory` の flaky 対応
  - ホットパス静的解析: `AggregateAndBlend` の全経路が事前確保バッファで動作し割当なしを確認
  - Unity MCP 経由で Mono ヒープページサイズを実測（probe で `string[1000]` alloc = 40960 byte = 1 ページ）
  - 修正: ループ 1,000 → 50,000 iter、許容しきい値 `ManagedPageNoiseToleranceBytes = 64 * 1024`（ページ 2 枚分）
  - 修正後 TestRunnerApi 経由で 5 連続 pass 確認（flaky 再現せず）

## 決定事項

- **CHANGELOG の日付ポリシー**: npm 公開 / タグ作成前は `Unreleased` を維持。P22-07 タスクで実リリース日に置換
- **EditMode GCAlloc テストの測定方針**: 精密な per-method counter は存在しないため、「ループ回数 × 最小想定割当 >> ページノイズ許容」の条件を満たすループサイズ + ページ許容の組合せで検証
- **許容しきい値 = 64KB**: Mono ページ実測値（32〜40KB）の 2 倍。32 byte/iter の実回帰でも 50,000 iter で 1.6MB になるため 25 倍のマージンで検出可能

## 捨てた選択肢と理由

- **`GC.GetAllocatedBytesForCurrentThread()` への置換**: Unity Mono では未実装で常に 0 を返すため使えない（probe で確認済）
- **`Profiler.GetTotalAllocatedMemoryLong()` を使用**: managed 差分を返さず native のみ（probe で `string[1000]` alloc の Profiler diff = 0 を確認）
- **`Recorder.Get("GC.Alloc")` の `sampleBlockCount`**: EditMode では Profiler の frame sampling が機能せず、`Profiler.enabled = true` でも sampleBlockCount が常に 0（probe で確認済）
- **tolerance を緩めるだけで対応**: 感度が落ちるので NG。ループ増量と組み合わせて実割当を magnify する方針を採用
- **PlayMode への移設**: テスト目的（Domain + Adapters の統合パイプライン検証）は EditMode の枠内で完結するため移設不要。`SetWeightZeroAllocationTests` 等の PlayMode 側は既に存在

## ハマりどころ

- **Unity MCP `Unity_RunCommand` の namespace 自動ラップ**: nested `private` class が namespace レベルに hoist されて `CS1527` エラー。top-level `internal sealed class` として分離して回避
- **`Application.dataPath` のパス**: Unity project root の `/Assets` を返す。`Path.Combine(..., "..")` で行くのは Unity project root（= `FacialControl/FacialControl/`）であって、**リポジトリ root（= `FacialControl/`）ではない**。test-results ファイルを探すパスを間違えて「ファイルなし」と誤認した
- **TestRunnerApi 経由の Full EditMode run**: `Filter { testMode = TestMode.EditMode }` だけで実行すると MCP が "User interactions are not supported" を返す。単一テスト名指定やフィルタ付きは動く。回避策未解明（Performance Testing の Prebuild/PostBuild hook が悪さしている可能性）
- **ICallbacks の GC**: `TestRunnerApi.RegisterCallbacks` は弱参照的挙動のため、コマンド script exit 後にコールバックが GC される。`static` フィールドで参照を保持すれば解決

## 学び

- Unity EditMode で managed alloc を per-method 精度で測る公式手段は事実上存在しない（3 種の API すべて機能不全を確認）
- 代替パターン: 「ループ回数を桁違いに増やして実割当を magnify + ページサイズを固定 tolerance として吸収」が現実解
- Mono ヒープは 32〜40KB ページで伸びる。`GC.Collect()` 直後の `GetTotalMemory(false)` は heap minimum を返すが、以降の微小活動で次ページ確保が起きると丸ごと差分に乗る
- Unity MCP のコード生成は regex ベースの namespace ラップのため、C# の class nesting / アクセス修飾子の組合せに制約がある。top-level に classes を並べる書き方が安定

## 次にやること

**優先度：高**

- クイックスタート手順を実機で上から実行して詰まる箇所がないか検証（GUI ファースト化後の初検証）
- preview.1 リリースステップ: `package.json` バリデーション → uOsc / FacialControl を npmjs.com 公開 → `v0.1.0-preview.1` タグ作成

**優先度：中**

- `Samples~/MultiSourceBlendDemo/MultiSourceBlendDemoHUD.cs` と `Assets/Samples/MultiSourceBlendDemoHUD.cs` の doc コメント差分解消
- `TryWriteSnapshot_SteadyStateCalls_DoNotAllocate`（`LayerInputSourceAggregatorTests.cs:1107`）も同パターン（`GC.GetTotalMemory` + `<= 0`）だが現状 flaky 報告なし。将来 flaky 化したら同じ修正を展開

**優先度：低（preview.2 以降）**

- `IProfileJsonLoader` I/F 設計 + StreamingAssets / TextAsset / Addressables の 3 実装
- `JsonSchemaDefinition.SampleConfigJson` と旧 `default_config.json` の役割重複棚卸し

## 関連ファイル

### 修正
- `FacialControl/Packages/com.hidano.facialcontrol/CHANGELOG.md`
- `FacialControl/Packages/com.hidano.facialcontrol/Tests/EditMode/Integration/OscControllerBlendingIntegrationTests.cs`

### 静的解析で参照（変更なし）
- `FacialControl/Packages/com.hidano.facialcontrol/Runtime/Domain/Services/LayerInputSourceAggregator.cs`
- `FacialControl/Packages/com.hidano.facialcontrol/Runtime/Domain/Services/LayerInputSourceWeightBuffer.cs`
- `FacialControl/Packages/com.hidano.facialcontrol/Runtime/Domain/Services/LayerBlender.cs`
- `FacialControl/Packages/com.hidano.facialcontrol/Runtime/Adapters/InputSources/OscInputSource.cs`
- `FacialControl/Packages/com.hidano.facialcontrol/Runtime/Domain/Services/ExpressionTriggerInputSourceBase.cs`

### 参考テスト（PlayMode 側の類似実装）
- `FacialControl/Packages/com.hidano.facialcontrol/Tests/PlayMode/Performance/SetWeightZeroAllocationTests.cs` — managed diff は `<= 0`、Profiler diff は `<= 1024` のハイブリッド

### 一時出力
- `FacialControl/test-results/pipeline-alloc-test.txt` — 単発検証結果
- `FacialControl/test-results/pipeline-alloc-multi.txt` — 5 連続実行結果（全 pass）
