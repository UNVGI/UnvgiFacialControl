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

### S-1: ボーン参照の相対 Path 化
- **出典**: [`HANDOVER.md`](../HANDOVER.md) の「次にやること」優先度低 #5（task #3 として注記）
- **内容**: `GazeExpressionConfig.leftEyeBonePath` / `rightEyeBonePath` を「ボーン名（`string`）」ではなく「参照モデル root からの相対 path」として保存する。`BoneTransformResolver` は現状 `node.name == name` の完全一致動作のため、同名ボーンが複数ある HumanoidRig 等で「最初の発見」が採用される落とし穴がある。`LeftEye` と `LeftEyeEnd` のような sibling 取り違えも理屈上発生し得る。
- **トリガ**: 同名ボーン衝突の不具合報告が来た / Humanoid 以外のリグでデモ予定が立った / Inspector で「使用モデル」設定 UX を見直すタイミング
- **影響範囲**: `Runtime/Adapters/Bone/BoneTransformResolver.cs`、`Runtime/Adapters/ScriptableObject/GazeExpressionConfig.cs`、JSON schema（`leftEyeBonePath` の意味変更 → schema bump 検討）

### S-2: デモ asset の旧スキーマ field 残置確認
- **出典**: [`HANDOVER.md`](../HANDOVER.md) の「次にやること」優先度中 #3
- **内容**: `MultiSourceBlendDemoCharacter.asset` に `_gazeConfigs[].leftEyeXBlendShape` 等の旧 field が YAML として残置されている可能性がある。Inspector 経由で 1 回 save すれば消えるはずだが、CI / 手動確認で念のため検証する。
- **トリガ**: Sample 経路の手動回帰確認時 / Phase 6 完了レビューの残課題消化時
- **影響範囲**: `FacialControl/Assets/Samples/FacialControl InputSystem/0.1.0-preview.2/Multi Source Blend Demo/`、`Packages/com.hidano.facialcontrol.inputsystem/Samples~/`

### S-3: Activate→Deactivate→ ゼロ復帰の PlayMode 統合テスト
- **出典**: [`HANDOVER.md`](../HANDOVER.md) の「次にやること」優先度中 #4
- **内容**: 直近の Bug A 修正（`LayerUseCase.UpdateWeights` の `HasBeenActive` ガード経由ゼロ補間）は EditMode 単体テスト 2 本でカバーしたが、実フレーム同期での挙動は未検証。`LayerExpressionSource.UpdateExpressions(empty)` 経路を MonoBehaviour ライフサイクル + Time.deltaTime 経由で踏む PlayMode テストを追加する。
- **トリガ**: 同経路の追加リグレッションが発生したとき / preview.1 リリース前の品質ゲート強化時
- **影響範囲**: `Tests/PlayMode/Integration/`（新規ファイル想定）

### S-4: Sample Scene の手動再生確認 + UPM Import Sample 検証
- **出典**: [`.kiro/specs/inspector-and-data-model-redesign/RETRY-SUMMARY.md`](../.kiro/specs/inspector-and-data-model-redesign/RETRY-SUMMARY.md) Task 6.5 残課題
- **内容**: batch retry 範囲外の GUI 操作。Multi Source Blend Demo / Analog Binding Demo を Unity Editor で開いて Play 実行 + UPM Package Manager `Import Sample` 経由で空プロジェクトに import して動作確認する。
- **トリガ**: preview.1 リリース前の最終レビュー
- **影響範囲**: 動作確認のみ（コード変更なし）

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
  - OSC マッピング Editor UI
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

### M-7: Bone 名衝突警告（非 Humanoid 対応）
- **出典**: [`.kiro/specs/bone-control/tasks.md`](../.kiro/specs/bone-control/tasks.md) スコープ外メモ MINOR-2
- **内容**: preview.1 は Humanoid-first（VTuber/VRM 中心）。非 Humanoid の手入力リグで同名ボーンが複数ヒットした場合、preview.1 は「最初の発見を採用、警告なし」で確定。preview.2 以降で警告 UI を検討。
- **トリガ**: 非 Humanoid のキャラクターでバグ報告が来た / S-1（相対 Path 化）と一緒に対処すると相性が良い

