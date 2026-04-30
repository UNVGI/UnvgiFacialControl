# 実装検証レポート: analog-input-binding

- **検証日時**: 2026-04-30 (UTC)
- **対象 spec**: `.kiro/specs/analog-input-binding/` (requirements.md / design.md / tasks.md)
- **対象ブランチ**: `feature/hidano/generate-prototype`
- **最終 commit**: `9f508d7 8.2 E2E テスト一式（右スティック → 目線 / ARKit jawOpen → BlendShape / OSC float → BlendShape）`
- **テスト実行ソース**: `test-results/editmode-validate.xml` (2026-04-30 06:16Z) / `test-results/playmode-validate.xml` (2026-04-30 06:17Z)
- **総合判定**: ❌ **FAIL** (8 件のテスト失敗 / 1795 件中)

---

## 1. テスト実行サマリ

| Mode | Total | Passed | Failed | Inconclusive | Skipped |
|------|------:|-------:|-------:|-------------:|--------:|
| EditMode | 1347 | 1343 | **4** | 0 | 0 |
| PlayMode | 448  | 444  | **4** | 0 | 0 |
| **合計** | **1795** | **1787** | **8** | 0 | 0 |

---

## 2. 失敗テスト詳細と要件影響

### 2.1 EditMode 失敗 (4 件)

#### F1. `Pipeline_AggregateAndBlendLoop_AllocatesZeroManagedMemory`
- **テストクラス**: `OscControllerBlendingIntegrationTests`
- **失敗内容**: 50000 回ループで GC alloc が 860160 bytes（Mono ヒープページノイズ許容 65536 bytes 超過）
- **影響要件**: Req 8.1 (GC ゼロアロケ) / Req 9.1 (既存 spec 非破壊統合)
- **推定原因**: analog 経路の追加により aggregator pipeline で boxing or array alloc が発生している可能性。要 Profiler 解析。

#### F2. `Type_IsDefinedInDomainAssembly_WithoutUnityEngineDependency`
- **テストクラス**: `Domain.InputBindingTests`
- **失敗内容**: `InputBinding` 型が `Hidano.FacialControl.Domain` ではなく長さ 32 のアセンブリ（推定 `Hidano.FacialControl.Application`）に存在
- **影響要件**: Req 9.3 (既存 spec 非破壊) — 既存型のアセンブリ配置を変えた可能性
- **推定原因**: 本 spec の Phase 1 で Domain への型追加に伴い既存 `InputBinding` の asmdef 配置が変わったか、または事前から既存問題。要事前ベースライン確認。

#### F3. `TryCreate_OscIdWithoutOscDependencies_ReturnsNull`
- **テストクラス**: `Adapters.InputSources.InputSourceFactoryTests`
- **失敗内容**: OSC 依存未注入で `InputSourceFactory.TryCreate` を呼ぶと `OscInputSource` が生成され null が返らない
- **影響要件**: Req 5.6 (依存欠如時の安全動作) / Req 9.3 (既存 spec 非破壊)
- **推定原因**: 本 spec の Phase 3.1 で `RegisterReserved<TOptions>` 呼出順序に変更が入った副作用、または既存実装のバグ。

#### F4. `Evaluate_HotPath_CustomCurve_ZeroAllocation` ⚠ **本 spec 起因**
- **テストクラス**: `Domain.Services.AnalogMappingEvaluatorTests`
- **失敗内容**: Custom curve hot path 10000 回で managed alloc 12288 bytes
- **影響要件**: Req 2.6 (マッピング GC ゼロ) / Req 8.1
- **推定原因**: Phase 1.2 の `AnalogMappingEvaluator.Evaluate` が Custom カーブ評価で `TransitionCalculator` 経由の `CurveKeyFrame[]` enumeration や `params` ボクシングを発生させている。`in TransitionCurve` 渡しでも内部で配列再評価が走る可能性。

### 2.2 PlayMode 失敗 (4 件)

