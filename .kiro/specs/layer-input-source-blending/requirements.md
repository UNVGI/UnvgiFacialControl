# Requirements Document

## Project Description (Input)
レイヤーごとに複数の入力源（ゲームコントローラ / OSC フェイシャルキャプチャ / 外部リップシンク等）を有効化し、入力源ごとにブレンドウェイトを設定可能にする機能。既存の LayerBlender を拡張し、同一レイヤー内で複数入力源を重み付き合成できるようにする。

【背景】
現状の FacialControl では「レイヤー」はレイヤー間の優先度ブレンドと、同レイヤー内の Expression 同士の排他制御（LastWins / Blend）のみに使われている。BlendShape を複数の入力源（ゲームパッド操作 / フェイシャルキャプチャ OSC / uLipSync などの外部リップシンクプロバイダ）から駆動したい場合、現設計では後勝ちで上書きされてしまい、明示的な入力源切替・重み付きブレンドはできない。

【想定ユースケース】
1. 状況切替: ある時は eye / emotion レイヤーをゲームコントローラで操作し、別の場面ではフェイシャルキャプチャに切り替える
2. 重み付きブレンド: 眉の動き（emotion レイヤーに含まれる BlendShape）をゲームコントローラ 50% + フェイシャルキャプチャ 50% で合成
3. 特定入力源固定: lipsync レイヤーは常に uLipSync のみ有効、OSC の口パラメータは無視

【スコープ】
- レイヤー単位の「入力源ウェイトマトリクス」を JSON プロファイルで表現
- InputBindingProfile / OSC 受信 / ILipSyncProvider を「入力源」として抽象化
- 入力源ごとのウェイトはランタイム変更可能（状況切替）
- BlendShape 単位の入力源ルーティングは今回スコープ外（必要なら別スペック）

【非スコープ】
- 新規入力源プラグイン API の拡張（既存抽象の範囲で対応）
- Editor UI 拡張は最小限（JSON 直編集前提で可）

feature name: layer-input-source-blending

## Requirements

## Introduction
本機能は既存の `LayerBlender`（`Hidano.FacialControl.Domain.Services.LayerBlender`）を拡張し、各レイヤーに対して複数の入力源（Input Source）を同時に供給・重み付き合成できるようにする。入力源は InputSystem によるコントローラ / キーボード入力、OSC 経由のフェイシャルキャプチャ、`ILipSyncProvider` からのリップシンク入力などを抽象化した概念として扱う。既存のレイヤー優先度ブレンドと `LayerSlot` オーバーライドの挙動は維持したまま、レイヤー内部での入力源合成機構を追加する。これにより、状況に応じた入力源切替（ゲームコントローラ ⇄ フェイシャルキャプチャ）、重み付きブレンド（コントローラ 50% + キャプチャ 50%）、特定入力源固定（lipsync レイヤーは uLipSync のみ）といったユースケースを JSON プロファイルとランタイム API の両面から実現する。

## Boundary Context
- **In scope**:
  - レイヤー単位の「入力源ウェイトマトリクス」を表現するドメインモデル
  - FacialProfile JSON での入力源ウェイト宣言と読み書き
  - `LayerBlender` の拡張: レイヤーへの入力が単一値配列ではなく「入力源別値配列 + 重み」の集合となる
  - 入力源の抽象インターフェース（既存の `InputBindingProfile` / OSC / `ILipSyncProvider` を入力源としてアダプトするための契約）
  - ランタイムでの入力源ウェイト変更 API と、未接続・無効入力源のフェイルセーフ
- **Out of scope**:
  - BlendShape 単位（1 つの BlendShape だけを特定の入力源に割り当てる）のルーティング
  - 新規入力源プラグイン API の拡張（例: 新しいセンサークラス、Web カメラトラッキング）
  - Editor UI 拡張（preview 段階では JSON 直接編集で運用）
  - Timeline / AnimationClip からの入力源ウェイト自動操作
