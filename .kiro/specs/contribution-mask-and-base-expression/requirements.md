# Requirements Document

## Project Description (Input)
BlendShape 単位の contribution mask による layer 合成と、 SO root に持つベース表情 (Base Expression) を導入する。これにより emotion (controller 由来 Digital expression) と LipSync の合成が「contribute する index は上書き、 contribute しない index は下位 layer / ベース表情の値が残る」 で自然に動作するようにする。

### 背景

現状の `LayerBlender.Blend` は per-blendshape index で `output[i] = lerp(output[i], values[i], weight)` を実行するため、 高優先度 layer (例: lipsync) の weight=1.0 で blend すると、 当該 layer が contribute していない index (例: 目) も lerp の 0 補間で消えてしまう。 これにより emotion + lipsync を素直に合成できない。

`layerOverrideMask` は Inspector で設定可能だが Phase 3.4 で導入予定の `ExpressionResolver` 経由で解釈される設計で、 現状は無視されている。

ユーザー (FacialControl ライブラリ開発者本人) の意向: 加算 + cap 1.0 ではなく「上書き合成 + 自分が値を持つ index しか上書きしない」 方針を採用。 さらに常に怒った顔のキャラや衣装で特定 BlendShape をロックしたいユースケースに対応するため、 ベース表情の概念を SO root に導入する。

### 実装方針 (案 D + ベース表情)

#### R1. BlendShape contribution mask
- `IInputSource` に「自分が contribute する BlendShape index 集合」を取得する API を追加 (BitArray か ReadOnlySpan<int>)
- `ExpressionTriggerInputSource` / `LipSyncInputSource` (`ULipSyncProvider`) / `AnalogBlendShapeInputSource` 各実装で contribute index を提供
- `LayerInputSourceAggregator` で source 群の mask を OR 集約して layer 全体の contribute mask を構築
- `LayerPlayable` / `LayerBehaviour` の出力に mask を併走させる
- `LayerBlender.Blend` を「mask 立っている index のみ lerp 上書き、 立ってない index は下位 layer の値を保持」に変更

#### R2. ベース表情 (Base Expression)
- `FacialCharacterProfileSO` (root) に `_baseExpression` を追加。 1 個の AnimationClip 参照 + bake 後の per-blendshape weight 配列
- `FacialControlMixer.ComputeOutput` で layer 合成前に出力バッファをベース表情の値で初期化 (現状は Array.Clear で 0 初期化)
- ベース表情 AnimationClip が null の場合は全 BlendShape 0 で初期化 (= 現状互換)
- ベース表情用の AnimationClip サンプリングは既存 `AnimationClipExpressionSampler` を流用

#### R3. AnimationClip からの contribute mask 抽出と bake (Editor)
- `AnimationClipExpressionSampler` に「curve binding に含まれる BlendShape 名集合」を返す API を追加
- Editor 自動保存時に Expression / Base Expression の AnimationClip から bake
- bake 結果は `ExpressionSnapshot` / `BaseExpressionSnapshot` 等のメタデータに保存

#### R4. Inspector UI
- `FacialCharacterProfileSOInspector` の最上位に「ベース表情」セクションを追加
- AnimationClip ObjectField + bake された BlendShape weight プレビュー (任意)
- ベース表情未設定時の説明 HelpBox

#### R5. JSON schema bump
- profile.json に `baseExpression` フィールドを追加
- 既存 profile が `baseExpression` 欠如時は全 0 base として読み込む (forward compat)
- migration ツールは preview 段階につき不要 (ユーザー報告: 既存ユーザーゼロ)

#### R6. テスト
- `LayerBlenderTests`: contribute mask 解釈の挙動 (mask 全立て = 旧 lerp 互換、 mask 部分立て = base / 下位 layer 保持)
- `LayerInputSourceAggregatorTests`: source mask の OR 集約
- `FacialControlMixerTests`: ベース表情からの初期化
- `AnimationClipExpressionSamplerTests`: curve binding からの contribute index 抽出
- emotion + lipsync 合成シナリオの統合テスト

### 非スコープ (別 spec / backlog)

- アナログ入力 Weight 値による Expression Lerp 駆動 (S-7)
- AdapterBinding Slug ドロップダウン UI (S-8)
- Phase 3.4 `ExpressionResolver` + `layerOverrideMask` 解釈 (引き続き別 spec で並行設計)
- 衣装毎に複数 BaseExpressionVariant を持つ拡張 (preview.2 以降)
- 「強制ピン (上位 layer から上書きされない BlendShape)」 概念 (将来検討)

### 制約 / 設計原則

- preview 段階のため破壊的変更を許容
- 既存 lerp 挙動との互換性は「mask 全立て + base 全 0」 で担保
- ヒープ確保ゼロ目標 (per-frame) を維持。 mask 用の bool[] / BitArray は preallocated
- Domain 層は UnityEngine 非依存を維持
- `IInputSource` に追加するメンバーは breaking change だが preview 許容

### 期待されるユーザー体験

1. ユーザーが Inspector で Layer A (emotion, priority=0) と Layer B (lipsync, priority=1) を作る
2. Layer A の Expression は AnimationClip で目・眉・口を駆動、 Layer B の LipSync は phoneme entry で口関連 BlendShape のみ駆動
3. 実行: emotion 表情を発火しつつ lipsync を再生
4. 結果: 目・眉は emotion の値、 口は lipsync で上書き、 contribute されない index は base (デフォルト全 0) のまま
5. 怒り顔キャラを作る場合: SO の baseExpression に「怒り眉・怒り目」 を含む AnimationClip をアサインすれば、 Expression 発火していない index は怒り顔のまま残る

## Requirements
<!-- Will be generated in /kiro-spec-requirements phase -->
