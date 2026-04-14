# セッション引き継ぎノート (2026-04-14)

## 今回やったこと

- プロジェクト状況の調査と現状レポート作成（P22 進行中、preview.1 リリース直前）
- **P22-06 完了**: EditMode 695/695 + PlayMode 240/240 全 Green を確認
- Unity Editor での動作確認手順をユーザーに案内（SampleScene + Donut-Chan）
- **Editor UI フォント修正**: 8 箇所の `fontSize` / `unityFontStyleAndWeight` 指定を削除して標準フォントに戻した（EditMode 695 Passed 再確認済み）
- VSeeFace OSC 接続方法を回答（`/avatar/parameters/{name}` 形式、`receivePort` 設定）
- **P23（キーコンフィグ永続化）を kiro spec へ移行**:
  - spec 名: `input-binding-persistence`
  - 4 フェーズ全て承認済み（requirements / design / tasks）
  - `ready_for_implementation: true`
- `docs/work-procedure.md` に P23 セクション追加、`docs/requirements-qa.md` に Q8-3 追加

## 決定事項

- **P23 を preview.1 スコープに含める**（リリース順序: P23 完了 → P22-04 再実行 → P22-06 再実行 → P22-07〜10）
- **設計判断 1-C**: `InputSystemAdapter` を MonoBehaviour → 純粋 C# クラス + `IDisposable` へリファクタ（破壊的変更、CHANGELOG 明記）
- **設計判断 2-C**: `InputBindingProfileSOEditor` が `JsonFilePath` から JSON を直接 1 度パース → ローカルキャッシュ + 手動リフレッシュボタン。`FacialProfileSO` 本体は完全不変
- **設計判断 3-B**: サンプルは `NewFacialProfile.asset` から独立。専用の最小 `sample_profile.json` + `SampleFacialProfileSO.asset` + `SampleInputBinding.asset` を新設
- **タスク 8 追加**: ドキュメント更新（quickstart / README / CHANGELOG）を独立タスク化して Req 6 を完全カバー

## 捨てた選択肢と理由

- **1-A (GetComponent)** / **1-B (AddComponent 内部生成)**: ユーザーが MonoBehaviour 2 個アタッチする煩雑さ、Inspector 隠蔽の不透明さで却下。1-C で破壊的変更を許容
- **2-A (Inspector 描画ごとに JSON パース)**: I/O 過多で却下
- **2-B (FacialProfileSO に CachedProfile プロパティ追加)**: 一時採用したが Boundary 違反のため 2-C に変更
- **3-A (既存 NewFacialProfile.asset を流用)**: ユーザーが Profile 差し替え時に動かなくなる依存問題で却下
- **3-C (空テンプレ提供)**: 「キー1 で実際にまばたきする」が自動で確認できないため却下
- **work-procedure.md ベース継続**: kiro spec の段階的 approval / TDD 強制のメリットを重視して移行
- **/kiro:spec-impl 個別実行**: CLAUDE.md グローバルルールに従い `/kiro:spec-run` バッチ実行を採用

## ハマりどころ

- kiro skill (`/kiro:spec-design`, `/kiro:spec-tasks`) の引数 placeholder が未置換で `-y` を feature name と誤認識。Agent 直接呼び出しで回避
- requirements 初版で `InputSystemAdapter` の変更を Out of Boundary と書いていたが、design 判断 1-C で In Scope に修正が必要だった
- design.md に Boundary（FacialProfileSO 不変）と Implementation Notes（CachedProfile 追加）の矛盾あり → 選択 C で解消
- spec-tasks の初回生成で **Req 6（ドキュメント更新）が漏れていた** → タスク 8 を後付けで追加

## 学び

- kiro spec 移行は **明示的な approval gate** が複雑な仕様変更（破壊的 API 変更含む）の整合性管理に有効
- TDD タスク分解（Red-Green ペアを tasks.md に明示）が実装フェーズの方向性を統一する
- 「捨てた選択肢と理由」を Boundary や Adjacent expectations に書き残すと、後フェーズで同じ議論を蒸し返さずに済む
- 要件 → 設計 → タスクの各フェーズで **要件カバレッジを機械的に検証**する習慣が必要（タスク 8 漏れの教訓）

