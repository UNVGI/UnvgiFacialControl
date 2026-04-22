# HANDOVER (2026-04-22)

## 今回やったこと

- 前回 HANDOVER の優先度高 3 件を消化
  1. **PlayMode 全体回帰**: batchmode で 1 発実行 → **261/261 Passed**（task 6.2 時点 247 から +14 件）、duration 4.15s、exit=0。artifact を `FacialControl/test-results/playmode-full.xml` に配置
  2. **tasks.md checkbox 一括更新**: `.kiro/specs/layer-input-source-blending/tasks.md` の 57 箇所（47 リーフ + 10 セクション）を `[ ]` → `[x]` に flip、未チェック残 0
  3. **preview.1 finalize**: CHANGELOG `[0.1.0-preview.1] - Unreleased` → `- 2026-04-21` に更新

## 決定事項

- **preview.1 を finalize**（preview.2 には bump しない）。現 CHANGELOG は「全実装が preview.1 に束ねられた初回リリース」構造
- **git tag は main merge 後**に打つ。feature ブランチでは付けない（付け替えを避ける）
- **npmjs.com 公開は保留**。preview 段階では UPM 経由のみ
- `package.json` version は `0.1.0-preview.1` のまま据え置き

## 捨てた選択肢と理由

- **preview.2 へ即 bump + 新 Unreleased セクション追加**: まだ preview.1 を正式リリースしていない段階で bump すると CHANGELOG の履歴が歪む。preview.1 を確定 → main merge → tag → 次サイクルで bump が正攻法
- **feature ブランチ tip に直接 `v0.1.0-preview.1` タグ**: 2 名体制の PR レビュー運用を踏まえると main merge 後の付与が正規ルート。feature 上で打つと後で付け替えが必要
- **`-testResults ./FacialControl/test-results/playmode-full.xml` 指定**: Unity は `-testResults` を projectPath 相対で解決する仕様のため、実ファイルは `FacialControl/FacialControl/test-results/` に出力された。次回から `-testResults test-results/xxx.xml`（projectPath 相対）と書く方が素直

## ハマりどころ

- **`-testResults` と `-logFile` のパス解決基準が異なる**: 前者は projectPath 相対、後者は CWD 相対。exit=0 でもログで「Test run completed」は見えるのに目的位置に XML が無く焦った。Glob で探索して実在確認 → `mv` で canonical 位置へ移動
- **`FacialControl/FacialControl/test-results/editmode-P03-T02.xml`（2026-02-03 付）が残存**: 過去の誤配置 artifact。今回は触らず放置（本題と無関係）
- **`head -5` が permission で拒否**: Bash ツールの head/cat は制限あり。Read で offset/limit 指定か Grep で代替する

## 学び

- **PlayMode 全体回帰は 5 秒未満で完了する**: 261 テストでも batchmode なら高速。10 分タイムアウト内に十分収まるため分割不要
- **Unity batchmode のパス解決仕様の差異**: `-testResults` = projectPath 相対 / `-logFile` = CWD 相対。CLAUDE.md のコマンド例 `-testResults ./test-results/editmode.xml` は projectPath 相対前提で書かれていた
- **tasks.md の一括 flip は `Edit replace_all`** で十分。47 タスク × Edit 呼出は過剰、正規表現 sed も不要
- **CHANGELOG finalize は 1 行置換**。`Unreleased` → 日付のみで済むのは、事前に entry を正しく束ねておいたおかげ

## 次にやること

優先度高:
- **`feature/hidano/generate-prototype` → `main` の PR 作成**: Junki Hiroi のレビュー待ち。PR body には preview.1 の破壊的変更 3 点（inputSources 必須化 / legacy 廃止 / InputSourceCategory 追加）と移行ガイド参照を明記
- **main merge 後に `v0.1.0-preview.1` タグ付与 + push**: `git tag v0.1.0-preview.1 <merge-commit>` → `git push origin v0.1.0-preview.1`

優先度中:
- **preview.2 の開発開始時**: `package.json` version を `0.1.0-preview.2` に bump、CHANGELOG に `## [0.1.0-preview.2] - Unreleased` 新設
- **HANDOVER 前回分の残タスク**:
  - `FacialProfileSO_InputSourcesView` の自動テスト追加（repaint O(1) 回帰防止）
  - D-12 Tooltip: Controller + Keyboard 同時押しの加算が意図挙動である旨 Inspector に Help Box 追加

優先度低:
- PlayMode Performance を実機（セルフホストランナー）で再計測
- `FacialControl/FacialControl/test-results/editmode-P03-T02.xml` の旧 artifact 整理

## 関連ファイル

今回触った / 生成したファイル:
- `.kiro/specs/layer-input-source-blending/tasks.md`（57 checkbox 一括 flip）
- `FacialControl/Packages/com.hidano.facialcontrol/CHANGELOG.md`（preview.1 finalize）
- `FacialControl/test-results/playmode-full.xml`（261 Passed、最新 PlayMode 完全 run、218KB）
- `FacialControl/test-results/playmode-full.log`（2036 行、batchmode 実行ログ）

参照のみ:
- `FacialControl/Packages/com.hidano.facialcontrol/package.json`（version は据置）
- `docs/migration-guide.md`（preview 破壊的変更の移行手順、PR body で参照想定）
