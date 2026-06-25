# Requirements Document

## Project Description (Input)
feature 名候補: burst-blendshape-output（または animationjob-blendshape-output）

## 目的
3D キャラクターの BlendShape 出力経路を、現在の「main thread で blend 計算 → C# で SkinnedMeshRenderer.SetBlendShapeWeight 直書き」から、PlayableGraph + AnimationScriptPlayable + IAnimationJob（Burst）ベースの正統な Animation 出力経路に作り直す。最終目標は 10 体同時制御の性能要件と「毎フレーム GC ゼロ」を満たすこと。

## 現状（作り直しの出発点）
- ランタイムのブレンド計算は `LayerUseCase`（Application）→ `LayerInputSourceAggregator` / `LayerBlender` / `LayerExpressionSource`（Domain, 通常 C#）で行い、`FacialController.LateUpdate` が `BlendedOutputSpan` を読んで `SkinnedMeshRenderer.SetBlendShapeWeight` に直書きしている。
- `PlayableGraph`（`PlayableGraphBuilder` / `FacialControlMixer` / `LayerPlayable` / `AnimationPlayableOutput`）は構築・Play されるが、`FacialControlMixer` は `PlayableBehaviour`（ScriptPlayable）で AnimationStream に一切書かず、出力 NativeArray は誰にも読まれない＝事実上デッド。コミット 296ef96（2026-04-22, Multi Source Blend 導入時）に「PlayableGraph の出力はバイパスする（preview.2 以降で撤去検討）」とコメント。
- IAnimationJob による AnimationStream 書込みは過去一度も実装されておらず、spec 上は bone-control の BoneWriter 文脈で preview.2 以降に先送り（backlog M-8）。

## 今回のスコープ（採用方針）
- **blend 全体を Burst 化する**。Domain の aggregate/blend ロジック（`LayerInputSourceAggregator` / `LayerBlender` / `LayerExpressionSource` の遷移計算）を Burst 互換の struct + NativeArray に移植し、`IAnimationJob.ProcessAnimation(AnimationStream)` 内で aggregate + blend + stream への BlendShape 書込みまで完結させる。
- 既存の `LayerInputSourceWeightBuffer`（任意スレッド書込み・SwapIfDirty）/ per-layer 複数入力ソース集約 / layerOverrideMask 抑制 / overlay suppress / lipsync phoneme overlay / ARKit-PerfectSync 自動生成 などの既存挙動を回帰させないこと。
- backlog M-8 の方針「インターフェース設計で Jobs/Burst 差替え可能」に沿い、Job 化後も差替え可能な抽象を保つ。

## スコープ外（今回やらない）
- BoneWriter のボーン直書きの IAnimationJob / OnAnimatorIK 化（bone-control 側で別途 preview.2 以降に再評価済み）。今回は BlendShape 出力経路のみ。
- Timeline 統合本体（将来対応）。ただし AnimationScriptPlayable ベースに寄せることで将来の Timeline 連携と矛盾しない設計にはする。

## 主要な論点（dig で詰めたい）
- clean architecture との整合: Domain は現状 Unity 非依存方針だが、Burst 化で Unity.Collections / Unity.Burst 依存が Domain に入る。これを Domain に許容するか、Adapters 側に Burst 実装を置き Domain は純ロジックの仕様だけ持つか。
- 既存 PlayableGraph 資産（FacialControlMixer / LayerPlayable）を作り直すか撤去して新規 AnimationScriptPlayable グラフにするか。
- 文字列キー（BlendShape 名）解決を job 外（事前 index 解決）に追い出す方法、NativeArray のライフサイクル / Persistent 確保、複数キャラ（10 体）での job スケジューリング単位。
- TransitionCurve（カスタムカーブ/イージング）の Burst 内評価方法。
- 既存 EditMode/PlayMode テスト資産との整合・回帰検証方針。

## Introduction
本仕様は、FacialControl コアパッケージ（`com.hidano.facialcontrol`）の BlendShape 出力経路を、現行の「メインスレッドでの blend 計算 → C# による `SkinnedMeshRenderer.SetBlendShapeWeight` 直書き」から、`PlayableGraph` + `AnimationScriptPlayable` + `IAnimationJob`（Burst コンパイル）ベースの正統な Animation 出力経路へ作り直すことを目的とする。Domain の aggregate / blend ロジックを Burst 互換の struct + `NativeArray` に移植し、`IAnimationJob.ProcessAnimation(AnimationStream)` 内で「集約 → ブレンド → AnimationStream への BlendShape 書込み」までを完結させる。最終目標は、同時 10 体以上のキャラクター制御という性能要件と「毎フレームの GC アロケーションゼロ」を満たしつつ、既存機能（マルチソース／レイヤー別ブレンド、`layerOverrideMask` 抑制、overlay suppress、リップシンク音素 overlay、ARKit/PerfectSync 自動生成、`LayerInputSourceWeightBuffer` 経由のランタイム重み書込み、`TransitionCurve` 評価）を一切回帰させないことである。

