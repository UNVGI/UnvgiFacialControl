using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Editor.Common;
using Hidano.FacialControl.Editor.Sampling;

namespace Hidano.FacialControl.Editor.Tools
{
    /// <summary>
    /// Expression 作成支援ツール。
    /// BlendShape スライダーをリアルタイムプレビューしながら AnimationClip にベイクする。
    /// 既存 AnimationClip を割り当てると <see cref="IExpressionAnimationClipSampler.SampleSnapshot"/>
    /// 経由でスライダー値が復元される。
    /// </summary>
    public class ExpressionCreatorWindow : EditorWindow
    {
        private const string WindowTitle = "Expression 作成";
        private const float MinWindowWidth = 700f;
        private const float MinWindowHeight = 500f;
        private const int PreviewSize = 256;
        private const float BakeButtonMinWidth = 140f;
        private const string SavePreviewButtonName = "expression-creator-save-preview-png-button";
        private const string CreateNewClipButtonName = "expression-creator-create-new-clip-button";

        // モデル参照
        private GameObject _targetObject;
        private SkinnedMeshRenderer[] _skinnedMeshRenderers;
        private ObjectField _modelField;
        private string[] _availableBlendShapeNames = Array.Empty<string>();
        private HelpBox _blendShapeHelpBox;

        // プレビュー
        private PreviewRenderWrapper _previewWrapper;
        private IMGUIContainer _previewContainer;

        // BlendShape 管理
        private List<BlendShapeEntry> _blendShapeEntries = new List<BlendShapeEntry>();
        private ScrollView _blendShapeListView;
        private TextField _blendShapeSearchField;
        private string _blendShapeSearchText = "";

        // ベイク先 AnimationClip
        private ObjectField _clipField;
        private AnimationClip _targetClip;

        // ステータス
        private Label _statusLabel;

        // 依存
        private IExpressionAnimationClipSampler _sampler;
        private Func<string> _savePreviewPathProvider;
        private Func<int, int, Texture2D> _previewTextureCapture;
        private Action<string, byte[]> _pngFileWriter;
        private Func<string> _createClipPathProvider;
        private Action<AnimationClip, string> _clipAssetCreator;
        private Func<string, AnimationClip> _clipAssetLoader;
        private Action _assetDatabaseSaveAssets;

        [MenuItem("Tools/FacialControl/Expression 作成", false, 20)]
        public static void ShowWindow()
        {
            var window = GetWindow<ExpressionCreatorWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(MinWindowWidth, MinWindowHeight);
        }

        private void OnEnable()
        {
            _sampler = new AnimationClipExpressionSampler();
            _previewWrapper = new PreviewRenderWrapper();
            ConfigureSavePreviewDependencies();
            ConfigureCreateClipDependencies();
        }

        private void OnDisable()
        {
            _previewWrapper?.Dispose();
            _previewWrapper = null;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;

            var styleSheet = FacialControlStyles.Load();
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);

            var mainContainer = new VisualElement();
            mainContainer.style.flexDirection = FlexDirection.Row;
            mainContainer.style.flexGrow = 1;
            root.Add(mainContainer);

            // ========================================
            // 左パネル: プレビュー + モデル選択
            // ========================================
            var leftPanel = new VisualElement();
            leftPanel.name = "expression-creator-left-panel";
            leftPanel.style.width = PreviewSize + 16;
            leftPanel.style.minWidth = PreviewSize + 16;
            leftPanel.style.paddingLeft = 4;
            leftPanel.style.paddingRight = 4;
            leftPanel.style.paddingTop = 4;
            mainContainer.Add(leftPanel);

            _modelField = new ObjectField("モデル")
            {
                objectType = typeof(GameObject),
                allowSceneObjects = true,
                tooltip = "BlendShape プレビュー対象のモデル。シーン上に FacialController があれば自動解決される。"
            };
            _modelField.RegisterValueChangedCallback(OnModelChanged);
            leftPanel.Add(_modelField);

