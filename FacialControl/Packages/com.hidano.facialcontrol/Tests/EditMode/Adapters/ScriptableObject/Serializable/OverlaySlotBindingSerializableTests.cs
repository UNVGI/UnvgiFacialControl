using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.ScriptableObjectTests.Serializable
{
    [TestFixture]
    public class OverlaySlotBindingSerializableTests
    {
        [Test]
        public void TypeDefinition_DoesNotExposeLegacyExpressionIdField()
        {
            var field = typeof(OverlaySlotBindingSerializable).GetField("expressionId");

            Assert.That(field, Is.Null);
        }

        [Test]
        public void UnitySerialization_LegacyExpressionIdKey_IsIgnoredAndNewFieldsKeepDefaults()
        {
            var host = ScriptableObject.CreateInstance<SerializationHost>();
            try
            {
                EditorJsonUtility.FromJsonOverwrite(
                    "{\"MonoBehaviour\":{\"binding\":{\"slot\":\"blink\",\"expressionId\":\"legacy_overlay\"}}}",
                    host);

                Assert.That(host.binding, Is.Not.Null);
                Assert.That(host.binding.slot, Is.EqualTo("blink"));
                Assert.That(host.binding.suppress, Is.False);
                Assert.That(host.binding.animationClip, Is.Null);
                Assert.That(host.binding.cachedSnapshot, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void GetState_DefaultFallback_ReturnsDefaultFallback()
        {
            var binding = new OverlaySlotBindingSerializable
            {
                slot = "blink",
            };

            Assert.That(binding.GetState(), Is.EqualTo(OverlaySlotBindingState.DefaultFallback));
        }

        [Test]
        public void GetState_SuppressIgnoresAnimationClip_ReturnsSuppress()
        {
            var clip = new AnimationClip { name = "OverlaySlotBindingSerializable_SuppressClip" };
            try
            {
                var binding = new OverlaySlotBindingSerializable
                {
                    slot = "blink",
                    suppress = true,
                    animationClip = clip,
                };

                Assert.That(binding.GetState(), Is.EqualTo(OverlaySlotBindingState.Suppress));
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void GetState_AnimationClipAssigned_ReturnsOverride()
        {
            var clip = new AnimationClip { name = "OverlaySlotBindingSerializable_OverrideClip" };
            try
            {
                var binding = new OverlaySlotBindingSerializable
                {
                    slot = "blink",
                    animationClip = clip,
                };

                Assert.That(binding.GetState(), Is.EqualTo(OverlaySlotBindingState.Override));
            }
            finally
            {
                Object.DestroyImmediate(clip);
            }
        }

        private sealed class SerializationHost : ScriptableObject
        {
            public OverlaySlotBindingSerializable binding = new OverlaySlotBindingSerializable();
        }
    }
}
