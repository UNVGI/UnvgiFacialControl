# Backlog（後回しタスク一覧）

> **作成日**: 2026-05-04
> **目的**: 「別 PR ネタ」「preview.2 以降」「別 spec で対処」と先送りされた項目を 1 箇所に集約する。
> **対象外**: ロードマップ全体（[`README.md`](../README.md) / [`docs/technical-spec.md`](technical-spec.md) を参照）、現在進行中の spec タスク（`.kiro/specs/*/tasks.md` を参照）。

---

## 運用ルール

1. **新規エントリの書き方**: 1 項目につき以下を 1 ブロックにまとめる
   - 見出し（`### {識別子}: {一行サマリ}`）
   - 出典（どのファイル・どのセッションで後回しが決まったか）
   - 背景（なぜ後回しか）
   - 着手判断のトリガ（いつ拾い直すか）
2. **HANDOVER.md からの昇格**: セッション引き継ぎノート（[`../HANDOVER.md`](../HANDOVER.md)）の「次にやること」優先度低項目は、当該セッションで処理されなかった場合に本ファイルへ昇格させる。HANDOVER.md は次セッションで上書きされる前提。
3. **完了時**: 別 spec 化された / 別 PR でマージされた / 不要と判断された場合は該当ブロックを **削除** し、commit message に削除理由を残す。完了済み項目は本ファイルに残さない。
4. **粒度**: 1 PR 〜 1 spec 相当のサイズで記載。spec 内のタスク粒度（leaf レベル）まで細分化しない。
5. **言及のみで本ファイルに書かない場所**: 機能単位の「preview.2 以降に延期」（`docs/technical-spec.md` 1.5 節）と Addressables 計画（`README.md` `### preview.2 以降の予定`）は既に明示されているため、本ファイルでは要約 + リンクで参照する。

---

## 短期（別 PR / preview.1 内に拾う候補）

### S-9: LipSync の AnimationClip 形式が動かない件の根本対応
- **出典**: ユーザー報告（2026-05-10）「LipsyncをAnimationClipで設定できない」/ HANDOVER.md タスク 8 の積み残し
- **内容**: `ULipSyncAdapterBinding.TryFillAnimationClipSnapshot` は `entry.Clip.SampleAnimation(ctx.HostGameObject, sampleTime)` 経由で口形状クリップを採取するが、ユーザー作成 AnimationClip の rendererPath とキャラ階層が一致しない / クリップが BlendShape カーブを持たない等で snapshot が空になり結果として ContributeMask が空になる → BlendShape 直指定形式（`BlendShapePhonemeEntry`）では動くが AnimationClip 形式（`AnimationClipPhonemeEntry`）では動かない、という挙動になる。
  - 直近 PR で「sample 結果が全 0 のとき LogWarning」「Inspector に AnimationClip 未割り当て HelpBox」を追加して原因切り分けはできるようにしたが、ユーザー視点の体験としては依然「動かない」。
  - 候補根本対応: (a) 「FacialProfile 内 Expression（既登録の表情）を音素として直接指定する `ExpressionPhonemeEntry` を新設」して AnimationClip 採取を AdapterBinding 内では行わず profile 既存値を流用、(b) AnimationClip の path を `AnimationUtility.GetCurveBindings`（Editor only）で事前検証し runtime で path 自動補正する、(c) sample 失敗時に `BlendShapePhonemeEntry` 風の暗黙 fallback を提供。
- **トリガ**: ユーザーが本格的に AnimationClip 形式を必要とするタイミング（口の組み合わせ表現が複数 BS 必要になったとき） / preview.2 のリップシンク機能拡張時
- **影響範囲**: `Packages/com.hidano.facialcontrol.lipsync/Runtime/Adapters/ULipSyncAdapterBinding.cs`、`PhonemeEntries/`（新 `ExpressionPhonemeEntry` 追加）、`Editor/Inspector/PhonemeEntryListView.cs`（形式 dropdown に追加）

