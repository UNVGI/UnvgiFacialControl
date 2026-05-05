# Requirements Document

## Project Description (Input)
FacialControl のアダプタパッケージ統合アーキテクチャ刷新。現状、`com.hidano.facialcontrol.inputsystem` パッケージが core の `FacialCharacterProfileSO` を継承して `FacialCharacterSO` を作る形になっており、単一継承のため OSC / ARKit / Lipsync 等の複数アダプタを 1 キャラクターで併用するとアーキテクチャ的に行き詰まる。各アダプタパッケージが個別に MonoBehaviour Extension + 専用 ScriptableObject を提供しているため、ユーザーは複数の Inspector を並べて設定する必要があり、UX が悪い。さらに `MultiSourceBlend`（同一レイヤーに複数入力源を並置し加重和ブレンドする）という重要概念が InputSystem パッケージのサンプルにしか存在せず、core の一般機能として昇格していない。

採用する設計（Approach A、ユーザー確定済み）: core 側の `FacialCharacterProfileSO` に `[SerializeReference] List<AdapterBindingBase> AdapterBindings` フィールドを追加し、各アダプタパッケージが `AdapterBindingBase` を継承した具象クラス（例: `InputSystemAdapterBinding`, `OscAdapterBinding`, `ARKitAdapterBinding`）を提供する。具象クラスには `[FacialAdapterBinding(displayName: "Input System")]` 属性を付与し、core 側 Inspector は `TypeCache.GetTypesWithAttribute<FacialAdapterBindingAttribute>()` で attribute 付きクラスを auto-discovery してメニューに列挙する。各アダプタ専用の PropertyDrawer は当該パッケージで `[CustomPropertyDrawer(typeof(InputSystemAdapterBinding))]` として提供（core は変更不要）。これにより新規アダプタパッケージを追加する際 core の変更が一切要らないことを必須要件とする。

同時に `MultiSourceBlend` の概念（レイヤーごとの `inputSources[]` 加重和ブレンド）を core 一般機能に昇格させ、`MultiSourceBlendDemo` 相当のサンプルも core 側で提供する。

ポリシー: preview 段階のため後方互換は不要（既存 SO/Asset/JSON は破壊変更可）。既存の OSC / InputSystem / ARKit パッケージは新アーキテクチャに移行する。ユーザー目線では「単一の SO ファイルに全部入る」UX を目指す。

スコープ外: ランタイムでのアダプタの動的有効化/無効化、ScriptableObject 以外のデータ永続化形式（いずれも preview.2 以降に検討）。

## Introduction

本仕様は、FacialControl パッケージ（preview 段階）におけるアダプタ統合アーキテクチャの刷新を目的とした破壊的改修である。現状、各アダプタパッケージ（InputSystem / OSC / ARKit など）が個別に core SO を継承する派生 ScriptableObject を提供する単一継承モデルとなっており、複数アダプタを 1 キャラクターで併用できない。本仕様では、core 側の `FacialCharacterProfileSO` に `[SerializeReference] List<AdapterBindingBase> AdapterBindings` を追加し、各アダプタパッケージが属性ベースで自己記述する `AdapterBindingBase` 派生クラスを提供する **Approach A（属性駆動の Adapter Binding コンポジション）** を採用する。`MultiSourceBlend` 概念は core 一般機能へ昇格させ、サンプルも core 同梱とする。preview 段階のため schema 互換性は破壊するが、新規アダプタパッケージ追加時に core への変更が一切不要であることを必須要件とする。

## Boundary Context

- **In scope**:
  - `FacialCharacterProfileSO` への `[SerializeReference] List<AdapterBindingBase> AdapterBindings` フィールド追加と Domain / Adapters / Editor 各層の対応
  - `AdapterBindingBase` 抽象クラスと `FacialAdapterBindingAttribute` の core 提供
  - core Inspector による `TypeCache.GetTypesWithAttribute` を用いたアダプタ実装の auto-discovery と AddDropdown UI
  - 各アダプタパッケージ側での `[CustomPropertyDrawer(typeof(...AdapterBinding))]` 提供契約
  - 既存 OSC / InputSystem / ARKit パッケージの新アーキテクチャへの移行（具象 `*AdapterBinding` クラスへの再実装）
  - `MultiSourceBlend`（レイヤー単位の `inputSources[]` 加重和ブレンド）の core 機能昇格
  - `MultiSourceBlendDemo` 相当の core 同梱サンプル（`Samples~/MultiSourceBlendDemo/`）
  - 「単一の `FacialCharacterProfileSO` ファイルに全アダプタ設定が入る」UX
  - クリーンアーキテクチャ契約の維持（Domain は Unity 非依存、Editor は IMGUI を新規追加しない 等）