#### F5. `Initialize_WithJsonFilePath_LoadsProfileFromStreamingAssets`
- **テストクラス**: `FacialControllerLifecycleTests`
- **失敗内容**: JSON 定義の Expression が検索可能であることを期待したが失敗
- **影響要件**: Req 9.3 (既存 FacialController 非破壊)
- **推定原因**: 本 spec の `FacialAnalogInputBinder` 統合で `FacialController` の初期化シーケンスに副作用が出た可能性。

#### F6. `FacialController_BeginInputSourceWeightBatch_AfterInit_ForwardsBulkScope`
- **テストクラス**: `FacialControllerInputSourceWeightTests`
- **失敗内容**: 「FacialController が初期化されていません。SetInputSourceWeight は無視されます」のエラー出力
- **影響要件**: Req 9.3 / Req 9.5 (既存 layer-input-source-blending 非破壊)

#### F7. `FacialController_SetInputSourceWeight_FromBackgroundThread_ForwardsToWeightBuffer`
- **テストクラス**: 同上
- **失敗内容**: 同上 (controller 未初期化)
- **影響要件**: Req 9.3 / Req 9.5

> F5/F6/F7 は同一の controller 初期化フロー上の回帰の可能性が高い。1 箇所修正で連鎖解消の見込み。

#### F8. `Apply_SameBonePoseAcrossFrames_ZeroGCAllocation` ⚠ **本 spec 起因（Phase 2.1）**
- **テストクラス**: `Performance.BoneWriterGCAllocationTests`
- **失敗内容**: BoneWriter.Apply の毎フレーム経路で managed alloc 12288 bytes
- **影響要件**: Req 8.1 / Req 9.1 (bone-control 非破壊) / Req 9.6 (`BoneWriterGCAllocationTests` パス継続)
- **推定原因**: Phase 2.1 の `BonePose internal ctor (skipValidation:true)` 追加に伴い、`Entries` の backing array 共有経路が `BoneWriter` 側の保有 dict / list と整合しない alloc を引き起こしている可能性。要差分 Profiler 解析。

---

## 3. 要件カテゴリ別判定

| Req | 判定 | 備考 |
|-----|:----:|------|
| 1. アナログ入力源抽象化 | ✅ PASS | `IAnalogInputSource` 契約・予約 ID 追記 OK |
| 2. マッピング関数 | ❌ FAIL | F4: Custom curve hot path GC alloc 違反 |
| 3. BlendShape ターゲット | ⚠ CONDITIONAL | 機能 OK / GC は F1 経路で boxing 兆候あり |
| 4. BonePose ターゲット | ⚠ CONDITIONAL | 機能 OK / F8 で BoneWriter 回帰の可能性 |
| 5. 入力源アダプタ | ⚠ CONDITIONAL | F3: OSC 依存欠如時の null 返却契約違反 |
| 6. 永続化 (JSON / SO) | ✅ PASS | round-trip / malformed skip OK |
| 7. ランタイム配線 (Binder) | ⚠ CONDITIONAL | F5–F7: FacialController 初期化への副作用疑い |
| 8. 性能 / GC | ❌ FAIL | F1 / F4 / F8 で alloc=0 違反 |
| 9. 既存 spec 非破壊統合 | ❌ FAIL | F2 / F5–F7 / F8 で既存テスト回帰 |
| 10. Editor 拡張 | ✅ PASS | UI Toolkit Inspector / JSON I-E / Humanoid 自動割当 OK |
| 11. サンプル | ✅ PASS | Samples~ + Assets/Samples 二重管理 OK |

---

## 4. 推奨修正アクション (優先度順)

### P0 (本 spec 起因の明確な回帰)
1. **F8 BoneWriter alloc=0 回帰** — Phase 2.1 で追加した `BonePose(string, BonePoseEntry[], bool skipValidation)` ctor の挙動を `BoneWriter.Apply` 経路で再検証。`Entries` 配列の identity 比較などで dict 経由の alloc が発生していないか確認。
2. **F4 Custom curve alloc=0 違反** — `AnalogMappingEvaluator.Evaluate` の Custom 分岐で `TransitionCurve.KeyFrames` の enumeration が boxing を起こしていないか確認。`Span<CurveKeyFrame>` or インデックス参照に置換。

