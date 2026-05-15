using System;
using System.Collections.Generic;
using Hidano.FacialControl.Adapters.AdapterBindings;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Adapters.OSC;
using Hidano.FacialControl.Domain.Adapters;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Tests.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.AdapterBindings
{
    [TestFixture]
    public sealed class OscSenderAdapterBindingTests
    {
        private const int PortBase = 19420;
        private static int s_portCounter;

        [Test]
        public void Type_HasSerializableAndFacialAdapterBindingAttributes()
        {
            Assert.That(Attribute.IsDefined(typeof(OscSenderAdapterBinding), typeof(SerializableAttribute)), Is.True);

            object[] attrs = typeof(OscSenderAdapterBinding)
                .GetCustomAttributes(typeof(FacialAdapterBindingAttribute), inherit: false);

            Assert.That(attrs.Length, Is.EqualTo(1));
            Assert.That(((FacialAdapterBindingAttribute)attrs[0]).DisplayName, Is.EqualTo("OSC Sender"));
        }

        [Test]
        public void Type_IsSealedAdapterBindingWithParameterlessConstructor()
        {
            Type type = typeof(OscSenderAdapterBinding);

            Assert.That(type.IsSealed, Is.True);
            Assert.That(typeof(AdapterBindingBase).IsAssignableFrom(type), Is.True);
            Assert.That(type.GetConstructor(Type.EmptyTypes), Is.Not.Null);
        }

        [Test]
        public void OnStart_ValidContext_AddsHostSubscribesAndGeneratesIdentity()
        {
            var bus = new RecordingFacialOutputBus();
            var binding = new OscSenderAdapterBinding { Slug = "osc-sender" };
            binding.Configure("127.0.0.1", AllocatePort());
            var host = new GameObject("OscSenderAdapterBindingTests");

            try
            {
                binding.OnStart(CreateContext(bus, host, new[] { "smile" }));

                Assert.That(binding.IsStarted, Is.True);
                Assert.That(binding.HelperHost, Is.Not.Null);
                Assert.That(binding.HelperHost.IsConfigured, Is.True);
                Assert.That(bus.Observer, Is.SameAs(binding));
                Assert.That(binding.Identity.Uuid, Is.Not.EqualTo(Guid.Empty));
                Assert.That(binding.Identity.StartedAtUnixMs, Is.GreaterThanOrEqualTo(0L));
            }
            finally
            {
                binding.Dispose();
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void OnStart_InvalidSlug_WarnsAndDoesNotSubscribe()
        {
            var bus = new RecordingFacialOutputBus();
            var binding = new OscSenderAdapterBinding { Slug = string.Empty };
            binding.Configure("127.0.0.1", AllocatePort());
            var host = new GameObject("OscSenderAdapterBindingInvalidSlugTests");

            LogAssert.Expect(LogType.Warning, "[OscSenderAdapterBinding] Slug '' is invalid. OSC Sender will not start.");

            try
            {
                binding.OnStart(CreateContext(bus, host, new[] { "smile" }));

                Assert.That(binding.IsStarted, Is.False);
                Assert.That(binding.HelperHost, Is.Null);
                Assert.That(bus.Observer, Is.Null);
            }
            finally
            {
                binding.Dispose();
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Dispose_AfterStart_UnsubscribesAndResetsState()
        {
            var bus = new RecordingFacialOutputBus();
            var binding = new OscSenderAdapterBinding { Slug = "osc-sender" };
            binding.Configure("127.0.0.1", AllocatePort());
            var host = new GameObject("OscSenderAdapterBindingDisposeTests");

            try
            {
                binding.OnStart(CreateContext(bus, host, new[] { "smile" }));
                Assert.That(bus.Observer, Is.SameAs(binding));

                binding.Dispose();

                Assert.That(binding.IsStarted, Is.False);
                Assert.That(binding.HelperHost, Is.Null);
                Assert.That(bus.Observer, Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static AdapterBuildContext CreateContext(
            IFacialOutputBus bus,
            GameObject host,
            IReadOnlyList<string> blendShapeNames)
        {
            return new AdapterBuildContext(
                new FacialProfile("2.0.0"),
                blendShapeNames,
                new InputSourceRegistry(),
                bus,
                new ManualTimeProvider(),
                host,
                lipSyncProvider: null);
        }

        private static int AllocatePort()
        {
            return PortBase + System.Threading.Interlocked.Increment(ref s_portCounter);
        }

        private sealed class RecordingFacialOutputBus : IFacialOutputBus
        {
            public IFacialOutputObserver Observer { get; private set; }

            public bool HasObservers => Observer != null;

            public void Subscribe(IFacialOutputObserver observer)
            {
                Observer = observer;
            }

            public void Unsubscribe(IFacialOutputObserver observer)
            {
                if (ReferenceEquals(Observer, observer))
                {
                    Observer = null;
                }
            }

            public void Publish(
                ReadOnlySpan<float> postBlendValues,
                ReadOnlySpan<GazeSnapshot> gazeSnapshots)
            {
                Observer?.OnFacialOutputPublished(postBlendValues, gazeSnapshots);
            }
        }
    }
}
