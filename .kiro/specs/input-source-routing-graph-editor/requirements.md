# Requirements Document

## Project Description (Input)
入力源ルーティング・グラフエディタ（Input Source Routing Graph Editor）。

## 背景・解決する課題
FacialControl のプロファイル（FacialCharacterProfileSO）では、各レイヤーの入力源を `inputSources[].id` という文字列 slug（例 `lipsync-overlay:a`）で保持し、現状 Inspector では生の PropertyField で人間が手打ちしている（FacialCharacterProfileSOInspector.cs:1708-1712）。この id は本来自由入力ではなく、プロファイルに載った Adapter Binding が GetDefaultLayerInputSources()/registry slug/overlay slot 宣言から算出できる確定値である。手打ち運用のため binding slug 形（ulipsync:a）と overlay 登録 prefix 形（lipsync-overlay:a）の取り違えが起き、InputSourceRegistry.TryResolve が無言で外れて音素が欠落する事故が実際に発生した（HANDOVER 参照）。本機能はこの「人間が id 文字列を直書きする運用」を廃し、グラフィカルな配線 UI に置き換える。

## 中心思想
id 文字列をソースノードの出力ポートが内部保持し、画面にはラベル（「あ」等）のみ表示。ユーザーはノード間に線（エッジ）を引くだけ。エッジが canonical id を運ぶので slug 不一致が物理的に発生不能になる。

## UI 形態（確定事項）
- パッチベイ/ノードグラフ形式。左=入力源ノード → 中=レイヤーノード → 右=合成出力ノード、の左→右フロー。
- 配置: 専用エディタウィンドウ（FacialCharacterProfileSO を選んで「ルーティングを編集」ボタンから開く独立ウィンドウ）。既存 Inspector は従来通り残す。
- 技術基盤: UnityEditor.Experimental.GraphView（Shader Graph 等で使われる公式 GraphView。ノード/エッジ/ズーム/ミニマップが標準提供）。experimental 名前空間である点はリスクとして design で扱う。

## ノード3種とデータ対応
- ソースノード: `_adapterBindings[]` が公開する id（GetDefaultLayerInputSources ＋ registry slug ＋ overlay slot 宣言）から生成。読み取り専用。各出力ポートに canonical id を紐づけ、ラベルのみ表示・ホバーで実 id を tooltip。uLipSync なら a/i/u/e/o の5ポート。
- レイヤーノード: `_layers[]`（name/priority/exclusionMode/overrideMask）。レイヤー属性を直接編集可能。
- エッジ（source→layer）: `_layers[i].inputSources[]` の各 {id, weight} に対応。配線＝Declaration 追加、切断＝削除、エッジ上の weight ノブ（ドラッグ/数値）＝weight 編集。
- 出力ノード: priority + overrideMask の合成順序を俯瞰（読み取り専用）。

このグラフは FacialCharacterProfileSO の _layers/_slots/_defaultOverlays の別ビューであり、保存先は既存 SO のまま。JSON エクスポート経路は無改修で通すこと。

## 自動配線ボタン（スコープ内）
binding 追加時にソースノードが自動出現。ノードヘッダの「overlay レイヤーへ一括配線」ボタンで、binding.GetDefaultLayerInputSources を全レイヤー名で評価し該当レイヤーへ weight 1.0 のエッジを一括生成。slot 未宣言なら _slots に a/i/u/e/o を同時宣言（既存「Phoneme slots を初期化」と統合）。常用ケースの手入力をゼロにする。

## 無効配線の可視化（スコープ内・限定）
既存プロファイルを開いた時、どのソースポートにも一致しない id（旧 ulipsync:a や typo）を「赤い破線の宙ぶらりんエッジ」として描画し、ユーザーが事故に気づけるようにする。※ワンクリック自動修復ボタンは今回スコープ外（横断機能は自動配線のみ採用）。

## スコープ外（Non-Goals）
- 無効 id のワンクリック自動修復ボタン（別途検討）。
- ランタイム UI（Editor 専用。CLAUDE.md 方針通りランタイム UI は提供しない）。

## 技術・規約上の制約
- Editor 拡張は UI Toolkit ベース（既存 FacialControlCommon.uss と整合）。GraphView も UI Toolkit 系。
- C# 名前空間 Hidano.FacialControl、4スペース、明示的 public/private、日本語コメント。
- TDD（Red-Green-Refactor）。Editor ロジック（id 列挙・配線↔SO 変換・検証）は EditMode テスト可能な純粋ロジックに切り出す。GraphView 描画自体は薄く保つ。
- 関連既存型: InputSourceId, FacialProfile(LayerInputSources/Slots), InputSourceDeclaration, FacialCharacterProfileSO(_layers/_slots/_defaultOverlays/_adapterBindings), IInputSourceRegistry/InputSourceRegistry, PhonemeOverlaySlots, LipSyncPhonemeOverlayInputSource, ULipSyncAdapterBinding(GetDefaultLayerInputSources/ApplyInitialDefaults)。
- パッケージ: com.hidano.facialcontrol の Editor アセンブリに配置（lipsync binding 固有に依存しない汎用設計。uLipSync は一例として扱う）。

## Requirements
<!-- Will be generated in /kiro-spec-requirements phase -->