### S-7: アナログ入力 Weight 値による Expression Lerp 駆動
- **出典**: ユーザー報告（2026-05-09）「目線操作以外のアナログ操作のExpressionが意図した動作になっていない」
- **内容**: 現状、`ExpressionInputSourceAdapter` は Hold/Toggle 二値で Expression を on/off するため、controller のアナログトリガー（0.0〜1.0）を握っても expression weight は遷移時間ベースで一定速度進行してしまう。ユーザー要求は「InputAction の analog value がそのまま無入力↔押し切りの Lerp 値になる」挙動。実装には (1) `ExpressionTriggerInputSource` への continuous-weight API 追加、(2) `InputSystemAdapterBinding` に gaze 用とは別の "analog expression bindings" リスト追加、(3) `InputSystemAdapterBindingDrawer` の対応 UI、(4) `OnLateTick` での毎フレーム value→weight 反映、が必要で 1 PR の規模を超える。
- **トリガ**: コントローラー操作のテストでアナログ表情を扱うシナリオが必須になったとき / preview.1 リリース前の機能完成度レビュー
- **影響範囲**: `Runtime/Adapters/InputSources/ExpressionInputSourceAdapter.cs`, `ExpressionTriggerInputSource.cs`, `InputSystemAdapterBinding.cs`, `Editor/AdapterBindings/InputSystemAdapterBindingDrawer.cs`, 対応 EditMode/PlayMode テスト

---

## 中期（preview.2 以降 / 別 spec 候補）

### M-1: 既知の機能延期（technical-spec.md 1.5 節と同期）
- **出典**: [`docs/technical-spec.md`](technical-spec.md) 「preview.2 以降に延期」表
- **対象機能**:
  - 自動まばたき（`IBlinkTrigger` 実装）
  - 視線制御（Vector3 ターゲット + BlendShape / ボーン両対応）
  - VRM 対応
  - Timeline 統合
  - テクスチャ切替 / UV アニメーションの JSON 対応
  - ホットリロード自動検知
- **方針**: 各機能について preview.2 着手時に独立 spec を切る。本 backlog では存在のみ記録し、詳細は technical-spec.md 1.5 節と各 spec の Non-Goals を権威にする。

### M-2: Addressables / プロファイル JSON ロード経路抽象化
- **出典**: [`README.md`](../README.md) `### preview.2 以降の予定`
- **内容**: `IProfileJsonLoader` を導入して StreamingAssets / TextAsset / Addressables の 3 経路を差し替え可能にする。デフォルトは StreamingAssets 維持で破壊的変更なし。
- **トリガ**: preview.2 マイルストーン着手 / キャラクター Prefab + 表情プロファイルを 1 セットで Addressables 配信したいユーザー要望

### M-3: ARKit ネイティブ受信（`ArKitNativeAnalogSource`）
- **出典**: [`.kiro/specs/analog-input-binding/research.md`](../.kiro/specs/analog-input-binding/research.md) Topic 8 の Follow-up
- **内容**: preview.1 では ARKit データは外部キャプチャアプリ（iFacialMocap / Live Link Face / Hana 等）から OSC 経由で取得する想定。ネイティブ ARKit SDK 受信は未対応。`IAnalogInputSource` を実装するだけで取り込める設計（Req 5.6）にしてあるので、新 source として追加するのみ。
- **トリガ**: iOS ターゲットが正式追加された / OSC 経由のレイテンシが問題化した

### M-4: BonePose 多重 provider のブレンド合成
- **出典**: [`.kiro/specs/analog-input-binding/tasks.md`](../.kiro/specs/analog-input-binding/tasks.md) スコープ外メモ
- **内容**: 現状 `IBonePoseProvider.SetActiveBonePose(in BonePose)` は per-frame 1 回呼び出しで「単一 active BonePose」を組み立てる側にのみ責任を持つ。複数 provider のブレンド合成（例: 視線追従 + 頭部表情 + 物理ジッタ）は preview.2 以降の別 spec で再設計する。
- **トリガ**: 視線 + 頭部 + その他の複合 bone 制御要件が顕在化したとき

