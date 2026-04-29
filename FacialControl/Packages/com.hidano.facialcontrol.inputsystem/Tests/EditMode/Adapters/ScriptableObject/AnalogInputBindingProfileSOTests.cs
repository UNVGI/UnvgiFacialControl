using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hidano.FacialControl.Adapters.ScriptableObject;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.ScriptableObject
{
    /// <summary>
    /// <see cref="AnalogInputBindingProfileSO"/> の EditMode 検証 (Req 6.1, 6.2, 6.4, 6.6, 6.7, 9.3)。
    /// </summary>
    [TestFixture]
    public class AnalogInputBindingProfileSOTests
    {
        private const string SampleJson = @"{
  ""version"": ""1.0.0"",
  ""bindings"": [
    {
      ""sourceId"": ""analog-bonepose.right_stick"",
      ""sourceAxis"": 0,
      ""targetKind"": ""bonepose"",
      ""targetIdentifier"": ""RightEye"",
      ""targetAxis"": ""Y"",
      ""mapping"": {
        ""deadZone"": 0.1,
        ""scale"": 30.0,
        ""offset"": 0.0,
        ""curveType"": ""Linear"",
        ""curveKeyFrames"": [],
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
        private string _tempFile;

        [TearDown]
        public void TearDown()
        {
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

        [Test]
        public void IsScriptableObject_NotInputBindingProfileSO()
        {
            _so = UnityEngine.ScriptableObject.CreateInstance<AnalogInputBindingProfileSO>();

            Assert.IsInstanceOf<UnityEngine.ScriptableObject>(_so);
            // 離散トリガー側 SO とは別の型である (Req 9.3 並走)
            Assert.IsNotInstanceOf<InputBindingProfileSO>(_so);
        }

        [Test]
        public void JsonText_GetterSetter_Roundtrip()
        {
            _so = UnityEngine.ScriptableObject.CreateInstance<AnalogInputBindingProfileSO>();

            _so.JsonText = SampleJson;
            Assert.AreEqual(SampleJson, _so.JsonText);
        }

        [Test]
        public void ToDomain_WithEmptyJsonText_ReturnsEmptyProfile()
        {
            _so = UnityEngine.ScriptableObject.CreateInstance<AnalogInputBindingProfileSO>();

            var profile = _so.ToDomain();

            Assert.AreEqual(0, profile.Bindings.Length);
        }

        [Test]
        public void ToDomain_WithSampleJsonText_ReturnsParsedProfile()
        {
            _so = UnityEngine.ScriptableObject.CreateInstance<AnalogInputBindingProfileSO>();
            _so.JsonText = SampleJson;

            var profile = _so.ToDomain();

            Assert.AreEqual("1.0.0", profile.Version);
            Assert.AreEqual(2, profile.Bindings.Length);

            var bindings = profile.Bindings.Span;
            Assert.AreEqual("analog-bonepose.right_stick", bindings[0].SourceId);
            Assert.AreEqual(AnalogBindingTargetKind.BonePose, bindings[0].TargetKind);
            Assert.AreEqual("RightEye", bindings[0].TargetIdentifier);
            Assert.AreEqual(AnalogTargetAxis.Y, bindings[0].TargetAxis);

            Assert.AreEqual(AnalogBindingTargetKind.BlendShape, bindings[1].TargetKind);
            Assert.AreEqual("Mouth_A", bindings[1].TargetIdentifier);
        }

        [Test]
        public void ToDomain_RoundTrip_PreservesValues()
        {
            _so = UnityEngine.ScriptableObject.CreateInstance<AnalogInputBindingProfileSO>();
            _so.JsonText = SampleJson;

            var first = _so.ToDomain();

            // 再保存 → 別 SO に再ロードしても値が同値で復元される
            var resaved = Hidano.FacialControl.Adapters.Json.AnalogInputBindingJsonLoader.Save(first);
            var so2 = UnityEngine.ScriptableObject.CreateInstance<AnalogInputBindingProfileSO>();
            try
            {
                so2.JsonText = resaved;
                var second = so2.ToDomain();

                Assert.AreEqual(first.Version, second.Version);
                Assert.AreEqual(first.Bindings.Length, second.Bindings.Length);

                var a = first.Bindings.Span;
                var b = second.Bindings.Span;
                for (int i = 0; i < a.Length; i++)
                {
                    Assert.AreEqual(a[i].SourceId, b[i].SourceId);
                    Assert.AreEqual(a[i].SourceAxis, b[i].SourceAxis);
                    Assert.AreEqual(a[i].TargetKind, b[i].TargetKind);
                    Assert.AreEqual(a[i].TargetIdentifier, b[i].TargetIdentifier);
                    Assert.AreEqual(a[i].TargetAxis, b[i].TargetAxis);
                    Assert.AreEqual(a[i].Mapping.DeadZone, b[i].Mapping.DeadZone, 1e-5f);
                    Assert.AreEqual(a[i].Mapping.Scale, b[i].Mapping.Scale, 1e-5f);
                    Assert.AreEqual(a[i].Mapping.Offset, b[i].Mapping.Offset, 1e-5f);
                    Assert.AreEqual(a[i].Mapping.Min, b[i].Mapping.Min, 1e-5f);
                    Assert.AreEqual(a[i].Mapping.Max, b[i].Mapping.Max, 1e-5f);
                    Assert.AreEqual(a[i].Mapping.Invert, b[i].Mapping.Invert);
                    Assert.AreEqual(a[i].Mapping.Curve.Type, b[i].Mapping.Curve.Type);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so2);
            }
        }

        [Test]
        public void ImportJson_ReadsFileIntoJsonText()
        {
            _so = UnityEngine.ScriptableObject.CreateInstance<AnalogInputBindingProfileSO>();
            _tempFile = Path.Combine(Path.GetTempPath(),
                $"analog_binding_profile_so_test_{Guid.NewGuid():N}.json");
            File.WriteAllText(_tempFile, SampleJson);

            _so.ImportJson(_tempFile);

            Assert.AreEqual(SampleJson, _so.JsonText);
            var profile = _so.ToDomain();
            Assert.AreEqual(2, profile.Bindings.Length);
        }

        [Test]
        public void ExportJson_WritesJsonTextToFile()
        {
            _so = UnityEngine.ScriptableObject.CreateInstance<AnalogInputBindingProfileSO>();
            _so.JsonText = SampleJson;
            _tempFile = Path.Combine(Path.GetTempPath(),
                $"analog_binding_profile_so_test_{Guid.NewGuid():N}.json");

            _so.ExportJson(_tempFile);

            Assert.IsTrue(File.Exists(_tempFile));
            var contents = File.ReadAllText(_tempFile);
            Assert.AreEqual(SampleJson, contents);
        }

        [Test]
        public void ImportExport_RoundTrip_ProducesEquivalentProfile()
        {
            _so = UnityEngine.ScriptableObject.CreateInstance<AnalogInputBindingProfileSO>();
            _so.JsonText = SampleJson;
            _tempFile = Path.Combine(Path.GetTempPath(),
                $"analog_binding_profile_so_test_{Guid.NewGuid():N}.json");

            _so.ExportJson(_tempFile);

            var so2 = UnityEngine.ScriptableObject.CreateInstance<AnalogInputBindingProfileSO>();
            try
            {
                so2.ImportJson(_tempFile);

                var first = _so.ToDomain();
                var second = so2.ToDomain();

                Assert.AreEqual(first.Version, second.Version);
                Assert.AreEqual(first.Bindings.Length, second.Bindings.Length);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so2);
            }
        }

        [Test]
        public void HasJsonTextSerializedField()
        {
            FieldInfo field = typeof(AnalogInputBindingProfileSO).GetField(
                "_jsonText", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "_jsonText フィールドが AnalogInputBindingProfileSO に見つかりません。");
            Assert.IsTrue(field.IsDefined(typeof(SerializeField), inherit: false),
                "_jsonText には [SerializeField] が付与されている必要があります。");
        }

        [Test]
        public void HasStreamingAssetPathSerializedField()
        {
            FieldInfo field = typeof(AnalogInputBindingProfileSO).GetField(
                "_streamingAssetPath", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "_streamingAssetPath フィールドが AnalogInputBindingProfileSO に見つかりません。");
            Assert.IsTrue(field.IsDefined(typeof(SerializeField), inherit: false),
                "_streamingAssetPath には [SerializeField] が付与されている必要があります。");
        }

        [Test]
        public void HasCreateAssetMenuAttribute()
        {
            var attr = typeof(AnalogInputBindingProfileSO).GetCustomAttribute<CreateAssetMenuAttribute>();
            Assert.IsNotNull(attr, "AnalogInputBindingProfileSO に [CreateAssetMenu] 属性が必要です。");
            StringAssert.Contains("FacialControl/Analog Input Binding Profile", attr.menuName);
        }
    }
}
