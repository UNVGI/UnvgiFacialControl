using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Hidano.FacialControl.Adapters.ScriptableObject;

namespace Hidano.FacialControl.Tests.EditMode.Editor.Inspector
{
    /// <summary>
    /// tasks.md 9.2: <c>FacialProfileSO_BonePoseView</c> の UI Toolkit 表示を検証する Red テスト（EditMode）
    /// (Req 9.1 / 9.2 / 9.4 / 9.6)。
    ///
    /// <para>
    /// 観測完了条件:
    /// <list type="bullet">
    ///   <item><c>Foldout</c> + <c>ListView</c> で BonePose 一覧が表示される（Req 9.1）</item>
    ///   <item>各エントリ行に boneName 入力（<c>DropdownField</c> または <c>TextField</c>）+
    ///     <c>Vector3Field</c>（X, Y, Z degrees）+ 削除ボタンが配置される（Req 9.2 / 9.4）</item>
    ///   <item>エントリ追加 / 削除 / 編集後に <see cref="EditorUtility.SetDirty(UnityEngine.Object)"/> が呼ばれ、
    ///     SO アセットがダーティになる（Req 9.4）</item>
    ///   <item>ランタイム asmdef からは参照不可（Editor asmdef 内に閉じる、Req 9.6）</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// 設計意図: 9.3 の Green 実装が未着手の段階でも本テストファイルがコンパイル可能であるよう、
    /// View クラスへのアクセスは <see cref="Assembly"/> + <see cref="Type"/> リフレクション経由で行う。
    /// 型解決失敗時は <see cref="Assert.IsNotNull(object, string)"/> で明示的に Red を出す。
    /// </para>
    ///
    /// _Requirements: 9.1, 9.2, 9.4, 9.6
    /// _Boundary: Editor.Inspector.FacialProfileSO_BonePoseView
    /// </summary>
    [TestFixture]
    public class FacialProfileSO_BonePoseViewTests
    {
        private const string ViewTypeFullName =
            "Hidano.FacialControl.Editor.Inspector.FacialProfileSO_BonePoseView";

        private const string EditorAssemblyName = "Hidano.FacialControl.Editor";
        private const string AdaptersAssemblyName = "Hidano.FacialControl.Adapters";

        private FacialProfileSO _so;
        private readonly List<UnityEngine.Object> _tracked = new List<UnityEngine.Object>();

        [SetUp]
        public void SetUp()
        {
            _so = UnityEngine.ScriptableObject.CreateInstance<FacialProfileSO>();
            _so.name = "BonePoseViewTestTarget";
            _tracked.Add(_so);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _tracked.Count; i++)
            {
                if (_tracked[i] != null)
                    UnityEngine.Object.DestroyImmediate(_tracked[i]);
            }
            _tracked.Clear();
            _so = null;
        }

        // ----------------------------------------------------------------
        // ヘルパー: View 型 / Assembly のリフレクション解決
        // ----------------------------------------------------------------