            _blendShapeHelpBox = new HelpBox(
                "モデルを設定するか、シーン上に FacialController を配置すると BlendShape スライダーが表示されます。",
                HelpBoxMessageType.Info);
            _blendShapeHelpBox.style.marginTop = 4;
            leftPanel.Add(_blendShapeHelpBox);

            _previewContainer = new IMGUIContainer(OnPreviewGUI);
            _previewContainer.style.width = PreviewSize;
            _previewContainer.style.height = PreviewSize;
            _previewContainer.style.marginTop = 4;
            _previewContainer.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f, 1f);
            leftPanel.Add(_previewContainer);

            var cameraResetButton = new Button(OnCameraReset) { text = "カメラリセット" };
            cameraResetButton.AddToClassList(FacialControlStyles.ActionButton);
            cameraResetButton.style.marginTop = 4;
            leftPanel.Add(cameraResetButton);

            var savePreviewButton = new Button(OnSavePreviewClicked) { text = "プレビューを PNG として保存" };
            savePreviewButton.name = SavePreviewButtonName;
            savePreviewButton.AddToClassList(FacialControlStyles.ActionButton);
            savePreviewButton.style.marginTop = 4;
            leftPanel.Add(savePreviewButton);

            var resetButton = new Button(OnResetBlendShapes) { text = "全スライダーリセット" };
            resetButton.AddToClassList(FacialControlStyles.ActionButton);
            resetButton.style.marginTop = 4;
            leftPanel.Add(resetButton);

            // ========================================
            // 右パネル: AnimationClip + BlendShape スライダー
            // ========================================
            var rightPanel = new VisualElement();
            rightPanel.style.flexGrow = 1;
            rightPanel.style.paddingLeft = 4;
            rightPanel.style.paddingRight = 4;
            rightPanel.style.paddingTop = 4;
            mainContainer.Add(rightPanel);

            _clipField = new ObjectField("AnimationClip")
            {
                objectType = typeof(AnimationClip),
                allowSceneObjects = false,
                tooltip = "ベイク対象の AnimationClip。割り当てると現在の値がスライダーに復元される。"
            };
            _clipField.RegisterValueChangedCallback(OnClipFieldChanged);

            var clipRow = new VisualElement();
            clipRow.style.flexDirection = FlexDirection.Row;
            clipRow.style.alignItems = Align.Center;
            rightPanel.Add(clipRow);

            _clipField.style.flexGrow = 1;
            clipRow.Add(_clipField);

            var createNewClipButton = new Button(OnCreateNewClipClicked) { text = "新規作成" };
            createNewClipButton.name = CreateNewClipButtonName;
            createNewClipButton.AddToClassList(FacialControlStyles.ActionButton);
            createNewClipButton.style.marginLeft = 4;
            createNewClipButton.style.flexShrink = 0f;
            clipRow.Add(createNewClipButton);

            // BlendShape 検索
            _blendShapeSearchField = new TextField("BlendShape 検索");
            _blendShapeSearchField.RegisterValueChangedCallback(OnBlendShapeSearchChanged);
            _blendShapeSearchField.style.marginTop = 8;
            rightPanel.Add(_blendShapeSearchField);

            // BlendShape スライダーリスト
            _blendShapeListView = new ScrollView(ScrollViewMode.Vertical);
            _blendShapeListView.style.flexGrow = 1;
            _blendShapeListView.style.marginTop = 4;
            rightPanel.Add(_blendShapeListView);

            // ========================================
            // 下部: ベイクボタン + ステータス
            // ========================================
            var bottomSection = new VisualElement();
            bottomSection.style.flexDirection = FlexDirection.Row;
            bottomSection.style.paddingLeft = 4;
            bottomSection.style.paddingRight = 4;
            bottomSection.style.paddingBottom = 4;
            bottomSection.style.paddingTop = 4;
            bottomSection.style.justifyContent = Justify.FlexEnd;

