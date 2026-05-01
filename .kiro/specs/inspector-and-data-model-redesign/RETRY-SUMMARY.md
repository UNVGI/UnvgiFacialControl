# Spec Retry Batch Execution Summary

Started: 2026-05-01T00:09:59Z

Mode: --max-turns 200, timeout 3600s (60min)

| Task ID | Title | Result | Duration | Log |
|---------|-------|--------|----------|-----|
| 2.1 | Editor 専用サンプラの interface と stateless 実装� | OK | 156s | run-logs-retry/task-2.1.log |
| 2.3 | 中間 JSON schema v2.0 の DTO を新設し、snapshot tabl | OK | 2373s | run-logs-retry/task-2.3.log |
| 3.2 | LayerSlot struct を撤去し、LayerOverrideMask への置� | OK | 265s | run-logs-retry/task-3.2.log |
| 3.3 | FacialProfile から BonePoses 配列を撤去し、Expressi | OK | 395s | run-logs-retry/task-3.3.log |
| 3.4 | ExpressionResolver サービスを新設し、SnapshotId → | OK | 270s | run-logs-retry/task-3.4.log |
| 3.5 | Domain AnalogBindingEntry から Mapping field を撤去し� | OK | 308s | run-logs-retry/task-3.5.log |
| 3.6 | SystemTextJsonParser と FacialCharacterProfileConverter を | OK | 1118s | run-logs-retry/task-3.6.log |
| 3.7 | Phase 3 完了レビュー — Domain 全層の破壊書換が CI 緑 | OK | - | run-logs-retry/task-3.7.log |

## Task 3.7 Phase 3 完了レビュー — 結果

判定: **OK**（Phase 3 scope は完了。3 件失敗は Phase 5.1 の test infrastructure 課題で Domain 外）

### 検証結果

1. ✅ `LayerSlot` / `BonePose` / `BonePoseEntry` / `AnalogMappingFunction` / `AnalogMappingEvaluator` の actual code reference ゼロ（残存は CHANGELOG / 撤去理由 comment / 別概念の name pattern のみ）
2. ✅ `ExpressionUseCase.Activate(Expression)` API シグネチャ維持。Application Tests 50/50 緑
3. ⚠ Unity Test Runner EditMode: 910/913 pass。Domain layer は 769/769 全緑。3 件失敗はすべて `FacialCharacterSOInspectorTests`（Phase 5.1 担当範囲）の panel-less ListView Q<>() 起因で、Phase 3 retry による regression ではない（commit e31968a で test 追加時点から同じ pattern）

詳細は `run-logs-retry/task-3.7.log` を参照。
| 3.7 | Phase 3 完了レビュー — Domain 全層の破壊書換� | OK | 535s | run-logs-retry/task-3.7.log |
| 4.1 | Analog deadzone / scale / offset / clamp の 4 種 InputProcessor を stateless で実装する | OK | - | run-logs-retry/task-4.1.log |

## Task 4.1 結果

判定: **OK**（前 batch run の commit 3d65003 で実装済。RETRY 検証で再確認）

### 検証結果

1. ✅ `AnalogDeadZoneProcessor` / `AnalogScaleProcessor` / `AnalogOffsetProcessor` / `AnalogClampProcessor` の 4 ファイルが `Packages/com.hidano.facialcontrol.inputsystem/Runtime/Adapters/Processors/` 配下に存在し、`InputProcessor<float>` 派生で stateless（public な float field のみ serialize）
2. ✅ `Process(value, control)` の挙動が design.md Topic 3 / 要件 6.1, 6.4 通り
3. ✅ Unity Test Runner EditMode: `AnalogProcessorTests` 27/27 緑（4.1 の 12 ケース + 4.2 の 15 ケース）
4. ✅ コンパイル成功、無関係領域への regression なし
| 4.1 | Analog deadzone / scale / offset / clamp の 4 種 InputProc | OK | 474s | run-logs-retry/task-4.1.log |
| 4.2 | Analog curve / invert の 2 種 InputProcessor を stateless で実装する | OK | - | test-results/editmode-task-4-2-retry.xml |

## Task 4.2 結果

判定: **OK**（前 batch run の commit 3d65003 で実装済。RETRY 検証で再確認）

### 検証結果

1. ✅ `AnalogInvertProcessor`（stateless、`-value`）と `AnalogCurveProcessor`（preset 4 種、Linear/EaseIn=v*v/EaseOut=1-(1-v)^2/EaseInOut=SmoothStep）が `Packages/com.hidano.facialcontrol.inputsystem/Runtime/Adapters/Processors/` 配下に存在し、`InputProcessor<float>` 派生
2. ✅ Curve preset enum 定数を `private const int` で定義済（PresetLinear=0, PresetEaseIn=1, PresetEaseOut=2, PresetEaseInOut=3）
3. ✅ Unity Test Runner EditMode: `AnalogProcessorTests` 27/27 緑（4.1 の 12 ケース + 4.2 の Invert 3 + Curve 4preset×3=12 = 15 ケース）
4. ✅ コンパイル成功、無関係領域への regression なし
5. ✅ 既に Phase 4.3 (`AnalogProcessorRegistration`) で `InvertProcessorName` / `CurveProcessorName` 登録済
| 4.2 | Analog curve / invert の 2 種 InputProcessor を stateless | OK | 164s | run-logs-retry/task-4.2.log |
| 4.3 | InputProcessor 6 種を Editor / Runtime 両方で一括登録する初期化フックを設置する | OK | - | test-results/playmode-task-4-3-retry.xml |