### M-5: 視線追従の Vector3 ターゲット指定 / カメラ目線
- **出典**: [`.kiro/specs/analog-input-binding/tasks.md`](../.kiro/specs/analog-input-binding/tasks.md) / [`.kiro/specs/bone-control/design.md`](../.kiro/specs/bone-control/design.md) Non-Goals
- **内容**: preview.1 の analog-input-binding は `BonePose` Euler 直接指定に閉じる。Vector3 ターゲット → Euler 解決と「カメラ目線」自動制御は preview.2 後半の別 spec。
- **トリガ**: VTuber 実機ユースケースで「カメラ目線トグル」需要が定常化

### M-6: BonePoseSO 独立化（複数プロファイル間で共有）
- **出典**: [`.kiro/specs/bone-control/research.md`](../.kiro/specs/bone-control/research.md) §「BonePose は FacialProfileSO 内包」/ [`.kiro/specs/bone-control/gap-analysis.md`](../.kiro/specs/bone-control/gap-analysis.md) §9
- **内容**: preview.1 では `BonePose` は `FacialProfileSO` に内包する形で確定。複数 FacialProfile 間で同じ BonePose を共有したいユースケースが顕在化したら独立 `BonePoseSO` に切り出す。
- **トリガ**: 同じ視線設定を複数キャラクター間で使い回したいという要望が発生

### M-8: Burst / IAnimationJob への差替
- **出典**: [`.kiro/specs/bone-control/research.md`](../.kiro/specs/bone-control/research.md) §「PlayableGraph + LateUpdate」/ [`.kiro/specs/analog-input-binding/design.md`](../.kiro/specs/analog-input-binding/design.md)
- **内容**: preview.1 は通常 C# の範囲で性能目標達成。`IBonePoseSource` / `IBonePoseProvider` インターフェースは安定しているので Burst 化 / `IAnimationJob` への差替は実装差替のみで可能な状態に保つ。
- **トリガ**: パフォーマンス計測でホットスポット化が確認されたとき

### M-9: Editor 上の curve エディタ統合（フル GUI マッピング編集）
- **出典**: [`.kiro/specs/analog-input-binding/research.md`](../.kiro/specs/analog-input-binding/research.md) Topic 4 Follow-up / [`.kiro/specs/analog-input-binding/tasks.md`](../.kiro/specs/analog-input-binding/tasks.md) スコープ外メモ
- **内容**: preview.1 のアナログマッピング編集は「読取専用 Inspector + JSON Import/Export + Humanoid 自動割当ボタン」に留める。フル GUI（curve エディタ統合）は preview.2 以降。
- **トリガ**: ユーザーから「JSON 直編集が辛い」というフィードバックが集まったとき

### M-11: スキーマ migration パス（preview の破壊変更を 1.0 で吸収）
- **出典**: [`.kiro/specs/analog-input-binding/design.md`](../.kiro/specs/analog-input-binding/design.md) `version` field の扱い
- **内容**: preview 中は JSON スキーマの破壊的変更を許容している（CLAUDE.md / 要件方針）。1.0 リリースに向けて `schemaVersion` ベースの migration パスを設計する。analog binding profile の `version` field は preview 中は文字列保持のみで分岐なしの状態。
- **トリガ**: 1.0 リリース直前のスキーマ凍結フェーズ

### M-12: AnalogBindingBinder の責務分割
- **出典**: [`.kiro/specs/analog-input-binding/research.md`](../.kiro/specs/analog-input-binding/research.md) Topic 17 Follow-up
- **内容**: preview.1 の `FacialAnalogInputBinder` は BlendShape / BonePose 両方の binding を 1 MonoBehaviour で扱う。責務肥大が問題化したら `AnalogBlendShapeBinder` / `AnalogBonePoseBinder` の 2 MonoBehaviour に分割する。
- **トリガ**: 該当ファイルの行数が増えてレビュー困難化したとき