本フェーズではアーキテクチャ上の実装方式（Burst 実装を Domain と Adapters のどちらに置くか、既存 PlayableGraph 資産を作り直すか撤去するか、Job のスケジューリング単位など）は確定させず、満たすべき振る舞い・制約・性能 NFR・非回帰要件のみを定義する。具体的な設計判断は design フェーズで行う。

## Boundary Context
- **In scope（本仕様の対象）**:
  - BlendShape 出力経路の `AnimationScriptPlayable` + `IAnimationJob`（Burst）化
  - aggregate / blend / AnimationStream 書込みの Job 内完結
  - 既存挙動（マルチソース集約・レイヤー別ブレンド・各種抑制・overlay・自動生成・ランタイム重み書込み・遷移カーブ評価）の非回帰維持
  - 性能 NFR（毎フレーム GC ゼロ・同時 10 体以上）の達成
- **Out of scope（本仕様で扱わない）**:
  - BoneWriter のボーン直書きの `IAnimationJob` / `OnAnimatorIK` 化（bone-control 側の別仕様。backlog M-8）
  - Timeline 統合本体（将来対応）
  - 音声解析・リップシンクの音素検出ロジック（外部プラグイン入力を受けるインターフェースのみ既存維持）
- **Adjacent expectations（隣接システム／仕様への期待）**:
  - BoneWriter 経路は本仕様の変更後も従来どおり動作し続けること（BlendShape 経路の作り直しがボーン制御を破壊しないこと）
  - `AnimationScriptPlayable` ベースへ寄せることで、将来の Timeline 連携と矛盾しない構造を保つこと
  - backlog M-8 の方針「インターフェース設計で Jobs/Burst 差替え可能」を維持すること

## Open Questions and Decisions (Dig)

本セクションは dig 面談で確定した設計方向の決定を記録する。各決定は design フェーズの制約となる。

| ID | トピック | 決定 | 根拠 | リスク | 関連要件 |
|----|---------|------|------|--------|---------|
| D-1 | 既存 PlayableGraph 資産の扱い | `FacialControlMixer` / `LayerPlayable` / 現行 `PlayableGraphBuilder` を撤去し、`AnimationScriptPlayable` ベースの新規グラフを新設する。旧経路との並走期間は設けない | デッドコードを残さず最小構成にする。preview 段階・破壊的変更許容のためハードカット可 | 中: 既存テスト/参照の撤去作業。旧経路に依存した検証資産の差し替えが必要 | Req 1.3, 1.4, 7.4 |
| D-2 | Burst 化のスコープ | 出力経路（`AnimationScriptPlayable` + `IAnimationJob`）に加え、aggregate / blend / 遷移計算を **Job 内 Burst 化**する（フルスコープを維持）。Burst は本スペックの必須要件 | 同時 10 体制御の性能を最大化する判断。M-8 の「将来差し替え」ではなく本スペックで踏み込む。Burst が出力経路自体に必須ではない点（writer-only でも目的達成可）は確認したうえで、性能優先で B を選択 | 大: blend ロジックの struct 移植・`TransitionCurve` の Burst 内評価（LUT 等）など実装規模が大きい | Req 2 全体, Req 6.4 |

## Requirements

### Requirement 1: AnimationStream 経由の BlendShape 出力経路
**Objective:** Unity エンジニア（パッケージ利用者）として、BlendShape の最終出力を正統な Animation 出力経路（`AnimationScriptPlayable` + `IAnimationJob`）で行いたい。これにより、Animation システムと整合した出力と将来の Timeline 連携への適合性を得たいからである。

#### Acceptance Criteria
1. When キャラクターの表情出力が実行されるとき、the BlendShape Output System shall 最終的な BlendShape 重みを `IAnimationJob.ProcessAnimation(AnimationStream)` 内から `AnimationStream` 経由で書き込む。
2. The BlendShape Output System shall メインスレッドからの `SkinnedMeshRenderer.SetBlendShapeWeight` 直書きを BlendShape 出力経路として用いない。
3. When 出力経路が構築されるとき、the BlendShape Output System shall `PlayableGraph` 上で `AnimationScriptPlayable` を経由して `AnimationPlayableOutput` へ接続する。
4. The BlendShape Output System shall 現在デッド状態の `FacialControlMixer`（AnimationStream へ書かない `ScriptPlayable`）のバイパス挙動を解消し、出力結果が実際に `AnimationStream` へ反映される状態にする。
5. While 出力経路が動作している間、the BlendShape Output System shall BoneWriter によるボーン制御経路を従来どおり機能させ続ける。