- **Adjacent expectations**:
  - 既存の `LayerBlender.Blend` / `ApplyLayerSlotOverrides` の公開 API と GC フリー特性（`Runtime/Domain/Services/LayerBlender.cs`）は本機能でも維持する
  - `docs/requirements.md` FR-002（マルチレイヤー表情制御）、FR-004（OSC）、FR-005（入力デバイス制御）、FR-007（リップシンク連携）と整合する
  - 毎フレーム処理での GC アロケーションはゼロ目標（非機能要件 4.1）

## Open Questions and Decisions (Dig)

本セクションは deep-dig 調査で洗い出した曖昧点・暗黙前提・未決事項と、その暫定決定をまとめる。各項目は EARS 要件本文に反映済み。AskUserQuestion ツールが本セッションで利用不可だったため、確認対話の代替として明示的な Decision / Rationale / Risk を併記する。ユーザーは以下の決定を承認または差し戻せる。

### D-1: 「入力源」の意味論 — Expression トリガーか BlendShape 値提供か（リスク: 高）
- **曖昧点**: 既存コードでは `InputBindingProfile` は `(ActionName → ExpressionId)` マッピングであり、BlendShape 値は Expression + TransitionCalculator 経由で生成される。したがって「コントローラを入力源として扱う」ことには 2 つの解釈がある。
  - 解釈 A: 入力源 = Expression トリガー。コントローラ押下で Expression が有効化され、従来の遷移経路で BlendShape 値が生成される。複数入力源合成は「どの Expression を採用するか」の合成になる。
  - 解釈 B: 入力源 = BlendShape 値プロバイダ。コントローラは何らかの仕組みで BlendShape 値配列（`float[BlendShapeCount]`）を直接供給し、Expression パイプラインをバイパスする。
- **決定**: **解釈 B を採用**。入力源は「レイヤーの BlendShape 値配列を直接提供するプロバイダ」とする。理由: ユースケース「コントローラ 50% + キャプチャ 50% でブレンド」「lipsync は uLipSync 固定、OSC 口パラメータ無視」は Expression 層での合成では表現できず、BlendShape 値レベルでの合成が必要なため。
- **含意**:
  - 既存 Expression → Transition → レイヤー BlendShape 配列のパイプライン出力は、「legacy / expression パイプラインという名前の暗黙の入力源」として扱う（Req 3.2）。コントローラや OSC がレイヤーに接続されていなくても、この暗黙入力源が常にウェイト 1 で存在することで後方互換を維持する。
  - コントローラ駆動の Expression トリガー（既存機能）は本スペックでは改変しない。Expression パイプラインは `legacy` 入力源として温存される。
- **残存リスク**: Expression 駆動と直接 BlendShape 駆動が 1 レイヤー上で混在した場合の意図しない加算。→ Req 2.3 のクランプで出力は有界だが、ユーザーが誤設定した場合の UX は design フェーズで検討。

### D-2: レイヤー内合成の数式（リスク: 高）
- **曖昧点**: Req 2.2–2.3 は「per-source 重みで結合」と述べるが、(a) 加重和 Σ wᵢ·vᵢ、(b) 正規化加重和 Σ wᵢ·vᵢ / Σ wᵢ、(c) lerp 連鎖、(d) 最大値、のどれを指すか不明。
- **決定**: **(a) 加重和 + 最終クランプ** を採用。すなわち各 BlendShape k について `output[k] = clamp01(Σ wᵢ · values_i[k])`。
  - 正規化は行わない（Req 2.3: 「個別ソース重みを再スケールしない」）。
  - 重み合計が 1 未満でも合計が 1 を超えても、合計値をそのまま計算しクランプのみ適用。
- **Rationale**: ユースケース「コントローラ 50% + キャプチャ 50%」で両方が同じ BlendShape を 1.0 出力した場合、(a) は 1.0（クランプ後）、(b) は 1.0、(c) は 0.75（chainの順序依存で 0.5→lerp(0.5,1,0.5)=0.75）となる。正規化ありの (b) は「全ソース弱化時に相対的に強く出る」望まぬ挙動を生む。(a) が一番直観的で「重みの意味」が保存される。
- **残存リスク**: 加重和 > 1 になる状況で重みを下げる学習コストが発生。→ 診断 API (Req 8.1) で現在合成重みを可視化することで緩和。

