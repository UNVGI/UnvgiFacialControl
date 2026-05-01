using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.InputSystem.Adapters.ScriptableObject;

namespace Hidano.FacialControl.InputSystem.Tests.EditMode.Editor.Inspector
{
    /// <summary>
    /// Phase 5.1: <c>FacialCharacterSOInspector</c> の UI Toolkit Inspector 検証。
    /// Source of Truth が AnimationClip に切り替わった新 UX を検証する。
    /// </summary>
    /// <remarks>
    /// <para>必須テスト（spec tasks.md 5.1 Red 段階）:</para>
    /// <list type="bullet">
    ///   <item><c>AnimationClipNull_DisplaysValidationError_AndDisablesSave</c></item>
    ///   <item><c>DuplicateId_DisplaysValidationError_AndDisablesSave</c></item>
    ///   <item><c>ZeroLayerOverrideMask_DisplaysValidationError</c></item>
    ///   <item><c>NewExpression_GeneratesGuidId</c></item>
    ///   <item><c>AnimationClipChanged_RefreshesRendererPathSummary</c></item>
    ///   <item><c>AnimationClipAssigned_PopulatesNameFromFileName</c></item>
    /// </list>
    /// </remarks>
    [TestFixture]
    public class FacialCharacterSOInspectorTests
    {
        private const string EditorTypeFullName =
            "Hidano.FacialControl.InputSystem.Editor.Inspector.FacialCharacterSOInspector";

        private const string EditorAssemblyName = "Hidano.FacialControl.InputSystem.Editor";

        private FacialCharacterSO _so;
        private UnityEditor.Editor _editor;

        [SetUp]
        public void SetUp()
        {
            _so = ScriptableObject.CreateInstance<FacialCharacterSO>();
            _so.name = "FacialCharacterSOInspectorTestTarget";
        }

        [TearDown]
        public void TearDown()
        {
            if (_editor != null)
            {
                UnityEngine.Object.DestroyImmediate(_editor);
                _editor = null;
            }
            if (_so != null)
            {
                UnityEngine.Object.DestroyImmediate(_so);
                _so = null;
            }
        }

        // ----------------------------------------------------------------
        // ヘルパー
        // ----------------------------------------------------------------

