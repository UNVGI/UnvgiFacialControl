using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hidano.FacialControl.Adapters.Json;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.Json
{
    [TestFixture]
    public class AnalogInputBindingJsonLoaderTests
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

        [Test]
        public void Load_ValidSampleJson_ReturnsProfileWithTwoEntries()
        {
            var profile = AnalogInputBindingJsonLoader.Load(SampleJson);

            Assert.AreEqual("1.0.0", profile.Version);
            Assert.AreEqual(2, profile.Bindings.Length);

            var bindings = profile.Bindings.Span;
            Assert.AreEqual("analog-bonepose.right_stick", bindings[0].SourceId);
            Assert.AreEqual(0, bindings[0].SourceAxis);
            Assert.AreEqual(AnalogBindingTargetKind.BonePose, bindings[0].TargetKind);
            Assert.AreEqual("RightEye", bindings[0].TargetIdentifier);
            Assert.AreEqual(AnalogTargetAxis.Y, bindings[0].TargetAxis);
            Assert.AreEqual(0.1f, bindings[0].Mapping.DeadZone, 1e-5f);
            Assert.AreEqual(30.0f, bindings[0].Mapping.Scale, 1e-5f);
            Assert.AreEqual(-45.0f, bindings[0].Mapping.Min, 1e-5f);
            Assert.AreEqual(45.0f, bindings[0].Mapping.Max, 1e-5f);
            Assert.AreEqual(TransitionCurveType.Linear, bindings[0].Mapping.Curve.Type);

            Assert.AreEqual(AnalogBindingTargetKind.BlendShape, bindings[1].TargetKind);
            Assert.AreEqual("Mouth_A", bindings[1].TargetIdentifier);
        }

        [Test]
        public void Load_NullOrWhitespaceJson_ReturnsEmptyProfile()
        {
            var profileNull = AnalogInputBindingJsonLoader.Load(null);
            var profileEmpty = AnalogInputBindingJsonLoader.Load(string.Empty);
            var profileWs = AnalogInputBindingJsonLoader.Load("   \n\t");

            Assert.AreEqual(0, profileNull.Bindings.Length);
            Assert.AreEqual(0, profileEmpty.Bindings.Length);
            Assert.AreEqual(0, profileWs.Bindings.Length);
        }

        [Test]
        public void Load_NullBindings_ReturnsEmptyProfile()
        {
            const string json = "{ \"version\": \"1.0.0\" }";
            var profile = AnalogInputBindingJsonLoader.Load(json);

            Assert.AreEqual("1.0.0", profile.Version);
            Assert.AreEqual(0, profile.Bindings.Length);
        }

        [Test]
        public void Load_EmptyBindingsArray_ReturnsEmptyProfile()
        {
            const string json = "{ \"version\": \"1.0.0\", \"bindings\": [] }";
            var profile = AnalogInputBindingJsonLoader.Load(json);

            Assert.AreEqual(0, profile.Bindings.Length);
        }

        [Test]
        public void Load_UnknownTargetKind_LogsWarningAndSkipsEntry()
        {
            const string json = @"{
  ""version"": ""1.0.0"",
  ""bindings"": [
    {
      ""sourceId"": ""analog-blendshape.x"",
      ""sourceAxis"": 0,
      ""targetKind"": ""mystery"",
      ""targetIdentifier"": ""Foo"",
      ""targetAxis"": ""X"",
      ""mapping"": { ""deadZone"": 0, ""scale"": 1, ""offset"": 0, ""curveType"": ""linear"", ""invert"": false, ""min"": 0, ""max"": 1 }
    },
    {
      ""sourceId"": ""analog-blendshape.y"",
      ""sourceAxis"": 0,
      ""targetKind"": ""blendshape"",
      ""targetIdentifier"": ""Bar"",
      ""targetAxis"": ""X"",
      ""mapping"": { ""deadZone"": 0, ""scale"": 1, ""offset"": 0, ""curveType"": ""linear"", ""invert"": false, ""min"": 0, ""max"": 1 }
    }
  ]
}";
            LogAssert.Expect(LogType.Warning, new Regex(".*targetKind.*mystery.*"));

            var profile = AnalogInputBindingJsonLoader.Load(json);

            Assert.AreEqual(1, profile.Bindings.Length);
            Assert.AreEqual("Bar", profile.Bindings.Span[0].TargetIdentifier);
        }

        [Test]
        public void Load_MissingTargetIdentifier_LogsWarningAndSkipsEntry()
        {
            const string json = @"{
  ""bindings"": [
    {
      ""sourceId"": ""analog-blendshape.x"",
      ""sourceAxis"": 0,
      ""targetKind"": ""blendshape"",
      ""targetIdentifier"": """",
      ""targetAxis"": ""X"",
      ""mapping"": { ""deadZone"": 0, ""scale"": 1, ""offset"": 0, ""curveType"": ""linear"", ""invert"": false, ""min"": 0, ""max"": 1 }
    }
  ]
}";
            LogAssert.Expect(LogType.Warning, new Regex(".*targetIdentifier.*"));

            var profile = AnalogInputBindingJsonLoader.Load(json);

            Assert.AreEqual(0, profile.Bindings.Length);
        }

        [Test]
        public void Load_MinGreaterThanMax_LogsWarningAndSkipsEntry()
        {
            const string json = @"{
  ""bindings"": [
    {
      ""sourceId"": ""analog-blendshape.x"",
      ""sourceAxis"": 0,
      ""targetKind"": ""blendshape"",
      ""targetIdentifier"": ""Foo"",
      ""targetAxis"": ""X"",
      ""mapping"": { ""deadZone"": 0, ""scale"": 1, ""offset"": 0, ""curveType"": ""linear"", ""invert"": false, ""min"": 1, ""max"": 0 }
    }
  ]
}";
            LogAssert.Expect(LogType.Warning, new Regex(".*min.*max.*"));

            var profile = AnalogInputBindingJsonLoader.Load(json);

            Assert.AreEqual(0, profile.Bindings.Length);
        }

        [Test]
        public void Load_NegativeSourceAxis_LogsWarningAndSkipsEntry()
        {
            const string json = @"{
  ""bindings"": [
    {
      ""sourceId"": ""analog-blendshape.x"",
      ""sourceAxis"": -1,
      ""targetKind"": ""blendshape"",
      ""targetIdentifier"": ""Foo"",
      ""targetAxis"": ""X"",
      ""mapping"": { ""deadZone"": 0, ""scale"": 1, ""offset"": 0, ""curveType"": ""linear"", ""invert"": false, ""min"": 0, ""max"": 1 }
    }
  ]
}";
            LogAssert.Expect(LogType.Warning, new Regex(".*sourceAxis.*"));

            var profile = AnalogInputBindingJsonLoader.Load(json);

            Assert.AreEqual(0, profile.Bindings.Length);
        }

        [Test]
        public void Load_MalformedJson_LogsWarningAndReturnsEmptyProfile()
        {
            const string json = "{ this is not valid JSON";
            LogAssert.Expect(LogType.Warning, new Regex(".*パース.*失敗.*"));

            var profile = AnalogInputBindingJsonLoader.Load(json);

            Assert.AreEqual(0, profile.Bindings.Length);
        }

        [Test]
        public void Load_UnknownCurveType_LogsWarningAndSkipsEntry()
        {
            const string json = @"{
  ""bindings"": [
    {
      ""sourceId"": ""analog-blendshape.x"",
      ""sourceAxis"": 0,
      ""targetKind"": ""blendshape"",
      ""targetIdentifier"": ""Foo"",
      ""targetAxis"": ""X"",
      ""mapping"": { ""deadZone"": 0, ""scale"": 1, ""offset"": 0, ""curveType"": ""bogus"", ""invert"": false, ""min"": 0, ""max"": 1 }
    }
  ]
}";
            LogAssert.Expect(LogType.Warning, new Regex(".*curveType.*"));

            var profile = AnalogInputBindingJsonLoader.Load(json);

            Assert.AreEqual(0, profile.Bindings.Length);
        }

        [Test]
        public void Load_CurveTypeCaseInsensitive_ParsesAllPresets()
        {
            const string json = @"{
  ""bindings"": [
    { ""sourceId"": ""a"", ""sourceAxis"": 0, ""targetKind"": ""BLENDSHAPE"", ""targetIdentifier"": ""A"", ""targetAxis"": ""X"",
      ""mapping"": { ""deadZone"": 0, ""scale"": 1, ""offset"": 0, ""curveType"": ""EASEIN"", ""invert"": false, ""min"": 0, ""max"": 1 } },
    { ""sourceId"": ""b"", ""sourceAxis"": 0, ""targetKind"": ""BonePose"", ""targetIdentifier"": ""B"", ""targetAxis"": ""z"",
      ""mapping"": { ""deadZone"": 0, ""scale"": 1, ""offset"": 0, ""curveType"": ""easeOut"", ""invert"": false, ""min"": 0, ""max"": 1 } }
  ]
}";
            var profile = AnalogInputBindingJsonLoader.Load(json);

            Assert.AreEqual(2, profile.Bindings.Length);
            Assert.AreEqual(AnalogBindingTargetKind.BlendShape, profile.Bindings.Span[0].TargetKind);
            Assert.AreEqual(TransitionCurveType.EaseIn, profile.Bindings.Span[0].Mapping.Curve.Type);
            Assert.AreEqual(AnalogBindingTargetKind.BonePose, profile.Bindings.Span[1].TargetKind);
            Assert.AreEqual(AnalogTargetAxis.Z, profile.Bindings.Span[1].TargetAxis);
            Assert.AreEqual(TransitionCurveType.EaseOut, profile.Bindings.Span[1].Mapping.Curve.Type);
        }

        [Test]
        public void RoundTrip_SampleJson_PreservesAllValues()
        {
            var profile = AnalogInputBindingJsonLoader.Load(SampleJson);
            var json2 = AnalogInputBindingJsonLoader.Save(profile);
            var profile2 = AnalogInputBindingJsonLoader.Load(json2);

            Assert.AreEqual(profile.Version, profile2.Version);
            Assert.AreEqual(profile.Bindings.Length, profile2.Bindings.Length);

            var a = profile.Bindings.Span;
            var b = profile2.Bindings.Span;
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

        [Test]
        public void RoundTrip_CustomCurveKeyFrames_PreservesAllFields()
        {
            string json = @"{
  ""version"": ""1.0.0"",
  ""bindings"": [
    {
      ""sourceId"": ""analog-blendshape.x"",
      ""sourceAxis"": 0,
      ""targetKind"": ""blendshape"",
      ""targetIdentifier"": ""Mouth_A"",
      ""targetAxis"": ""X"",
      ""mapping"": {
        ""deadZone"": 0.05,
        ""scale"": 2.0,
        ""offset"": -0.1,
        ""curveType"": ""custom"",
        ""curveKeyFrames"": [
          { ""time"": 0.0, ""value"": 0.0, ""inTangent"": 0.0, ""outTangent"": 1.0, ""inWeight"": 0.1, ""outWeight"": 0.2, ""weightedMode"": 1 },
          { ""time"": 1.0, ""value"": 1.0, ""inTangent"": 1.0, ""outTangent"": 0.0, ""inWeight"": 0.3, ""outWeight"": 0.4, ""weightedMode"": 2 }
        ],
        ""invert"": true,
        ""min"": 0.0,
        ""max"": 1.0
      }
    }
  ]
}";
            var profile = AnalogInputBindingJsonLoader.Load(json);
            Assert.AreEqual(1, profile.Bindings.Length);

            var entry = profile.Bindings.Span[0];
            Assert.AreEqual(TransitionCurveType.Custom, entry.Mapping.Curve.Type);
            Assert.AreEqual(2, entry.Mapping.Curve.Keys.Length);
            Assert.IsTrue(entry.Mapping.Invert);

            var keys = entry.Mapping.Curve.Keys.Span;
            Assert.AreEqual(0f, keys[0].Time, 1e-5f);
            Assert.AreEqual(1f, keys[0].OutTangent, 1e-5f);
            Assert.AreEqual(0.1f, keys[0].InWeight, 1e-5f);
            Assert.AreEqual(0.2f, keys[0].OutWeight, 1e-5f);
            Assert.AreEqual(1, keys[0].WeightedMode);
            Assert.AreEqual(1f, keys[1].Time, 1e-5f);
            Assert.AreEqual(2, keys[1].WeightedMode);

            var json2 = AnalogInputBindingJsonLoader.Save(profile);
            var profile2 = AnalogInputBindingJsonLoader.Load(json2);
            var keys2 = profile2.Bindings.Span[0].Mapping.Curve.Keys.Span;
            Assert.AreEqual(2, keys2.Length);
            for (int i = 0; i < keys.Length; i++)
            {
                Assert.AreEqual(keys[i].Time, keys2[i].Time, 1e-5f);
                Assert.AreEqual(keys[i].Value, keys2[i].Value, 1e-5f);
                Assert.AreEqual(keys[i].InTangent, keys2[i].InTangent, 1e-5f);
                Assert.AreEqual(keys[i].OutTangent, keys2[i].OutTangent, 1e-5f);
                Assert.AreEqual(keys[i].InWeight, keys2[i].InWeight, 1e-5f);
                Assert.AreEqual(keys[i].OutWeight, keys2[i].OutWeight, 1e-5f);
                Assert.AreEqual(keys[i].WeightedMode, keys2[i].WeightedMode);
            }
        }

        [Test]
        public void Save_EmptyProfile_ProducesValidJsonRoundTrip()
        {
            var empty = new AnalogInputBindingProfile(string.Empty, System.Array.Empty<AnalogBindingEntry>());
            var json = AnalogInputBindingJsonLoader.Save(empty);

            Assert.IsFalse(string.IsNullOrEmpty(json));

            var profile = AnalogInputBindingJsonLoader.Load(json);
            Assert.AreEqual(0, profile.Bindings.Length);
        }

        [Test]
        public void Load_VersionPreservedAsString()
        {
            const string json = @"{ ""version"": ""2.5.preview"", ""bindings"": [] }";
            var profile = AnalogInputBindingJsonLoader.Load(json);
            Assert.AreEqual("2.5.preview", profile.Version);
        }
    }
}
