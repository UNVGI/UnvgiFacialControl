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
<!-- Will be generated in /kiro-spec-requirements phase -->