- **Out of scope**:
  - ランタイムでのアダプタの動的有効化/無効化（preview.2 以降）
  - ScriptableObject 以外のデータ永続化形式（preview.2 以降）
  - 既存 schema 互換読み込み（preview 段階のため不要）
  - 新規アダプタ実装そのもの（本仕様は基盤のみ。OSC / InputSystem / ARKit の移行は本仕様の範囲だが、新規センサー対応等は対象外）
  - Timeline 統合 / VRM 対応 / VR・モバイル対応
- **Adjacent expectations**:
  - `inspector-and-data-model-redesign` spec: `FacialCharacterSO` Inspector UX 改善・データモデル簡素化の方針と整合
  - `layer-input-source-blending` spec: D-1 ハイブリッド入力源モデル、D-2 加重和合成、D-3 2 段階パイプラインなど MultiSourceBlend の決定事項を踏襲
  - クリーンアーキテクチャ契約: `Hidano.FacialControl.Domain` は Unity 非依存、Adapters のみ Engine 依存可、Editor は UI Toolkit
  - パッケージ独立性: core は OSC / InputSystem / ARKit を**知らない**。既存 `com.hidano.facialcontrol.osc` / `com.hidano.facialcontrol.inputsystem` は core を参照する片方向依存
  - `docs/requirements.md` FR-002（マルチレイヤー）/ FR-004（OSC）/ FR-005（入力デバイス）/ FR-007（リップシンク）と整合
  - 性能契約: 毎フレームのヒープ確保ゼロ目標、同時 10 体以上のキャラクター制御を想定

## Open Questions and Decisions (Dig)

| ID | トピック | 決定 | Rationale | Risk |
|----|----------|------|-----------|------|
| D-1 | AdapterBindingBase の責務スコープ | **Data + Runtime Factory** | binding 自身が `BuildRuntime()` で IInputSource を生成しライフサイクル管理。MonoBehaviour Extension 不要化と「単一 SO に全部入る UX」を最大化 | High |
| D-2 | MonoBehaviour Extension の運命 | **撤廃して FacialController が自動管理** | `FacialController` が SO の AdapterBindings を読んで runtime を生成・破棄する。Scene 上の Extension コンポーネントは廃止 | High |
| D-3 | AdapterBinding と layer.inputSources[] の統合 | **AdapterBinding が IInputSource を InputSourceFactory に登録** | binding 1 つが N 個の IInputSource を unique id で factory 登録。layer.inputSources[] は引き続き id 文字列で参照。既存 MultiSourceBlend / 予約 ID 体系を生かす | High |
| D-4 | 同型 binding 複数追加時の ID 衝突解決 | **ユーザー設定の Slug フィールド** | binding ごとに `slug: string` を持ち、layer.inputSources[].id は `"osc:vrchat"` `"osc:vmc"` のような複合 id で参照。明示的・順序非依存・ユーザーフレンドリ | High |
| D-5 | BuildRuntime context の形 | **DI コンテナ（VContainer 採用）** | VContainer の LifetimeScope 経由で AdapterBinding に依存を注入。Unity native との親和性が高く `[Inject]` で typed resolve 可能 | High |
| D-6 | Asset 参照（InputActionAsset 等）の保持 | **binding 内フィールドとして保持** | `[SerializeReference]` 内に Asset 参照（`UnityEngine.Object` 派生）を直接持つ。Inspector で per-binding 結線、Scene 非依存、「単一 SO に全部入る」原則を維持 | Medium |

## Requirements

### Requirement 1: AdapterBinding 抽象と属性ベース discovery

**Objective:** Unity エンジニアとして、各アダプタパッケージが core の `FacialCharacterProfileSO` を継承せずに自己記述的な型として登録できるようにしたい。これにより、単一継承の制約を排除し、複数アダプタを併用可能にしたい。

#### Acceptance Criteria