### M-13: 複数 Vector2 入力源での gaze 同時駆動（multi-source gaze blending）
- **出典**: `.kiro/specs/gaze-config-promotion/design.md` の重複 `expressionId` 取り扱い決定（2026-05-06 セッション）
- **内容**: `gaze-config-promotion` spec の preview.1 では、`InputSystemAdapterBinding._gazeInputBindings[]` の中で同 `expressionId` が複数あった場合は **warn ログ + 最初の 1 件のみ採用** とする。これは「左スティックと右スティックの両方を同じ gaze に bind」のような multi-source 同時駆動を **preview.1 では非対応** にする選択。理由は `GazeBoneBinding` の現状構造が単一 `IAnalogVector2InputSource` 前提で組まれており、複数源の合成戦略（max-magnitude blend / sum / dead-zone-aware merge / 後勝ち等）を確定するための設計判断が preview.1 のスコープを超えるため。preview.2 以降の別 spec で「multi-source gaze blending」として正式に設計する。要検討項目: 合成戦略 (a) max-magnitude / (b) component-wise sum / (c) priority + fallback / (d) ユーザー設定可能な切替。`GazeBoneBinding` 内部構造を「複数 source 受け」に拡張する変更も同時に必要。
- **トリガ**: 1.0 リリースに向けて実機 VTuber 環境で「複数コントローラ / OSC + InputSystem 同居」要件が顕在化したとき / `gaze-config-promotion` 完了後の sample 拡張で multi-source デモを切るとき
- **影響範囲**: `Runtime/Adapters/Bone/GazeBoneBinding.cs`, `Runtime/Adapters/Bone/GazeBonePoseProvider.cs`, `Packages/com.hidano.facialcontrol.inputsystem/Runtime/Adapters/AdapterBindings/InputSystemAdapterBinding.cs`, 新規合成戦略型, テスト
- **関連**: M-4（BonePose 多重 provider のブレンド合成 — 異なる bone への複数 provider の話で別概念だが、合成戦略の設計はここと共通化できる可能性あり）

### M-15: ARKit 検出機能の責務分離 / 命名規約データセット抽象化
- **出典**: 2026-05-14 セッション「`com.hidano.facialcontrol` 内に ARKit 依存があるか」のアーキ確認
- **内容**: `Runtime/Domain/Services/ARKitDetector.cs` / `Runtime/Application/UseCases/ARKitUseCase.cs` / `Editor/Windows/ARKitDetectorWindow.cs` / `Editor/Tools/ARKitEditorService.cs` は **Apple ARKit SDK / ARFoundation / `UnityEngine.XR` には一切依存していない**（grep ヒット 0、manifest.json に XR 系パッケージなし）。実体は ARKit 52 + PerfectSync 13 個の **BlendShape 名文字列定数** と、name → layerGroup (eye/mouth/brow/cheek/nose) の Dictionary、および完全一致検出ロジックのみ。バイナリ依存ゼロなので preview.1 リリースのブロッカーではない。
  - ただし以下 2 点が中期的な整理候補:
    - (1) **クラス名・API 名に "ARKit" が固定**されている。将来 VRoid / iFacialMocap 独自命名 / VRM Standard Expressions などの別命名規約データセットが追加された場合、`PerfectSyncDetector` を別途作るか `ARKitDetector` に詰め込み続けるかが曖昧。データセットとロジックを分離して `BlendShapeNamingDetector` + `IBlendShapeNamingScheme`（ARKit / PerfectSync / VRM 等の datasource を差し替え可能）にする方が拡張に強い。
    - (2) `ARKitUseCase.GenerateOscMapping(string[])` が **OSC 互換マッピング (`/avatar/parameters/{name}`)** を生成しているが、OSC binding 本体は `com.hidano.facialcontrol.osc` に分離済み。マッピング生成だけ core 側にあるのは命名上の責務漏れ気味。ただし `OscMapping` / `OscConfiguration` 型自体は Domain にいて profile JSON の `osc` セクションを担うため、Domain 配置の理屈は通る。整理するなら `OscMappingAutoGenerator` のような中立名 + 生成器の所属再考。
  - 候補対応: (a) `ARKitDetector` → `BlendShapeNamingDetector` リネーム + 命名規約データセットを `IBlendShapeNamingScheme` 抽象化、(b) `ARKitUseCase.GenerateOscMapping` を OSC パッケージへ移管、(c) Editor ツール (`ARKitDetectorWindow`) を `BlendShapeNamingDetectorWindow` 化、(d) 将来別 UPM (`com.hidano.facialcontrol.arkit-detection`) への切り出しは現状の規模ではオーバーキルなので不採用。
