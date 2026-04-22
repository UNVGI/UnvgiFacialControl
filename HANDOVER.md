# HANDOVER (2026-04-22 #3)

## 今回やったこと

- `Assets/Samples/` の孤児アセット棚卸し（`NewFacialProfile.asset` が scene 未参照）→ ユーザー削除
- `Assets/Editor/` 配下 4 スクリプト（Attach/Diag/Dump/Verify）の使用状況調査（scene/CI/package 本体から参照なし）→ ユーザー削除
- `Assets/StreamingAssets/FacialControl/` の 3 JSON 棚卸し（`multi_source_blend_demo.json` のみ dev scene 参照、他 2 つは孤児）→ ユーザー削除
- `Packages/com.hidano.facialcontrol/Templates/` 棚卸し＆不要削除:
  - `default_profile.json` → README L64 から参照あり、残す
  - `default_inputactions.inputactions` → Runtime 側 `FacialControlDefaultActions.inputactions` と実質重複、削除
  - `default_config.json` → README/docs から参照経路なし、削除
- CHANGELOG.md 整合更新:
  - Adapters 節 L49：「デフォルト InputAction Asset」→ `Runtime/Adapters/Input/FacialControlDefaultActions.inputactions` をファイル実体付きで明記
  - サンプル節：旧 3 項目 → `Samples~/MultiSourceBlendDemo` 1 項目に集約
  - テンプレート節：3 項目 → `Templates/default_profile.json` のみに縮小

## 決定事項

- FacialControl 仕様として必要な SO は 2 種：`FacialProfileSO`（FacialController）と `InputBindingProfileSO`（FacialInputBinder）
- `InputActionAsset` は Unity 標準 SO。Runtime 同梱の `FacialControlDefaultActions.inputactions` をデフォルトとするためユーザー自作不要
- `StreamingAssets/FacialControl/multi_source_blend_demo.json` は Samples~ 側にコピーがあっても **dev scene 動作に必須**のため削除不可（CLAUDE.md dual maintenance ルール）
- CHANGELOG の InputAction Asset 記述は Adapters 節に一本化（Templates 節から重複排除）

## 捨てた選択肢と理由

- **StreamingAssets を Samples~ 配下に移す** → 不採用。UPM import 時は `Assets/Samples/com.hidano.facialcontrol/x.y.z/` 配下に展開され StreamingAssets 扱いにならない。`FacialProfileSO._jsonFilePath` が StreamingAssets 相対パス前提である以上、README での手動コピー案内が現実解
- **`Assets/Editor/` の `Diag*`/`Verify*` を残す案** → 全削除を選択。必要になれば git 履歴から復活可能
- **`default_config.json` を README で案内して活かす案 (a)** → 削除 (b) を選択
- **CHANGELOG の InputAction Asset を「その他」節新設 (c)** → 不採用。Adapters 節 L49 に既存行があったのでそちらをエンリッチして一本化

## ハマりどころ

- `Templates/default_inputactions.inputactions` と `Runtime/Adapters/Input/FacialControlDefaultActions.inputactions` が 387 行同サイズで紛らわしい。GUID は別物で scene 参照は Runtime 側（guid `a3b4c5d6e7f8091a2b3c4d5e6f708192`）
- CHANGELOG Adapters 節 L49 に既に「デフォルト InputAction Asset」行があり、Templates 節との重複を見落としかけた

## 学び

- UPM の `Samples~/` は import 時に `Assets/Samples/{package}/{version}/` 配下にコピーされるため、StreamingAssets や ProjectSettings 等 Unity 特殊フォルダへの配置は自動化できない
- `FacialControlConfig` の JSON Parse/Serialize API は Runtime 実装済みだが、ユーザー向けサンプル/ドキュメントのエントリポイントが無いと実質死に機能
- Templates フォルダは「ユーザーが手動コピーする雛形」の性質上、README からのリンクが無いと参照経路が消える

## 次にやること

**優先度：高**
- 特になし（preview.1 タグ切り前のクリーンアップは一通り完了）

**優先度：中**
- `Samples~/MultiSourceBlendDemo/MultiSourceBlendDemoHUD.cs` と `Assets/Samples/MultiSourceBlendDemoHUD.cs` の doc コメント差分が残存（機能差はない）。次回 edit 時に揃える
- StreamingAssets 手動コピー問題の自動化案：Editor 拡張での import 後コピー or `FacialProfileSO` に TextAsset 直参照モード追加（preview 段階の破壊的変更で検討）

**優先度：低**
- `Runtime/Adapters/Json/JsonSchemaDefinition.cs:317` の `SampleConfigJson` const と旧 `default_config.json` の役割が重複している可能性。preview.2 以降で棚卸し

## 関連ファイル

- `FacialControl/Packages/com.hidano.facialcontrol/CHANGELOG.md`（編集）
- `FacialControl/Packages/com.hidano.facialcontrol/Templates/default_profile.json`（残存）
- `FacialControl/Packages/com.hidano.facialcontrol/Templates/default_config.json`（削除）
- `FacialControl/Packages/com.hidano.facialcontrol/Templates/default_inputactions.inputactions`（削除）
- `FacialControl/Packages/com.hidano.facialcontrol/package.json`（参照のみ）
- `FacialControl/Packages/com.hidano.facialcontrol/Runtime/Adapters/Input/FacialControlDefaultActions.inputactions`（canonical、参照のみ）
- `FacialControl/Packages/com.hidano.facialcontrol/Samples~/MultiSourceBlendDemo/`（参照のみ）
- `FacialControl/Assets/Samples/SampleFacialProfileSO.asset` / `SampleInputBinding.asset` / `SampleScene.unity`（scene 配線確認）
- `FacialControl/Assets/StreamingAssets/FacialControl/multi_source_blend_demo.json`（dev scene 必須）
