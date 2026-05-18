# Requirements Document

## Project Description (Input)
preview.1 リリース直前の Editor UX 改善 + バグ修正パック。`docs/backlog.md` の短期項目 S-10〜S-16 を 1 spec として集約し、各サブタスクを `/kiro:spec-run` で連続実行する。詳細は `docs/backlog.md`（S-10〜S-16 の各ブロック）を必ず参照すること。

### 含まれるタスク

#### S-10: OSC 受信側 ContributeMask / TryWriteValues の index 空間ずれ修正
- **現象**: `OscReceiverDemo` + `OscOutputDemo` 同一プロセス起動で `ArgumentException: Array lengths must be the same.` が `BitArray.Or` 経由で毎フレーム発生（`LayerInputSourceAggregator.AggregateInternal` → `LayerUseCase.UpdateWeights` → `FacialController.LateUpdate`）。
- **根本原因**: `HeartbeatConsistencyChecker._contributeMask` / `_skipMask` が OSC マッピング数長で構築されるのに対し、`LayerInputSourceAggregator._perLayerMask[l]` は mesh の BlendShape 数長で構築される。Or で長さ不一致。
- **潜在バグ**: `OscInputSource.TryWriteValues` が `readBuffer[i] → output[i]` 同一 index でコピーしているが、`readBuffer` は mapping index 空間、`output` は mesh BlendShape index 空間なので順序が異なれば値が誤った BlendShape に乗る。
- **対応方針候補**:
  - (a) `OscAdapterBinding.OnStart` で `ctx.BlendShapeNames` を `HeartbeatConsistencyChecker` に渡し、`_contributeMask` / `_skipMask` を mesh BlendShape 数長で構築（mapping name → mesh index 逆引き）。
  - (b) `OscInputSource` 内部に `mappingIndex → meshIndex` 逆引きを保持させ、`TryWriteValues` を mesh-index 空間で書き込むよう改修。
  - (c) `IInputSource.ContributeMask` interface コメントと Aggregator 前提のズレを契約として明文化。
  - 参考実装: `AnalogBlendShapeInputSource` の `nameToIndex` 構築 + mesh-index 空間 ContributeMask 立て。
- **テスト**: 既存 `Tests/EditMode/Adapters/OSC/HeartbeatConsistencyCheckerTests.cs` を mesh-index 空間追従、新規 `Tests/EditMode/Adapters/InputSources/OscInputSourceMaskTests.cs`（mapping 数 ≠ mesh 数のケース）。
- **影響範囲**: `Packages/com.hidano.facialcontrol.osc/Runtime/Adapters/OSC/HeartbeatConsistencyChecker.cs`, `Adapters/AdapterBindings/OscAdapterBinding.cs`, `Adapters/InputSources/OscInputSource.cs`。

#### S-11: Expression 遷移メタデータの AnimationClip ベイク撤去
- **現状**: `ExpressionClipBakery.Bake` が AnimationClip に (a) 0F 目の BlendShape 値、(b) 遷移時間 / カーブの `AnimationEvent` メタの 2 種を書き込む。`ExpressionCreatorWindow` 右ペインに「遷移メタデータ」Foldout が出る。一方、`ExpressionSerializable.transitionDuration` / `transitionCurve` は Profile Inspector の Expression Row でも編集可能（`FacialCharacterProfileSOInspector.cs:1796` の Slider）。同じ値が 2 か所で編集できる混乱。
- **方針**: AnimationClip 内は 0F 目の BlendShape / Bone キーのみ記録し、遷移時間 / カーブの `AnimationEvent` は書かない。source of truth は Profile Inspector の Expression Row 一本化。
- **対応**: `ExpressionClipBakery.Bake` から `AnimationEvent` 書き込み除去、`AnimationClipExpressionSampler.SampleSummary` を「メタが無ければ既定値を返す」挙動に揃える、`ExpressionCreatorWindow` から「遷移メタデータ」Foldout（`_transitionDurationField` / `_curveTypeDropdown`）を削除、AutoExporter / SampleSummary 参照経路の動作確認、Domain の `Expression` bridge ctor（`TransitionDuration` / `TransitionCurve` を受ける方）の撤去可否を別 PR で判断。
- **影響範囲**: `Packages/com.hidano.facialcontrol/Editor/Tools/ExpressionClipBakery.cs`, `Editor/Tools/ExpressionCreatorWindow.cs`, `Editor/Sampling/AnimationClipExpressionSampler.cs`, `Editor/AutoExport/FacialCharacterProfileExporter.cs`, 対応 EditMode テスト。

