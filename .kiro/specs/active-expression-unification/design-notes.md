# active 取得 2 系統統合 — 設計メモ（dig 進行中）

長期方針: **B（active 取得を系2ベースに統一）**

## 背景と確認済み事実（コード調査）

### 系1: ExpressionUseCase
- `IActiveExpressionProvider` を実装。`TryGetTopActiveExpression(layer)` を提供。
- フィードされるのは **`FacialController.Activate/Deactivate`（public API）のみ**。
- `LayerUseCase.UpdateWeights` 内で **2 用途**に使われる:
  1. `GetActiveExpressions()` → `LayerExpressionSource`（各レイヤー sourceIdx=0）の遷移値駆動。
  2. `_layerSuppressed` 計算（`Expression.OverrideMask` = layerOverrideMask 抑制）。
- `OverlayInputSource.ResolveSnapshot()` が `TryGetTopActiveExpression(emotionLayer)` で overlay suppress/snapshot 解決。

### 系2: ExpressionTriggerInputSource（ExpressionTriggerInputSourceBase）
- 追加 `IInputSource`（各レイヤー sourceIdx=1+）として `LayerUseCase` に注入。
- フィード元は `ExpressionInputSourceAdapter`（InputSystem）。`ExpressionUseCase.Activate` を呼ばない。
- **重要**: `ExpressionInputSourceAdapter` は内部に keyboard / controller の **2 sink** を持つ。
  → 1 レイヤー内に系2 インスタンスが複数存在しうる（device 別）。
- 各インスタンスが独自 LIFO スタック `ActiveExpressionIds`（public, 末尾=top）を保持。

### 判明した追加の重複（論点を超える発見）
- InputSystem 経路では sourceIdx=0（系1 駆動の LayerExpressionSource）は常に空で、
  実際の表情は sourceIdx=1+（系2）が出力。**系1 の遷移計算は InputSystem 経路で死荷重**。
  → 「active provider が空」だけでなく「遷移計算が二重実装で片方が空回り」。

### OSC 経路
- OSC receiver は **raw BlendShape 値を ValueProvider として書込む**。系1/系2 いずれの
  「表情 identity」も生成しない。→ OSC 由来の表情は active expression として観測不能（現状）。

## 未解決の論点（dig 対象）
1. 系2 複数インスタンスから emotion レイヤー top をどう特定するか（device 跨ぎの順序問題）
2. IActiveExpressionProvider を系2 実装に差替 vs ExpressionUseCase を系2 ファサード化
3. ExpressionUseCase（系1）の存廃 / public Activate-Deactivate / OSC の扱い
4. overlay suppress と layerOverrideMask の共通化点
5. 既存テスト移行（StubActiveProvider）
6. 検証スコープ（①overlay suppress のみ か ①②）

## 決定ログ（2026-06-08 dig 2 ラウンド完了）

### 方向（Round 1）
- active 取得を系2 ベースに統一。「top 1 つ」でなくアクティブ全表情の suppress/mask を集約適用。
- suppress/mask 設定は各 Expression のまま不変（active 判定経路のみ修正、再定義しない）。
- 同 slot 競合の合成ルール、device 跨ぎ順序は後で詰める。

### Round 2
- **Q1=A 後期バインド provider handle**: 空の集約 provider を先に生成→OverlayInputSource に渡す→系2 解決後に `SetSources`。build 順非破壊・OverlayInputSource 不変・循環なし。目的（最小リスク/順序非破壊/両 consumer 供給/OverlayInputSource 不変）ベースで決定。
- **Q2=A 系1 残す**。完全削除は backlog M-25。
- **Q3=B テストを系2(TriggerOn)駆動に書換え** + 系2→provider→OverlayInputSource 統合テスト。
- **Q4=A overlay suppress を先に単独**実装・実機検証。layerOverrideMask の `_layerSuppressed` rewire は follow-up。

### 実装段階（Q4=A）
1. `Layer2ActiveExpressionProvider` 新規（系2 集約、`IActiveExpressionProvider` 実装、後期バインド `SetSources`、profile 保持で id→Expression 解決）。
2. `FacialController` で後期バインド構築。step2 で空 provider を OverlayInputSource に渡し、step3（系2 解決）後に `SetSources((layer, source) 群)`。
3. OverlayInputSource は既存のまま（`TryGetTopActiveExpression` 利用）。まず top 1 つで実機動作確認。全 active 集約は follow-up（合成ルール未決と整合）。
4. テスト: 系2 `TriggerOn` 駆動の統合テスト（系2→provider→OverlayInputSource）。
5. 新 preview → MOVIN_Test 実機検証。
