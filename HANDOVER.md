# HANDOVER (2026-04-22 #2)

## 今回やったこと

- **multi-source blending の runtime 配線欠落を発見・修復**: `LayerUseCase.UpdateWeights` がランタイムで一度も呼ばれず、IInputSource パイプラインが死コードだった。前回 HANDOVER で「preview.1 finalize」と書いたが、実際は intra-layer contract が自動テストで通るだけで、実機では BlendShape まで届いていなかった
- **Phase 1 runtime wiring 完成** (3 gap 埋め):
  1. `LayerUseCase.BlendedOutputSpan` (zero-alloc アクセサ) 追加
  2. `FacialController.LateUpdate` を mixer 読取から `_layerUseCase.UpdateWeights` + `BlendedOutputSpan` 読取に差替
  3. `FacialController.TryGetExpressionTriggerSourceById` 追加 (Samples デモから source を直接 TriggerOn/Off するため)
- **LayerUseCase blend フィルタのバグ修正**: `_layerSources[l].HasBeenActive` のみで blend 対象を絞っていたため、additional IInputSource だけのレイヤー (profile.inputSources 宣言のみ) が常に除外されていた。`_layerHasAdditionalSources[]` を追加し「profile で宣言した layer は無条件で blend 対象」に拡張
- **SampleScene に Samples 専用デモを結線**:
  - `MultiSourceBlendDemoHUD.cs` (OnGUI: weight スライダー + 各ソース trigger ボタン + snapshot)
  - `Assets/Editor/AttachMultiSourceBlendDemoHUD.cs` で scene に GameObject を自動配置
  - `Assets/Editor/DumpMikuBlendShapes.cs` で Miku の BlendShape 名 (85 個) を抽出
  - `Assets/Editor/VerifyDemoProfileBlendShapes.cs` で demo プロファイルと mesh の BlendShape 名一致検証 (10/10 マッチ)
  - `Assets/Editor/DiagMultiSourceBlend.cs` で runtime 状態を console + file にダンプする診断メニュー
- **demo data を Samples に隔離**:
  - `StreamingAssets/FacialControl/multi_source_blend_demo.json` 新設 (smile/angry/surprise/troubled + blink、Miku BlendShape 直接参照)
  - `default_profile.json` は neutral 状態に復帰
  - `sample_profile.json` は test fixture 用に復帰 (blink id は UUID 維持)
  - `SampleFacialProfileSO.asset._jsonFilePath` を `multi_source_blend_demo.json` に更新
- **目視検証クリア**: UnityMCP 経由で controller-expr smile + keyboard-expr angry を同時トリガ → 笑い=50 / 怒り=50 / 口角上げ=30 / 左眉下げ=25 / 右眉下げ=25 (すべて期待通り)
- **テスト回帰**: EditMode **1054/1054 Passed**, PlayMode **261/261 Passed** (新規 +7: BlendedOutputSpan×3 / TryGet×4 / AdditionalSourceOnly×1、前回比 +7)

## 決定事項

- **preview.1 のステータスを "runtime 統合含め動作確認済" に正式格上げ**。前回 HANDOVER の finalize 宣言は誤り (runtime 統合未完だった)
- **新 API 追加は許容**: publish 前なので `BlendedOutputSpan` / `TryGetExpressionTriggerSourceById` を preview.1 に加える
- **demo data は Samples に隔離**: `default_profile.json` / `sample_profile.json` は触らず、demo 専用 JSON を新設。「出所不明の Miku 依存 Expression が default に混入」を避ける
- **Phase 2/3 (物理入力 → IInputSource dispatch refactor) は後回し**: scope が大きく既存テスト大量更新が必要。HUD ボタンで代替できるため demo 目的には不要
- **キーボード 1 = blink バインディングの修復不要**: HUD でデバッグ可能なため (ユーザー判断)

## 捨てた選択肢と理由

