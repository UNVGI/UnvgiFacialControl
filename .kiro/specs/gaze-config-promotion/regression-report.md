# Regression Report — preview.2 体験不具合 / 2026-05-06

## 背景

`gaze-config-promotion` spec の Phase 1〜4（commits `e99e901`〜`46bd0e7`）完了直後の手動検証で、Hidano が以下 4 件の体験面不具合を報告。本ドキュメントは原因と再発防止策をまとめる。

## 対応した不具合と原因

### 1. GazeConfigs 行 UI が横長で視認不能

**症状**: `FacialCharacterProfileSO` Inspector の GazeConfigs セクションで、各行が「目線 / 左目 / 右目 / 上 / 下 / 外 / 内 / 左Clip / 右Clip / 上Clip / 下Clip / 自動設定 / 削除」を 1 行に詰め込んだ結果、Inspector 幅で水平方向にクリップされ、現在値が一切表示されない。

**原因**: commit `1107947` (`3.2 GazeConfigs セクションの opt-in 追加 UX と単行 UI を実装する`) で導入された「単行 UI」設計。tasks/design に "単行 UI" が要求として明示されていたが（design.md `## GazeConfigs Section / Single-row layout`）、実 Inspector 幅（300px 前後）で 12 フィールドを並べる現実性を検討した形跡が無い。`width: 48` の FloatField はラベル文字「上」と数値の両方を表示しきれず、値が見えなくなった。

**修正**: `FacialCharacterProfileSOInspector.BuildGazeConfigRow` を `FlexDirection.Row` → `Column` 化し、各フィールドの `style.width` 指定を撤去。ヘッダ行（Expression 名 + 自動設定 + 削除ボタン）のみ横並びに残し、編集フィールド本体は Unity デフォルトの縦並び（label-left / value-right）に戻した。

### 2. Gaze 入力バインディングが `InputActionReference` 直接選択

**症状**: `_gazeInputBindings` の各行が `ObjectField(InputActionReference)` を表示。`InputActionAsset` のサブアセットを直接ドラッグ＆ドロップ or アセットピッカーで選ばせる UI で、他の Expression バインディング（`_expressionBindings`）の「ActionMap → Action 名 dropdown」UX と乖離。

**原因**: commit `0d981ab` (`2.1 InputSystemGazeBinding を新設`) で `InputSystemGazeBinding.inputActionRef: InputActionReference` を採用。runtime では `binding.inputActionRef.action.name` から action 名を取り出して `InputActionMap.FindAction(name)` する形だったため、データを保持するうえで `InputActionReference` である必然性は無く、「string actionName」で十分だった。design.md でも UX 一貫性は要件化されておらず、純粋な設計見落とし。Phase 3.7 の drawer 実装も「新フィールドに合わせて差し替える」方針で、UX 検討が先送りされた。

**修正**: 
- `InputSystemGazeBinding.inputActionRef: InputActionReference` → `actionName: string` に変更（`ExpressionBindingEntry.actionName` と同型）。
- `InputSystemAdapterBinding.BuildAnalogSources` / `TryFindAnalogSource` を新フィールドに合わせて簡素化（`.action.name` lookup を削除）。
- `InputSystemAdapterBindingDrawer` の `CreateGazeInputBindingRow` で `ObjectField` → `DropdownField` 化し、`CollectActionNames` を再利用して binding 直下の ActionMap から候補を構築。
- 関連テスト（`InputSystemGazeBindingTests` フィールド検査、`InputSystemAdapterBindingIntegrationTests` セットアップ）と Sample asset YAML を同期。

### 3. Sample 遷移時間が 0.1s に統一されていない

**症状**: Sample profile（`MultiSourceBlendDemoCharacter.asset` / `profile.json`）の `transitionDuration` が表情ごとに 0.25 / 0.25 / 0.2 / 0.1 / 0 とバラついた状態で commit。Hidano が以前統一したはずの 0.1s 統一が消失。

