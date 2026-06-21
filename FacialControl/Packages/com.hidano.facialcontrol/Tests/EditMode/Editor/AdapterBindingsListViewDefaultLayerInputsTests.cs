using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Adapters;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Editor.Inspector.AdapterBindings;
using Hidano.FacialControl.LipSync.Adapters;
using Hidano.FacialControl.Tests.EditMode.Adapters.ScriptableObjectTests.AdapterBindings;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hidano.FacialControl.Tests.EditMode.Editor
{
    [Serializable]
    [FacialAdapterBinding(displayName: "ZZZ_ListViewTest_DefaultLayerInputs")]
    public sealed class MockDefaultLayerInputsBinding :
        AdapterBindingBase,
        IAdapterBindingDefaultLayer,
        IAdapterBindingDefaultLayerInputs
    {
        public string DefaultLayerName => "overlay";
        public string DefaultLayerInputSourceId => "legacy-single";
        public ExclusionMode DefaultLayerExclusionMode => ExclusionMode.Blend;

        public IEnumerable<(string id, float weight)> GetDefaultLayerInputSources(string layerName)
        {
            return new[]
            {
                ("overlay:a", 1.0f),
                ("lipsync-overlay:a", 0.75f),
                ("overlay:i", 0.5f),
            };
        }
    }

    [Serializable]
    [FacialAdapterBinding(displayName: "ZZZ_ListViewTest_LegacySingleDefaultLayer")]
    public sealed class MockLegacySingleDefaultLayerBinding :
        AdapterBindingBase,
        IAdapterBindingDefaultLayer
    {
        public string DefaultLayerName => "legacy-layer";
        public string DefaultLayerInputSourceId => "legacy-single";
        public ExclusionMode DefaultLayerExclusionMode => ExclusionMode.LastWins;
    }

    [Serializable]
    [FacialAdapterBinding(displayName: "ZZZ_ListViewTest_LayerNameSensitiveDefaultLayerInputs")]
    public sealed class MockLayerNameSensitiveDefaultLayerInputsBinding :
        AdapterBindingBase,
        IAdapterBindingDefaultLayer,
        IAdapterBindingDefaultLayerInputs
    {
        public string DefaultLayerName => "special-layer";
        public string DefaultLayerInputSourceId => "legacy-should-not-be-used";
        public ExclusionMode DefaultLayerExclusionMode => ExclusionMode.Blend;

        public IEnumerable<(string id, float weight)> GetDefaultLayerInputSources(string layerName)
        {
            if (string.Equals(layerName, "special-layer", StringComparison.Ordinal))
            {
                return new[]
                {
                    ("special:primary", 1.0f),
                    ("special:secondary", 0.25f),
                };
            }

            if (string.Equals(layerName, "overlay", StringComparison.Ordinal))
            {
                return new[]
                {
                    ("overlay:unexpected", 1.0f),
                };
            }

            return Array.Empty<(string id, float weight)>();
        }
    }

    [TestFixture]
    public class AdapterBindingsListViewDefaultLayerInputsTests
    {
        private const string TempFolderParent = "Assets";
        private const string TempFolderName = "__Temp_AdapterBindingsListViewDefaultLayerInputsTests";
        private static readonly string TempFolderPath = TempFolderParent + "/" + TempFolderName;

        private string _assetPath;
        private TestFacialCharacterProfileSO _so;
        private SerializedObject _serializedObject;
        private SerializedProperty _listProperty;
        private static MethodInfo s_resolveDefaultInputSourcesMethod;

        [SetUp]
        public void SetUp()
        {
            if (!AssetDatabase.IsValidFolder(TempFolderPath))
            {
                AssetDatabase.CreateFolder(TempFolderParent, TempFolderName);
            }

            _assetPath = TempFolderPath + "/AdapterBindingsListViewDefaultLayerInputsTests_"
                + Guid.NewGuid().ToString("N")
                + ".asset";
            _so = ScriptableObject.CreateInstance<TestFacialCharacterProfileSO>();
            AssetDatabase.CreateAsset(_so, _assetPath);
            AssetDatabase.SaveAssets();

            _serializedObject = new SerializedObject(_so);
            _listProperty = _serializedObject.FindProperty("_adapterBindings");
            Assert.IsNotNull(_listProperty);
        }

        [TearDown]
        public void TearDown()
        {
            _serializedObject = null;
            _listProperty = null;
            _so = null;

            if (!string.IsNullOrEmpty(_assetPath))
            {
                AssetDatabase.DeleteAsset(_assetPath);
                _assetPath = null;
            }
            if (AssetDatabase.IsValidFolder(TempFolderPath))
            {
                var remaining = AssetDatabase.FindAssets(string.Empty, new[] { TempFolderPath });
                if (remaining == null || remaining.Length == 0)
                {
                    AssetDatabase.DeleteAsset(TempFolderPath);
                }
            }
        }

        [Test]
        public void AutoFillDefaultInputSources_BindingImplementsMultipleInputs_AddsAllIdsToLayer()
        {
            var descriptor = AdapterBindingDiscovery.FindByType(typeof(MockDefaultLayerInputsBinding));
            Assert.IsTrue(descriptor.HasValue);

            var view = new AdapterBindingsListView(_listProperty);
            view.AddBindingFromDescriptor(descriptor.Value);

            _serializedObject.Update();

            Assert.AreEqual(1, _so.Layers.Count);
            var layer = _so.Layers[0];
            Assert.AreEqual("overlay", layer.name);
            Assert.AreEqual(ExclusionMode.Blend, layer.exclusionMode);

            var ids = layer.inputSources.Select(source => source.id).ToArray();
            CollectionAssert.AreEqual(
                new[] { "overlay:a", "lipsync-overlay:a", "overlay:i" },
                ids);
            CollectionAssert.DoesNotContain(ids, "legacy-single");

            var weights = layer.inputSources.Select(source => source.weight).ToArray();
            CollectionAssert.AreEqual(new[] { 1.0f, 0.75f, 0.5f }, weights);
        }

        [Test]
        public void ResolveDefaultInputSources_MultipleInputsBinding_UsesDefaultLayerNameSequence()
        {
            var binding = new MockLayerNameSensitiveDefaultLayerInputsBinding();

            var sources = InvokeResolveDefaultInputSources(binding);

            CollectionAssert.AreEqual(
                new[]
                {
                    ("special:primary", 1.0f),
                    ("special:secondary", 0.25f),
                },
                sources);
            CollectionAssert.DoesNotContain(sources.Select(source => source.id).ToArray(), "legacy-should-not-be-used");
        }

        [Test]
        public void ResolveDefaultInputSources_LegacySingleSourceBinding_ReturnsWeightOneSingleId()
        {
            var binding = new MockLegacySingleDefaultLayerBinding();

            var sources = InvokeResolveDefaultInputSources(binding);

            CollectionAssert.AreEqual(
                new[]
                {
                    ("legacy-single", 1.0f),
                },
                sources);
        }

        [Test]
        public void ResolveDefaultInputSources_ULipSyncBinding_CurrentInspectorPathReturnsEmpty()
        {
            var binding = new ULipSyncAdapterBinding();

            var sources = InvokeResolveDefaultInputSources(binding);

            Assert.That(sources, Is.Empty,
                "現行 AdapterBindingsListView は DefaultLayerName ('lipsync') をそのまま GetDefaultLayerInputSources に渡すため、" +
                "overlay のみを返す uLipSync binding では source が列挙されない挙動を先に固定する。");
        }

        private static IReadOnlyList<(string id, float weight)> InvokeResolveDefaultInputSources(
            IAdapterBindingDefaultLayer binding)
        {
            s_resolveDefaultInputSourcesMethod ??=
                typeof(AdapterBindingsListView).GetMethod(
                    "ResolveDefaultInputSources",
                    BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(s_resolveDefaultInputSourcesMethod,
                "AdapterBindingsListView.ResolveDefaultInputSources を reflection で解決できる必要がある。");

            return (List<(string id, float weight)>)s_resolveDefaultInputSourcesMethod.Invoke(null, new object[] { binding });
        }
    }
}
