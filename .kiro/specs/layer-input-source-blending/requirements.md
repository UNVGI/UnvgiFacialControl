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

## Requirements

### Requirement 1: 入力源の抽象化とレイヤー入力モデル
**Objective:** Unity エンジニアとして、コントローラ / OSC / リップシンクなど異種の入力源を統一的にレイヤーへ供給したいので、入力源を共通のインターフェースと値配列として扱えるようにしたい。

#### Acceptance Criteria
1. The Layer Input Source Blending Service shall define a domain-level abstraction that represents an input source as a tuple of (source identifier, BlendShape value array, validity flag).
2. The Layer Input Source Blending Service shall accept multiple input sources per layer without assuming a fixed number of sources at compile time.
3. When an input source provides a BlendShape value array whose length does not match the layer's BlendShape count, the Layer Input Source Blending Service shall process only the overlapping range and leave the remaining indices unchanged.
4. If an input source is marked as invalid or disconnected, the Layer Input Source Blending Service shall treat its contribution as zero without raising an exception.
5. The Layer Input Source Blending Service shall not depend on Unity-specific APIs within the Domain layer so that the abstraction remains testable under EditMode without PlayMode fixtures.

### Requirement 2: 入力源ウェイトマトリクスとレイヤー内合成
**Objective:** Unity エンジニアとして、同一レイヤー内で複数入力源を重み付きで合成したいので、レイヤーごとに入力源ウェイトマトリクスを定義し一貫した合成結果を得たい。

#### Acceptance Criteria
1. The Layer Input Source Blending Service shall associate each layer with a weight map keyed by input source identifier, where each weight is a float in the range 0 to 1.
2. When computing a layer's aggregated BlendShape values, the Layer Input Source Blending Service shall combine per-source values using per-source weights such that a source with weight 0 contributes nothing and a source with weight 1 contributes its full value.
3. While the sum of input source weights within a single layer exceeds 1, the Layer Input Source Blending Service shall clamp the final per-BlendShape result to the range 0 to 1 without rescaling individual source weights.
4. If no input source is registered for a layer, the Layer Input Source Blending Service shall output the layer's default (zero) BlendShape values and log a warning at most once per layer per session.
5. When an input source weight is set outside the 0 to 1 range, the Layer Input Source Blending Service shall silently clamp the value into 0 to 1, consistent with the existing `LayerBlender` clamping policy.
6. The Layer Input Source Blending Service shall preserve the current inter-layer priority blending and `LayerSlot` override semantics of `LayerBlender` after per-layer source aggregation is applied.

### Requirement 3: FacialProfile JSON スキーマ拡張
**Objective:** Unity エンジニアとして、入力源ウェイトをプロファイル JSON で宣言したいので、レイヤー定義に入力源ウェイトマトリクスを追加し、ランタイムで読み書き可能にしたい。

#### Acceptance Criteria
1. The FacialProfile JSON parser shall recognize an optional `inputSources` field on each layer definition that lists input source identifiers and their initial weights.
2. When a layer definition omits the `inputSources` field, the FacialProfile JSON parser shall default that layer to a single implicit input source mapped to the existing legacy pipeline so that existing profiles continue to work unchanged.
3. If an `inputSources` entry specifies an unknown identifier, the FacialProfile JSON parser shall log a warning and skip that entry without aborting profile loading.
4. When the same input source identifier appears more than once under a single layer, the FacialProfile JSON parser shall keep the last occurrence and log a warning about the duplicate.
5. The FacialProfile JSON parser shall serialize input source weights back to JSON with stable ordering (declaration order preserved) so that round-trip import/export does not introduce spurious diffs.
6. Where the JSON schema evolves during the preview phase, the FacialProfile JSON parser shall follow the project's preview-stage breaking-change policy (documented in `docs/requirements.md` FR-001) without requiring migration scripts.

### Requirement 4: ランタイムでの入力源ウェイト制御
**Objective:** Unity エンジニアとして、シーン内の状況に応じて入力源ウェイトを切り替えたいので、ランタイム API から入力源ウェイトを安全に変更できるようにしたい。

#### Acceptance Criteria
1. The Layer Input Source Blending Service shall expose a runtime API that sets the weight of a single input source on a specified layer in O(1) or O(sourcesPerLayer) time.
2. When a runtime weight change is requested for an existing (layer, input source) pair, the Layer Input Source Blending Service shall apply the new weight starting from the next blending evaluation without interrupting ongoing expression transitions.
3. If a runtime weight change targets a non-existent layer or input source identifier, the Layer Input Source Blending Service shall log a warning and leave all other weights unchanged.
4. While a weight change is in progress from a non-main thread, the Layer Input Source Blending Service shall ensure thread-safe visibility of the new weight for the next main-thread evaluation without requiring the caller to take locks explicitly.
5. The Layer Input Source Blending Service shall provide a bulk API that applies multiple (layer, input source, weight) updates atomically within a single frame so that coordinated scenario switches (e.g., "switch eye + emotion from controller to capture") are observed as a single transition.

### Requirement 5: 既存入力源アダプタとの統合
**Objective:** Unity エンジニアとして、既存のコントローラ / OSC / リップシンク実装を再利用したいので、これらを入力源抽象にアダプトするだけで新機構に接続できるようにしたい。

#### Acceptance Criteria
1. The Input Source Adapter layer shall provide an adapter that exposes the current `InputBindingProfile`-driven BlendShape values as a single input source identified by a stable string identifier.
2. The Input Source Adapter layer shall provide an adapter that exposes OSC-received BlendShape values (from the existing double-buffered OSC receiver) as a single input source identified by a stable string identifier.
3. The Input Source Adapter layer shall provide an adapter that exposes `ILipSyncProvider` output as a single input source identified by a stable string identifier, usable on the lipsync layer.
4. When the OSC receiver has not produced any data within the current frame, the OSC input source adapter shall report the input source as valid with the last received values rather than producing a spurious zero frame.
5. While an `ILipSyncProvider` returns no value (e.g., silence or plugin not running), the lipsync input source adapter shall mark the input source as inactive so that the Layer Input Source Blending Service treats its contribution as zero without producing audible jitter.
6. Where an adapter needs configuration (port, device, identifier name), the Input Source Adapter layer shall accept that configuration through constructor arguments or ScriptableObject/JSON settings without requiring changes to the Domain abstraction.

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
1. The Layer Input Source Blending Service shall expose a read-only snapshot API that returns, for each layer, the list of currently registered input source identifiers and their effective weights.
2. The Layer Input Source Blending Service shall be fully exercisable under EditMode tests using fakes for `InputBindingProfile`, OSC receiver, and `ILipSyncProvider` without requiring PlayMode.
3. When per-source weights are changed via the runtime API, the Layer Input Source Blending Service shall make the change observable through the snapshot API on the next evaluation.
4. The FacialProfile JSON parser shall emit identical, stable JSON output for equivalent input-source weight configurations so that snapshot-based tests can assert round-trip equality.
5. Where verbose diagnostic logging is enabled (e.g., via a development flag), the Layer Input Source Blending Service shall log, at most once per second per layer, the current per-source weights to aid debugging without flooding the Unity Console.
