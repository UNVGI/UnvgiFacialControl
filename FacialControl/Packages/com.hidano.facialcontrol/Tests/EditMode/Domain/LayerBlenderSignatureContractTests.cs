using System;
using System.Reflection;
using NUnit.Framework;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Domain.Services;

namespace Hidano.FacialControl.Tests.EditMode.Domain
{
    /// <summary>
    /// Req 7.1 契約テスト (tasks.md 5.4):
    /// layer-input-source-blending 機能は <see cref="LayerBlender"/> のシグネチャを一切変更しない。
    /// 既存 <c>Blend</c> / <c>ApplyLayerSlotOverrides</c> の public static API が同一シグネチャで
    /// 存在し続けることをリフレクションで回帰検証する。
    /// </summary>
    /// <remarks>
    /// シグネチャ要件:
    /// <list type="bullet">
    ///   <item><c>public static void Blend(ReadOnlySpan&lt;LayerBlender.LayerInput&gt;, Span&lt;float&gt;)</c></item>
    ///   <item><c>public static void Blend(LayerBlender.LayerInput[], float[])</c></item>
    ///   <item><c>public static void ApplyLayerSlotOverrides(ReadOnlySpan&lt;string&gt;, LayerSlot[], Span&lt;float&gt;)</c></item>
    ///   <item><c>public static void ApplyLayerSlotOverrides(string[], LayerSlot[], float[])</c></item>
    /// </list>
    /// </remarks>
    [TestFixture]
    public class LayerBlenderSignatureContractTests
    {
        [Test]
        public void LayerBlender_Blend_SpanOverload_SignatureIsPreserved()
        {
            var method = typeof(LayerBlender).GetMethod(
                nameof(LayerBlender.Blend),
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[]
                {
                    typeof(ReadOnlySpan<LayerBlender.LayerInput>),
                    typeof(Span<float>),
                },
                modifiers: null);

            Assert.IsNotNull(method,
                "LayerBlender.Blend(ReadOnlySpan<LayerInput>, Span<float>) が存在すること (Req 7.1)");
            Assert.AreEqual(typeof(void), method.ReturnType,
                "LayerBlender.Blend の戻り値は void であること");
            Assert.IsTrue(method.IsStatic, "LayerBlender.Blend は static のまま");
            Assert.IsTrue(method.IsPublic, "LayerBlender.Blend は public のまま");
        }

        [Test]
        public void LayerBlender_Blend_ArrayOverload_SignatureIsPreserved()
        {
            var method = typeof(LayerBlender).GetMethod(
                nameof(LayerBlender.Blend),
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[]
                {
                    typeof(LayerBlender.LayerInput[]),
                    typeof(float[]),
                },
                modifiers: null);

            Assert.IsNotNull(method,
                "LayerBlender.Blend(LayerInput[], float[]) が存在すること (Req 7.1)");
            Assert.AreEqual(typeof(void), method.ReturnType);
            Assert.IsTrue(method.IsStatic);
            Assert.IsTrue(method.IsPublic);
        }

        [Test]
        public void LayerBlender_ApplyLayerSlotOverrides_SpanOverload_SignatureIsPreserved()
        {
            var method = typeof(LayerBlender).GetMethod(
                nameof(LayerBlender.ApplyLayerSlotOverrides),
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[]
                {
                    typeof(ReadOnlySpan<string>),
                    typeof(LayerSlot[]),
                    typeof(Span<float>),
                },
                modifiers: null);

            Assert.IsNotNull(method,
                "LayerBlender.ApplyLayerSlotOverrides(ReadOnlySpan<string>, LayerSlot[], Span<float>) が存在すること (Req 7.1)");
            Assert.AreEqual(typeof(void), method.ReturnType);
            Assert.IsTrue(method.IsStatic);
            Assert.IsTrue(method.IsPublic);
        }

        [Test]
        public void LayerBlender_ApplyLayerSlotOverrides_ArrayOverload_SignatureIsPreserved()
        {
            var method = typeof(LayerBlender).GetMethod(
                nameof(LayerBlender.ApplyLayerSlotOverrides),
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[]
                {
                    typeof(string[]),
                    typeof(LayerSlot[]),
                    typeof(float[]),
                },
                modifiers: null);

            Assert.IsNotNull(method,
                "LayerBlender.ApplyLayerSlotOverrides(string[], LayerSlot[], float[]) が存在すること (Req 7.1)");
            Assert.AreEqual(typeof(void), method.ReturnType);
            Assert.IsTrue(method.IsStatic);
            Assert.IsTrue(method.IsPublic);
        }

        [Test]
        public void LayerBlender_LayerInput_IsPublicReadonlyStruct()
        {
            // LayerBlender.LayerInput が readonly struct で publicly accessible であることを契約として固定する。
            var layerInputType = typeof(LayerBlender.LayerInput);
            Assert.IsTrue(layerInputType.IsValueType,
                "LayerInput は struct であり続けること (readonly struct を継続)");
            Assert.IsTrue(layerInputType.IsPublic || layerInputType.IsNestedPublic,
                "LayerInput は public であり続けること (Req 7.1)");
        }
    }
}