1. The FacialControl Core package shall define an abstract base class `AdapterBindingBase` in the Domain layer that adapter packages extend; the base class shall remain free of Unity Engine references except `Unity.Collections`.
2. The FacialControl Core package shall define an attribute `FacialAdapterBindingAttribute` whose constructor takes at least a `displayName: string`; the attribute shall be applicable only to non-abstract classes derived from `AdapterBindingBase`.
3. The FacialControl Core Inspector shall discover all concrete `AdapterBindingBase` subclasses annotated with `FacialAdapterBindingAttribute` at Editor load time using `UnityEditor.TypeCache.GetTypesWithAttribute<FacialAdapterBindingAttribute>()`.
4. When discovery completes, the FacialControl Core Inspector shall populate the "Add Adapter Binding" dropdown using each discovered type's `displayName` and shall sort the entries in stable alphabetical order by `displayName`.
5. The FacialControl Core package shall NOT contain any compile-time reference to specific adapter types (e.g., `InputSystemAdapterBinding`, `OscAdapterBinding`, `ARKitAdapterBinding`).
6. Where a new adapter package is added with a class extending `AdapterBindingBase` and annotated with `FacialAdapterBindingAttribute`, the FacialControl Core package shall expose that adapter in the Inspector dropdown without any source-level change to the Core package.
7. If two discovered adapter types declare the same `displayName`, the FacialControl Core Inspector shall log a warning via `Debug.LogWarning` listing both fully qualified type names and shall append the type name as a disambiguating suffix in the dropdown.

### Requirement 2: FacialCharacterProfileSO への AdapterBindings コレクション

**Objective:** Unity エンジニアとして、1 つの `FacialCharacterProfileSO` で複数アダプタ（InputSystem + OSC + ARKit など）を併用したい。これにより、単一継承モデルを脱却し、単一の SO ファイルに全アダプタ設定をまとめられる。

#### Acceptance Criteria

1. The FacialCharacterProfileSO shall declare a serialized field `AdapterBindings` typed as `List<AdapterBindingBase>` and annotated with `[SerializeReference]` to support polymorphic serialization across adapter package boundaries.
2. The FacialCharacterProfileSO shall allow zero or more `AdapterBindingBase` instances in `AdapterBindings`, with no compile-time upper bound enforced by the Core package.
3. When a `FacialCharacterProfileSO` is serialized to disk, the `AdapterBindings` field shall persist concrete subclass type identity using Unity's `[SerializeReference]` mechanism so that round-trip load reconstructs the original concrete type.
4. The FacialCharacterProfileSO shall allow the same concrete `AdapterBindingBase` subclass to appear multiple times in `AdapterBindings` (e.g., two `OscAdapterBinding` instances bound to different ports) unless the specific subclass declares otherwise via its own attribute.
5. When the user adds a new adapter via the Inspector dropdown described in Requirement 1.4, the FacialControl Core Inspector shall instantiate the chosen subclass via `Activator.CreateInstance` (or equivalent) and shall append it to `AdapterBindings`.
6. When the user removes an adapter entry from the Inspector, the FacialControl Core Inspector shall remove the corresponding element from `AdapterBindings` and shall mark the asset dirty.
7. If `AdapterBindings` contains a `null` element after deserialization (e.g., due to a missing assembly), the FacialControl Core Inspector shall display a "Missing Adapter" placeholder entry with a remove button and shall NOT abort loading the rest of the asset.

### Requirement 3: アダプタパッケージ側の PropertyDrawer 提供契約

**Objective:** Unity エンジニアとして、各アダプタの専用 Inspector UI を当該アダプタパッケージ側で実装したい。これにより、core を改変せずに各アダプタが独自の編集体験を提供できる。

#### Acceptance Criteria