- **(1) API 追加せず**: 初期提案したが preview.1 finalize 直後で筋が悪いと指摘された → 一度は「既存 API 完結」に切替えたが実装ギャップに気付き「publish 前なら新 API OK」で再反転
- **(2) Samples 専用の独立 MonoBehaviour で自前 Aggregator を組む**: 採用せず。FacialController 本体に手を入れた方が preview.1 として正しい
- **default_profile.json を書き換えて demo データを載せる**: ユーザーから「出所不明の authoring 混入」と指摘され、Samples 専用 JSON に隔離する方針へ変更

## ハマりどころ

- **`LayerUseCase.UpdateWeights` がランタイムで呼ばれていなかった**: 自動テストだけ green で runtime dark の状態が preview.1 finalize 後も残っていた。`FacialController.LateUpdate` が PlayableGraph mixer を読むだけで Aggregator 出力を無視する設計だった
- **`Hidano.FacialControl.Samples.EditorTools` namespace 内で `Application.xxx` が `Hidano.FacialControl.Application` を掴む**: UnityEngine.Application を明示修飾しないとコンパイル失敗。3 回引っかかった
- **SampleScene の FacialController は `SampleFacialProfileSO.asset` (guid `d7e9a2c4...`) を参照しており、`NewFacialProfile.asset` (guid `2faa955c...`) ではなかった**: `default_profile.json` を書き換えても scene には届かず。demo 用 JSON を書き換える時は asset 側の参照を追跡して正しいファイルに書く必要あり
- **`inputSources: []` (空配列) は JSON parser が preview.1 破壊的変更で拒否** (`FormatException` → `CreateDefaultProfile` fallback)。非空必須。layer ごとに最低 1 source を宣言するか、layer 自体を削除
- **Unity Editor の Game View が非 focus だと Play mode でも tick しない**: MCP から `EnterPlaymode` しても `Time.frameCount=2` で止まる。`Application.runInBackground = true` で解決 (今回 HUD.Start で立てた)
- **UnityMCP の `Unity_RunCommand` は Unity が compile 中だと受け付けない**: Play 中にスクリプトを追加・編集すると再コンパイルがトリガされ、途中で詰まることがある。stop→edit→enter の順が安全
- **UnityMCP が別プロジェクトの Unity に接続**: 途中で `RealtimeAvatarController` に繋がっていた。複数 Unity 起動時は MCP がどちらに接続しているか要確認 (`Unity_RunCommand` で `Directory.GetCurrentDirectory()` を出して確認)
- **UnityMCP compile environment が package asmdef を参照しない**: `Hidano.FacialControl.*` 型は直接使えず reflection 経由。さらに `out` 引数 reflection で MCP 自身が NRE を起こすことがあるため、Editor menu 経由で呼ぶのが確実
- **TestRunner 結果は Console に出ない**: TestRunnerApi の callback を登録してファイルに書き出すか、batchmode で xml を取るしかない

## 学び

- **preview.X を finalize する前に「実機 Play でどれか 1 ソース以上が BlendShape まで届く」ことを 1 回は目視せよ**: 自動テストで契約が通っていても runtime 配線が抜けていれば使い物にならない
- **UnityMCP の tool 群は開発中調査に有用**: `ManageEditor.Play/Stop/GetState` + `ManageMenuItem.Execute` + `ReadConsole` + `RunCommand` の組合せで Editor 状態を遠隔操作できる。特に "diag + trigger" メニュー 2 つを `Assets/Editor/` に置いておくと、MCP から呼べて反復デバッグが速い
- **`GC.GetTotalMemory(false)` ベースの alloc テストは環境ゆらぎを起こす**: `OscControllerBlendingIntegrationTests.Pipeline_AggregateAndBlendLoop_AllocatesZeroManagedMemory` が TestRunner で 1 回だけ 16KB 計測 → 再実行で pass。flaky 扱い。再現性のある alloc バグが混入したわけではない
- **Layer blend の lerp(weight=1) は全 BlendShape index を上書きする**: 高 priority layer が weight=1 のままだと下位 layer の出力を wipe する。同時 trigger での「emotion smile + eye blink」が期待通りブレンドしない (pre-existing design issue、今回スコープ外)