#### S-12: ExpressionCreatorWindow UI 改善
- **(1) ボタン潰れ**: `ExpressionCreatorWindow.cs:192-204` bottomSection に flex-end 配置された「AnimationClip にベイク」ボタンが幅を確保できず潰れる。`FacialControlStyles.ActionButton` の幅指定 / `flexShrink` 組合せが原因と推定。`style.flexShrink = 0` + `style.minWidth` 追加で修正。
- **(2) 画像書き出し**: 現プレビュー出力（256×256 RenderTexture）を PNG で保存するボタンを leftPanel に追加。`PreviewRenderWrapper` から `Texture2D.ReadPixels` で取得 → `EditorUtility.SaveFilePanel` で保存先指定。
- **(3) Clip 無し新規作成**: ObjectField 隣に「新規作成」ボタンを追加。`EditorUtility.SaveFilePanelInProject` で保存先指定 → 空 AnimationClip 生成 → `_clipField.value` に代入して `OnClipFieldChanged` を発火。
- **影響範囲**: `Editor/Tools/ExpressionCreatorWindow.cs`, `Editor/Common/PreviewRenderWrapper.cs`（PNG 出力 API 追加）, `Tests/EditMode/Editor/Tools/ExpressionCreatorWindowTests.cs`。

#### S-13: Expression Row の Gaze 用 AnimationClip スロット非表示（isGaze=true 時）
- **現状**: Profile Inspector の Expression Row では `isGaze=true` のときに遷移時間 Slider は非表示（`FacialCharacterProfileSOInspector.cs:1805`）にしているが、AnimationClip ObjectField は同行内で表示され続けている。
- **方針**: `isGaze=true` では AnimationClip による補間は適用されないため、UI 上もスロット自体を `DisplayStyle.None` にして混乱を避ける。
- **影響範囲**: `Editor/Inspector/FacialCharacterProfileSOInspector.cs` の Expression Row 構築箇所、対応 EditMode テスト。

#### S-14: GazeConfigs の一括再生成ボタン + Undo 経路復元
- **現状**: GazeConfig 手動追加 dropdown は `RebuildGazeConfigsUI` で `DisplayStyle.None` に隠されている（`FacialCharacterProfileSOInspector.cs:915`）。アナログ表情追加時の自動生成のみが正規ルートで、誤って GazeConfig 行を削除した場合の再生成手段が UI 上に無く、Undo でも復元されないケースが報告されている。
- **方針**:
  - (a) `GazeConfigBulkResolveButton` 近辺に「GazeConfig を一括再生成」ボタンを明示配置し、未生成の Gaze 用 Expression を一括スキャンして補完。
  - (b) GazeConfig 削除経路に `Undo.RecordObject` を正しく挿し、Ctrl+Z で復元できることを EditMode テストで保証。
- **影響範囲**: `Editor/Inspector/FacialCharacterProfileSOInspector.cs`, 対応 EditMode テスト。

#### S-15: Gaze 左右別 Action UI 配置見直し
- **現状**: `InputSystemAdapterBindingDrawer.BindExpressionBindingRow` 並び順は `Action 名 → useDistinct チェック → 左 Action → 右 Action → 表情 ID → 動作モード → トリガモード`。Gaze + `useDistinct=true` で元「Action 名」を `DisplayStyle.None` で非表示（`UpdateGazeActionFieldVisibility`, `InputSystemAdapterBindingDrawer.cs:526-553`）。
- **方針**: 並び順を `表情 ID → 動作モード → useDistinct チェック → 左 Action → 右 Action → Action 名 → トリガモード` に変更。Gaze + `useDistinct=true` 時の「Action 名」は非表示ではなくグレーアウト（`SetEnabled(false)`）にして「左右別を ON にしたから単一 Action が無効化されている」ことを視覚化。
- **影響範囲**: `Packages/com.hidano.facialcontrol.inputsystem/Editor/AdapterBindings/InputSystemAdapterBindingDrawer.cs`, 対応 EditMode テスト。

#### S-16: uLipSync PhonemeEntry Undo 後の表示不整合
- **現象**: BlendShape ↔ AnimationClip モード切り替え時に Undo 経路で値自体が古いまま表示される。
- **根本原因推定**: `PhonemeEntryListView.SetEntryKind` が `managedReferenceValue` を差し替え後 `ApplyModifiedPropertiesAndRefresh` → `_listView.Rebuild()` を呼ぶが、Undo 経路は `Undo.undoRedoPerformed` を購読していないため、`bindItem` が直近 SerializedProperty 値で再描画されない。
- **方針**: `PhonemeEntryListView` コンストラクタで `Undo.undoRedoPerformed += OnUndoRedoPerformed` を購読、Detach 時（`UnregisterCallback<DetachFromPanelEvent>`）に解除。`OnUndoRedoPerformed` で `serializedObject.Update()` + `_listView.Rebuild()`。Undo → モード切替 → 表示値が SerializedProperty と一致することを EditMode テストで保証。
- **影響範囲**: `Packages/com.hidano.facialcontrol.lipsync/Editor/Inspector/PhonemeEntryListView.cs`, 対応 EditMode テスト。

### 共通要件
- 全タスク TDD 厳守（Red-Green-Refactor）
- EditMode テストは新規追加 / 既存更新の両方を想定
- Domain / Application / Adapters / Presentation のレイヤー分離維持
- 言語: 日本語（`requirements.md` / `design.md` / `tasks.md` は日本語で記述）
- preview 段階のため破壊的変更（`AnimationEvent` 撤去、UI 並び順変更）は許容
- `backlog.md` の対応エントリ（S-10〜S-16）は完了時に削除すること

## Requirements
<!-- Will be generated in /kiro-spec-requirements phase -->
