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

## Requirements
<!-- Will be generated in /kiro-spec-requirements phase -->