### D-3: レイヤー内合成と inter-layer ブレンドの順序（リスク: 高）
- **曖昧点**: 「レイヤー内で入力源を集約 → その結果を既存 LayerBlender に渡す」のか、「入力源単位で直接 inter-layer ブレンドに参加する」のか。
- **決定**: **2 段階パイプライン**。(1) 各レイヤーで入力源集約を行い per-layer `float[]` を生成、(2) 既存の `LayerBlender.Blend(ReadOnlySpan<LayerInput>, Span<float>)` にそのまま渡す。既存 API は変更しない（Req 7.2 準拠）。
- **Rationale**: 既存の inter-layer lerp 連鎖の挙動（priority/weight/lerp）は仕様として広く依存されているため保持。新機能は per-layer 集約の前段として追加される純増加機能となる。
- **API 形**: 新規サービス（仮: `LayerInputSourceAggregator`）が per-layer 集約を担い、その出力を既存 `LayerBlender.Blend` に供給する。

### D-4: 「レイヤーウェイト」と「入力源ウェイト」の関係（リスク: 中）
- **曖昧点**: 既存 `LayerInput.Weight`（レイヤー全体の inter-layer ウェイト）と新規「入力源ウェイト」の掛け算順序、および 0 の扱い。
- **決定**: 独立。レイヤーウェイトは既存通り inter-layer ブレンドで適用。入力源ウェイトは per-layer 集約内で適用。すなわち `LayerOutput[k] = clamp01(Σ wᵢ · values_i[k])` を計算し、それを既存 `LayerInput(priority, layerWeight, layerOutput)` として `LayerBlender.Blend` に渡す。
- **含意**: レイヤーウェイト 0 なら入力源の設定に関わらずそのレイヤーの寄与はゼロ。入力源全部が weight 0 なら per-layer 出力もゼロで、下位レイヤーが見える。

### D-5: 暗黙 legacy 入力源のウェイト初期値（リスク: 中）
- **曖昧点**: `inputSources` 未宣言レイヤーは「legacy 入力源 1 本」にフォールバック（Req 3.2）だが、ウェイトは？
- **決定**: ウェイト 1.0 固定。`inputSources` を宣言したレイヤーでも、`legacy` を明示的に含めない限り legacy 入力源は参加しない（＝直接駆動モードに切り替わる）。`legacy` を明示的に `inputSources` に含めれば、Expression 駆動と直接駆動が D-2 の加重和で混合される。
- **Rationale**: 既存プロファイルの後方互換（Req 7.1）を最小手間で維持。明示宣言で切替可能。

### D-6: 入力源識別子の名前空間と予約名（リスク: 中）
- **曖昧点**: `inputSources` のキーはフラットな文字列。衝突ポリシーなし。
- **決定**:
  - 識別子は ASCII `[a-zA-Z0-9_.-]{1,64}`。それ以外は parser が警告＋スキップ。
  - 予約 ID: `legacy`（暗黙 Expression パイプライン）、`osc`、`lipsync`、`input`（InputBindingProfile 由来の直接駆動 — 将来拡張用プレースホルダ）。予約 ID はアダプタ実装側が登録する。
  - アダプタ実装者は予約 ID の衝突を避け、独自拡張は `x-` プレフィックス（例: `x-mycompany-arm-sensor`）を推奨。
- **Rationale**: 将来の入力源追加（Req 5 / Web カメラトラッキング等）を見越した最小限の規約。

### D-7: ランタイム API のスレッドポリシー（リスク: 中）
- **曖昧点**: Req 4.4 は「非メインスレッドからの変更でも次フレームで可視化、ロック不要」。lock-free MPMC を要求するか、軽量な `Interlocked.Exchange` + ダブルバッファで十分か。
- **決定**: **ダブルバッファ + Volatile read/write** で実装する（OscDoubleBuffer の踏襲パターン）。呼び出し側はどのスレッドからでも Set を発行可能、値は次の `Aggregate()` 呼び出し（メインスレッドを想定）で swap されて観測される。強い lock-free MPMC 性は要求しない。
- **含意**: 同一 (layer, source) への同フレーム内複数書き込みは最後の書き込みが勝つ（定義済み動作）。Req 4.5 のバルク API は、内部で「次 swap まで待つペンディング辞書」に溜めて一括反映する方式で実装する。