## Task 4.3 結果

判定: **OK**（前 batch run の commit 3d65003 で実装済。RETRY 検証で再確認）

### 検証結果

1. ✅ `AnalogProcessorRegistration` 静的クラスが `Packages/com.hidano.facialcontrol.inputsystem/Runtime/Adapters/Processors/AnalogProcessorRegistration.cs` に存在し、`#if UNITY_EDITOR [InitializeOnLoad]` + `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]` の両経路から `Register()` を呼び出す
2. ✅ 6 processor (DeadZone / Scale / Offset / Clamp / Curve / Invert) を `InputSystem.RegisterProcessor<T>(name)` で一括登録
3. ✅ 登録名定数 (`DeadZoneProcessorName` / `ScaleProcessorName` / `OffsetProcessorName` / `ClampProcessorName` / `CurveProcessorName` / `InvertProcessorName`) を `public const string` で公開、`ProcessorNames` 配列でも提供
4. ✅ Unity Test Runner PlayMode: `AnalogProcessorRegistrationTests` 7/7 緑（ProcessorNames_HasSixDistinctEntries + Register_*_IsResolvableByName ×6）
5. ✅ コンパイル成功、Exit code 0
| 4.3 | InputProcessor 6 種を Editor / Runtime 両方で一括登� | OK | 182s | run-logs-retry/task-4.3.log |
| 4.4 | InputDeviceCategorizer を新設し、InputBinding.path から DeviceCategory を 0-alloc で推定する | OK | - | test-results/editmode-task-4-4-retry.xml |

## Task 4.4 結果

判定: **OK**（前 batch run の commit f0b78ed で実装済。RETRY 検証で再確認）

### 検証結果

1. ✅ `Runtime/Adapters/Input/InputDeviceCategorizer.cs` に `DeviceCategory` enum (Keyboard=0 / Controller=1) と静的 `Categorize(string bindingPath, out bool wasFallback)` メソッドが存在
2. ✅ 認識 prefix を `static readonly string[] ControllerPrefixes` で定数化（`<Gamepad>` / `<Joystick>` / `<XRController>` / `<Pen>` / `<Touchscreen>`）。`<Keyboard>` は別 const
3. ✅ 0-alloc: `string.StartsWith(prefix, StringComparison.Ordinal)` で判別し、`null` / 空文字 / 未認識 prefix では `wasFallback=true` + `DeviceCategory.Controller` を返す（Req 7.5）
4. ✅ Unity Test Runner EditMode: `InputDeviceCategorizerTests` 5/5 緑（Categorize_Keyboard / Gamepad / XRController / UnknownPrefix / NullOrEmpty）
5. ✅ Phase 3 RETRY 完了後の現状で再コンパイル成功、Exit code 0
| 4.4 | InputDeviceCategorizer を新設し、InputBinding.path か� | OK | 139s | run-logs-retry/task-4.4.log |
| 4.5 | ExpressionInputSourceAdapter を新設し、Keyboard/Control | OK | 691s | run-logs-retry/task-4.5.log |
| 4.6 | Keyboard/Controller 個別 InputSource を物理削除し、 | OK | 350s | run-logs-retry/task-4.6.log |
| 4.7 | InputSystemAdapter に device 推定経路を追加し、Ana | OK | 527s | run-logs-retry/task-4.7.log |
| 4.8 | 0-alloc 検証 PlayMode/Performance テストを整備する | OK | - | test-results/playmode-task-4-8-retry.xml |

## Task 4.8 結果

判定: **OK**（前 batch run の commit で 3 種 PlayMode/Performance テスト実装済。RETRY 検証で再確認）

### 検証結果

1. ✅ `Tests/PlayMode/Performance/` 配下に 3 件の AllocationTests クラスが存在:
   - `ExpressionResolverAllocationTests`（4 ケース）: `TryResolve` を 100 frames 連続呼出し、`GC.GetTotalMemory(false)` delta = 0 / `ProfilerRecorder("GC.Alloc")` = 0 / 未登録 SnapshotId 経路 / `TryGetSnapshot` の 0-alloc を検証
   - `ExpressionInputSourceAdapterAllocationTests`（4 ケース）: `InputTestFixture` 経由で keyboard / gamepad の `Press`/`Release` を 100 回 dispatch、`Tick(0.016f)` を 100 frames 反復、bind/unbind warm-up 後のヒープ漏洩なしを検証
   - `AnalogProcessorAllocationTests`（8 ケース）: 6 種 processor 連結 1000 回 (`GC.GetTotalMemory` + `ProfilerRecorder` の双方) と単独 processor 各 1000 回の per-Process 0-alloc を検証
2. ✅ Unity Test Runner PlayMode: `Hidano.FacialControl.InputSystem.Tests.PlayMode.Performance` 16/16 緑（duration 0.144s）
3. ✅ コンパイル成功、無関係領域への regression なし
4. ✅ Req 11.1 / 11.3 / 11.4 / 11.5 / 12.7 を Performance テスト経路で保証
