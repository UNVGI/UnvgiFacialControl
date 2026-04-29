using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Hidano.FacialControl.Adapters.Json;
using Hidano.FacialControl.Adapters.ScriptableObject;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.InputSystem.Tests.EditMode.Editor.Inspector
{
    /// <summary>
    /// tasks.md 7.1: <c>AnalogInputBindingProfileSOEditor</c> の UI Toolkit Inspector 検証
    /// (Req 4.4 / 10.1〜10.6)。
    /// <para>
    /// 観測完了条件:
    /// <list type="bullet">
    ///   <item>UI Toolkit (<see cref="Foldout"/> + <see cref="ListView"/>) で bindings を読取専用列挙する (Req 10.1)</item>
    ///   <item>Import / Export ボタン経由で SO の <c>_jsonText</c> がファイルと同期する (Req 10.2 / 10.3)</item>
    ///   <item>Humanoid 自動割当で BonePose-target binding の <c>targetIdentifier</c> が
    ///     <see cref="Hidano.FacialControl.Adapters.Bone.HumanoidBoneAutoAssigner"/> 由来の bone 名で上書きされる (Req 4.4 / 10.4)</item>
    ///   <item>型は Editor asmdef 内に閉じ、ランタイム asmdef からは参照不可 (Req 10.6)</item>
    /// </list>
    /// </para>
    /// </summary>
    [TestFixture]
    public class AnalogInputBindingProfileSOEditorTests
    {
        private const string EditorTypeFullName =
            "Hidano.FacialControl.Editor.Inspector.AnalogInputBindingProfileSOEditor";

        private const string EditorAssemblyName = "Hidano.FacialControl.InputSystem.Editor";
        private const string AdaptersAssemblyName = "Hidano.FacialControl.InputSystem";
        private const string CoreAdaptersAssemblyName = "Hidano.FacialControl.Adapters";

        private const string SampleJson = @"{
  ""version"": ""1.0.0"",
  ""bindings"": [
    {
      ""sourceId"": ""analog-bonepose.right_stick"",
      ""sourceAxis"": 0,
      ""targetKind"": ""bonepose"",
      ""targetIdentifier"": ""LeftEye"",
      ""targetAxis"": ""Y"",
      ""mapping"": {
        ""deadZone"": 0.1,
        ""scale"": 30.0,
        ""offset"": 0.0,
        ""curveType"": ""Linear"",
        ""invert"": false,
        ""min"": -45.0,
        ""max"": 45.0
      }
    },
    {
      ""sourceId"": ""analog-bonepose.right_stick"",
      ""sourceAxis"": 1,
      ""targetKind"": ""bonepose"",
      ""targetIdentifier"": ""RightEye"",
      ""targetAxis"": ""X"",
      ""mapping"": {
        ""deadZone"": 0.1,
        ""scale"": 30.0,
        ""offset"": 0.0,
        ""curveType"": ""Linear"",
        ""invert"": false,
        ""min"": -45.0,
        ""max"": 45.0
      }
    },
    {
      ""sourceId"": ""analog-blendshape.arkit_jaw"",
      ""sourceAxis"": 0,
      ""targetKind"": ""blendshape"",
      ""targetIdentifier"": ""Mouth_A"",
      ""targetAxis"": ""X"",
      ""mapping"": {
        ""deadZone"": 0.0,
        ""scale"": 1.0,
        ""offset"": 0.0,
        ""curveType"": ""Linear"",
        ""invert"": false,
        ""min"": 0.0,
        ""max"": 1.0
      }
    }
  ]
}";

        private AnalogInputBindingProfileSO _so;
        private UnityEditor.Editor _editor;
        private string _tempFile;

        [SetUp]
        public void SetUp()
        {
            _so = UnityEngine.ScriptableObject.CreateInstance<AnalogInputBindingProfileSO>();
            _so.name = "AnalogInputBindingProfileSOEditorTestTarget";
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

            if (!string.IsNullOrEmpty(_tempFile) && File.Exists(_tempFile))
            {
                File.Delete(_tempFile);
            }
            _tempFile = null;
        }

        // ----------------------------------------------------------------
        // ヘルパー: Editor 型解決とインスタンス化
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

        private UnityEditor.Editor CreateEditor()
        {
            var editorType = ResolveEditorType();
            Assert.IsNotNull(editorType,
                $"型 '{EditorTypeFullName}' が Editor アセンブリ '{EditorAssemblyName}' に存在しません。"
                + " 7.1 で AnalogInputBindingProfileSOEditor を実装してください。");

            var editor = UnityEditor.Editor.CreateEditor(_so, editorType);
            Assert.IsNotNull(editor, "UnityEditor.Editor.CreateEditor が null を返しました。");
            return editor;
        }

        private VisualElement CreateInspectorGuiAndReturnRoot()
        {
            _editor = CreateEditor();
            var rootElement = _editor.CreateInspectorGUI();
            Assert.IsNotNull(rootElement, "CreateInspectorGUI() は null を返してはなりません。");
            return rootElement;
        }

        // ================================================================
        // Req 10.6: 型は Editor asmdef 内に閉じる（ランタイム不可視）
        // ================================================================

        [Test]
        public void EditorType_IsDefinedInEditorAssembly()
        {
            var editorAsm = ResolveAssembly(EditorAssemblyName);
            Assert.IsNotNull(editorAsm,
                $"Editor アセンブリ '{EditorAssemblyName}' が AppDomain にロードされていません。");

            var editorType = editorAsm.GetType(EditorTypeFullName, throwOnError: false, ignoreCase: false);
            Assert.IsNotNull(editorType,
                $"型 '{EditorTypeFullName}' が Editor アセンブリに存在しません。"
                + " 7.1 で AnalogInputBindingProfileSOEditor を Editor.Inspector 名前空間に実装してください。");
        }

        [Test]
        public void EditorType_IsNotDefinedInRuntimeAdaptersAssembly()
        {
            var inputsystemRuntimeAsm = ResolveAssembly(AdaptersAssemblyName);
            Assert.IsNotNull(inputsystemRuntimeAsm,
                $"InputSystem ランタイムアセンブリ '{AdaptersAssemblyName}' が AppDomain にロードされていません。");

            var typeInRuntime = inputsystemRuntimeAsm.GetType(EditorTypeFullName, throwOnError: false, ignoreCase: false);
            Assert.IsNull(typeInRuntime,
                "AnalogInputBindingProfileSOEditor は InputSystem ランタイムアセンブリから参照可能であってはなりません（Req 10.6）。");

            var coreAdaptersAsm = ResolveAssembly(CoreAdaptersAssemblyName);
            if (coreAdaptersAsm != null)
            {
                var typeInCore = coreAdaptersAsm.GetType(EditorTypeFullName, throwOnError: false, ignoreCase: false);
                Assert.IsNull(typeInCore,
                    "AnalogInputBindingProfileSOEditor はコア Adapters アセンブリから参照可能であってはなりません（Req 10.6）。");
            }
        }

        // ================================================================
        // Req 10.1: ListView に bindings を列挙する
        // ================================================================

        [Test]
        public void Inspector_ListView_EnumeratesBindings()
        {
            _so.JsonText = SampleJson;
            var root = CreateInspectorGuiAndReturnRoot();

            var listView = root.Q<ListView>();
            Assert.IsNotNull(listView,
                "Inspector ルート要素に bindings を列挙する ListView が含まれている必要があります（Req 10.1）。");

            // RefreshItems が呼ばれていることをアサート
            InvokeInternalMethod(_editor, "RefreshItems");

            var itemsSource = listView.itemsSource;
            Assert.IsNotNull(itemsSource, "ListView.itemsSource が null です。");
            Assert.AreEqual(3, itemsSource.Count,
                "ListView は SO の bindings 件数と一致する要素を保持する必要があります（Req 10.1）。");

            var foldout = root.Q<Foldout>();
            Assert.IsNotNull(foldout, "Inspector ルート要素に Foldout が含まれている必要があります（Req 10.1）。");
        }

        // ================================================================
        // Req 10.2: Import で _jsonText が更新されダーティ化される
        // ================================================================

        [Test]
        public void ImportJsonFromPath_UpdatesJsonText_AndMarksTargetDirty()
        {
            _tempFile = Path.Combine(Path.GetTempPath(),
                $"analog_binding_editor_test_{Guid.NewGuid():N}.json");
            File.WriteAllText(_tempFile, SampleJson);

            _ = CreateInspectorGuiAndReturnRoot();
            EditorUtility.ClearDirty(_so);
            Assert.IsFalse(EditorUtility.IsDirty(_so), "前提: 検証開始前に SO はダーティでない必要があります。");

            InvokeInternalMethod(_editor, "ImportJsonFromPath", new object[] { _tempFile });

            Assert.AreEqual(SampleJson, _so.JsonText,
                "Import 後、SO の JsonText が読み込んだファイル内容で更新されている必要があります（Req 10.2）。");
            Assert.IsTrue(EditorUtility.IsDirty(_so),
                "Import 後、EditorUtility.SetDirty(target) が呼ばれている必要があります（Req 10.2）。");
        }

        // ================================================================
        // Req 10.3: Export で JSON ファイルが生成される
        // ================================================================

        [Test]
        public void ExportJsonToPath_WritesJsonTextToFile()
        {
            _so.JsonText = SampleJson;
            _tempFile = Path.Combine(Path.GetTempPath(),
                $"analog_binding_editor_test_{Guid.NewGuid():N}.json");

            _ = CreateInspectorGuiAndReturnRoot();

            InvokeInternalMethod(_editor, "ExportJsonToPath", new object[] { _tempFile });

            Assert.IsTrue(File.Exists(_tempFile),
                $"Export 後、JSON ファイルが生成されている必要があります: {_tempFile}（Req 10.3）。");
            var contents = File.ReadAllText(_tempFile);
            Assert.AreEqual(SampleJson, contents,
                "Export 内容は SO の JsonText と一致する必要があります（Req 10.3）。");
        }

        // ================================================================
        // Req 4.4 / 10.4: Humanoid 自動割当で BonePose-target binding の
        //                 targetIdentifier が更新される
        // ================================================================

        [Test]
        public void AutoAssignEyeBoneNames_UpdatesBonePoseBindings_TargetIdentifier()
        {
            _so.JsonText = SampleJson;
            _ = CreateInspectorGuiAndReturnRoot();
            EditorUtility.ClearDirty(_so);

            const string LeftBone = "MyLeftEyeBone";
            const string RightBone = "MyRightEyeBone";

            InvokeInternalMethod(_editor, "AutoAssignEyeBoneNames", new object[] { LeftBone, RightBone });

            var profile = _so.ToDomain();
            Assert.AreEqual(3, profile.Bindings.Length, "binding 件数は変更されてはなりません。");

            var bindings = profile.Bindings.Span;
            int leftHits = 0, rightHits = 0;
            for (int i = 0; i < bindings.Length; i++)
            {
                var b = bindings[i];
                if (b.TargetKind != AnalogBindingTargetKind.BonePose) continue;
                if (string.Equals(b.TargetIdentifier, LeftBone, StringComparison.Ordinal)) leftHits++;
                if (string.Equals(b.TargetIdentifier, RightBone, StringComparison.Ordinal)) rightHits++;
            }
            Assert.AreEqual(1, leftHits,
                $"BonePose-target で targetIdentifier='LeftEye' のエントリが '{LeftBone}' に置換される必要があります（Req 4.4 / 10.4）。");
            Assert.AreEqual(1, rightHits,
                $"BonePose-target で targetIdentifier='RightEye' のエントリが '{RightBone}' に置換される必要があります（Req 4.4 / 10.4）。");

            // BlendShape-target binding は触らない
            bool blendshapeUntouched = false;
            for (int i = 0; i < bindings.Length; i++)
            {
                var b = bindings[i];
                if (b.TargetKind == AnalogBindingTargetKind.BlendShape
                    && string.Equals(b.TargetIdentifier, "Mouth_A", StringComparison.Ordinal))
                {
                    blendshapeUntouched = true;
                }
            }
            Assert.IsTrue(blendshapeUntouched,
                "BlendShape-target binding の targetIdentifier は Humanoid 自動割当で変更されてはなりません。");

            Assert.IsTrue(EditorUtility.IsDirty(_so),
                "Humanoid 自動割当後、EditorUtility.SetDirty(target) が呼ばれている必要があります。");
        }

        [Test]
        public void AutoAssignEyeBoneNames_WithEmptyBindings_DoesNotThrow()
        {
            _ = CreateInspectorGuiAndReturnRoot();

            Assert.DoesNotThrow(() =>
                InvokeInternalMethod(_editor, "AutoAssignEyeBoneNames", new object[] { "L", "R" }));
        }

        // ----------------------------------------------------------------
        // ヘルパー: internal メソッド呼出
        // ----------------------------------------------------------------

        private static void InvokeInternalMethod(object instance, string methodName, object[] parameters = null)
        {
            var method = instance.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(method,
                $"AnalogInputBindingProfileSOEditor に '{methodName}' メソッド (internal 可) が必要です。");
            method.Invoke(instance, parameters);
        }
    }
}
