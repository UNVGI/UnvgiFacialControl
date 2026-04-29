using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Hidano.FacialControl.Adapters.ScriptableObject;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.ScriptableObjectTests
{
    /// <summary>
    /// tasks.md 5.1: <see cref="FacialProfileSO"/> に <c>_bonePoses</c>
    /// Serializable フィールドが追加されることを検証する Red テスト（EditMode）
    /// (Req 8.1, 8.4, 10.1)。
    ///
    /// <para>
    /// 観測完了条件:
    /// <list type="bullet">
    ///     <item>
    ///         <c>BonePoseSerializable</c>（<c>id</c>, <c>entries[]</c>）/
    ///         <c>BonePoseEntrySerializable</c>（<c>boneName</c>, <c>Vector3 eulerXYZ</c>）が
    ///         <see cref="System.SerializableAttribute"/> 付きで定義されていること
    ///     </item>
    ///     <item>
    ///         <see cref="FacialProfileSO"/> の <c>_bonePoses</c> フィールドが
    ///         <see cref="UnityEditor.SerializedProperty"/> として読み取れること
    ///     </item>
    ///     <item>
    ///         既存 SO アセット（<c>_bonePoses</c> 未設定 = 既定生成）が空配列で初期化されること
    ///         （Req 8.4 / 10.1）
    ///     </item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// 設計意図: ランタイム型に対するテストは reflection で行い、5.2 の Green 実装前でも
    /// このファイルがコンパイルできるようにしている（types が無い場合は Assert で fail）。
    /// </para>
    ///
    /// _Requirements: 8.1, 8.4, 10.1
    /// _Boundary: Adapters.ScriptableObject.FacialProfileSO
    /// </summary>
    [TestFixture]
    public class FacialProfileSOBonePoseTests
    {
        private const string BonePoseSerializableTypeName =
            "Hidano.FacialControl.Adapters.ScriptableObject.BonePoseSerializable";
        private const string BonePoseEntrySerializableTypeName =
            "Hidano.FacialControl.Adapters.ScriptableObject.BonePoseEntrySerializable";
        private const string BonePosesFieldName = "_bonePoses";

        private static Assembly AdaptersAssembly => typeof(FacialProfileSO).Assembly;

        private static Type ResolveType(string fullName)
        {
            return AdaptersAssembly.GetType(fullName, throwOnError: false, ignoreCase: false);
        }

        // ================================================================
        // BonePoseSerializable / BonePoseEntrySerializable の Serializable 定義
        // (Req 8.1)
        // ================================================================

        [Test]
        public void BonePoseSerializable_TypeIsDefined_WithSerializableAttribute()
        {
            var type = ResolveType(BonePoseSerializableTypeName);

            Assert.IsNotNull(
                type,
                $"型 '{BonePoseSerializableTypeName}' が存在しません。Adapters.ScriptableObject 名前空間に定義してください。");
            Assert.IsTrue(
                type.IsDefined(typeof(SerializableAttribute), inherit: false),
                $"{type.FullName} に [Serializable] 属性が必要です（JsonUtility / SerializedObject 連携のため）。");
        }

        [Test]
        public void BonePoseSerializable_HasIdField_OfTypeString()
        {
            var type = ResolveType(BonePoseSerializableTypeName);
            Assert.IsNotNull(type, $"型 '{BonePoseSerializableTypeName}' が存在しません。");

            var field = type.GetField(
                "id",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.IsNotNull(field, "BonePoseSerializable に 'id' フィールドが必要です。");
            Assert.AreEqual(typeof(string), field.FieldType, "'id' は string 型である必要があります。");
        }

        [Test]
        public void BonePoseSerializable_HasEntriesField_OfTypeBonePoseEntrySerializableArray()
        {
            var type = ResolveType(BonePoseSerializableTypeName);
            Assert.IsNotNull(type, $"型 '{BonePoseSerializableTypeName}' が存在しません。");
            var entryType = ResolveType(BonePoseEntrySerializableTypeName);
            Assert.IsNotNull(entryType, $"型 '{BonePoseEntrySerializableTypeName}' が存在しません。");

            var field = type.GetField(
                "entries",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.IsNotNull(field, "BonePoseSerializable に 'entries' フィールドが必要です。");
            Assert.IsTrue(field.FieldType.IsArray, "'entries' は配列型である必要があります。");
            Assert.AreEqual(
                entryType,
                field.FieldType.GetElementType(),
                "'entries' は BonePoseEntrySerializable[] でなければなりません。");
        }

        [Test]
        public void BonePoseEntrySerializable_TypeIsDefined_WithSerializableAttribute()
        {
            var type = ResolveType(BonePoseEntrySerializableTypeName);

            Assert.IsNotNull(
                type,
                $"型 '{BonePoseEntrySerializableTypeName}' が存在しません。Adapters.ScriptableObject 名前空間に定義してください。");
            Assert.IsTrue(
                type.IsDefined(typeof(SerializableAttribute), inherit: false),
                $"{type.FullName} に [Serializable] 属性が必要です。");
        }

        [Test]
        public void BonePoseEntrySerializable_HasBoneNameField_OfTypeString()
        {
            var type = ResolveType(BonePoseEntrySerializableTypeName);
            Assert.IsNotNull(type, $"型 '{BonePoseEntrySerializableTypeName}' が存在しません。");

            var field = type.GetField(
                "boneName",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.IsNotNull(field, "BonePoseEntrySerializable に 'boneName' フィールドが必要です。");
            Assert.AreEqual(typeof(string), field.FieldType, "'boneName' は string 型である必要があります。");
        }

        [Test]
        public void BonePoseEntrySerializable_HasEulerXYZField_OfTypeVector3()
        {
            var type = ResolveType(BonePoseEntrySerializableTypeName);
            Assert.IsNotNull(type, $"型 '{BonePoseEntrySerializableTypeName}' が存在しません。");

            var field = type.GetField(
                "eulerXYZ",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.IsNotNull(field, "BonePoseEntrySerializable に 'eulerXYZ' フィールドが必要です。");
            Assert.AreEqual(typeof(Vector3), field.FieldType, "'eulerXYZ' は Vector3 型である必要があります（degrees）。");
        }

        // ================================================================
        // FacialProfileSO._bonePoses が SerializedProperty として読み取れる
        // (Req 8.1)
        // ================================================================

        [Test]
        public void FacialProfileSO_BonePosesField_IsDeclaredAsBonePoseSerializableArray()
        {
            var bonePoseType = ResolveType(BonePoseSerializableTypeName);
            Assert.IsNotNull(bonePoseType, $"型 '{BonePoseSerializableTypeName}' が存在しません。");

            var field = typeof(FacialProfileSO).GetField(
                BonePosesFieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            Assert.IsNotNull(field, $"FacialProfileSO に '{BonePosesFieldName}' フィールドが必要です。");
            Assert.IsTrue(field.FieldType.IsArray, $"'{BonePosesFieldName}' は配列型である必要があります。");
            Assert.AreEqual(
                bonePoseType,
                field.FieldType.GetElementType(),
                $"'{BonePosesFieldName}' は BonePoseSerializable[] でなければなりません。");
        }

        [Test]
        public void FacialProfileSO_BonePosesField_HasSerializeFieldAttribute()
        {
            var field = typeof(FacialProfileSO).GetField(
                BonePosesFieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            Assert.IsNotNull(field, $"FacialProfileSO に '{BonePosesFieldName}' フィールドが必要です。");
            Assert.IsTrue(
                field.IsDefined(typeof(SerializeField), inherit: false) || field.IsPublic,
                $"'{BonePosesFieldName}' は [SerializeField] 付きで宣言される必要があります（Inspector / SerializedObject 連携のため）。");
        }

        [Test]
        public void FacialProfileSO_BonePosesField_IsAccessibleAsSerializedProperty()
        {
            var so = UnityEngine.ScriptableObject.CreateInstance<FacialProfileSO>();
            try
            {
                using (var serializedObject = new SerializedObject(so))
                {
                    var property = serializedObject.FindProperty(BonePosesFieldName);

                    Assert.IsNotNull(
                        property,
                        $"SerializedObject から '{BonePosesFieldName}' を SerializedProperty として取得できる必要があります。");
                    Assert.IsTrue(
                        property.isArray,
                        $"'{BonePosesFieldName}' は配列 SerializedProperty である必要があります。");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        // ================================================================
        // 既定生成 SO は _bonePoses が空配列で初期化される (Req 8.4, 10.1)
        // ================================================================

        [Test]
        public void FacialProfileSO_DefaultInstance_HasEmptyBonePosesArray()
        {
            var so = UnityEngine.ScriptableObject.CreateInstance<FacialProfileSO>();
            try
            {
                using (var serializedObject = new SerializedObject(so))
                {
                    var property = serializedObject.FindProperty(BonePosesFieldName);

                    Assert.IsNotNull(
                        property,
                        $"SerializedObject から '{BonePosesFieldName}' を取得できません。フィールドを追加してください。");
                    Assert.AreEqual(
                        0,
                        property.arraySize,
                        $"既定生成された FacialProfileSO の '{BonePosesFieldName}' は空配列で初期化される必要があります（Req 8.4 / 10.1）。");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void FacialProfileSO_DefaultInstance_BonePosesFieldValue_IsNotNullOrEmpty()
        {
            var so = UnityEngine.ScriptableObject.CreateInstance<FacialProfileSO>();
            try
            {
                var field = typeof(FacialProfileSO).GetField(
                    BonePosesFieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                Assert.IsNotNull(field, $"FacialProfileSO に '{BonePosesFieldName}' フィールドが必要です。");

                var value = field.GetValue(so) as Array;

                // null も許容するが、Unity のシリアライズは配列を空配列で初期化するため
                // 既定では length == 0 であることを期待する。
                int length = value?.Length ?? 0;
                Assert.AreEqual(
                    0,
                    length,
                    $"既定生成された FacialProfileSO の '{BonePosesFieldName}' は要素 0 件であるべきです（Req 10.1）。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }
    }
}