### D-8: OSC アダプタの「古い値保持」ポリシー（リスク: 中）
- **曖昧点**: Req 5.4 は「当フレームにデータが無くても最後の値を有効扱い」と規定。しかしフェイシャルキャプチャが切断しても「最後のポーズで固まる」UX はユーザーが意図しない場合がある。
- **決定**:
  - デフォルト挙動は Req 5.4 通り（last valid を維持）。
  - 追加で「staleness タイムアウト」設定を OSC アダプタに持たせる（デフォルト無効 = 無制限）。設定値（秒）を超えて新規データが無い場合、アダプタは `IsValid=false` を返しソースの寄与をゼロにする。
  - 設定は JSON (`inputSources[i].options.staleness_seconds`) または ScriptableObject 経由で指定可能。
- **Rationale**: 「キャプチャ切断 → 固まった表情で配信続行」事故を、ユーザーが選択的に回避できるオプトイン機構を提供。デフォルトは既存要件通り。

### D-9: 診断 API のスレッド安全性 / GC（リスク: 低）
- **曖昧点**: Req 8.1 snapshot API が毎回アロケーションすると Req 6.1（GC ゼロ）と矛盾する恐れ。
- **決定**: snapshot API は 2 系統提供。
  - `TryWriteSnapshot(Span<LayerSourceWeightEntry> buffer, out int written)` — GC フリー、ホットパス向け。
  - `GetSnapshot()` — List を返す便利版、Editor/診断用途。毎フレーム呼ぶことは想定しない（ドキュメントで明記）。

### D-10: Performance ベンチの明確化（リスク: 低）
- **曖昧点**: Req 6.1 の「set が不変なら GC ゼロ」は、入力源追加/削除の頻度を問う前提。
- **決定**: 入力源の追加/削除は低頻度操作（シーン切替・プロファイル読込時）と定義。毎フレームの Set（ウェイト値変更）は GC ゼロ。`PlayMode/Performance/GCAllocationTests` に per-frame Set パターンのテストを追加する（実装フェーズで tasks 化）。

### D-11: Editor UI の最小スコープ（リスク: 低）
- **曖昧点**: 「Editor UI 拡張は最小限」とあるが `FacialProfileSO` Inspector での最低限の可視化は必要か。
- **決定**: preview 段階では JSON 直編集前提。Inspector には「現在の入力源ウェイトマップ（読み取り専用）」のみ表示（Req 8.1 の snapshot API を使用）。編集 UI は提供しない。編集 UI は別スペック（out of scope）。

---

## Requirements

### Requirement 1: 入力源の抽象化とレイヤー入力モデル
**Objective:** Unity エンジニアとして、コントローラ / OSC / リップシンクなど異種の入力源を統一的にレイヤーへ供給したいので、入力源を共通のインターフェースと値配列として扱えるようにしたい。

#### Acceptance Criteria
1. The Layer Input Source Blending Service shall define a domain-level abstraction (D-1 解釈 B: BlendShape 値プロバイダ) that represents an input source as a tuple of (source identifier, BlendShape value array, validity flag), where the values are the layer's BlendShape weights directly (not Expression IDs).
2. The Layer Input Source Blending Service shall accept multiple input sources per layer without assuming a fixed number of sources at compile time.
3. When an input source provides a BlendShape value array whose length does not match the layer's BlendShape count, the Layer Input Source Blending Service shall process only the overlapping range and leave the remaining indices unchanged.
4. If an input source is marked as invalid or disconnected, the Layer Input Source Blending Service shall treat its contribution as zero without raising an exception.
5. The Layer Input Source Blending Service shall not depend on Unity-specific APIs within the Domain layer so that the abstraction remains testable under EditMode without PlayMode fixtures.
6. The Layer Input Source Blending Service shall reserve the input source identifier `legacy` for the implicit Expression + TransitionCalculator pipeline so that existing profiles continue to drive BlendShape values through the same data path (D-1, D-5).
7. The Layer Input Source Blending Service shall restrict input source identifiers to the pattern `[a-zA-Z0-9_.-]{1,64}` and reserve the identifiers `legacy`, `osc`, `lipsync`, and `input` for first-party adapters (D-6); third-party adapters shall use the `x-` prefix.