## 次にやること

### [HIGH] 1. 新セッションで P23 実装フェーズを開始
```
# /clear で context リセット後、新セッションで以下を実行:
/kiro:spec-status input-binding-persistence  # ready 確認
/kiro:spec-run input-binding-persistence     # バッチ実行
```

### [HIGH] 2. P23 完了後、preview.1 リリース手順を実行
tasks.txt の残タスクを順次:
- `P22-04` CHANGELOG 更新（P23 内容を反映）
- `P22-06` 全テスト最終通過確認（P23 後の再実行）
- `P22-07` リリース日更新
- `P22-08` `npm view com.hidano.uosc` 公開確認
- `P22-09` `npm publish` で `com.hidano.facialcontrol@0.1.0-preview.1` 公開
- `P22-10` `v0.1.0-preview.1` タグ作成

### [MEDIUM] 3. P24: プレビューカメラを SceneViewStyleCameraController へ置き換え
- ユーザーフィードバック 3 番目（未着手）
- npm `com.hidano.scene-view-style-camera-controller` 公開済み（ScopedRegistries 経由で取得可）
- 統合方針: `OrbitHandler` / `PanHandler` 等の `public static` ハンドラーを `PreviewRenderWrapper` から流用（MonoBehaviour 部分は使わない）
- preview.1 後に着手でも可

### [LOW] 4. メモリ更新
- `memory/MEMORY.md` に kiro spec ワークフローの実例として `input-binding-persistence` の進め方を追記すると将来参照しやすい

## 関連ファイル

### Spec ドキュメント
- `.kiro/specs/input-binding-persistence/spec.json` (phase: ready)
- `.kiro/specs/input-binding-persistence/requirements.md` (7 Req / 32 AC)
- `.kiro/specs/input-binding-persistence/design.md` (710 行)
- `.kiro/specs/input-binding-persistence/tasks.md` (8 メジャー / 17 サブ)

### プロジェクトドキュメント更新
- `docs/work-procedure.md` (P23 セクション追加、依存関係図更新)
- `docs/requirements-qa.md` (Q8-3 追加)
- `tasks.txt` (P23-1.1〜8.3 + P22-04, 06, 07, 08, 09, 10)

### フォント修正済み Editor ファイル
- `FacialControl/Packages/com.hidano.facialcontrol/Editor/Inspector/FacialProfileSOEditor.cs`
- `FacialControl/Packages/com.hidano.facialcontrol/Editor/Windows/ARKitDetectorWindow.cs`
- `FacialControl/Packages/com.hidano.facialcontrol/Editor/Windows/ProfileCreationDialog.cs`
- `FacialControl/Packages/com.hidano.facialcontrol/Editor/Tools/ExpressionCreatorWindow.cs`

### P23 で触る予定のファイル（実装フェーズ）
- `Runtime/Domain/Models/InputBinding.cs` (新規)
- `Runtime/Adapters/Input/InputSystemAdapter.cs` (リファクタ)
- `Runtime/Adapters/Input/FacialInputBinder.cs` (新規)
- `Runtime/Adapters/ScriptableObject/InputBindingProfileSO.cs` (新規)
- `Editor/Inspector/InputBindingProfileSOEditor.cs` (新規)
- `Assets/Samples/SampleScene.unity` (更新)
- `Assets/Samples/TestExpressionToggle.cs` (削除)
- `Assets/Samples/SampleFacialProfileSO.asset` (新規)
- `Assets/Samples/SampleInputBinding.asset` (新規)
- `Assets/StreamingAssets/FacialControl/sample_profile.json` (新規)
- `Documentation~/quickstart.md` (更新)
- `README.md` / `CHANGELOG.md` (パッケージ内、更新)