        private static Assembly ResolveAssembly(string name)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                if (string.Equals(assemblies[i].GetName().Name, name, StringComparison.Ordinal))
                {
                    return assemblies[i];
                }
            }
            return null;
        }

        private static Type ResolveEditorType()
        {
            var asm = ResolveAssembly(EditorAssemblyName);
            return asm?.GetType(EditorTypeFullName, throwOnError: false, ignoreCase: false);
        }

        private VisualElement CreateInspectorGuiAndReturnRoot()
        {
            var editorType = ResolveEditorType();
            Assert.IsNotNull(editorType,
                $"型 '{EditorTypeFullName}' が Editor アセンブリ '{EditorAssemblyName}' に存在しません。");

            _editor = UnityEditor.Editor.CreateEditor(_so, editorType);
            Assert.IsNotNull(_editor, "UnityEditor.Editor.CreateEditor が null を返しました。");

            var root = _editor.CreateInspectorGUI();
            Assert.IsNotNull(root, "CreateInspectorGUI() は null を返してはなりません。");
            return root;
        }

        private static AnimationClip CreateBlendShapeAnimationClip(string clipName, string rendererPath, string blendShapeName, float value)
        {
            var clip = new AnimationClip { name = clipName };
            var binding = new EditorCurveBinding
            {
                path = rendererPath,
                propertyName = $"blendShape.{blendShapeName}",
                type = typeof(SkinnedMeshRenderer),
            };
            var curve = AnimationCurve.Constant(0f, 0f, value);
            AnimationUtility.SetEditorCurve(clip, binding, curve);
            return clip;
        }

        private void EnsureLayerExists(string layerName)
        {
            var so = new SerializedObject(_so);
            var layersProp = so.FindProperty("_layers");
            layersProp.InsertArrayElementAtIndex(layersProp.arraySize);
            var elem = layersProp.GetArrayElementAtIndex(layersProp.arraySize - 1);
            var nameProp = elem.FindPropertyRelative("name");
            if (nameProp != null) nameProp.stringValue = layerName;
            var priorityProp = elem.FindPropertyRelative("priority");
            if (priorityProp != null) priorityProp.intValue = 0;
            so.ApplyModifiedProperties();
        }

        private void AddExpression(string id, AnimationClip clip, string layer, params string[] overrideLayers)
        {
            var so = new SerializedObject(_so);
            var expressions = so.FindProperty("_expressions");
            expressions.InsertArrayElementAtIndex(expressions.arraySize);
            var elem = expressions.GetArrayElementAtIndex(expressions.arraySize - 1);
            elem.FindPropertyRelative("id").stringValue = id ?? string.Empty;
            elem.FindPropertyRelative("name").stringValue = clip != null ? clip.name : string.Empty;
            elem.FindPropertyRelative("layer").stringValue = layer ?? string.Empty;
            elem.FindPropertyRelative("animationClip").objectReferenceValue = clip;

            var maskProp = elem.FindPropertyRelative("layerOverrideMask");
            maskProp.ClearArray();
            if (overrideLayers != null)
            {
                for (int i = 0; i < overrideLayers.Length; i++)
                {
                    maskProp.InsertArrayElementAtIndex(i);
                    maskProp.GetArrayElementAtIndex(i).stringValue = overrideLayers[i];
                }
            }
            so.ApplyModifiedProperties();
        }

        private static T GetConstString<T>(string fieldName)
        {
            var t = ResolveEditorType();
            var field = t.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
            return (T)field.GetValue(null);
        }

        // ----------------------------------------------------------------
        // 基本: CreateInspectorGUI が例外を投げず VisualElement を返す
        // ----------------------------------------------------------------

        [Test]
        public void CreateInspectorGUI_ReturnsVisualElementWithoutException()
        {
            VisualElement root = null;
            Assert.DoesNotThrow(() =>
            {
                root = CreateInspectorGuiAndReturnRoot();
            });
            Assert.IsNotNull(root);
        }

        [Test]
        public void Inspector_HasAllRequiredFoldouts()
        {
            var root = CreateInspectorGuiAndReturnRoot();
            string[] expectedFoldoutNames = new[]
            {
                "facial-character-input-foldout",
                "facial-character-expression-bindings-foldout",
                "facial-character-analog-bindings-foldout",
                "facial-character-layers-foldout",
                "facial-character-expressions-foldout",
                "facial-character-debug-foldout",
            };
            foreach (var name in expectedFoldoutNames)
            {
                var foldout = root.Q<Foldout>(name: name);
                Assert.IsNotNull(foldout, $"Foldout name='{name}' が見つかりません。");
            }
        }

        // ----------------------------------------------------------------
        // 1. AnimationClipNull → validation エラー + Save 無効
        // ----------------------------------------------------------------

        [Test]
        public void AnimationClipNull_DisplaysValidationError_AndDisablesSave()
        {
            EnsureLayerExists("emotion");
            // AnimationClip を null のままで Expression を追加（mask は埋めて他のエラーを排除）
            AddExpression("expr-id-A", clip: null, layer: "emotion", overrideLayers: new[] { "emotion" });

            var root = CreateInspectorGuiAndReturnRoot();

            var helpBox = root.Q<HelpBox>(name: "facial-character-expressions-validation");
            Assert.IsNotNull(helpBox, "Expression セクションに validation HelpBox が必要です。");
            Assert.AreEqual(DisplayStyle.Flex, helpBox.style.display.value,
                "AnimationClip が null の Expression があるとき HelpBox は表示されるべきです。");

            var saveButton = root.Q<Button>(name: "facial-character-save-button");
            Assert.IsNotNull(saveButton, "Save ボタンが見つかりません。");
            Assert.IsFalse(saveButton.enabledSelf, "validation エラー時は Save ボタンが disabled であるべきです。");
        }

        // ----------------------------------------------------------------
        // 2. DuplicateId → validation エラー + Save 無効
        // ----------------------------------------------------------------

        [Test]
        public void DuplicateId_DisplaysValidationError_AndDisablesSave()
        {
            EnsureLayerExists("emotion");
            var clipA = CreateBlendShapeAnimationClip("ClipA", string.Empty, "Smile", 0.5f);
            var clipB = CreateBlendShapeAnimationClip("ClipB", string.Empty, "Smile", 0.7f);

            AddExpression("dup-id", clipA, "emotion", "emotion");
            AddExpression("dup-id", clipB, "emotion", "emotion");

            var root = CreateInspectorGuiAndReturnRoot();

            var helpBox = root.Q<HelpBox>(name: "facial-character-expressions-validation");
            Assert.IsNotNull(helpBox);
            Assert.AreEqual(DisplayStyle.Flex, helpBox.style.display.value,
                "Id が重複しているとき HelpBox は表示されるべきです。");
            StringAssert.Contains("dup-id", helpBox.text);

            var saveButton = root.Q<Button>(name: "facial-character-save-button");
            Assert.IsNotNull(saveButton);
            Assert.IsFalse(saveButton.enabledSelf,
                "validation エラー時は Save ボタンが disabled であるべきです。");
        }

        // ----------------------------------------------------------------
        // 3. Zero LayerOverrideMask → validation エラー
        // ----------------------------------------------------------------

        [Test]
        public void ZeroLayerOverrideMask_DisplaysValidationError()
        {
            EnsureLayerExists("emotion");
            var clip = CreateBlendShapeAnimationClip("ClipZero", string.Empty, "Smile", 0.5f);

            // overrideLayers を空にする → zero LayerOverrideMask
            AddExpression("expr-zero", clip, "emotion", new string[0]);

            var root = CreateInspectorGuiAndReturnRoot();

            var helpBox = root.Q<HelpBox>(name: "facial-character-expressions-validation");
            Assert.IsNotNull(helpBox);
            Assert.AreEqual(DisplayStyle.Flex, helpBox.style.display.value,
                "LayerOverrideMask が空の Expression があるとき HelpBox は表示されるべきです。");

            var saveButton = root.Q<Button>(name: "facial-character-save-button");
            Assert.IsNotNull(saveButton);
            Assert.IsFalse(saveButton.enabledSelf);
        }

        // ----------------------------------------------------------------
        // 4. 新規 Expression の Id 自動採番（GUID）
        // ----------------------------------------------------------------

        [Test]
        public void NewExpression_GeneratesGuidId()
        {
            EnsureLayerExists("emotion");
            // Id を空文字で Expression を追加
            AddExpression(string.Empty, clip: null, layer: "emotion", overrideLayers: new[] { "emotion" });

            var root = CreateInspectorGuiAndReturnRoot();

            // Inspector が生成されると BindExpressionRow の中で GUID が割当てられる。
            // SerializedObject 経由で検査。
            _editor.serializedObject.Update();
            var expressionsProp = _editor.serializedObject.FindProperty("_expressions");
            Assert.GreaterOrEqual(expressionsProp.arraySize, 1);
            var idProp = expressionsProp.GetArrayElementAtIndex(0).FindPropertyRelative("id");
            Assert.IsNotNull(idProp);
            // GUID "N" 形式 = 32 桁の hex
            Assert.AreEqual(32, idProp.stringValue.Length,
                $"自動採番された Id は GUID 'N' 形式（32 桁）であるべきです。実際: '{idProp.stringValue}'");
            Assert.IsTrue(Guid.TryParseExact(idProp.stringValue, "N", out _),
                "自動採番された Id は GUID として parse できる必要があります。");
        }

        // ----------------------------------------------------------------
        // 5. AnimationClip 変更時に RendererPath summary が refresh される
        // ----------------------------------------------------------------

        [Test]
        public void AnimationClipChanged_RefreshesRendererPathSummary()
        {
            EnsureLayerExists("emotion");
            var clip1 = CreateBlendShapeAnimationClip("ClipR1", "Body/Face", "Smile", 0.5f);

            AddExpression("expr-r", clip1, "emotion", "emotion");

            var root = CreateInspectorGuiAndReturnRoot();

            // RendererPath summary が AnimationClip からサンプリングされて存在することを確認
            var summary = root.Q<ListView>(name: "expression-row-renderer-summary");
            Assert.IsNotNull(summary,
                "Expression 行に RendererPath summary ListView が必要です。");
            // 初期状態で summary は read-only であるべき
            Assert.IsFalse(summary.enabledSelf,
                "RendererPath summary は read-only (disabled) であるべきです。");
        }

        // ----------------------------------------------------------------
        // 6. AnimationClip 割当時に Name がファイル名から派生される
        // ----------------------------------------------------------------

        [Test]
        public void AnimationClipAssigned_PopulatesNameFromFileName()
        {
            EnsureLayerExists("emotion");
            // Name を空にして Expression を追加し、AnimationClip を割り当てる
            AddExpression("expr-name", clip: null, layer: "emotion", overrideLayers: new[] { "emotion" });
            var so = new SerializedObject(_so);
            var nameProp = so.FindProperty("_expressions").GetArrayElementAtIndex(0).FindPropertyRelative("name");
            nameProp.stringValue = string.Empty;
            so.ApplyModifiedProperties();

            // Inspector を生成
            var root = CreateInspectorGuiAndReturnRoot();

            // AnimationClip の ObjectField に新しい clip を設定
            var clipField = root.Q<ObjectField>(name: "expression-row-clip-field");
            Assert.IsNotNull(clipField, "Expression 行に AnimationClip ObjectField が必要です。");

            var newClip = CreateBlendShapeAnimationClip("MySmileClip", string.Empty, "Smile", 0.5f);
            clipField.value = newClip;

            // Name が AnimationClip 名から派生されたか確認
            _editor.serializedObject.Update();
            var refreshedName = _editor.serializedObject.FindProperty("_expressions")
                .GetArrayElementAtIndex(0)
                .FindPropertyRelative("name")
                .stringValue;
            Assert.AreEqual("MySmileClip", refreshedName,
                "AnimationClip 割当時に Name はファイル名 (拡張子なし) から派生されるべきです (Req 1.2)。");
        }
    }
}
