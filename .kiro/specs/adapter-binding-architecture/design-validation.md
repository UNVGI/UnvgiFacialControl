# Design Review: adapter-binding-architecture

## Design Review Summary

design.md は 13 要件 + Addendum + 12 個の D-* 決定をほぼ網羅的にカバーしており、Requirements Traceability テーブル・コンポーネント仕様・2 段階移行計画・Mermaid フロー図まで揃った高密度な設計書である。Domain 0-alloc 既存資産を温存しつつ Adapters 層の glue だけで全要件を消化する Option B + Option C の選択は妥当で、研究フェーズ（research.md）の知見も適切に反映されている。ただし **Domain 純度契約と `AdapterBindingBase` 実装の間に明確な矛盾** があり、これと **Phase 1 並走期の slug ID 衝突可能性** を解消しないと tasks 生成段階で曖昧さが残るため、後述の 3 件を確定させた上で承認するのが妥当。

## Critical Issues

### Critical Issue 1: Domain 純度契約と `AdapterBindingBase` の `[UnityEngine.SerializeField]` が矛盾している

**Concern**: design.md L489（Responsibilities & Constraints）と L509（コードサンプル）が直接矛盾する。L489 では「Unity Engine 参照を一切持たない（`Unity.Collections` のみ許容）」と明記しつつ、L509 で `[UnityEngine.SerializeField] private string _slug;` を `Hidano.FacialControl.Domain.Adapters.AdapterBindingBase` 上に配置している。Req 4.2 / Req 11.1 / steering tech.md「Domain は Unity 非依存」/ Domain asmdef（`references: ["Unity.Collections"]`）も同じ方向の契約。

**Impact**: tasks フェーズで「Domain asmdef に `UnityEngine` 参照を追加するか」「`AdapterBindingBase` を Adapters 層へ移すか」「`[SerializeField]` を外して具象側にだけ slug フィールドを置かせるか」の根本判断が決まらない。誤って Domain asmdef へ Engine 参照を入れると steering で禁止された「破ってはならない契約」を踏み抜き、EditMode テストの Domain 単独ビルド（asmdef 隔離）も影響を受ける。slug は Req 12.1（`AdapterBindingBase` が serialized slug を持つ）で必須なので、無視できない。

**Suggestion**:
- 案 A（推奨）: `AdapterBindingBase` を **Adapters 層**（`Hidano.FacialControl.Adapters.Adapters` などの新サブ namespace）へ移し、Domain には残さない。Req 4.2 / 11.1 は「Domain 配置」を要求しているため要件改修が前提となる。
- 案 B: `AdapterBindingBase` を Domain に残し、`[Serializable]` のみ付け、slug フィールドは具象クラス側で `[UnityEngine.SerializeField]` を持たせる規約とする。Req 12.1 の「`AdapterBindingBase` が slug を declare する」を「`AdapterBindingBase` が `string Slug { get; }` プロパティを公開し、永続化責任は具象に委ねる」と再解釈する。
- 案 C: Domain asmdef に `UnityEngine.CoreModule` 参照を追加し、`[SerializeField]` の使用のみを許容（既存 Domain ファイルにも `using UnityEngine;` がある先例あり、ただし `SerializeField` 使用は新規）。これは契約を緩めるため要件レビューが必要。

design.md か requirements.md のいずれかを修正してこの矛盾を解消し、その判断を `## Design Decisions` に追記する必要がある。

**Traceability**: Req 1.1, 4.2, 11.1, 12.1, 13.1
**Evidence**: design.md `## Components and Interfaces > Domain Layer > AdapterBindingBase`（L483-540、特に L489, L509）。`Domain asmdef` (`Runtime/Domain/Hidano.FacialControl.Domain.asmdef`) `references` に `Unity.Collections` のみ。

---

### Critical Issue 2: Phase 1 並走期間の挙動契約と slug 衝突防御が未定義

**Concern**: design.md は Option B（二段階移行）を採用し L877, L887 で「Phase 1 では旧 `IFacialControllerExtension` 経路と新 `_adapterBindings` 経路が併存」と明示しているが、以下が未確定:
1. Phase 1 で `_adapterBindings` がフィールドのみ追加され、`FacialController.Initialize` から actually 駆動されるのか、あるいは「フィールドのみ存在し VContainer 配線は Phase 2 まで no-op」なのか。L1236（Migration mermaid）と L877（FacialController 修正記述）で読み解きが分かれる。
2. user が Phase 1 中に旧 OscFacialControllerExtension（既存）を scene に置きつつ、新 SO の `_adapterBindings` に Mock binding を入れた場合、`InputSourceRegistry`（新, slug-keyed）と `InputSourceFactory.RegisterReserved`（旧, reserved id）の両方が同 `LayerInputSourceRegistry` に登録を試み、layer.inputSources[].id 解決が二重経路になる。L887 は「CHANGELOG で『併用しないこと』を明記する」で逃げているが、コード契約として「どちらが勝つか」「衝突時の挙動」が不明。
3. R-E（research.md）の「Phase 1 PR が既存テスト全 green」を維持するためには、新 `AdapterBindings` 経路の `FacialController` driving は Phase 2 まで oprionally OFF にする必要がある。ON/OFF のトグル機構（feature flag / 空 list で skip 等）が design に書かれていない。

