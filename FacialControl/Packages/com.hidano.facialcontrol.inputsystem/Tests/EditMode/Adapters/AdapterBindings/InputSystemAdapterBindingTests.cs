using System;
using System.Linq;
using Hidano.FacialControl.Adapters.AdapterBindings.InputSystem;
using Hidano.FacialControl.Domain.Adapters;
using NUnit.Framework;
using UnityEditor;

namespace Hidano.FacialControl.InputSystem.Tests.EditMode.Adapters.AdapterBindings
{
    /// <summary>
    /// task 10.1 EditMode 観測可能完了条件:
    /// <see cref="InputSystemAdapterBinding"/> が <c>[Serializable]</c> +
    /// <c>[FacialAdapterBinding(displayName: "Input System")]</c> 付きであり、
    /// <c>UnityEditor.TypeCache.GetTypesWithAttribute&lt;FacialAdapterBindingAttribute&gt;()</c>
    /// で discovery 列挙されることを assert する（Req 6.1, 7.4, 7.5）。
    /// </summary>
    /// <remarks>
    /// 本ファイルは Red 段階のテストであり、
    /// <c>Hidano.FacialControl.Adapters.AdapterBindings.InputSystem.InputSystemAdapterBinding</c>
    /// が未実装のためコンパイル時に CS0246 / CS0234 が発生して Red 状態となる
    /// （task 10.2 の Green 化対象）。
    /// </remarks>
    [TestFixture]
    public class InputSystemAdapterBindingTests
    {
        private const string ExpectedDisplayName = "Input System";

        [Test]
        public void Type_HasSerializableAttribute_ForSerializeReferenceRoundTrip()
        {
            object[] attrs = typeof(InputSystemAdapterBinding)
                .GetCustomAttributes(typeof(SerializableAttribute), inherit: false);

            Assert.That(attrs.Length, Is.EqualTo(1),
                "InputSystemAdapterBinding に [Serializable] が付いていないと [SerializeReference] の round-trip が破綻する（Req 2.3）。");
        }

        [Test]
        public void Type_HasFacialAdapterBindingAttributeWithDisplayNameInputSystem()
        {
            object[] attrs = typeof(InputSystemAdapterBinding)
                .GetCustomAttributes(typeof(FacialAdapterBindingAttribute), inherit: false);

            Assert.That(attrs.Length, Is.EqualTo(1),
                "InputSystemAdapterBinding には [FacialAdapterBinding] が 1 件だけ付与されているべき（Req 6.1）。");

            var attr = (FacialAdapterBindingAttribute)attrs[0];
            Assert.That(attr.DisplayName, Is.EqualTo(ExpectedDisplayName),
                $"[FacialAdapterBinding] の displayName は \"{ExpectedDisplayName}\" であるべき（Req 6.1）。");
        }

        [Test]
        public void Type_DerivesFromAdapterBindingBase()
        {
            Assert.That(typeof(AdapterBindingBase).IsAssignableFrom(typeof(InputSystemAdapterBinding)), Is.True,
                "InputSystemAdapterBinding は AdapterBindingBase の派生でなければならない（Req 6.1）。");
        }

        [Test]
        public void Type_IsConcreteSealedClass()
        {
            Type type = typeof(InputSystemAdapterBinding);

            Assert.That(type.IsAbstract, Is.False,
                "InputSystemAdapterBinding は具象（非 abstract）クラスでなければならない（Req 1.2 - non-abstract derived from AdapterBindingBase）。");
            Assert.That(type.IsSealed, Is.True,
                "InputSystemAdapterBinding は sealed でなければならない（拡張は別 binding で実現）。");
        }

        [Test]
        public void Type_HasParameterlessConstructor_ForActivatorCreateInstance()
        {
            // Req 2.5: Inspector の Add ドロップダウンが Activator.CreateInstance 等で具象を生成できる必要がある。
            System.Reflection.ConstructorInfo ctor = typeof(InputSystemAdapterBinding)
                .GetConstructor(Type.EmptyTypes);

            Assert.That(ctor, Is.Not.Null,
                "Activator.CreateInstance で生成可能な parameterless constructor が必要（Req 2.5）。");
        }

        [Test]
        public void TypeCache_DiscoversInputSystemAdapterBindingViaFacialAdapterBindingAttribute()
        {
            // Req 1.3, 6.1: 各アダプタ package の binding 具象は TypeCache で discovery 列挙される。
            System.Collections.Generic.List<Type> discovered = TypeCache
                .GetTypesWithAttribute<FacialAdapterBindingAttribute>()
                .ToList();

            CollectionAssert.Contains(discovered, typeof(InputSystemAdapterBinding),
                "TypeCache discovery で InputSystemAdapterBinding が列挙されるべき（Req 1.3, 6.1）。");
        }

        [Test]
        public void TypeCache_DiscoveredEntry_DisplayNameMatchesInputSystem()
        {
            FacialAdapterBindingAttribute attr = TypeCache
                .GetTypesWithAttribute<FacialAdapterBindingAttribute>()
                .Where(t => t == typeof(InputSystemAdapterBinding))
                .Select(t => (FacialAdapterBindingAttribute)t
                    .GetCustomAttributes(typeof(FacialAdapterBindingAttribute), inherit: false)[0])
                .FirstOrDefault();

            Assert.That(attr, Is.Not.Null);
            Assert.That(attr.DisplayName, Is.EqualTo(ExpectedDisplayName));
        }
    }
}
