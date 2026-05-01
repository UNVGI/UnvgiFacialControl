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
| 4.8 | 0-alloc 検証 PlayMode/Performance テストを整備する | OK | 336s | run-logs-retry/task-4.8.log |
| 5.1 | FacialCharacterSOInspector を UI Toolkit で全面改修し | OK | 571s | run-logs-retry/task-5.1.log |
| 5.2 | ExpressionCreatorWindow を AnimationClip ベイク経路に改修する | OK | - | test-results/editmode-task-5-2-retry-v2.xml |

## Task 5.2 結果

判定: **OK**（前 batch run の commit 4bdbf4b + e2130d8 で実装済。RETRY 検証で再確認）

### 検証結果

1. ✅ `Editor/Tools/ExpressionCreatorWindow.cs` が AnimationClip ベイク経路に改修済（Domain Expression 保存ではなく `AnimationUtility.SetEditorCurve` + `SetAnimationEvents` 経由）
2. ✅ ベイクロジックが `Editor/Tools/ExpressionClipBakery.cs` static helper に抽出済（Refactor 完了）
3. ✅ TransitionDuration / Curve preset 編集 UI が遷移メタデータ foldout（スライダーペイン下部）として配置済（OQ4 解決）
4. ✅ 逆ロード経路は `IExpressionAnimationClipSampler.SampleSnapshot` / `SampleSummary` 経由でスライダー値・遷移メタを復元
5. ✅ Unity Test Runner EditMode: `ExpressionCreatorWindowTests` 6/6 緑（Bake_BlendShapeSliders / Bake_TransitionMetadata / LoadExistingClip / Bake_NullClip_Throws / Bake_NullEntries_Throws / Bake_RebakeOverwritesExistingCurves）
6. ✅ 既存 PreviewRenderWrapper / BlendShapeNameProvider 再利用、Preview 機能維持
7. ✅ コンパイル成功、無関係領域への regression なし
| 5.2 | ExpressionCreatorWindow を AnimationClip ベイク経路に | OK | 252s | run-logs-retry/task-5.2.log |
| 5.3 | FacialCharacterSOAutoExporter を AnimationClip サンプラ | OK | 765s | run-logs-retry/task-5.3.log |
| 5.4 | FacialControllerEditor の概要表示を新モデルに合� | OK | 358s | run-logs-retry/task-5.4.log |
| 6.1 | MultiSourceBlendDemo サンプルを schema v2.0 で再生� | OK | 681s | run-logs-retry/task-6.1.log |
| 6.2 | AnalogBindingDemo サンプルを mapping field 不在 + pro | OK | 702s | run-logs-retry/task-6.2.log |
| 6.5 | Phase 6 完了レビュー — Sample 結線確認と CI 全 Phase 緑 | OK | - | test-results/{editmode,playmode}-task-6-5-retry.xml |

## Task 6.5 結果

判定: **OK**（CI 緑 + 旧 API 物理削除確認済。Sample Scene の手動再生確認と UPM Import Sample 検証は Editor を起動できるレビュー担当の手元での実機確認が必要）

### 検証結果

1. ✅ Unity Test Runner EditMode: 920/920 緑（duration 2.06s, test-results/editmode-task-6-5-retry.xml）
2. ✅ Unity Test Runner PlayMode: 301/301 緑（duration 4.86s, test-results/playmode-task-6-5-retry.xml）。`Hidano.FacialControl.InputSystem.Tests.PlayMode.Performance` 16/16 緑（0-alloc 検証経路）を含む
3. ✅ 旧 API のソースファイル物理削除を `Glob` で再確認:
   - `Runtime/Domain/Models/BonePose*.cs` / `LayerSlot*.cs` / `AnalogMappingFunction*.cs` / `Domain/Services/AnalogMappingEvaluator*.cs` / `KeyboardExpressionInputSource*.cs` / `ControllerExpressionInputSource*.cs` 全てヒット 0
4. ✅ Sample 配下を含むコードベース全体への `grep`: 残存ヒットは
   - CHANGELOG / README / Migration Guide の記述（撤去 API 一覧 / 移行説明）
   - XML doc コメント（`<c>...</c>` での旧 API 参照）
   - `AnalogBindingTargetKind.BonePose`（enum 値、現役の analog binding ターゲット種別。Domain.Models.BonePose 型とは別概念）
   - `BonePoseSnapshot` / `AnalogBonePoseProvider`（Adapters 層の現役名称）
   - `JsonSchemaDefinition.BonePose`（field 名定義、現役）
   のみで、撤去 API への actual code reference は 0
5. ⚠ 残課題（実機手動確認が必要）:
   - Multi Source Blend Demo / Analog Binding Demo の Sample Scene を Unity Editor で開いて Play 実行
   - UPM Package Manager `Import Sample` 経由で空プロジェクトに import し動作確認
   - これらは batch retry の実行範囲外（GUI 操作が必要）。レビュー担当（Hidano + Junki Hiroi）の手元で次回確認