**原因**: commits `9e470ad` (`4.2 Sample profile.json 2 ファイルを新スキーマに同期`) と `08ef1fa` (`4.1 Sample asset 2 ファイルを新スキーマに同期`) でスキーマ移行の際、値の出所が古い snapshot（schema v1 時点で個別に設定されていた値）からコピーされた。新スキーマに sync する作業として「フィールド名・構造の同期」だけが意識され、「過去に user が手動でバランス調整した値（0.1s 統一）の保全」は意識されなかった。

**修正**: 4 ファイル（dev `Assets/Samples/.../*.asset` & `*.json`、UPM `Samples~/.../*.asset` & `*.json`）の `transitionDuration` を全て 0.1 に書き換え。

### 4. 動作モード EnumField が Toggle に強制リセット

**症状**: Expression Binding 行の「動作モード」 EnumField で Hold を選ぶと、保存後に Toggle に戻る。

**原因**: `InputSystemAdapterBindingDrawer.BindExpressionBindingRow` の EnumField ハンドリングが、ListView の `bindItem` 内で `RegisterValueChangedCallback` を呼ぶ手動パターンを採用していた（`afd5156` 由来、本 spec で導入された問題ではない）。`bindItem` は ListView item の virtualization / 再利用 / 行追加削除 で繰り返し呼ばれ、その都度 callback が累積。Phase 3 で Inspector 側に GazeConfigs UI を追加した結果、`serializedObject` 変更頻度が上がり、callback 累積による副作用が安定再現するようになった。

具体的には：
- callback クロージャは `index` をキャプチャしているが、ListView 仮想化により同一 EnumField インスタンスが index=0 と index=1 で再利用されると、古い callback が古い index へ書き込み続ける。
- 結果として、ある行の Hold 選択イベントが別行（既に Toggle が保存済み）への上書きを誘発し、UI 側が次の rebind で「保存済み値 = Toggle」を読み戻して Toggle 表示に戻る。

**修正**: `triggerModeField.Unbind(); triggerModeField.BindProperty(triggerModeProp);` に置換。Unity 標準の SerializedProperty バインディングが累積問題と仮想化を内部で正しく処理する。

## 共通する根本原因

| 観点 | 観察事実 |
|------|----------|
| **TDD 範囲** | Inspector の VisualElement 名 / 存在チェック / 結線 op はテスト化されていたが、「ユーザがフィールドを編集して保存し再描画後に値が保たれる」end-to-end の挙動はテスト無し。EnumField bindItem 累積問題が長期未検出だった原因 |
| **UX 一貫性レビュー** | spec フェーズ（design / tasks）で「他バインディングとの UI 一貫性」が要件として扱われず、実装後の手動検証で初めて指摘 |
| **Sample 値の保全** | スキーマ移行を行うとき、「構造の sync」と「過去に手調整された値の保全」が別軸の作業であることが意識されていない。tasks.md にも「値は別途確認」のチェックポイント無し |
| **デザイン段階の物理検証不足** | 「単行 UI」決定時に Inspector 幅 × フィールド数 × ラベル幅の物理計算 / モックアップが行われず、実機検証で初めて視認不能と判明 |

## 再発防止策

### A. Inspector 編集系テストに「値往復」ケースを必須化

**対象**: 全ての SerializedProperty を編集する VisualElement（EnumField / DropdownField / TextField / ObjectField / FloatField）。

**追加するテストパターン**（EditMode）:
1. 初期値が表示される
2. UI 経由で値を変更すると `serializedObject` に反映される
3. 同じ ListView 内で複数行を編集しても他行に副作用が無い
4. 行を追加 / 削除した後も、既存行の値が UI と SerializedProperty で一致

`InputSystemAdapterBindingDrawer` の TriggerMode / ActionDropdown / ExpressionDropdown 各分について、上記 4 ケースのテストを次タスクで追加する（spec への追加候補）。

### B. ListView bindItem 規約の明文化

`Documentation~/contributor-guide.md`（または steering）に以下を追加：