### Requirement 2: aggregate / blend パイプラインの Burst 化
**Objective:** Unity エンジニアとして、レイヤー集約・ブレンド・遷移計算を Burst コンパイル済み Job 内で完結させたい。これにより、同時多体・毎フレーム処理での CPU コストと GC 圧を最小化したいからである。

#### Acceptance Criteria
1. The BlendShape Output System shall レイヤー集約（`LayerInputSourceAggregator` 相当）・ブレンド（`LayerBlender` 相当）・遷移計算（`LayerExpressionSource` 相当）のロジックを Burst 互換の struct + `NativeArray` で表現する。
2. When 1 フレームの BlendShape 出力が計算されるとき、the BlendShape Output System shall 集約・ブレンド・`AnimationStream` への書込みを同一の `IAnimationJob` 内で完結させる。
3. The BlendShape Output System shall Job 内で参照するデータを Burst 非対応の管理ヒープ型（マネージドオブジェクト・`string` 等）に依存しない形で保持する。
4. When BlendShape 名（文字列キー）から出力先インデックスを解決する必要があるとき、the BlendShape Output System shall その解決を Job 実行前（事前 index 解決）に完了させ、Job 内では文字列解決を行わない。
5. While Job が稼働している間、the BlendShape Output System shall 集約結果がレイヤー優先度・カテゴリ内排他方式（LastWins / Blend）の既存仕様どおりに合成されることを保証する。

### Requirement 3: ハイブリッド入力モデルとランタイム重み書込みの非回帰
**Objective:** Unity エンジニアとして、D-1 ハイブリッド入力モデル（ExpressionTrigger + ValueProvider）と任意スレッドからのランタイム重み書込みを、出力経路作り直し後も従来どおり使い続けたい。これにより、既存の入力連携コードを変更せずに済ませたいからである。

#### Acceptance Criteria
1. The BlendShape Output System shall per-layer の複数入力ソース集約（weighted-sum → clamp01）を既存仕様どおりに維持する。
2. When 任意スレッドから `LayerInputSourceWeightBuffer` に重みが書き込まれるとき、the BlendShape Output System shall その書込みを既存の SwapIfDirty セマンティクスで安全に取り込む。
3. When `LayerInputSourceWeightBuffer` の重みが更新された次の出力フレームになるとき、the BlendShape Output System shall 更新後の重みを集約・ブレンド結果に反映する。
4. The BlendShape Output System shall ExpressionTrigger（バイナリのスタックベース）と ValueProvider（直接値書込み）の双方を入力ソースとして従来どおり受け付ける。

### Requirement 4: 抑制・オーバーレイ機能の非回帰
**Objective:** Unity エンジニアとして、レイヤー抑制とオーバーレイ系機能（`layerOverrideMask` 抑制・overlay suppress・リップシンク音素 overlay）を出力経路作り直し後も同一の結果で得たい。これにより、既存プロファイルや配信セットアップが破綻しないようにしたいからである。

#### Acceptance Criteria
1. While `layerOverrideMask` による抑制が有効な間、the BlendShape Output System shall 対象レイヤーの寄与を既存仕様どおりに抑制する。
2. While overlay suppress が有効な間、the BlendShape Output System shall 抑制対象の表情寄与を既存仕様どおりに抑制する。
3. When リップシンク音素 overlay が適用されるとき、the BlendShape Output System shall 該当 BlendShape へ既存仕様どおりに音素 overlay の重みを反映する。
4. The BlendShape Output System shall 抑制・overlay の適用結果が、作り直し前の出力経路と同一の最終 BlendShape 重みになることを保証する。

### Requirement 5: ARKit / PerfectSync およびプロファイル仕様の非回帰
**Objective:** Unity エンジニア（VTuber 配信向け利用者）として、ARKit 52 / PerfectSync の自動検出・プロファイル自動生成と BlendShape 命名規則の自由度を、出力経路作り直し後も完全に維持したい。これにより、フェイシャルキャプチャ連動が引き続き正しく動作するようにしたいからである。