### Requirement 2: 入力源ウェイトマトリクスとレイヤー内合成
**Objective:** Unity エンジニアとして、同一レイヤー内で複数入力源を重み付きで合成したいので、レイヤーごとに入力源ウェイトマトリクスを定義し一貫した合成結果を得たい。

#### Acceptance Criteria
1. The Layer Input Source Blending Service shall associate each layer with a weight map keyed by input source identifier, where each weight is a float in the range 0 to 1.
2. When computing a layer's aggregated BlendShape values, the Layer Input Source Blending Service shall compute, for each BlendShape index k, `output[k] = clamp01(Σ wᵢ · values_i[k])` where wᵢ is the source weight and values_i[k] is the i-th source's value (D-2: weighted-sum-with-final-clamp, no normalization).
3. While the sum of input source weights within a single layer exceeds 1, the Layer Input Source Blending Service shall clamp the final per-BlendShape result to the range 0 to 1 without rescaling individual source weights (D-2).
4. If no input source is registered for a layer (including no legacy source), the Layer Input Source Blending Service shall output the layer's default (zero) BlendShape values and log a warning at most once per layer per session.
5. When an input source weight is set outside the 0 to 1 range, the Layer Input Source Blending Service shall silently clamp the value into 0 to 1, consistent with the existing `LayerBlender` clamping policy.
6. The Layer Input Source Blending Service shall preserve the current inter-layer priority blending and `LayerSlot` override semantics of `LayerBlender` after per-layer source aggregation is applied (D-3: 2-stage pipeline — aggregate within layer, then feed legacy `LayerBlender.Blend`).
7. The Layer Input Source Blending Service shall apply the per-layer inter-layer weight (`LayerInput.Weight`) independently of input source weights; source weights affect only intra-layer aggregation and do not multiply with the inter-layer weight at the aggregation stage (D-4).

### Requirement 3: FacialProfile JSON スキーマ拡張
**Objective:** Unity エンジニアとして、入力源ウェイトをプロファイル JSON で宣言したいので、レイヤー定義に入力源ウェイトマトリクスを追加し、ランタイムで読み書き可能にしたい。

#### Acceptance Criteria
1. The FacialProfile JSON parser shall recognize an optional `inputSources` field on each layer definition whose value is an ordered array of objects `{ "id": string, "weight": float, "options": object? }`, where `weight` defaults to 1.0 if omitted and `options` is an adapter-specific opaque map.
2. When a layer definition omits the `inputSources` field, the FacialProfile JSON parser shall default that layer to a single implicit `legacy` input source with weight 1.0 so that existing profiles continue to work unchanged (D-5).
3. If an `inputSources` entry specifies an identifier not matching the pattern in Requirement 1.7 or an identifier for which no adapter is registered at runtime, the FacialProfile JSON parser shall log a warning and skip that entry without aborting profile loading.
4. When the same input source identifier appears more than once under a single layer, the FacialProfile JSON parser shall keep the last occurrence and log a warning about the duplicate.
5. The FacialProfile JSON parser shall serialize input source weights back to JSON with stable ordering (declaration order preserved) so that round-trip import/export does not introduce spurious diffs.
6. Where the JSON schema evolves during the preview phase, the FacialProfile JSON parser shall follow the project's preview-stage breaking-change policy (documented in `docs/requirements.md` FR-001) without requiring migration scripts.
7. The FacialProfile JSON parser shall pass through the per-source `options` map to the corresponding adapter without interpreting its contents, supporting adapter-specific configuration such as `staleness_seconds` (D-8) without requiring core schema changes.