> **ListView の `bindItem` 内では、SerializedProperty 結線に必ず `BindProperty` (`UnityEditor.UIElements`) を使い、再 bind 前に `Unbind()` を呼ぶ。`RegisterValueChangedCallback` の手動結線は、callback 累積と仮想化による index ドリフトを引き起こすため使用禁止。**

新規 drawer / inspector レビュー時のチェックリスト項目とする。

### C. UX 一貫性チェックを spec design phase の必須項目に

`design.md` テンプレ（`.claude/agents/spec-design-agent` 配下のプロンプト）に「既存 UI との一貫性」セクションを追加：

- 同種データ（バインディング、結線、参照、リスト）が複数箇所に存在する場合、UI 形式（dropdown / object picker / text）を必ず揃える。揃えない場合は理由を明記。
- 新規 UI 部品を導入する場合、想定 Inspector 幅（PC: 280〜340px、ナロー: 240px 程度）でのモックアップ or 既存類似 UI への参照を design.md に貼る。

### D. Sample 値保全のチェックポイント

スキーマ移行系 spec の `tasks.md` テンプレに次を必須で含める：

- [ ] 移行前 Sample の「ユーザが手動で調整した可能性のある値」（transition / interpolation / weight 系）を migration log に転記
- [ ] 移行後、Sample に対し移行前の意図ある値が保たれているか目視確認
- [ ] 統一基準があるフィールド（例: `transitionDuration` = 0.1s）はリファレンスを `CLAUDE.md` または steering に記載し、移行作業ではそこを参照

`CLAUDE.md` の `## 重要な注意事項` に「Sample 設定の標準値」サブセクションを追加し、`transitionDuration: 0.1s` を最初の項目として登録（次 PR で実施候補）。

### E. 今 spec の retro task

本 spec (`gaze-config-promotion`) の `tasks.md` の最後に retro セクションを追加し、以下を post-implementation の必須タスク化：

- [ ] 実機 Inspector で全行・全フィールドの値が視認できるかスクリーンショット確認
- [ ] Sample asset 経由で「Hold ↔ Toggle」「ActionDropdown 選択」「行追加 / 削除」の golden path を手動操作検証

## 影響を受けるファイル一覧（今回の修正）

- `FacialControl/Packages/com.hidano.facialcontrol/Editor/Inspector/FacialCharacterProfileSOInspector.cs` — GazeConfig 行を縦並び化
- `FacialControl/Packages/com.hidano.facialcontrol.inputsystem/Runtime/Adapters/ScriptableObject/InputSystemGazeBinding.cs` — `inputActionRef` → `actionName`
- `FacialControl/Packages/com.hidano.facialcontrol.inputsystem/Runtime/Adapters/AdapterBindings/InputSystemAdapterBinding.cs` — analog source 構築の単純化
- `FacialControl/Packages/com.hidano.facialcontrol.inputsystem/Editor/AdapterBindings/InputSystemAdapterBindingDrawer.cs` — Gaze 行 dropdown 化、TriggerMode の `BindProperty` 化
- `FacialControl/Packages/com.hidano.facialcontrol.inputsystem/Tests/EditMode/Adapters/ScriptableObject/InputSystemGazeBindingTests.cs`
- `FacialControl/Packages/com.hidano.facialcontrol.inputsystem/Tests/PlayMode/Integration/InputSystemAdapterBindingIntegrationTests.cs`
- `FacialControl/Packages/com.hidano.facialcontrol/Runtime/Adapters/Bone/GazeBoneBinding.cs` — doc コメント
- Sample asset (4 ファイル) — `transitionDuration` 0.1 統一、`actionName` への field rename 反映

## 次アクション

1. EditMode test 追加（A 案、本 PR 続編 or 別 PR）
2. ListView bindItem 規約をリポジトリ contributor doc に追記（B 案）
3. design-agent / tasks-agent プロンプトに UX 一貫性 / Sample 保全チェックを追加（C, D 案）
4. 本 spec `tasks.md` に retro セクション追加（E 案、本コミット内で同梱可）