- **トリガ**: ARKit 以外の命名規約サポート要望が出たとき（VRoid / VRM Standard Expressions / iFacialMocap 独自命名等） / 1.0 リリース前の Public API 凍結タイミング（preview 中なら破壊的リネーム可）
- **影響範囲**: `Runtime/Domain/Services/ARKitDetector.cs`, `Runtime/Application/UseCases/ARKitUseCase.cs`, `Editor/Windows/ARKitDetectorWindow.cs`, `Editor/Tools/ARKitEditorService.cs`, 対応 EditMode テスト（`Tests/EditMode/Domain/ARKitDetectorTests.cs`, `Tests/EditMode/Application/ARKitUseCaseTests.cs`）、Public API 名変更による下流影響
- **関連**: `osc-output-binding` spec（OSC 側の mode 別 mapping / Drawer / Samples は同 spec で回収済み。`GenerateOscMapping` の core 残置を将来リネームする場合のみ再検討）

### M-14: Domain への「動的 Expression driver」概念導入
- **出典**: `.kiro/specs/gaze-config-promotion/` セッションでの user 指摘（2026-05-06）
- **内容**: 現状 `Domain/Models/ExpressionSnapshot` は **AnimationClip サンプリング由来の静的 snapshot**（`BlendShapeSnapshot[]` + `BoneSnapshot[]` を不変保持）として設計されており、bone も BlendShape も first-class concept として表現可能。しかし「Vector2 入力で連続的に bone Euler を算出する gaze」のような **動的駆動 driver** は Domain の語彙に存在せず、`IBonePoseSource` / `GazeBonePoseProvider` という形で Adapters 層に閉じている。「目線操作も Expression の 1 種」という抽象化を Domain で表現するには、Expression を「静的 snapshot を持つもの」と「動的 driver を持つもの」の上位抽象に揃える Domain refactor が必要。具体的には (a) Expression interface を導入し `StaticExpressionSnapshot` と `DynamicExpressionDriver` を sibling として実装する / (b) `IExpressionEvaluator` を Domain に置き、入力に応じた evaluation strategy を expressing する / (c) 現状維持で動的 driver は Adapters のみ、のいずれか。preview.1 では (c) で固定。Domain refactor は preview.2 以降の大型 spec として独立化する。なお現状でも **Domain は BlendShape 前提ではなく `BoneSnapshot` も first-class** であるため、表情ボーンを使うキャラ（口元・眉毛・瞼を bone で動かす）の対応は本 spec の範囲外で既に成立している。
- **トリガ**: gaze 以外の動的 driver（手書き lip sync curve、procedural な微表情ジッタ、analog 駆動の口開き等）が Adapters 層に増えて統一抽象が欲しくなったとき / 1.0 リリースに向けて Domain の安定 API を凍結するタイミング
- **影響範囲**: `Runtime/Domain/Models/Expression.cs`, `Runtime/Domain/Models/ExpressionSnapshot.cs`, 新規 `IExpressionDriver` 等の interface, `Runtime/Adapters/Bone/GazeBonePoseProvider.cs` の Domain への昇格検討, `IFacialCharacterProfile` 経路, runtime 評価経路の再設計
- **関連**: M-8（Burst / IAnimationJob への差替 — Domain 純化の延長線）、M-4（BonePose 多重 provider のブレンド合成）