1. The FacialControl Core package shall NOT register a `[CustomPropertyDrawer]` for any concrete `AdapterBindingBase` subclass.
2. Each adapter package shall provide a `[CustomPropertyDrawer(typeof(<ConcreteAdapterBinding>))]` implementation in its Editor assembly to render the in-Inspector UI for its own binding type.
3. While the Core Inspector renders the `AdapterBindings` list, the Core Inspector shall delegate per-element drawing to Unity's PropertyDrawer resolution mechanism so that each concrete subclass is rendered by its package-provided drawer.
4. Where an adapter package does not provide a `[CustomPropertyDrawer]` for its concrete binding, the FacialControl Core Inspector shall fall back to the default `SerializedProperty` rendering and shall NOT raise an error.
5. The FacialControl Core Editor shall use UI Toolkit for the surrounding list UI (Add / Remove / Reorder controls) and shall NOT introduce new IMGUI panels for this surrounding chrome.
6. When a PropertyDrawer for an adapter binding throws an exception during rendering, the FacialControl Core Inspector shall catch the exception, log it via `Debug.LogError`, and shall display a fallback error placeholder for that single entry without breaking the rest of the Inspector.

### Requirement 4: パッケージ依存方向と Domain 純度

**Objective:** Unity エンジニアとして、core パッケージが各アダプタパッケージを参照しない一方向依存を維持したい。これにより、パッケージの独立配布性とクリーンアーキテクチャ契約を守れる。

#### Acceptance Criteria

1. The `com.hidano.facialcontrol` Core package shall NOT declare assembly definition references to `com.hidano.facialcontrol.osc`, `com.hidano.facialcontrol.inputsystem`, or any future adapter package.
2. The `AdapterBindingBase` abstract class shall reside in the `Hidano.FacialControl.Domain` assembly and shall be free of `UnityEngine.*` references except `Unity.Collections`.
3. Each adapter package's concrete `*AdapterBinding` class shall reside in that adapter package's Adapters assembly and shall reference the Core package's `Hidano.FacialControl.Domain` assembly for the base class.
4. The `FacialAdapterBindingAttribute` shall reside in the Core package and shall be available to adapter packages via the Core package reference alone.
5. While the Core Inspector performs auto-discovery, the Core Inspector shall query loaded assemblies via `TypeCache` without taking a hard reference to any adapter package assembly name or type.
6. If a Core package change would require referencing an adapter-specific symbol to support a new adapter package, the redesign shall be considered violating Requirement 1.5 and Requirement 1.6 and shall be rejected.

### Requirement 5: MultiSourceBlend の core 機能昇格

**Objective:** Unity エンジニアとして、レイヤー単位の `inputSources[]` 加重和ブレンドを InputSystem パッケージのサンプル機能ではなく core の一般機能として利用したい。これにより、すべてのアダプタが MultiSourceBlend を前提に設計できる。

#### Acceptance Criteria

