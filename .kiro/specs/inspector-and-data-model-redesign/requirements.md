# Requirements Document

## Project Description (Input)
FacialCharacterSO の Inspector UX 改善とデータモデル簡素化のための破壊的改修。Expression を AnimationClip 参照ベースに刷新し（Name=ファイル名、Id=GUID 自動生成、Layer=ドロップダウン、TransitionDuration / TransitionCurve / BlendShapeValues / RendererPath / BonePose は AnimationClip から自動取得・吸収）、LayerSlot を「Override 対象レイヤーのビットフラグ」に簡素化、RendererPath 手入力欄を廃止、BonePose 独立データを AnimationClip に統合、アナログバインディングの AnalogMappingFunction (deadzone/scale/offset/curve/invert/clamp) を InputAction Processor 化して AnalogBindingEntry を InputActionReference + targetIdentifier + targetAxis のみに簡素化、ExpressionBindingEntry の Category (Controller/Keyboard) を InputAction binding の device class から自動推定するように廃止、KeyboardExpressionInputSource / ControllerExpressionInputSource を単一 ExpressionInputSourceAdapter に統合。preview 段階のため schema 互換性は破壊する。Runtime は引き続き JSON ベースで動作させ、Editor 保存時に AnimationClip → 中間 JSON 表現にサンプリングして焼き込む。

## Requirements
<!-- Will be generated in /kiro-spec-requirements phase -->