**Impact**: tasks.md 生成時に Phase 1 の到達点を決められない。Phase 1 PR で何を merge して何を merge しないかが曖昧になり、PR レビュー粒度の見積りも不能。R-A / R-B（research）の VContainer / SerializeReference smoke test のみ Phase 1 でやって binding 駆動は Phase 2 まで凍結する、というのが最も安全だが、それを明記しないと実装者が独自判断で配線してしまう恐れ。

**Suggestion**: design.md `## Migration Strategy` に Phase 1 / Phase 2 の **動作契約マトリクス**（行: コンポーネント、列: Phase 1 / Phase 2、セル: 「定義のみ」「empty-list 駆動 OK」「fully wired」「deleted」）を追加。具体例:
- Phase 1: `_adapterBindings` フィールド追加、Discovery / ListView / SaveGuard 動作、`AdapterBindingHost` 単体テスト pass、しかし `FacialController.Initialize` から child scope build は **`_adapterBindings.Count == 0` の時だけ skip し、>0 なら新経路ON**、旧 `IFacialControllerExtension` 経路は無条件 ON。
- Phase 2: 旧経路全削除、新経路のみ。

加えて L887 の「両経路併用しないこと」を CHANGELOG だけでなく `FacialController.Initialize` での **runtime warning**（旧 Extension コンポーネントと新 binding が同時検出されたら `Debug.LogWarning`）に格上げすることを推奨。

**Traceability**: Req 6.7, 6.8, 6.9, 8.1-8.5, 10.1（TDD Red-Green-Refactor の Phase 単位での適用）
**Evidence**: design.md `## Migration Strategy`（L1232-1259）、`### FacialController（修正）`（L865-887）、`## Risks & Mitigations` R-E（research.md L257）。

---

### Critical Issue 3: `AdapterBindingHost` の 0-alloc 契約に try/catch + virtual dispatch の検証根拠が薄い

**Concern**: Req 9.1 / 9.5 と design.md L1265「`AdapterBindingHost` の `Tick` / `LateTick` / `FixedTick` 内は try/catch + virtual dispatch のみで allocation を発生させない」と主張しているが、以下が未保証:
1. `Debug.LogError($"[FacialControl] AdapterBindingHost '{_binding.GetType().FullName}' failed in {nameof(...)}: {ex}")` （L720）は **string interpolation で boxing と string アロケーションが発生**する。skip 条件下では call されないが、実 production で 1 binding が単発例外を出した「最初のフレーム」では確実にアロケが起こる。要件 9.5 は「unchanged set of bindings」での steady-state を要求しており「first exception frame」は対象外と読めるが、design に明記されていない。
2. `_skipped` フラグによる skip 後のフレームでも、VContainer は依然として `Tick()` を call する（design L671）。 host 内で `if (_skipped) return;` を実行して 0-alloc であることが必要。これは記述があるが、テストで保証する明示計画は L1219 `AdapterBindingHostAllocationTests` のみで「3 binding × 10 体の steady-state」しか assert していない。
3. `HostGameObject` を含む `AdapterBuildContext` を struct で stack 渡しする方針（L631）だが、`readonly struct` を `in` で渡しても、fields に reference type（`FacialProfile` / `IInputSourceRegistry` / `GameObject`）を含むため struct copy のコストは reference copy のみで OK だが、binding が `OnStart` 内で `ctx.HostGameObject.AddComponent<>()` を呼ぶ箇所（L1002）で `AddComponent` 自体が GC alloc を伴う。これは load-time（OnStart）であり Req 9.1（steady-state）対象外と思われるが、明示されていない。

**Impact**: 実装者が「0-alloc」を厳密に解釈すると、try/catch ブロックの logging 1 行で alloc 検出 test が fail し、fix loop が発生する。逆に緩く解釈すると Req 9.5 の test 実装が骨抜きになる。