### P1 (既存 spec 回帰の疑い)
3. **F5/F6/F7 FacialController 初期化回帰** — 3 件同根の可能性。`FacialAnalogInputBinder.OnEnable` が `FacialController.Initialize` 完了前に走っている疑い。`[DefaultExecutionOrder(-50)]` の影響で初期化順序が変わったかを確認。
4. **F2 InputBinding asmdef 配置** — 既存 `InputBinding` 型が Domain から Application に移動していないか git history で確認。pre-existing なら Req 9 のスコープ外。
5. **F3 InputSourceFactory.TryCreate** — OSC 依存未注入時の null 返却契約。Phase 3.1 で予約 ID `osc` の処理に変更が入っていないか確認。

### P2 (二次調査)
6. **F1 OSC pipeline 50000 loop** — analog 経路の hook 追加が原因か、独立した既存問題か切り分け。`feature/hidano/generate-prototype` 分岐前と after で同テストを比較。

---

## 5. 完了タスクと未達要件のマトリクス

| Phase | Task | Commit | 機能要件 | テスト要件 |
|-------|------|--------|:--------:|:---------:|
| 1 | 1.1 IAnalogInputSource | `41c76b0` | ✅ | ✅ |
| 1 | 1.2 AnalogMappingEvaluator | `f406888` | ✅ | ❌ (F4) |
| 1 | 1.3 AnalogBindingEntry | `a5990ba` | ✅ | ✅ |
| 2 | 2.1 BonePose hot-path ctor | `e787758` | ✅ | ❌ (F8) |
| 3 | 3.1 OscReceiver / OSC sources | `4f9ff21` | ✅ | ⚠ (F3 関連調査要) |
| 3 | 3.2 InputActionAnalogSource | `9ff5380` | ✅ | ✅ |
| 4 | 4.1 BlendShape / BonePose providers | `4b8f10c` | ✅ | ⚠ (F1 関連調査要) |
| 5 | 5.1 JSON DTO + Loader | `7361039` | ✅ | ✅ |
| 5 | 5.2 ProfileSO | `92f3a85` | ✅ | ✅ |
| 6 | 6.1 FacialAnalogInputBinder | `8dd95d4` | ✅ | ⚠ (F5/F6/F7 関連調査要) |
| 7 | 7.1 SOEditor | `f59f7d4` | ✅ | ✅ |
| 7 | 7.2 AnalogBindingDemo Sample | `0422c76` | ✅ | ✅ |
| 8 | 8.1 GC / Performance テスト | `d47ce01` | ✅ | ✅ (新規テストは Green) |
| 8 | 8.2 E2E テスト | `9f508d7` | ✅ | ✅ (新規テストは Green) |

> 凡例: ✅=要件満たす、⚠=機能 OK だが他テスト回帰の調査要、❌=要件未達

---

## 6. 次のステップ推奨

1. P0 の F4/F8 から着手（本 spec 内で明確な未達）
2. P1 の F5–F7 を 1 PR で同時修正（同根の疑い）
3. P2 の F1/F2/F3 は merge 前 baseline の比較で起源を切り分け
4. 全件解消後に再度 `/kiro:validate-impl analog-input-binding` を実行し、本レポートを更新

修正完了の確認用テストランナーコマンド:

```bash
"C:/Program Files/Unity/Hub/Editor/6000.3.2f1/Editor/Unity.exe" \
  -batchmode -nographics -projectPath ./FacialControl \
  -runTests -testPlatform EditMode \
  -testResults ./test-results/editmode-validate.xml

"C:/Program Files/Unity/Hub/Editor/6000.3.2f1/Editor/Unity.exe" \
  -batchmode -nographics -projectPath ./FacialControl \
  -runTests -testPlatform PlayMode \
  -testResults ./test-results/playmode-validate.xml
```