### Requirement 4: ランタイムでの入力源ウェイト制御
**Objective:** Unity エンジニアとして、シーン内の状況に応じて入力源ウェイトを切り替えたいので、ランタイム API から入力源ウェイトを安全に変更できるようにしたい。

#### Acceptance Criteria
1. The Layer Input Source Blending Service shall expose a runtime API that sets the weight of a single input source on a specified layer in O(1) or O(sourcesPerLayer) time.
2. When a runtime weight change is requested for an existing (layer, input source) pair, the Layer Input Source Blending Service shall apply the new weight starting from the next blending evaluation without interrupting ongoing expression transitions.
3. If a runtime weight change targets a non-existent layer or input source identifier, the Layer Input Source Blending Service shall log a warning and leave all other weights unchanged.
4. While a weight change is in progress from a non-main thread, the Layer Input Source Blending Service shall ensure thread-safe visibility of the new weight for the next main-thread evaluation using a double-buffered pending/active weight map with volatile read/write semantics, without requiring the caller to take locks explicitly (D-7). Multiple writes to the same (layer, source) within one frame shall apply last-writer-wins.
5. The Layer Input Source Blending Service shall provide a bulk API that applies multiple (layer, input source, weight) updates atomically within a single frame so that coordinated scenario switches (e.g., "switch eye + emotion from controller to capture") are observed as a single transition; the bulk update shall become visible on the next aggregation and shall not interleave with single-entry updates made during the same frame (D-7).

### Requirement 5: 既存入力源アダプタとの統合
**Objective:** Unity エンジニアとして、既存のコントローラ / OSC / リップシンク実装を再利用したいので、これらを入力源抽象にアダプトするだけで新機構に接続できるようにしたい。

#### Acceptance Criteria
1. The Input Source Adapter layer shall provide an adapter (`LegacyExpressionInputSource`, reserved id `legacy`) that exposes the current Expression + TransitionCalculator pipeline output as a single input source on each layer (D-1, D-5); this adapter shall be registered automatically so that profiles without `inputSources` declarations continue to operate.
2. The Input Source Adapter layer shall provide an adapter (reserved id `osc`) that exposes OSC-received BlendShape values (from the existing double-buffered OSC receiver) as a single input source.
3. The Input Source Adapter layer shall provide an adapter (reserved id `lipsync`) that exposes `ILipSyncProvider` output as a single input source, usable on the lipsync layer.
4. When the OSC receiver has not produced any data within the current frame, the OSC input source adapter shall report the input source as valid with the last received values rather than producing a spurious zero frame.
5. Where the OSC adapter is configured with an `options.staleness_seconds` value greater than zero, the adapter shall mark itself invalid (contributing zero) when the most recent OSC packet is older than that threshold (D-8); when the option is absent or zero, the adapter shall maintain the legacy "hold last value indefinitely" behavior.
6. While an `ILipSyncProvider` returns no value (e.g., silence or plugin not running), the lipsync input source adapter shall mark the input source as inactive so that the Layer Input Source Blending Service treats its contribution as zero without producing audible jitter.
7. Where an adapter needs configuration (port, device, identifier name, staleness), the Input Source Adapter layer shall accept that configuration through constructor arguments or ScriptableObject/JSON `options` map without requiring changes to the Domain abstraction (D-6, D-8).

### Requirement 6: パフォーマンスと GC 非発生
**Objective:** Unity エンジニアとして、毎フレームで入力源ブレンドを実行しても性能劣化が発生しないでほしいので、GC ゼロ・スレッドセーフ・低計算量で動作する実装を保証したい。