### M-16: uOSC vendor copy + zero-alloc fork（osc-output-binding spec Phase 10 / 11）
- **出典**: [`.kiro/specs/osc-output-binding/tasks.md`](../.kiro/specs/osc-output-binding/tasks.md) "preview.3 milestone（Deferred）" / [`.kiro/specs/osc-output-binding/uosc-modification-plan.md`](../.kiro/specs/osc-output-binding/uosc-modification-plan.md)
- **内容**: `osc-output-binding` spec の Phase 10 / 11 として記載済み。`com.hidano.uosc` を `Library/PackageCache` から `Packages/com.hidano.uosc/` へ vendor copy 化し、`Runtime/Core/Modern/` 配下に Span / ArrayPool / BinaryPrimitives ベースの新 API（`OscMessage` SoA struct, `OscMessagePool`, `OscWriter`, `OscBundleBuilder`, `OscClient`/`OscServer` (ring buffer + Socket.ReceiveFrom worker), `OscPacketParser` (ref struct), `OscMessageView`, `OscAddressHash` (UTF-8 → uint64)）を実装、送受信ホットパスを zero-alloc 化する。完了後に旧 `uOscClient` / `Bundle` / `Message` facade を撤去（Phase 11）。`osc-output-binding` 内の Req 10.1 「`OnLateTick` で毎フレーム 0 byte GC」を完全達成する。preview.2 では本 spec ロジック側のみ GC ゼロを確認し、uOSC 側 string / object[] alloc は baseline 計測に留めている（`OscSenderGCAllocationTests` / `OscReceiverGCAllocationTests` が `*` deferred マーク付き）。
- **トリガ**: 1 sender × 10+ receiver × 1000 BlendShape の高負荷シナリオで GC スパイクが実測課題化したとき / 1.0 凍結前の最終性能検証フェーズ
- **影響範囲**: `Packages/com.hidano.uosc/`（vendor 化）、`Packages/com.hidano.facialcontrol.osc/Runtime/Adapters/OSC/`（新 API への接続切り替え）、対応 PlayMode 性能テスト
- **関連**: `osc-output-binding` spec Req 8.12 / 10.1 / 10.6

### M-17: OverlaySlotBinding 既存テスト失敗（suppress + snapshot 同時存在の検証で 7 件 fail）
- **出典**: `osc-output-binding` spec 完了直後の `/kiro:validate-impl` セッション（2026-05-15）で実テスト実行時に検出
- **内容**: `SystemTextJsonParser.cs` が `OverlaySlotBinding slot='X' has invalid state: suppress=true and snapshot is not null` という validation を持っているが、サンプル JSON / テスト fixture 側に `suppress=true` AND non-null `snapshot` の組合せが残存しているため、以下 7 件の EditMode テストが恒常 fail する。`osc-output-binding` の commit 範囲外で発生しており、別 spec（overlay-clip-redesign 系）の積み残し tech debt。
  - `Hidano.FacialControl.Tests.EditMode.Adapters.Json.SystemTextJsonParserOverlaysTests.Parse_SampleProfileJson_RoundTripsEquivalentOverlaySchema`
  - `Hidano.FacialControl.Tests.EditMode.Adapters.Json.SystemTextJsonParserOverlaysTests.RoundTrip_ThreeOverlayStates_PreservesEquivalentProfile`
  - `Hidano.FacialControl.Tests.EditMode.Adapters.Json.SystemTextJsonParserRoundTripTests.ParseProfile_PopulatesLayerInputSourcesAlignedWithLayers`
  - `Hidano.FacialControl.Tests.EditMode.Adapters.Json.SystemTextJsonParserRoundTripTests.SerializeParseSerialize_SampleJson_ProducesIdenticalString`
  - `Hidano.FacialControl.Tests.EditMode.Adapters.ScriptableObjectTests.FacialCharacterProfileConverterTests.ToProfileSnapshotDto_DomainOverlayStates_EmitsNewOverlayDtoSchema`
  - `Hidano.FacialControl.Tests.EditMode.Adapters.ScriptableObjectTests.Serializable.OverlaySlotBindingSerializableTests.UnitySerialization_LegacyExpressionIdKey_IsIgnoredAndNewFieldsKeepDefaults`
  - `Hidano.FacialControl.Tests.EditMode.Editor.AutoExport.FacialCharacterProfileExporter_OverlayClipTests.ExportProfileJson_OverlaySlotsAndBindings_WritesNewSchema`
- 加えて PlayMode 側に同系の overlay 由来既存失敗が 2 件残存:
  - `Hidano.FacialControl.Tests.PlayMode.Integration.LayerLifecycleZeroFadeTests.ActivateThenDeactivate_DrivenByMonoBehaviourTick_FadesToZero`
  - `Hidano.FacialControl.Tests.PlayMode.Performance.OverlayInputSourcePerformanceTests.TryWriteValues_1000Frames_AllocatesZeroBytes`