            var bakeButton = new Button(OnBakeClicked) { text = "AnimationClip にベイク" };
            bakeButton.name = "expression-creator-bake-button";
            bakeButton.AddToClassList(FacialControlStyles.ActionButton);
            bakeButton.style.flexShrink = 0f;
            bakeButton.style.minWidth = BakeButtonMinWidth;
            bottomSection.Add(bakeButton);

            root.Add(bottomSection);

            _statusLabel = new Label();
            _statusLabel.AddToClassList(FacialControlStyles.StatusLabel);
            _statusLabel.style.paddingLeft = 4;
            _statusLabel.style.paddingBottom = 4;
            root.Add(_statusLabel);

            TryAutoResolveModelFromScene();
        }

        // ========================================
        // モデル選択
        // ========================================

        private void OnModelChanged(ChangeEvent<UnityEngine.Object> evt)
        {
            _targetObject = evt.newValue as GameObject;
            CollectBlendShapes();
            RefreshBlendShapeNameChoices();
            RebuildBlendShapeList();
            SetupPreview();
            RestoreSliderValuesFromTargetClip();
        }

        private void RefreshBlendShapeNameChoices()
        {
            _availableBlendShapeNames = BlendShapeNameProvider.GetBlendShapeNames(_targetObject);

            if (_blendShapeHelpBox != null)
            {
                _blendShapeHelpBox.style.display = _availableBlendShapeNames.Length == 0
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }
        }

        private string[] AvailableBlendShapeNames => _availableBlendShapeNames ?? Array.Empty<string>();

        /// <summary>
        /// 対象モデル配下の全 SkinnedMeshRenderer から BlendShape を収集し、
        /// 各エントリに Transform 階層パス（AnimationClip binding.path 用）を付与する。
        /// </summary>
        private void CollectBlendShapes()
        {
            _blendShapeEntries.Clear();

            if (_targetObject == null)
            {
                _skinnedMeshRenderers = null;
                return;
            }

            _skinnedMeshRenderers = _targetObject.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            for (int r = 0; r < _skinnedMeshRenderers.Length; r++)
            {
                var smr = _skinnedMeshRenderers[r];
                if (smr.sharedMesh == null)
                    continue;

                var rendererPath = AnimationUtility.CalculateTransformPath(
                    smr.transform, _targetObject.transform);

                int count = smr.sharedMesh.blendShapeCount;
                for (int i = 0; i < count; i++)
                {
                    var shapeName = smr.sharedMesh.GetBlendShapeName(i);
                    _blendShapeEntries.Add(new BlendShapeEntry
                    {
                        RendererName = smr.name,
                        RendererIndex = r,
                        RendererPath = rendererPath ?? string.Empty,
                        BlendShapeName = shapeName,
                        BlendShapeIndex = i,
                        Value = 0f
                    });
                }
            }
        }

        private void TryAutoResolveModelFromScene()
        {
            if (_modelField == null || _targetObject != null)
                return;

            var controller = UnityEngine.Object.FindFirstObjectByType<FacialController>();
            if (controller != null)
            {
                _modelField.value = controller.gameObject;
            }
        }

        // ========================================
        // BlendShape スライダー UI
        // ========================================

        private void OnBlendShapeSearchChanged(ChangeEvent<string> evt)
        {
            _blendShapeSearchText = evt.newValue ?? "";
            RebuildBlendShapeList();
        }

        private void RebuildBlendShapeList()
        {
            if (_blendShapeListView == null)
                return;

            _blendShapeListView.Clear();

            if (_blendShapeEntries.Count == 0)
            {
                var message = AvailableBlendShapeNames.Length == 0
                    ? "モデルを選択してください。"
                    : "選択中のモデルに BlendShape が見つかりませんでした。";
                var emptyLabel = new Label(message);
                emptyLabel.AddToClassList(FacialControlStyles.InfoLabel);
                emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                emptyLabel.style.marginTop = 20;
                _blendShapeListView.Add(emptyLabel);
                return;
            }

            string currentRenderer = null;

            for (int i = 0; i < _blendShapeEntries.Count; i++)
            {
                var entry = _blendShapeEntries[i];

                if (!string.IsNullOrEmpty(_blendShapeSearchText)
                    && entry.BlendShapeName.IndexOf(_blendShapeSearchText, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (currentRenderer != entry.RendererName)
                {
                    currentRenderer = entry.RendererName;
                    var header = new Label(entry.RendererName);
                    header.style.marginTop = 8;
                    header.style.marginBottom = 2;
                    _blendShapeListView.Add(header);
                }

                var row = CreateBlendShapeSliderRow(i);
                _blendShapeListView.Add(row);
            }
        }

        private VisualElement CreateBlendShapeSliderRow(int entryIndex)
        {
            var entry = _blendShapeEntries[entryIndex];

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 1;

            var nameLabel = new Label(entry.BlendShapeName);
            nameLabel.style.width = 160;
            nameLabel.style.minWidth = 100;
            nameLabel.style.overflow = Overflow.Hidden;
            nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.Add(nameLabel);

            var slider = new Slider(0f, 1f);
            slider.value = entry.Value;
            slider.style.flexGrow = 1;
            slider.style.minWidth = 80;

            var valueField = new FloatField();
            valueField.value = entry.Value;
            valueField.style.width = 55;
            valueField.style.marginLeft = 4;

            int capturedIndex = entryIndex;

            slider.RegisterValueChangedCallback(evt =>
            {
                _blendShapeEntries[capturedIndex].Value = evt.newValue;
                valueField.SetValueWithoutNotify(evt.newValue);
                ApplyBlendShapeToPreview(capturedIndex);
            });

            valueField.RegisterValueChangedCallback(evt =>
            {
                float clamped = Mathf.Clamp01(evt.newValue);
                _blendShapeEntries[capturedIndex].Value = clamped;
                slider.SetValueWithoutNotify(clamped);
                valueField.SetValueWithoutNotify(clamped);
                ApplyBlendShapeToPreview(capturedIndex);
            });

            row.Add(slider);
            row.Add(valueField);

            return row;
        }

        private void OnResetBlendShapes()
        {
            for (int i = 0; i < _blendShapeEntries.Count; i++)
            {
                _blendShapeEntries[i].Value = 0f;
            }

            RebuildBlendShapeList();
            ApplyAllBlendShapesToPreview();
        }

        private void OnCameraReset()
        {
            _previewWrapper.ResetCamera();
            _previewContainer.MarkDirtyRepaint();
        }

        private void OnSavePreviewClicked()
        {
            ConfigureSavePreviewDependencies();

            var path = _savePreviewPathProvider();
            if (string.IsNullOrEmpty(path))
                return;

            Texture2D texture = null;
            try
            {
                texture = _previewTextureCapture(PreviewSize, PreviewSize);
                if (texture == null)
                {
                    ShowStatus("PNG 保存用のプレビュー画像を取得できませんでした。", isError: true);
                    return;
                }

                var pngBytes = texture.EncodeToPNG();
                _pngFileWriter(path, pngBytes);
                ShowStatus($"プレビュー PNG を保存しました: {path}", isError: false);
            }
            catch (Exception ex)
            {
                ShowStatus($"プレビュー PNG 保存エラー: {ex.Message}", isError: true);
                Debug.LogError($"[ExpressionCreatorWindow] プレビュー PNG 保存エラー: {ex}");
            }
            finally
            {
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        // ========================================
        // プレビュー
        // ========================================

        private void SetupPreview()
        {
            _previewWrapper.Setup(_targetObject);

            if (_previewWrapper.IsInitialized)
            {
                _skinnedMeshRenderers = _previewWrapper.PreviewInstance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            }

            ApplyAllBlendShapesToPreview();
        }

        private void ApplyBlendShapeToPreview(int entryIndex)
        {
            if (!_previewWrapper.IsInitialized || _skinnedMeshRenderers == null)
                return;

            var entry = _blendShapeEntries[entryIndex];

            if (entry.RendererIndex >= 0 && entry.RendererIndex < _skinnedMeshRenderers.Length)
            {
                var smr = _skinnedMeshRenderers[entry.RendererIndex];
                if (smr != null && smr.sharedMesh != null)
                {
                    smr.SetBlendShapeWeight(entry.BlendShapeIndex, entry.Value * 100f);
                }
            }

            _previewContainer?.MarkDirtyRepaint();
        }

        private void ApplyAllBlendShapesToPreview()
        {
            if (!_previewWrapper.IsInitialized || _skinnedMeshRenderers == null)
                return;

            for (int i = 0; i < _blendShapeEntries.Count; i++)
            {
                var entry = _blendShapeEntries[i];
                if (entry.RendererIndex >= 0 && entry.RendererIndex < _skinnedMeshRenderers.Length)
                {
                    var smr = _skinnedMeshRenderers[entry.RendererIndex];
                    if (smr != null && smr.sharedMesh != null)
                    {
                        smr.SetBlendShapeWeight(entry.BlendShapeIndex, entry.Value * 100f);
                    }
                }
            }

            _previewContainer?.MarkDirtyRepaint();
        }

        private void OnPreviewGUI()
        {
            if (!_previewWrapper.IsInitialized)
            {
                GUILayout.Label("モデルを選択してください。", EditorStyles.centeredGreyMiniLabel,
                    GUILayout.Width(PreviewSize), GUILayout.Height(PreviewSize));
                return;
            }

            var rect = GUILayoutUtility.GetRect(PreviewSize, PreviewSize);

            var evt = Event.current;
            if (evt.type == EventType.MouseDown && rect.Contains(evt.mousePosition))
                _previewContainer.CaptureMouse();
            else if (evt.type == EventType.MouseUp && _previewContainer.HasMouseCapture())
                _previewContainer.ReleaseMouse();

            if (_previewWrapper.HandleInput(rect))
            {
                _previewContainer.MarkDirtyRepaint();
                Repaint();
            }

            _previewWrapper.Render(rect);
        }

        // ========================================
        // AnimationClip ロード
        // ========================================

        private void OnClipFieldChanged(ChangeEvent<UnityEngine.Object> evt)
        {
            _targetClip = evt.newValue as AnimationClip;
            RestoreSliderValuesFromTargetClip();
        }

        private void OnCreateNewClipClicked()
        {
            ConfigureCreateClipDependencies();

            var path = _createClipPathProvider();
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                var clip = new AnimationClip();
                _clipAssetCreator(clip, path);
                _assetDatabaseSaveAssets();

                var loadedClip = _clipAssetLoader(path) ?? clip;
                _clipField.value = loadedClip;
                if (_targetClip != loadedClip)
                {
                    _targetClip = loadedClip;
                    RestoreSliderValuesFromTargetClip();
                }

                ShowStatus($"AnimationClip を作成しました: {path}", isError: false);
            }
            catch (Exception ex)
            {
                ShowStatus($"AnimationClip 作成エラー: {ex.Message}", isError: true);
                Debug.LogError($"[ExpressionCreatorWindow] AnimationClip 作成エラー: {ex}");
            }
        }

        /// <summary>
        /// 現在の <see cref="_targetClip"/> から <see cref="IExpressionAnimationClipSampler"/> 経由で
        /// BlendShape 値を取得し、スライダーへ復元する。clip 未設定時は何もしない。
        /// </summary>
        private void RestoreSliderValuesFromTargetClip()
        {
            if (_targetClip == null || _sampler == null || _blendShapeEntries.Count == 0)
                return;

            try
            {
                var values = ExpressionClipBakery.LoadBlendShapeValues(_targetClip, _sampler);
                for (int i = 0; i < _blendShapeEntries.Count; i++)
                {
                    var entry = _blendShapeEntries[i];
                    var key = (entry.RendererPath ?? string.Empty, entry.BlendShapeName ?? string.Empty);
                    entry.Value = values.TryGetValue(key, out var value) ? Mathf.Clamp01(value) : 0f;
                }

                RebuildBlendShapeList();
                ApplyAllBlendShapesToPreview();
                ShowStatus($"AnimationClip を読み込みました: {_targetClip.name}", isError: false);
            }
            catch (Exception ex)
            {
                ShowStatus($"AnimationClip 読み込みエラー: {ex.Message}", isError: true);
                Debug.LogError($"[ExpressionCreatorWindow] AnimationClip 読み込みエラー: {ex}");
            }
        }

        // ========================================
        // ベイク
        // ========================================

        private void OnBakeClicked()
        {
            if (_targetClip == null)
            {
                ShowStatus("ベイク対象の AnimationClip を割り当ててください。", isError: true);
                return;
            }

            if (_blendShapeEntries.Count == 0)
            {
                ShowStatus("BlendShape を持つモデルを選択してください。", isError: true);
                return;
            }

            var entries = new List<ExpressionClipBakery.BlendShapeBakeEntry>(_blendShapeEntries.Count);
            for (int i = 0; i < _blendShapeEntries.Count; i++)
            {
                var e = _blendShapeEntries[i];
                if (e.Value > 0f)
                {
                    entries.Add(new ExpressionClipBakery.BlendShapeBakeEntry(
                        e.RendererPath, e.BlendShapeName, e.Value));
                }
            }

            var transitionDuration = Expression.DefaultTransitionDuration;
            var transitionCurvePreset = TransitionCurvePreset.Linear;

            try
            {
                Undo.RecordObject(_targetClip, "Expression ベイク");
                ExpressionClipBakery.Bake(_targetClip, entries, transitionDuration, transitionCurvePreset);
                EditorUtility.SetDirty(_targetClip);
                AssetDatabase.SaveAssetIfDirty(_targetClip);
                ShowStatus($"AnimationClip にベイクしました: {_targetClip.name}", isError: false);
            }
            catch (Exception ex)
            {
                ShowStatus($"ベイクエラー: {ex.Message}", isError: true);
                Debug.LogError($"[ExpressionCreatorWindow] ベイクエラー: {ex}");
            }
        }

        // ========================================
        // ヘルパー
        // ========================================

        private void ShowStatus(string message, bool isError)
        {
            if (_statusLabel == null)
                return;

            _statusLabel.text = message;

            _statusLabel.RemoveFromClassList(FacialControlStyles.StatusError);
            _statusLabel.RemoveFromClassList(FacialControlStyles.StatusSuccess);
            _statusLabel.AddToClassList(isError
                ? FacialControlStyles.StatusError
                : FacialControlStyles.StatusSuccess);

            _statusLabel.style.display = DisplayStyle.Flex;
        }

        private void ConfigureSavePreviewDependencies()
        {
            _savePreviewPathProvider ??= () => EditorUtility.SaveFilePanel(
                "プレビューを PNG として保存",
                "",
                "expression-preview.png",
                "png");
            _previewTextureCapture ??= (width, height) => _previewWrapper?.CapturePreviewTexture(width, height);
            _pngFileWriter ??= File.WriteAllBytes;
        }

        private void ConfigureCreateClipDependencies()
        {
            _createClipPathProvider ??= () => EditorUtility.SaveFilePanelInProject(
                "新規 AnimationClip を作成",
                "NewExpression",
                "anim",
                "");
            _clipAssetCreator ??= AssetDatabase.CreateAsset;
            _clipAssetLoader ??= AssetDatabase.LoadAssetAtPath<AnimationClip>;
            _assetDatabaseSaveAssets ??= AssetDatabase.SaveAssets;
        }

        private class BlendShapeEntry
        {
            public string RendererName;
            public int RendererIndex;
            public string RendererPath;
            public string BlendShapeName;
            public int BlendShapeIndex;
            public float Value;
        }
    }
}