#### Acceptance Criteria
1. The Layer Input Source Blending Service shall perform per-frame blending with zero managed heap allocations when the set of (layers, input sources) is unchanged.
2. The Layer Input Source Blending Service shall reuse pre-allocated buffers (e.g., `NativeArray` or pooled `float[]`) for per-source and per-layer intermediate values.
3. When the number of input sources across all layers is N and the BlendShape count is M, the Layer Input Source Blending Service shall complete per-frame blending in O(N * M) time without hidden quadratic behavior.
4. The Layer Input Source Blending Service shall maintain compatibility with the existing `LayerBlender` GC-free guarantee so that callers relying on `ReadOnlySpan<LayerInput>` / `Span<float>` overloads continue to operate allocation-free.
5. While ten or more characters are controlled simultaneously (per non-functional requirement 4.1), the Layer Input Source Blending Service shall not introduce per-character initialization work that scales worse than linearly in the number of characters.

### Requirement 7: 後方互換性と既存動作の保全
**Objective:** Unity エンジニアとして、既存のプロファイルと `LayerBlender` 利用コードを壊したくないので、新機能導入後も従来の挙動が再現されるようにしたい。

#### Acceptance Criteria
1. When a FacialProfile JSON does not declare any `inputSources`, the Layer Input Source Blending Service shall produce the same BlendShape output as the pre-extension `LayerBlender` for identical layer priorities, weights, and values.
2. The Layer Input Source Blending Service shall keep the existing `LayerBlender.Blend(ReadOnlySpan<LayerInput>, Span<float>)` and `LayerBlender.ApplyLayerSlotOverrides(...)` signatures callable from external packages without source-level breaking changes.
3. If an existing caller supplies a single input source per layer through the extended API, the Layer Input Source Blending Service shall produce results equivalent to calling the legacy `LayerBlender.Blend` with that layer's values (bit-equivalence within floating-point tolerance).
4. When a layer has a mixture of input sources with one source at weight 1 and all others at weight 0, the Layer Input Source Blending Service shall output exactly that source's values (within floating-point tolerance) for that layer.

### Requirement 8: 診断・テスト容易性
**Objective:** Unity エンジニアとして、入力源ブレンドの挙動を検証・デバッグしたいので、テスト可能な境界と観測可能な状態を確保したい。

#### Acceptance Criteria
1. The Layer Input Source Blending Service shall expose two read-only snapshot APIs: (a) a GC-free `TryWriteSnapshot(Span<LayerSourceWeightEntry> buffer, out int written)` suitable for per-frame use, and (b) a convenience `GetSnapshot()` returning a managed list for editor/diagnostic use only (D-9).
2. The Layer Input Source Blending Service shall be fully exercisable under EditMode tests using fakes for the `legacy` Expression pipeline, OSC receiver, and `ILipSyncProvider` without requiring PlayMode.
3. When per-source weights are changed via the runtime API, the Layer Input Source Blending Service shall make the change observable through the snapshot API on the next evaluation.
4. The FacialProfile JSON parser shall emit identical, stable JSON output for equivalent input-source weight configurations so that snapshot-based tests can assert round-trip equality.
5. Where verbose diagnostic logging is enabled (e.g., via a development flag), the Layer Input Source Blending Service shall log, at most once per second per layer, the current per-source weights to aid debugging without flooding the Unity Console.
6. The `FacialProfileSO` Inspector shall display a read-only view of the current per-layer input source weight map using the snapshot API (D-11); editing input source weights via the Inspector is out of scope for this spec.

---

## Dig Summary

### Investigation Overview
- AskUserQuestion ツールが本セッションで利用不可のため、対話形式の 1 回戦は行わず、コード調査ベースで 11 個の暗黙前提を洗い出し暫定決定を反映した。
- ラウンド数: 1（非対話・決定反映のみ）
- 洗い出した暗黙前提: 11（D-1〜D-11）
- 要件本文へ反映した決定: 11（各 AC に D-番号を相互参照）