- **トリガ**: overlay-clip 機能を本格利用するとき / 1.0 凍結前のテスト緑化フェーズ
- **影響範囲**: `Packages/com.hidano.facialcontrol/Runtime/Adapters/Json/SystemTextJsonParser.cs` の validation 緩和（`suppress=true` 時の snapshot 同時保持を許容し読み戻し時に snapshot を無視する等）、または サンプル JSON / SO fixture の修正、対応テスト fixture 更新
- **関連**: overlay-clip-redesign / layer-input-source-blending spec の積み残し

---

## 横断フォローアップ（実装着手時に再確認するメモ）

各 spec の `research.md` に `**Follow-up**:` として記載されている、preview.1 内の実装で「忘れずに対処」すべき項目。spec ごとに完結している場合は本ファイルに転記しない。**spec 着手前に該当 spec の `research.md` を必ず読み直す**。

| spec | follow-up が記載されている節 |
|------|-----------------------------|
| `layer-input-source-blending` | research.md Topic 1 / 3 / 4 / 6 / 7（asmdef CI 確認、深度超過テスト、診断 UI、要件文書注記、`using BulkScope` パターン）|
| `inspector-and-data-model-redesign` | research.md Topic で AnimationEvent 経由メタデータ書き戻し / schema v1.0 拒否 / Custom keyframe / OSC adaptor 経路（OSC 側 mapping は `osc-output-binding` spec で回収済み）|
| `analog-input-binding` | research.md Topic 4 / 9 / 10 / 14（curve エディタ → M-9、bone-control internal API、OscRouter 抽出、ARKit native → M-3、Binder 分割 → M-12 等）|
| `bone-control` | research.md §「LateUpdate 競合」「Quaternion.Euler 一致誤差」「BonePoseSO 独立化 → M-6」「Burst → M-8」「BoneWriter 必須化」|

---

## 関連ドキュメント

- 全体ロードマップ: [`README.md`](../README.md)
- 機能延期一覧: [`docs/technical-spec.md`](technical-spec.md) 1.5 節
- 直近セッション引き継ぎ: [`HANDOVER.md`](../HANDOVER.md)
- spec 個別の Out-of-Scope / Non-Goals: 各 `.kiro/specs/*/design.md` § Non-Goals
- 完了済み spec の撤去履歴: 各パッケージの `CHANGELOG.md`

---

## 履歴

- 2026-05-04: 新設。HANDOVER.md 優先度低 #5（S-1）/ Phase 6.5 残課題（S-4）/ 各 spec の preview.2 以降宣言（M-3〜M-12）を集約。
- 2026-05-06: spec `adapter-binding-architecture` クローズに伴い follow-up 2 件追加（S-5: Sample SO guid 再生成、S-6: VContainer dependency 宣言）。
- 2026-05-06: spec `gaze-config-promotion` 設計セッションで preview.1 スコープ外と確定した 2 件を追加（M-13: 複数 Vector2 入力源での gaze 同時駆動、M-14: Domain への動的 Expression driver 概念導入）。
- 2026-05-10: ユーザー指示で「LipSync の AnimationClip 形式が動かない件の根本対応」を S-9 として追加（凌ぎの診断ログ / HelpBox は既に main に入っている）。
- 2026-05-14: アーキ確認セッションで M-15（ARKit 検出機能の責務分離 / 命名規約データセット抽象化）を追加。SDK 依存はゼロだが、クラス名 ARKit 固定 / `GenerateOscMapping` の core 残置という整理候補が確認されたもの。
- 2026-05-15: `osc-output-binding` spec へ OSC 送信 / 受信 mode mapping / Drawer / Samples を引き上げたため、M-1 の「OSC マッピング Editor UI」と M-10（OSC アダプタの mapping 移植）を backlog から削除。
- 2026-05-10: 本セッションで以下を消化して削除: S-1（ボーン参照を相対 path / 単純名併用に拡張）、S-2（旧 schema field 残置なしを確認）、S-3（PlayMode 統合テスト追加）、S-4（Fork 先で確認済みのため不要と判断）、S-6（README に VContainer 依存と OpenUPM 設定例を追記）、S-8（slug 編集 UI を candidate ドロップダウン + 手動 override テキストの 2 段に変更）、M-7（同名ボーン衝突時の警告を追加。S-1 と同 PR で実装）。
