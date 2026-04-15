# HANDOVER (2026-04-15)

## 今回やったこと

- `/kiro:spec-run expression-preview-camera` で全 11 リーフタスクを自動実行（全 OK）
- `/kiro:validate-impl` で GO 判定取得（要件・設計・実装の整合性確認、EditMode 7/7 Pass）
- tasks.md チェックボックスを `[x]` に更新してコミット
- 追加修正 4 件:
  1. **マウス Y 反転**: `PreviewRenderWrapper.HandleInput` で `Delta.y` を反転（Orbit / Pan / Dolly drag 対象、Scroll は対象外）
  2. **初期カメラ前面化**: `Setup()` の rotation を `Quaternion.Euler(0, 180, 0)` に変更
  3. **Head ボーン pivot**: `CalculatePivotPoint()` 新設。Humanoid なら `Animator.GetBoneTransform(HumanBodyBones.Head)` の Y 高さを採用、それ以外は `bounds.center` フォールバック
  4. **ドラッグキャプチャ**: `_dragButton` / `_dragAlt` で MouseDown→MouseUp 間のキャプチャ、`ExpressionCreatorWindow.OnPreviewGUI` で `_previewContainer.CaptureMouse()` / `ReleaseMouse()` を呼んで rect 外でも継続動作
- 最終 EditMode テスト: **717/717 Passed**

## 決定事項

- マウス Y 軸は wrapper 側で反転し、外部パッケージ `SceneViewStyleCameraController` の Handler は変更しない
- カメラ pivot 高さは Humanoid Head ボーン優先、フォールバックは bounds.center
- ドラッグキャプチャは UI Toolkit の `CaptureMouse()` + wrapper 内 `_dragButton` 状態の二段構え
- Scroll wheel は「マウス上下移動」の対象外として Y 反転しない

## 捨てた選択肢と理由

- **Handler 側で Y 反転**: 外部パッケージを fork する必要があり依存管理が複雑化するため不採用。wrapper 側の反転で十分
- **PreviewInputFrame 構築時に Y 反転**: テストが構築済み frame を直接渡すため、tests と production で意味が異なり混乱を招く。switch 直前の反転に統一
- **GUIUtility.hotControl による IMGUI 内キャプチャ**: UI Toolkit の IMGUIContainer 内では領域外イベントが届かないため hotControl だけでは不十分。`CaptureMouse()` が必須
- **Head 高さに加え X/Z も Head ボーン位置採用**: Head はキャラ中心線からズレることがあるため X/Z は bounds.center を維持

## ハマりどころ

- IMGUIContainer は UI Toolkit の hit testing でカーソルが領域外に出るとイベント自体届かない → wrapper 側のロジックだけでは不足、`CaptureMouse()` が必須だった
- Unity Editor 起動中はバッチテスト実行不可（"Multiple Unity instances cannot open the same project"）
- spec-run 実行中、tasks.md のチェックボックス更新は子 claude 側では行われず、親側で手動更新が必要

## 学び

- UI Toolkit 配下の IMGUIContainer でドラッグ継続を実現するには `VisualElement.CaptureMouse()` 拡張メソッドを使う
- `Quaternion.Euler(0, 180, 0)` で「pivot 周回 180°」と「カメラ向き反転」を同時に達成できる（`pivotPoint - rotation * forward * dist` の幾何）
- `validate-impl-agent` は実装後の総合検証として要件カバレッジ・設計整合性・テスト結果を一括レポートしてくれる

## 次にやること

優先度中:
- PlayMode テストは未実行。必要に応じて実行
- ドラッグキャプチャの EditMode テスト追加（MouseDown → 領域外 MouseDrag → MouseUp フローの検証）

優先度低:
- VRM 対応（リリース後マイルストーン）
- ARKit 52 / PerfectSync 自動検出 + プロファイル自動生成

## 関連ファイル

- `FacialControl/Packages/com.hidano.facialcontrol/Editor/Common/PreviewRenderWrapper.cs`
- `FacialControl/Packages/com.hidano.facialcontrol/Editor/Common/PreviewInputFrame.cs`
- `FacialControl/Packages/com.hidano.facialcontrol/Editor/Tools/ExpressionCreatorWindow.cs`
- `FacialControl/Packages/com.hidano.facialcontrol/Editor/Hidano.FacialControl.Editor.asmdef`
- `FacialControl/Packages/com.hidano.facialcontrol/Tests/EditMode/Editor/PreviewRenderWrapperTests.cs`
- `FacialControl/Packages/com.hidano.facialcontrol/Tests/EditMode/Hidano.FacialControl.Tests.EditMode.asmdef`
- `FacialControl/Packages/manifest.json`
- `.kiro/specs/expression-preview-camera/{requirements,design,tasks,research,spec}.md|json`
- 外部パッケージ参照: `FacialControl/Library/PackageCache/com.hidano.scene-view-style-camera-controller@.../Runtime/Handlers/{Orbit,Pan,Dolly}Handler.cs`
