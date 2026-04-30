using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Hidano.FacialControl.InputSystem.Adapters.ScriptableObject;

namespace Hidano.FacialControl.InputSystem.Tests.EditMode.Editor.Inspector
{
    /// <summary>
    /// Task 5: <c>FacialCharacterSOInspector</c> の UI Toolkit Inspector 検証。
    /// <para>
    /// 観測完了条件:
    /// <list type="bullet">
    ///   <item><c>CreateInspectorGUI()</c> が例外なく <see cref="VisualElement"/> を返すこと</item>
    ///   <item>7 つのセクション (入力 / キーバインディング / アナログバインディング / レイヤー /
    ///         Expression / BonePose / デバッグ情報) が <see cref="Foldout"/> として存在すること</item>
    ///   <item>主要 SerializedProperty (inputActionAsset / actionMapName / expressionBindings /
    ///         analogBindings / layers / expressions / bonePoses / rendererPaths) に
    ///         相当する VisualElement が Inspector ルート内にあること (PropertyField / ObjectField / DropdownField 経由)</item>
    /// </list>
    /// </para>
    /// </summary>
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
                $"型 '{EditorTypeFullName}' が Editor アセンブリ '{EditorAssemblyName}' に存在しません。"
                + " Task 5 で FacialCharacterSOInspector を実装してください。");

            _editor = UnityEditor.Editor.CreateEditor(_so, editorType);
            Assert.IsNotNull(_editor, "UnityEditor.Editor.CreateEditor が null を返しました。");

            var root = _editor.CreateInspectorGUI();
            Assert.IsNotNull(root, "CreateInspectorGUI() は null を返してはなりません。");
            return root;
        }

        // ----------------------------------------------------------------
        // 1. CreateInspectorGUI が例外を投げず VisualElement を返す
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

        // ----------------------------------------------------------------
        // 2. 7 セクションが Foldout として存在する
        // ----------------------------------------------------------------

        [Test]
        public void Inspector_HasInputSectionFoldout()
        {
            var root = CreateInspectorGuiAndReturnRoot();
            var foldout = root.Q<Foldout>(name: "facial-character-input-foldout");
            Assert.IsNotNull(foldout, "入力 (Input) セクションの Foldout が見つかりません。");
        }

        [Test]
        public void Inspector_HasExpressionBindingsSectionFoldout()
        {
            var root = CreateInspectorGuiAndReturnRoot();
            var foldout = root.Q<Foldout>(name: "facial-character-expression-bindings-foldout");
            Assert.IsNotNull(foldout, "キーバインディングセクションの Foldout が見つかりません。");
        }

        [Test]
        public void Inspector_HasAnalogBindingsSectionFoldout()
        {
            var root = CreateInspectorGuiAndReturnRoot();
            var foldout = root.Q<Foldout>(name: "facial-character-analog-bindings-foldout");
            Assert.IsNotNull(foldout, "アナログバインディングセクションの Foldout が見つかりません。");
        }

        [Test]
        public void Inspector_HasLayersSectionFoldout()
        {
            var root = CreateInspectorGuiAndReturnRoot();
            var foldout = root.Q<Foldout>(name: "facial-character-layers-foldout");
            Assert.IsNotNull(foldout, "レイヤー (Layers) セクションの Foldout が見つかりません。");
        }

        [Test]
        public void Inspector_HasExpressionsSectionFoldout()
        {
            var root = CreateInspectorGuiAndReturnRoot();
            var foldout = root.Q<Foldout>(name: "facial-character-expressions-foldout");
            Assert.IsNotNull(foldout, "Expression セクションの Foldout が見つかりません。");
        }

        [Test]
        public void Inspector_HasBonePosesSectionFoldout()
        {
            var root = CreateInspectorGuiAndReturnRoot();
            var foldout = root.Q<Foldout>(name: "facial-character-boneposes-foldout");
            Assert.IsNotNull(foldout, "BonePose セクションの Foldout が見つかりません。");
        }

        [Test]
        public void Inspector_HasDebugSectionFoldout()
        {
            var root = CreateInspectorGuiAndReturnRoot();
            var foldout = root.Q<Foldout>(name: "facial-character-debug-foldout");
            Assert.IsNotNull(foldout, "デバッグ情報 (Debug) セクションの Foldout が見つかりません。");
        }

        [Test]
        public void Inspector_HasAllSevenSectionFoldouts()
        {
            var root = CreateInspectorGuiAndReturnRoot();
            string[] expectedFoldoutNames = new[]
            {
                "facial-character-input-foldout",
                "facial-character-expression-bindings-foldout",
                "facial-character-analog-bindings-foldout",
                "facial-character-layers-foldout",
                "facial-character-expressions-foldout",
                "facial-character-boneposes-foldout",
                "facial-character-debug-foldout",
            };
            foreach (var name in expectedFoldoutNames)
            {
                var foldout = root.Q<Foldout>(name: name);
                Assert.IsNotNull(foldout, $"Foldout name='{name}' が見つかりません。");
            }
        }

        // ----------------------------------------------------------------
        // 3. 主要 SerializedProperty に相当する VisualElement の bind 確認
        // ----------------------------------------------------------------

        /// <summary>
        /// VisualElement ツリー内に bindingPath が指定 propertyPath と一致する要素が
        /// 1 つ以上存在することを検証する。UI Toolkit の <see cref="IBindable"/>
        /// 実装は bindingPath プロパティを持ち、PropertyField / ObjectField /
        /// IntegerField / TextField などはこの経路で SerializedProperty にバインドされる。
        /// </summary>
        private static bool ContainsBindingPath(VisualElement root, string propertyPath)
        {
            if (root == null || string.IsNullOrEmpty(propertyPath))
            {
                return false;
            }

            // PropertyField の場合
            foreach (var pf in root.Query<PropertyField>().Build())
            {
                if (string.Equals(pf.bindingPath, propertyPath, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            // 任意の IBindable な VisualElement (ObjectField, TextField, IntegerField 等) を確認
            // BindableElement 経由でアクセスする
            foreach (var element in root.Query<BindableElement>().Build())
            {
                if (string.Equals(element.bindingPath, propertyPath, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void Inspector_BindsInputActionAssetProperty()
        {
            var root = CreateInspectorGuiAndReturnRoot();
            Assert.IsTrue(ContainsBindingPath(root, "_inputActionAsset"),
                "InputActionAsset プロパティに bind された VisualElement が存在しません。");
        }

        [Test]
        public void Inspector_BindsActionMapNameProperty()
        {
            var root = CreateInspectorGuiAndReturnRoot();
            Assert.IsTrue(ContainsBindingPath(root, "_actionMapName"),
                "actionMapName プロパティに bind された VisualElement が存在しません。");
        }

        [Test]
        public void Inspector_BindsExpressionBindingsProperty()
        {
            var root = CreateInspectorGuiAndReturnRoot();
            // ListView (BindableElement) の itemsSource は手動なので bindingPath は付かない。
            // ここでは ListView の存在を検証する。
            var listView = root.Q<Foldout>(name: "facial-character-expression-bindings-foldout")
                ?.Q<ListView>();
            Assert.IsNotNull(listView,
                "キーバインディングセクション内に ListView が存在する必要があります (_expressionBindings)。");
        }

        [Test]
        public void Inspector_BindsAnalogBindingsProperty()
        {
            var root = CreateInspectorGuiAndReturnRoot();
            var listView = root.Q<Foldout>(name: "facial-character-analog-bindings-foldout")
                ?.Q<ListView>();
            Assert.IsNotNull(listView,
                "アナログバインディングセクション内に ListView が存在する必要があります (_analogBindings)。");
        }

        [Test]
        public void Inspector_BindsLayersProperty()
        {
            var root = CreateInspectorGuiAndReturnRoot();
            Assert.IsTrue(ContainsBindingPath(root, "_layers"),
                "Layers プロパティに bind された VisualElement が存在しません。");
        }

        [Test]
        public void Inspector_BindsExpressionsProperty()
        {
            var root = CreateInspectorGuiAndReturnRoot();
            Assert.IsTrue(ContainsBindingPath(root, "_expressions"),
                "Expressions プロパティに bind された VisualElement が存在しません。");
        }

        [Test]
        public void Inspector_BindsRendererPathsProperty()
        {
            var root = CreateInspectorGuiAndReturnRoot();
            Assert.IsTrue(ContainsBindingPath(root, "_rendererPaths"),
                "RendererPaths プロパティに bind された VisualElement が存在しません。");
        }

        [Test]
        public void Inspector_BindsBonePosesProperty()
        {
            var root = CreateInspectorGuiAndReturnRoot();
            Assert.IsTrue(ContainsBindingPath(root, "_bonePoses"),
                "BonePoses プロパティに bind された VisualElement が存在しません。");
        }
    }
}