1. The FacialControl Core package shall provide the MultiSourceBlend domain model (per-layer `inputSources[]` weighted-sum blending) as a first-class feature in the Domain / Application layers, consistent with the decisions D-1 through D-15 of the `layer-input-source-blending` spec.
2. The MultiSourceBlend implementation shall reside in the Core package and shall NOT depend on `com.hidano.facialcontrol.inputsystem`, `com.hidano.facialcontrol.osc`, or any other adapter package.
3. When a layer is configured with multiple input sources, the Core MultiSourceBlend service shall compute, for each BlendShape index k, `output[k] = clamp01(Σ wᵢ · values_i[k])` (D-2 weighted-sum-with-final-clamp, no normalization).
4. The Core MultiSourceBlend service shall accept input sources contributed by any `AdapterBindingBase` subclass that produces BlendShape values, without the Core knowing the concrete adapter types.
5. The Core MultiSourceBlend service shall preserve the existing `LayerBlender` API and feed its per-layer aggregated outputs into `LayerBlender.Blend(...)` (D-3 two-stage pipeline).
6. The FacialControl Core package shall ship a sample under `Samples~/MultiSourceBlendDemo/` (registered in `package.json`'s `samples` array) that demonstrates MultiSourceBlend with at least two adapter bindings collaborating on a single layer.
7. While running the Core MultiSourceBlend service per frame on an unchanged set of (layers, sources), the service shall not allocate managed heap memory (consistent with the Core performance contract and `layer-input-source-blending` Req 6.1).

### Requirement 6: 既存アダプタパッケージの新アーキテクチャ移行

**Objective:** Unity エンジニアとして、既存の OSC / InputSystem / ARKit パッケージを新 Adapter Binding アーキテクチャに移行したい。これにより、preview 利用者が単一の `FacialCharacterProfileSO` で全アダプタを設定できる UX を実現する。

#### Acceptance Criteria

1. The `com.hidano.facialcontrol.inputsystem` package shall provide a concrete `InputSystemAdapterBinding` class that derives from `AdapterBindingBase` and is annotated with `[FacialAdapterBinding(displayName: "Input System")]`.
2. The `com.hidano.facialcontrol.osc` package shall provide a concrete `OscAdapterBinding` class that derives from `AdapterBindingBase` and is annotated with `[FacialAdapterBinding(displayName: "OSC")]`.
3. The ARKit / PerfectSync adapter (whether shipped as a separate package or inside an existing package, per the `product.md` release plan) shall provide a concrete `ARKitAdapterBinding` class that derives from `AdapterBindingBase` and is annotated with `[FacialAdapterBinding(displayName: "ARKit / PerfectSync")]`.
4. The `com.hidano.facialcontrol.inputsystem` package shall remove the existing `FacialCharacterSO` derived ScriptableObject (which inherits from `FacialCharacterProfileSO`) as part of this migration, since it is incompatible with the new composition-based model.
5. Each migrated adapter package shall provide a `[CustomPropertyDrawer(typeof(<ConcreteAdapterBinding>))]` in its Editor assembly to preserve the existing per-adapter editing UX.
6. When the migration is complete, a single `FacialCharacterProfileSO` asset shall be capable of holding `InputSystemAdapterBinding` + `OscAdapterBinding` + `ARKitAdapterBinding` simultaneously and shall serialize / deserialize all three round-trip without loss.
7. The migration shall be treated as a preview-stage breaking change per `docs/requirements.md` FR-001; existing assets created against the old single-inheritance model shall NOT be expected to load and shall require manual recreation.

### Requirement 7: 単一 SO ファイル UX

**Objective:** Unity エンジニアとして、表情・入力バインディング・OSC・ARKit などの全設定を単一の `FacialCharacterProfileSO` Inspector で完結したい。これにより、複数 SO を並べて編集する現在の煩雑さを排除できる。

#### Acceptance Criteria

1. The FacialCharacterProfileSO Inspector shall present `AdapterBindings` as a reorderable list with Add (dropdown of discovered adapters) / Remove / Move-Up / Move-Down controls.
2. While the user edits adapter binding fields in the Inspector, the FacialCharacterProfileSO Inspector shall persist all changes to the same single asset file as the rest of the profile data.
3. The FacialCharacterProfileSO Inspector shall NOT require the user to open a secondary asset, ScriptableObject, or component to configure any adapter that follows the `AdapterBindingBase` contract.
4. Where an adapter binding requires references to other Unity assets (e.g., `InputActionAsset`), the adapter's PropertyDrawer shall expose those object fields inline within the same Inspector view.
5. When the user adds an adapter binding that requires runtime-only configuration (e.g., OSC port number), the adapter's PropertyDrawer shall expose those fields inline so that no separate ScriptableObject is needed.

### Requirement 8: 破壊的変更ポリシーと preview スコープ

**Objective:** preview 段階の利用者として、本改修が schema 互換性を破壊することを明確に把握したい。これにより、preview 利用者が新アーキテクチャに乗り換える判断材料を得られる。

#### Acceptance Criteria

1. The redesigned FacialCharacterProfileSO shall NOT load pre-redesign serialized assets that relied on the old `FacialCharacterSO` single-inheritance model.
2. The CHANGELOG entry for this change shall mark the release as a breaking change and shall reference the Adapter Binding architecture migration.
3. The FacialControl Core package shall NOT include automatic migration code for old `FacialCharacterSO` assets, consistent with the preview-stage policy of `docs/requirements.md` FR-001.
4. Where existing scenes reference a removed `FacialCharacterSO` derived type, the user shall be expected to re-author the asset using `FacialCharacterProfileSO` + the appropriate `*AdapterBinding` entries.
5. The CHANGELOG and Documentation~ entries shall list the dropped types (`FacialCharacterSO`, etc.) explicitly so that preview users can plan their migration.

### Requirement 9: パフォーマンスと GC 非発生

**Objective:** Unity エンジニアとして、Adapter Binding アーキテクチャ刷新が毎フレームのヒープ確保ゼロ目標を破壊しないことを保証したい。これにより、同時 10 体以上のキャラクター制御における GC スパイクを回避できる。

#### Acceptance Criteria

1. While the runtime evaluates a `FacialCharacterProfileSO` whose `AdapterBindings` is unchanged, the FacialControl Core runtime shall NOT allocate managed heap memory per frame for steady-state evaluation of the adapter pipeline.
2. The FacialControl Core runtime shall NOT use reflection (`TypeCache`, `Activator`, attribute scans) on per-frame hot paths; reflection shall be confined to Editor / load-time / discovery contexts.
3. When `FacialCharacterProfileSO` is loaded, the Core runtime shall preallocate intermediate buffers (per-source / per-layer arrays) sized from the deserialized `AdapterBindings` and layer configuration.
4. The FacialControl Core runtime shall not introduce per-character initialization work that scales worse than linearly in `(adapterBindingCount + sourcesPerChar) × charCount` (consistent with `layer-input-source-blending` D-14).
5. The performance test suite under `Tests/PlayMode/Performance` shall verify zero per-frame allocations for a `FacialCharacterProfileSO` containing at least three adapter bindings using `GC.GetTotalMemory` delta or Unity Profiler markers.

### Requirement 10: テスト要件（TDD 厳守）

**Objective:** Unity エンジニアとして、本改修がプロジェクトの TDD 原則と EditMode/PlayMode 配置基準に従うことを保証したい。これにより、回帰防止と CI 安定性を維持できる。

#### Acceptance Criteria

1. The development team shall follow Red-Green-Refactor for every behavior introduced by this redesign.
2. The `Tests/EditMode` suite shall cover `AdapterBindingBase` polymorphic serialization round-trip, `FacialAdapterBindingAttribute` discovery via `TypeCache`, MultiSourceBlend domain logic with fakes for adapter bindings, and the dropdown enumeration sort/duplicate-name handling described in Requirement 1.7.
3. The `Tests/PlayMode` suite shall cover end-to-end runtime evaluation of a `FacialCharacterProfileSO` containing multiple adapter bindings, MultiSourceBlend per-frame execution under representative load, and zero-allocation verification for Requirement 9.5.
4. When a test verifies Editor Inspector behavior (Add dropdown, Remove, Reorder, missing-adapter placeholder), the test shall reside under `Tests/EditMode` because Inspector behavior does not require the Player loop.
5. When a test verifies actual InputSystem / OSC / ARKit input dispatch through an `*AdapterBinding`, the test shall reside under `Tests/PlayMode` because the underlying device / network simulation requires the Player loop.
6. Test class names shall follow `{Target}Tests` and method names shall follow `{Method}_{Condition}_{Expected}` (e.g., `Discover_TwoAttributesWithSameDisplayName_LogsWarningAndDisambiguates`).
7. The PlayMode performance test suite shall NOT introduce per-frame allocations while verifying Requirement 9.

### Requirement 11: クリーンアーキテクチャ契約の維持

**Objective:** Unity エンジニアとして、本改修が Domain / Application / Adapters / Editor のレイヤー分離を破壊しないことを保証したい。これにより、Runtime UI 提供禁止・Engine 非依存 Domain・Editor 専用機能の隔離といった既存契約を維持できる。

#### Acceptance Criteria

1. The `AdapterBindingBase` abstract class and `FacialAdapterBindingAttribute` shall reside in the `Hidano.FacialControl.Domain` assembly and shall NOT reference `UnityEngine.*` symbols beyond what is already permitted (`Unity.Collections`).
2. Concrete `*AdapterBinding` classes that need Engine-side dependencies (e.g., `InputActionReference`, `AnimationClip`, `IPEndPoint`) shall reside in their respective package's Adapters assembly and shall NOT pull those types into Domain.
3. The Core auto-discovery and the per-element rendering pipeline shall live in the Core Editor assembly with `includePlatforms: ["Editor"]` and shall NOT be reachable from Runtime assemblies.
4. The FacialControl Core Editor module shall use UI Toolkit for the `AdapterBindings` list chrome and shall NOT introduce new IMGUI panels (consistent with the project's "Editor は UI Toolkit" contract).
5. While preserving Domain purity, Domain types referenced from `AdapterBindingBase` shall be limited to plain C# / `Unity.Collections` types so that Domain remains testable under EditMode without PlayMode fixtures.
6. The Core package shall NOT provide any Runtime UI for editing `AdapterBindings`; runtime configuration changes (out of scope for preview) shall be deferred to preview.2 or later per the Project Description.