### M-8: Burst / IAnimationJob への差替
- **出典**: [`.kiro/specs/bone-control/research.md`](../.kiro/specs/bone-control/research.md) §「PlayableGraph + LateUpdate」/ [`.kiro/specs/analog-input-binding/design.md`](../.kiro/specs/analog-input-binding/design.md)
- **内容**: preview.1 は通常 C# の範囲で性能目標達成。`IBonePoseSource` / `IBonePoseProvider` インターフェースは安定しているので Burst 化 / `IAnimationJob` への差替は実装差替のみで可能な状態に保つ。
- **トリガ**: パフォーマンス計測でホットスポット化が確認されたとき

### M-9: Editor 上の curve エディタ統合（フル GUI マッピング編集）
- **出典**: [`.kiro/specs/analog-input-binding/research.md`](../.kiro/specs/analog-input-binding/research.md) Topic 4 Follow-up / [`.kiro/specs/analog-input-binding/tasks.md`](../.kiro/specs/analog-input-binding/tasks.md) スコープ外メモ
- **内容**: preview.1 のアナログマッピング編集は「読取専用 Inspector + JSON Import/Export + Humanoid 自動割当ボタン」に留める。フル GUI（curve エディタ統合）は preview.2 以降。
- **トリガ**: ユーザーから「JSON 直編集が辛い」というフィードバックが集まったとき

### M-10: OSC アダプタの mapping 移植（schema v2.0 / mapping 撤去への追従）
- **出典**: [`.kiro/specs/inspector-and-data-model-redesign/research.md`](../.kiro/specs/inspector-and-data-model-redesign/research.md) Topic / [`.kiro/specs/inspector-and-data-model-redesign/design.md`](../.kiro/specs/inspector-and-data-model-redesign/design.md) §「OSC 別 spec」
- **内容**: 直近の Domain 改修で `AnalogBindingEntry.Mapping` field と `AnalogMappingFunction` / `AnalogMappingEvaluator` を物理削除した。OSC アダプタ（`com.hidano.facialcontrol.osc`）は本仕様の影響を受けないが、OSC 側で別途 mapping 相当の処理を再実装する将来作業が発生する。
- **トリガ**: OSC 経由の VRChat 連携でアナログ表現が必要になったとき / `InputSourceCategory` enum を OSC 用に再導入する判断が出たとき

### M-11: スキーマ migration パス（preview の破壊変更を 1.0 で吸収）
- **出典**: [`.kiro/specs/analog-input-binding/design.md`](../.kiro/specs/analog-input-binding/design.md) `version` field の扱い
- **内容**: preview 中は JSON スキーマの破壊的変更を許容している（CLAUDE.md / 要件方針）。1.0 リリースに向けて `schemaVersion` ベースの migration パスを設計する。analog binding profile の `version` field は preview 中は文字列保持のみで分岐なしの状態。
- **トリガ**: 1.0 リリース直前のスキーマ凍結フェーズ

### M-12: AnalogBindingBinder の責務分割
- **出典**: [`.kiro/specs/analog-input-binding/research.md`](../.kiro/specs/analog-input-binding/research.md) Topic 17 Follow-up
- **内容**: preview.1 の `FacialAnalogInputBinder` は BlendShape / BonePose 両方の binding を 1 MonoBehaviour で扱う。責務肥大が問題化したら `AnalogBlendShapeBinder` / `AnalogBonePoseBinder` の 2 MonoBehaviour に分割する。
- **トリガ**: 該当ファイルの行数が増えてレビュー困難化したとき

---

## 横断フォローアップ（実装着手時に再確認するメモ）

各 spec の `research.md` に `**Follow-up**:` として記載されている、preview.1 内の実装で「忘れずに対処」すべき項目。spec ごとに完結している場合は本ファイルに転記しない。**spec 着手前に該当 spec の `research.md` を必ず読み直す**。

| spec | follow-up が記載されている節 |
|------|-----------------------------|
| `layer-input-source-blending` | research.md Topic 1 / 3 / 4 / 6 / 7（asmdef CI 確認、深度超過テスト、診断 UI、要件文書注記、`using BulkScope` パターン）|
| `inspector-and-data-model-redesign` | research.md Topic で AnimationEvent 経由メタデータ書き戻し / schema v1.0 拒否 / Custom keyframe / OSC adaptor 経路（M-10 と重複）|
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