#### Acceptance Criteria
1. The BlendShape Output System shall ARKit 52 / PerfectSync の自動検出およびプロファイル自動生成の既存挙動を維持する。
2. If モデルに存在しない（未対応の）BlendShape パラメータが与えられるとき、then the BlendShape Output System shall それを警告なしでスキップする。
3. The BlendShape Output System shall BlendShape 命名規則を固定せず、2 バイト文字・特殊記号を含む BlendShape 名を正しく扱う。
4. The BlendShape Output System shall プロファイル（基本 AnimationClip + カテゴリ + リップシンク用 Clip + 遷移時間）に基づく表情合成の既存仕様を維持する。

### Requirement 6: 遷移補間と TransitionCurve の Burst 内評価
**Objective:** Unity エンジニアとして、表情遷移の線形補間・イージング・カスタムカーブによる上書きを、Burst Job 内でも従来どおりの結果で評価したい。これにより、表情の動きの質感が作り直しで変化しないようにしたいからである。

#### Acceptance Criteria
1. The BlendShape Output System shall 遷移補間を既定の線形補間で行い、`TransitionCurve`（イージング／カスタムカーブ）が指定された場合はそれで上書きする。
2. The BlendShape Output System shall 遷移時間 0〜1 秒（既定 0.25 秒）の範囲を既存仕様どおりに扱う。
3. When 遷移中に新しい表情がトリガーされるとき、the BlendShape Output System shall 現在の補間値から即座に新しい遷移を開始する。
4. While Burst Job 内で遷移が評価される間、the BlendShape Output System shall `TransitionCurve` の評価結果を Burst 非対応のマネージド `AnimationCurve` 直接評価に依存せず取得する。
5. The BlendShape Output System shall 遷移補間の出力が、作り直し前の出力経路と等価な結果になることを保証する。

### Requirement 7: 性能要件（毎フレーム GC ゼロ・同時 10 体以上）
**Objective:** Unity エンジニアとして、出力経路を毎フレーム GC アロケーションゼロかつ同時 10 体以上で成立させたい。これにより、配信・ゲーム実行中の GC スパイクとフレーム落ちを回避したいからである。

#### Acceptance Criteria
1. While 定常運転（毎フレームの出力処理）が継続している間、the BlendShape Output System shall マネージドヒープのアロケーションを発生させない（毎フレーム GC アロケーションゼロ）。
2. The BlendShape Output System shall 同時 10 体以上のキャラクターの BlendShape 出力を成立させる。
3. The BlendShape Output System shall Job で使用する `NativeArray` 等のネイティブ確保を `Allocator.Persistent` 等で一度確保し、毎フレームの再確保を行わない。
4. When キャラクターまたは出力経路が破棄されるとき、the BlendShape Output System shall 確保済みのネイティブメモリと `PlayableGraph` リソースを解放する。

### Requirement 8: 差替え可能な抽象とクリーンアーキテクチャ整合
**Objective:** Unity エンジニアとして、Burst 化後も出力経路実装を差替え可能な抽象として保ち、クリーンアーキテクチャの依存方向契約を維持したい。これにより、将来の最適化や代替実装への置き換えと、パッケージのアーキテクチャ品質を両立させたいからである。

#### Acceptance Criteria
1. The BlendShape Output System shall Job 化後も backlog M-8 の方針に沿った差替え可能なインターフェース抽象を提供する。
2. The BlendShape Output System shall `Unity.Animation` 等の Engine 機能への依存を Adapters 層に封じ込め、asmdef の依存方向契約（Adapters → Application → Domain）を破らない。
3. The BlendShape Output System shall レンダーパイプライン非依存・シェーダー非依存・物理演算非依存の契約を維持する。
4. The BlendShape Output System shall エラーハンドリングを Unity 標準ログ（`Debug.Log/Warning/Error`）の範囲で行い、標準例外を超えるカスタム例外型を新設しない。

### Requirement 9: テスト整合と回帰検証
**Objective:** Unity エンジニアとして、既存の EditMode / PlayMode テスト資産と整合した形で作り直しの回帰検証を行いたい。これにより、TDD（Red-Green-Refactor）を維持しつつ非回帰を客観的に保証したいからである。

#### Acceptance Criteria
1. The BlendShape Output System shall 既存の集約・ブレンド・遷移に関するドメインロジックを、EditMode（モック／Fake のみ・同期実行）で検証可能な形に保つ。
2. The BlendShape Output System shall `AnimationStream` 書込み・`PlayableGraph` 実行・フレーム同期を要する挙動を、PlayMode テストで検証可能な形に保つ。
3. When 作り直し後の出力結果を検証するとき、the BlendShape Output System shall 既存挙動（マルチソース集約・抑制・overlay・遷移補間）に対する非回帰を確認できるテストを伴う。
4. The BlendShape Output System shall 毎フレーム GC アロケーションゼロを検証可能な計測手段（例: アロケーション計測テスト）を伴う。
