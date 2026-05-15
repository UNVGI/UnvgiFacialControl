using System;
using System.Collections.Generic;
using System.Linq;
using Hidano.FacialControl.Adapters.AdapterBindings;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Adapters.OSC;
using Hidano.FacialControl.Adapters.ScriptableObject;
using Hidano.FacialControl.Domain.Adapters;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Domain.Services;
using Hidano.FacialControl.Tests.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.AdapterBindings
{
    /// <summary>
    /// task 9.1 EditMode 観測可能完了条件: <see cref="OscAdapterBinding"/> が
    /// <c>[Serializable]</c> + <c>[FacialAdapterBinding(displayName: "OSC")]</c> 付きであり、
    /// <c>UnityEditor.TypeCache.GetTypesWithAttribute&lt;FacialAdapterBindingAttribute&gt;()</c>
    /// で discovery 列挙されることを assert する。
    /// </summary>
    /// <remarks>
    /// 本ファイルは Red 段階のテストであり、
    /// <c>Hidano.FacialControl.Adapters.AdapterBindings.OscAdapterBinding</c> が未実装のため
    /// コンパイル時に CS0246 / CS0234 が発生して Red 状態となる（task 9.2 の Green 化対象）。
    /// </remarks>
    [TestFixture]
    public class OscAdapterBindingTests
    {
        private const string ExpectedDisplayName = "OSC";

        [Test]
        public void Type_HasSerializableAttribute_ForSerializeReferenceRoundTrip()
        {
            object[] attrs = typeof(OscAdapterBinding)
                .GetCustomAttributes(typeof(SerializableAttribute), inherit: false);

            Assert.That(attrs.Length, Is.EqualTo(1),
                "OscAdapterBinding に [Serializable] が付いていないと [SerializeReference] の round-trip が破綻する。");
        }

        [Test]
        public void Type_HasFacialAdapterBindingAttributeWithDisplayNameOSC()
        {
            object[] attrs = typeof(OscAdapterBinding)
                .GetCustomAttributes(typeof(FacialAdapterBindingAttribute), inherit: false);

            Assert.That(attrs.Length, Is.EqualTo(1),
                "OscAdapterBinding には [FacialAdapterBinding] が 1 件だけ付与されているべき。");

            var attr = (FacialAdapterBindingAttribute)attrs[0];
            Assert.That(attr.DisplayName, Is.EqualTo(ExpectedDisplayName),
                $"[FacialAdapterBinding] の displayName は \"{ExpectedDisplayName}\" であるべき。");
        }

        [Test]
        public void Type_DerivesFromAdapterBindingBase()
        {
            Assert.That(typeof(AdapterBindingBase).IsAssignableFrom(typeof(OscAdapterBinding)), Is.True,
                "OscAdapterBinding は AdapterBindingBase の派生でなければならない。");
        }

        [Test]
        public void Type_IsConcreteSealedClass()
        {
            Type type = typeof(OscAdapterBinding);

            Assert.That(type.IsAbstract, Is.False,
                "OscAdapterBinding は具象（非 abstract）クラスでなければならない。");
            Assert.That(type.IsSealed, Is.True,
                "OscAdapterBinding は sealed でなければならない（拡張は別 binding で実現）。");
        }

        [Test]
        public void Type_HasParameterlessConstructor_ForActivatorCreateInstance()
        {
            // Inspector の Add ドロップダウンが Activator.CreateInstance 等で具象を生成できる必要がある。
            System.Reflection.ConstructorInfo ctor = typeof(OscAdapterBinding)
                .GetConstructor(Type.EmptyTypes);

            Assert.That(ctor, Is.Not.Null,
                "Activator.CreateInstance で生成可能な parameterless constructor が必要。");
        }

        [Test]
        public void TypeCache_DiscoversOscAdapterBindingViaFacialAdapterBindingAttribute()
        {
            // 各アダプタ package の binding 具象は TypeCache で discovery 列挙される。
            System.Collections.Generic.List<Type> discovered = TypeCache
                .GetTypesWithAttribute<FacialAdapterBindingAttribute>()
                .ToList();

            CollectionAssert.Contains(discovered, typeof(OscAdapterBinding),
                "TypeCache discovery で OscAdapterBinding が列挙されるべき。");
        }

        [Test]
        public void TypeCache_DiscoveredEntry_DisplayNameMatchesOSC()
        {
            FacialAdapterBindingAttribute attr = TypeCache
                .GetTypesWithAttribute<FacialAdapterBindingAttribute>()
                .Where(t => t == typeof(OscAdapterBinding))
                .Select(t => (FacialAdapterBindingAttribute)t
                    .GetCustomAttributes(typeof(FacialAdapterBindingAttribute), inherit: false)[0])
                .FirstOrDefault();

            Assert.That(attr, Is.Not.Null);
            Assert.That(attr.DisplayName, Is.EqualTo(ExpectedDisplayName));
        }

        [Test]
        public void OnStart_GazeVrchatMapping_RegistersVector2InputSourceUnderExpressionId()
        {
            var registry = new InputSourceRegistry();
            var binding = new OscAdapterBinding { Slug = "osc" };
            binding.Mappings = new List<OscMappingEntry>
            {
                new OscMappingEntry
                {
                    mode = OscMappingMode.Gaze_VRChat_XY,
                    expressionId = "eye",
                    addressPattern = "/avatar/parameters/eye",
                    leftRightIndependent = false,
                }
            };

            var host = new GameObject("OscAdapterBindingTests");
            try
            {
                AdapterBuildContext ctx = CreateContext(registry, host);

                binding.OnStart(ctx);

                Assert.That(binding.IsStarted, Is.True);
                Assert.That(binding.GazeSources.Count, Is.EqualTo(1));
                Assert.That(registry.TryResolve("osc:eye", out IInputSource inputSource), Is.True);
                var gazeSource = inputSource as GazeVector2InputSource;
                Assert.That(gazeSource, Is.Not.Null);

                gazeSource.Publish(0.25f, -0.5f);
                var config = new GazeBindingConfig { expressionId = "eye" };

                bool resolved = GazeBindingConfigResolver.TryResolve(
                    config,
                    registry,
                    out ResolvedGazeInputSources sources);

                Assert.That(resolved, Is.True);
                Assert.That(sources.LeftSource, Is.SameAs(gazeSource));
                Assert.That(sources.RightSource, Is.SameAs(gazeSource));
                Assert.That(sources.LeftSource.TryReadVector2(out float x, out float y), Is.True);
                Assert.That(x, Is.EqualTo(0.25f).Within(1e-6f));
                Assert.That(y, Is.EqualTo(-0.5f).Within(1e-6f));
            }
            finally
            {
                binding.Dispose();
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static AdapterBuildContext CreateContext(
            InputSourceRegistry registry,
            GameObject host)
        {
            return new AdapterBuildContext(
                new FacialProfile("2.0.0"),
                Array.Empty<string>(),
                registry,
                new FacialOutputBus(),
                new ManualTimeProvider(),
                host,
                lipSyncProvider: null);
        }
    }
}