        private static Assembly ResolveEditorAssembly()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                var name = assemblies[i].GetName().Name;
                if (string.Equals(name, EditorAssemblyName, StringComparison.Ordinal))
                    return assemblies[i];
            }
            return null;
        }

        private static Assembly ResolveAdaptersAssembly()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                var name = assemblies[i].GetName().Name;
                if (string.Equals(name, AdaptersAssemblyName, StringComparison.Ordinal))
                    return assemblies[i];
            }
            return null;
        }

        private static Type ResolveViewType()
        {
            var asm = ResolveEditorAssembly();
            return asm?.GetType(ViewTypeFullName, throwOnError: false, ignoreCase: false);
        }

        private object CreateView(out IDisposable disposable)
        {
            var viewType = ResolveViewType();
            Assert.IsNotNull(
                viewType,
                $"型 '{ViewTypeFullName}' が Editor アセンブリに存在しません。"
                + " 9.3 で FacialProfileSO_BonePoseView を実装してください。");

            var ctor = viewType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(FacialProfileSO) },
                modifiers: null);
            Assert.IsNotNull(
                ctor,
                $"{ViewTypeFullName} に '(FacialProfileSO target)' を受け取るコンストラクタが必要です。");

            var instance = ctor.Invoke(new object[] { _so });
            disposable = instance as IDisposable;
            return instance;
        }

        private static VisualElement GetRootElement(object viewInstance)
        {
            var prop = viewInstance.GetType().GetProperty(
                "RootElement",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(
                prop,
                $"{ViewTypeFullName} に 'RootElement' プロパティが必要です。");
            Assert.AreEqual(
                typeof(VisualElement),
                prop.PropertyType,
                "'RootElement' プロパティの型は VisualElement である必要があります。");

            var root = prop.GetValue(viewInstance) as VisualElement;
            Assert.IsNotNull(root, "'RootElement' は null であってはなりません。");
            return root;
        }

        // ================================================================
        // Req 9.6: 型は Editor asmdef 内に閉じる（ランタイム不可視）
        // ================================================================

        [Test]
        public void ViewType_IsDefinedInEditorAssembly()
        {
            var editorAsm = ResolveEditorAssembly();
            Assert.IsNotNull(
                editorAsm,
                $"Editor アセンブリ '{EditorAssemblyName}' が AppDomain にロードされていません。");

            var viewType = editorAsm.GetType(ViewTypeFullName, throwOnError: false, ignoreCase: false);
            Assert.IsNotNull(
                viewType,
                $"型 '{ViewTypeFullName}' が Editor アセンブリに存在しません。"
                + " 9.3 で FacialProfileSO_BonePoseView を Editor.Inspector 名前空間に実装してください。");
        }

        [Test]
        public void ViewType_IsNotDefinedInRuntimeAdaptersAssembly()
        {
            var adaptersAsm = ResolveAdaptersAssembly();
            Assert.IsNotNull(
                adaptersAsm,
                $"Adapters アセンブリ '{AdaptersAssemblyName}' が AppDomain にロードされていません。");

            var viewType = adaptersAsm.GetType(ViewTypeFullName, throwOnError: false, ignoreCase: false);
            Assert.IsNull(
                viewType,
                "FacialProfileSO_BonePoseView はランタイム (Adapters) アセンブリから参照可能であってはなりません（Req 9.6）。");
        }

        // ================================================================
        // 構成検証: ctor + RootElement
        // ================================================================

        [Test]
        public void Constructor_AcceptingFacialProfileSO_CreatesInstance()
        {
            IDisposable disposable = null;
            try
            {
                var view = CreateView(out disposable);
                Assert.IsNotNull(view, "ctor 呼出後に View インスタンスが生成されている必要があります。");
            }
            finally
            {
                disposable?.Dispose();
            }
        }

        [Test]
        public void RootElement_ReturnsVisualElement_NotNull()
        {
            IDisposable disposable = null;
            try
            {
                var view = CreateView(out disposable);
                var root = GetRootElement(view);
                Assert.IsNotNull(root);
            }
            finally
            {
                disposable?.Dispose();
            }
        }

        // ================================================================
        // Req 9.1: Foldout + ListView で BonePose 一覧を表示する
        // ================================================================

        [Test]
        public void RootElement_ContainsFoldout_ForBonePoseList()
        {
            IDisposable disposable = null;
            try
            {
                var view = CreateView(out disposable);
                var root = GetRootElement(view);

                var foldout = root.Q<Foldout>();
                Assert.IsNotNull(
                    foldout,
                    "View ルート要素に Foldout が含まれている必要があります（Req 9.1）。");
            }
            finally
            {
                disposable?.Dispose();
            }
        }

        [Test]
        public void RootElement_ContainsListView_ForBonePoseEntries()
        {
            IDisposable disposable = null;
            try
            {
                var view = CreateView(out disposable);
                var root = GetRootElement(view);

                var listView = root.Q<ListView>();
                Assert.IsNotNull(
                    listView,
                    "View ルート要素に BonePose 一覧の ListView が含まれている必要があります（Req 9.1）。");
            }
            finally
            {
                disposable?.Dispose();
            }
        }

        // ================================================================
        // Req 9.2 / 9.4: エントリ行は boneName 入力 + Vector3Field + 削除ボタン
        // ================================================================

        [Test]
        public void EntryRow_ContainsBoneNameInput_AndVector3Field_AndDeleteButton()
        {
            IDisposable disposable = null;
            try
            {
                // 1 件 BonePose を仕込んで ListView の makeItem / bindItem を駆動する
                _so.BonePoses = new[]
                {
                    new BonePoseSerializable
                    {
                        id = "preset",
                        entries = new[]
                        {
                            new BonePoseEntrySerializable
                            {
                                boneName = "Head",
                                eulerXYZ = new Vector3(0f, 0f, 0f),
                            },
                        },
                    },
                };

                var view = CreateView(out disposable);
                var root = GetRootElement(view);

                var listView = root.Q<ListView>();
                Assert.IsNotNull(
                    listView,
                    "ListView が見つかりません（Req 9.1）。");
                Assert.IsNotNull(
                    listView.makeItem,
                    "ListView.makeItem は設定されている必要があります（Req 9.2）。");

                var row = listView.makeItem();
                Assert.IsNotNull(row, "makeItem() が null を返してはなりません。");

                bool hasBoneNameInput =
                    row.Q<DropdownField>() != null
                    || row.Q<PopupField<string>>() != null
                    || row.Q<TextField>() != null;
                Assert.IsTrue(
                    hasBoneNameInput,
                    "エントリ行に boneName 入力（DropdownField/PopupField/TextField のいずれか）が必要です（Req 9.2）。");

                var vector3Field = row.Q<Vector3Field>();
                Assert.IsNotNull(
                    vector3Field,
                    "エントリ行に Euler 入力用 Vector3Field（X/Y/Z degrees）が必要です（Req 9.2）。");

                var button = row.Q<Button>();
                Assert.IsNotNull(
                    button,
                    "エントリ行に削除ボタン（Button）が配置されている必要があります（Req 9.4）。");
            }
            finally
            {
                disposable?.Dispose();
            }
        }

        // ================================================================
        // Req 9.4: エントリ追加 / 削除 / 編集後に EditorUtility.SetDirty が呼ばれる
        // ================================================================

        [Test]
        public void AddBonePose_OperationMarksTargetAsDirty()
        {
            IDisposable disposable = null;
            try
            {
                var view = CreateView(out disposable);
                EditorUtility.ClearDirty(_so);
                Assert.IsFalse(
                    EditorUtility.IsDirty(_so),
                    "前提: 検証開始前に SO はダーティでない必要があります。");

                InvokeAddBonePose(view);

                Assert.IsTrue(
                    EditorUtility.IsDirty(_so),
                    "BonePose 追加後に EditorUtility.SetDirty(target) が呼ばれている必要があります（Req 9.4）。");
            }
            finally
            {
                disposable?.Dispose();
            }
        }

        [Test]
        public void RemoveBonePose_OperationMarksTargetAsDirty()
        {
            IDisposable disposable = null;
            try
            {
                _so.BonePoses = new[]
                {
                    new BonePoseSerializable
                    {
                        id = "preset",
                        entries = new BonePoseEntrySerializable[0],
                    },
                };

                var view = CreateView(out disposable);
                EditorUtility.ClearDirty(_so);
                Assert.IsFalse(
                    EditorUtility.IsDirty(_so),
                    "前提: 検証開始前に SO はダーティでない必要があります。");

                InvokeRemoveBonePose(view, index: 0);

                Assert.IsTrue(
                    EditorUtility.IsDirty(_so),
                    "BonePose 削除後に EditorUtility.SetDirty(target) が呼ばれている必要があります（Req 9.4）。");
            }
            finally
            {
                disposable?.Dispose();
            }
        }

        [Test]
        public void EditBonePoseEntry_OperationMarksTargetAsDirty()
        {
            IDisposable disposable = null;
            try
            {
                _so.BonePoses = new[]
                {
                    new BonePoseSerializable
                    {
                        id = "preset",
                        entries = new[]
                        {
                            new BonePoseEntrySerializable
                            {
                                boneName = "Head",
                                eulerXYZ = Vector3.zero,
                            },
                        },
                    },
                };

                var view = CreateView(out disposable);
                EditorUtility.ClearDirty(_so);
                Assert.IsFalse(
                    EditorUtility.IsDirty(_so),
                    "前提: 検証開始前に SO はダーティでない必要があります。");

                InvokeUpdateEntryEuler(view, bonePoseIndex: 0, entryIndex: 0, newEuler: new Vector3(10f, 0f, 0f));

                Assert.IsTrue(
                    EditorUtility.IsDirty(_so),
                    "BonePose エントリの Euler 編集後に EditorUtility.SetDirty(target) が呼ばれている必要があります（Req 9.4）。");
            }
            finally
            {
                disposable?.Dispose();
            }
        }

        // ----------------------------------------------------------------
        // 操作呼出ヘルパー: 9.3 の Green 実装が以下のいずれかを公開することを期待する
        //   1. internal void AddBonePose() / RemoveBonePoseAt(int) / UpdateEntryEuler(...)
        //   2. ルート要素配下の名前付き Button / Vector3Field を介した UI トリガ
        // 双方未提供の場合、テストは Red 状態で fail する（9.3 の契約として明示）。
        // ----------------------------------------------------------------

        private void InvokeAddBonePose(object viewInstance)
        {
            var viewType = viewInstance.GetType();

            var addMethod = viewType.GetMethod(
                "AddBonePose",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
            if (addMethod != null)
            {
                addMethod.Invoke(viewInstance, parameters: null);
                return;
            }

            Assert.Fail(
                "View に 'AddBonePose()' メソッド (internal 可) が必要です（Req 9.4）。"
                + " 9.3 でエントリ追加 API を公開してください。");
        }

        private void InvokeRemoveBonePose(object viewInstance, int index)
        {
            var viewType = viewInstance.GetType();

            var removeMethod = viewType.GetMethod(
                "RemoveBonePoseAt",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(int) },
                modifiers: null);
            if (removeMethod != null)
            {
                removeMethod.Invoke(viewInstance, parameters: new object[] { index });
                return;
            }

            Assert.Fail(
                "View に 'RemoveBonePoseAt(int)' メソッド (internal 可) が必要です（Req 9.4）。"
                + " 9.3 でエントリ削除 API を公開してください。");
        }

        private void InvokeUpdateEntryEuler(
            object viewInstance,
            int bonePoseIndex,
            int entryIndex,
            Vector3 newEuler)
        {
            var viewType = viewInstance.GetType();

            var updateMethod = viewType.GetMethod(
                "UpdateEntryEuler",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(int), typeof(int), typeof(Vector3) },
                modifiers: null);
            if (updateMethod != null)
            {
                updateMethod.Invoke(
                    viewInstance,
                    parameters: new object[] { bonePoseIndex, entryIndex, newEuler });
                return;
            }

            Assert.Fail(
                "View に 'UpdateEntryEuler(int bonePoseIndex, int entryIndex, Vector3 newEuler)'"
                + " メソッド (internal 可) が必要です（Req 9.4）。9.3 でエントリ編集 API を公開してください。");
        }
    }
}