### Key Discoveries
1. **「入力源」は Expression トリガーではなく BlendShape 値プロバイダ**（D-1）。既存コードでは InputBindingProfile は Expression ID へのマッピングだったため、本機能は BlendShape 値を直接供給する新しい抽象層を導入する。Expression パイプラインは `legacy` という予約 ID で暗黙の入力源として温存する。
2. **レイヤー内合成は加重和＋最終クランプ**（D-2）。正規化や lerp 連鎖ではない。これにより「コントローラ 50% + キャプチャ 50%」が直観的に動作し、重み合計が 1 未満でも不自然な増幅は起きない。
3. **2 段階パイプライン**（D-3）。レイヤー内で入力源集約 → 既存 `LayerBlender.Blend` に供給。既存 API 非破壊。
4. **OSC staleness タイムアウト**（D-8）をオプトインで導入し、「キャプチャ切断時に表情が固まる」事故回避経路を確保。
5. **ダブルバッファ方式でスレッド安全性を確保**（D-7）。強い lock-free MPMC を要求しない実装コスト削減。

### All Decisions

| ID | トピック | 決定 | Rationale | リスク |
|------|-------------|---------------|-----------|-------|
| D-1 | 入力源の意味論 | BlendShape 値プロバイダとして定義、Expression は `legacy` 予約 ID | ユースケース「ソース間ブレンド」「ソース固定」が値レベル合成を要求 | 高→中 |
| D-2 | 合成式 | 加重和 + 最終クランプ（正規化なし） | 重みの意味が直感的、ユーザーの学習コスト最小 | 中 |
| D-3 | 合成順序 | 2 段階（intra-layer aggregate → inter-layer `LayerBlender`） | 既存 API 非破壊、純増加機能化 | 低 |
| D-4 | レイヤー vs 入力源ウェイト | 独立。レイヤー weight は inter-layer のみ、ソース weight は intra-layer のみ | 役割分離で意図が明確 | 低 |
| D-5 | legacy ソースのデフォルト | `inputSources` 未宣言 → legacy 1.0 単独 | 既存プロファイル完全互換 | 低 |
| D-6 | 識別子規約 | `[a-zA-Z0-9_.-]{1,64}`、予約 4 種、`x-` プレフィックスで拡張 | 将来拡張時の衝突回避 | 低 |
| D-7 | スレッドポリシー | ダブルバッファ + Volatile、lock-free MPMC 不要 | OscDoubleBuffer パターン踏襲、実装コスト最小 | 中 |
| D-8 | OSC staleness | オプトインの `options.staleness_seconds` | キャプチャ切断事故を選択回避可能に | 中 |
| D-9 | 診断 API の GC | TryWriteSnapshot（GC フリー）と GetSnapshot（List）の 2 系統 | Req 6.1 と Req 8.1 の両立 | 低 |
| D-10 | 性能ベンチ | per-frame Set パターンのテスト追加（tasks フェーズで定義） | GC ゼロ主張の検証 | 低 |
| D-11 | Editor UI スコープ | 読み取り専用表示のみ、編集 UI は別スペック | preview 段階の最小スコープ | 低 |

### Remaining Risks
- **Expression 駆動と直接 BlendShape 駆動の混在**（D-1）: 1 レイヤー上で `legacy` と `osc` が同時に weight > 0 の場合、Expression の意図した値と OSC の値が加算される。ユーザー誤設定時の UX は design フェーズで検討（警告ログ、Inspector 警告表示など）。
- **重み合計 > 1 時のクランプ挙動**（D-2）: 設定者が合成結果の飽和に気付きにくい。診断 API で可視化するが、design フェーズでスナップショットの UX 検討が必要。
- **バルク API のアトミック性とシングル Set の相互排除**（D-7）: 同フレーム内でバルクとシングルが混在した場合の順序規約は、design フェーズで詳細化する必要がある（現状は Req 4.5 に最低限の規約を記述）。

### Recommended Next Steps
1. **本要件書の承認** — 特に D-1（入力源の意味論）と D-2（合成式）はユースケース解釈に直結するため、`/kiro:spec-design` へ進む前に差し戻し可否を判断。
2. **spec-design フェーズで残存リスクの詳細設計** — D-1 混在警告、D-2 飽和可視化、D-7 バルク/シングル順序規約。
3. **AskUserQuestion ツールが利用可能な環境での再 dig** — 本ラウンドはコード調査ベースの単発決定のみ。対話ベースの Phase 3 深掘りは未実施。もし 2+ レベルの深掘りが必要な箇所が残っていれば、次セッションで実施可能。