**Suggestion**: design.md `## Performance & Scalability` を以下のように具体化:
- 「**Steady-state 0-alloc** は『例外なし・skip フラグ確定後の継続フレーム』に限定する」と明記。
- `AdapterBindingHost.Tick` の擬似コードを記載: `if (_skipped) return; try { _binding.OnTick(deltaTime); } catch (Exception ex) { _skipped = true; Debug.LogError(...); }`。
- `AdapterBindingHostAllocationTests` の検証範囲を「(a) 例外なし steady-state で 0-alloc、(b) skip 確定後 60 フレームで 0-alloc、(c) 例外発生フレームでのアロケは許容範囲（例: < 1KB）」の 3 シナリオに分解する旨を追記。
- 例外メッセージを **キャッシュ済み定数 string + `_binding.GetType().FullName` の cached string** で構築し、interpolation をやめる旨を Implementation Notes に追加（さらに保守的にしたい場合）。

**Traceability**: Req 9.1, 9.5, 10.7, 13.4, 13.5
**Evidence**: design.md `### AdapterBindingHost`（L661-721、特に L671, L720）、`## Performance & Scalability`（L1262-1268）、`### PlayMode Tests #3`（L1219）。

---

## Design Strengths

1. **MultiSourceBlend 既存 0-alloc 資産の温存戦略が明確**: `## Existing Architecture Analysis` と Option C 採用の意思決定（research.md `### Decision: InputSourceFactory を InputSourceRegistry にリネーム`）により、Domain 層 `LayerInputSourceAggregator` / `LayerInputSourceWeightBuffer` / `LayerInputSourceRegistry` を **1 行も変更しない** という線引きが明確。これにより回帰範囲を Adapters 層 glue に局所化でき、Req 5（MultiSourceBlend core 機能昇格）が概念的に既達済みであることを正しく利用している（gap-analysis.md `### Requirement 5` と整合）。
2. **R-1〜R-6 の研究結果が設計判断に直結**: research.md の各 Topic（VContainer lifecycle / SerializeReference null 挙動 / TypeCache caching / per-FC scope cost / AssetModificationProcessor / UI Toolkit ListView / OscReceiver helper 化）すべてが design.md の Decision または Implementation Notes に反映されており、`## Risks & Mitigations` で R-A 〜 R-F として早期 fail 戦略まで明示。設計判断のトレース性が高く、tasks 生成時のリスク見落としを最小化できる。

---

## Final Assessment

### Decision: **Conditional GO（条件付き GO）**

### Rationale
3 件の Critical Issue はいずれも **設計の根幹を覆す欠陥ではなく、tasks 生成前に確定すべき曖昧さ** である。Issue 1（Domain 純度契約矛盾）は要件 / 設計のいずれかを 1 ヶ所修正すれば解決、Issue 2（Phase 1 動作契約）は Migration Strategy セクションへの追記で解決、Issue 3（0-alloc 詳細契約）は Performance セクション拡張と test 計画明示で解決可能。設計の構造（属性駆動コンポジション + VContainer + slug-keyed Registry + 2 段階移行）と、要件カバレッジ（Req 1〜13 + Addendum すべてに Traceability テーブルでマッピング済み）、Phase 分離の妥当性、テスト戦略（EditMode/PlayMode 配置基準遵守、Req 10 全 AC カバー）はいずれも実装に十分な水準にある。

### Next Steps
1. **Issue 1 解決**: `AdapterBindingBase` の配置 layer と `[SerializeField]` 採否を user / lead 判断で確定 → design.md の該当箇所（L489 / L509）と requirements.md（Req 1.1, 4.2, 11.1, 12.1）を整合させる。Domain 純度を最優先するなら案 A（Adapters 層へ移動）を強く推奨。
2. **Issue 2 解決**: design.md `## Migration Strategy` に Phase 1/2 の動作契約マトリクス追加、`### FacialController（修正）` に「並走期の `_adapterBindings` 駆動条件」を明記、`Implementation Notes` に runtime warning 計画を追加。
3. **Issue 3 解決**: design.md `## Performance & Scalability` に steady-state 0-alloc の境界条件を明記、`AdapterBindingHostAllocationTests` の 3 シナリオ分解を Testing Strategy へ反映。
4. 上記 3 件の修正を design.md に反映後、`spec.json.approvals.design.approved = true` にして `/kiro:spec-tasks adapter-binding-architecture` へ進む。Phase 1 の最初のタスクとして R-A（VContainer smoke test）と R-B（SerializeReference round-trip test）を early-fail として配置すること（research.md `## Risks & Mitigations` と整合）。

### Open Questions for Designer
- Issue 1 の解決案 A / B / C のうちどれを採用するか。
- Issue 2 の Phase 1 動作: 「`_adapterBindings.Count == 0` の時だけ駆動 skip」と「Phase 1 全期間で新経路 OFF」のどちらを取るか。前者は user の早期試用が可能、後者はテスト安定性が高い。
- Issue 3 の steady-state 定義: 「例外発生フレームのアロケ許容上限」を performance test で具体値（例: < 256B）として持つか、「skip 確定後のみ 0-alloc」と緩めるか。