## 次にやること

優先度高:
- **`feature/hidano/generate-prototype` → `main` の PR 作成**: 今回の追加内容も含めて preview.1 として PR。変更要約に「runtime wiring 完成 + 新 API 2 点 (`BlendedOutputSpan` / `TryGetExpressionTriggerSourceById`) + demo 隔離」を明記
- **main merge 後に `v0.1.0-preview.1` タグ付与 + push**

優先度中:
- **Phase 2/3 (物理入力 → IInputSource dispatch refactor)**: `FacialInputBinder` / `InputSystemAdapter` が category に応じて `controller-expr` / `keyboard-expr` を直接 TriggerOn/Off する経路へ。既存 `InputSystemAdapterTests` / `FacialInputBinderTests` の大量更新を伴う。preview.2 候補
- **LayerBlender lerp 問題の見直し**: 高 priority layer の weight=1 が下位 layer 全 BS を wipe する挙動。multi-layer 目視検証中に発覚。preview.2 以降の設計検討
- **HUD 自動テスト**: Samples コンポーネント自体の回帰テスト (現状は目視のみ)

優先度低:
- 旧 `FacialControl/FacialControl/test-results/editmode-P03-T02.xml` など test-results/ のゴミ整理
- PlayableGraph / FacialControlMixer が現在出力に使われていないので preview.2 で撤去検討

## 関連ファイル

今回触った / 生成したファイル:
- `FacialControl/Packages/com.hidano.facialcontrol/Runtime/Application/UseCases/LayerUseCase.cs` (`BlendedOutputSpan` / `TryGetExpressionTriggerSourceById` / `_layerHasAdditionalSources` + フィルタ修正)
- `FacialControl/Packages/com.hidano.facialcontrol/Runtime/Adapters/Playable/FacialController.cs` (`LateUpdate` を Aggregator 経路に差替 + `TryGetExpressionTriggerSourceById` facade)
- `FacialControl/Packages/com.hidano.facialcontrol/Runtime/Domain/Services/ExpressionTriggerInputSourceBase.cs` (`ActiveExpressionIds` を `protected` → `public` 昇格)
- `FacialControl/Packages/com.hidano.facialcontrol/Tests/EditMode/Application/LayerUseCaseTests.cs` (+7 tests)
- `FacialControl/Assets/Samples/MultiSourceBlendDemoHUD.cs` (新規、OnGUI HUD)
- `FacialControl/Assets/Samples/SampleScene.unity` (HUD GameObject 追加)
- `FacialControl/Assets/Samples/SampleFacialProfileSO.asset` (JsonFilePath を multi_source_blend_demo.json に)
- `FacialControl/Assets/StreamingAssets/FacialControl/multi_source_blend_demo.json` (新規)
- `FacialControl/Assets/StreamingAssets/FacialControl/default_profile.json` (neutral に復帰)
- `FacialControl/Assets/StreamingAssets/FacialControl/sample_profile.json` (test fixture 状態に復帰、demo 書き換えから巻き戻し)
- `FacialControl/Assets/Editor/AttachMultiSourceBlendDemoHUD.cs` (新規、scene 結線)
- `FacialControl/Assets/Editor/DumpMikuBlendShapes.cs` (新規、BS 名抽出)
- `FacialControl/Assets/Editor/VerifyDemoProfileBlendShapes.cs` (新規、BS 名整合検証)
- `FacialControl/Assets/Editor/DiagMultiSourceBlend.cs` (新規、runtime 診断メニュー)

参照のみ:
- `FacialControl/Packages/com.hidano.facialcontrol/package.json`(version は据置)
- `FacialControl/Packages/com.hidano.facialcontrol/CHANGELOG.md`
