using System;
using System.Collections.Generic;
using System.Reflection;
using Hidano.FacialControl.Adapters.AdapterBindings.InputSystem;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Adapters;
using Hidano.FacialControl.InputSystem.Adapters.ScriptableObject;
using Hidano.FacialControl.InputSystem.Editor.AdapterBindings;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using InputBindingMode = Hidano.FacialControl.InputSystem.Adapters.ScriptableObject.BindingMode;
using Object = UnityEngine.Object;

namespace Hidano.FacialControl.InputSystem.Tests.EditMode.Adapters.AdapterBindings
{
    [TestFixture]
    public class InputSystemAdapterBindingDrawerTests
    {
        private const string BlinkSlotName = "blink";
        private const string WinkSlotName = "wink";
        private const string SparkleSlotName = "sparkle";

        private TestProfileSO _profileSo;
        private ScriptableObject _hostSo;
        private SerializedObject _serializedObject;

        [TearDown]
        public void TearDown()
        {
            _serializedObject?.Dispose();
            _serializedObject = null;

            if (_hostSo != null)
            {
                Object.DestroyImmediate(_hostSo);
                _hostSo = null;
            }

            if (_profileSo != null)
            {
                Object.DestroyImmediate(_profileSo);
                _profileSo = null;
            }
        }

        [Test]
        public void BindExpressionBindingRow_OverlaySlot_RendersDropdownFromProfileSlots()
        {
            _profileSo = ScriptableObject.CreateInstance<TestProfileSO>();
            SetSlots(_profileSo, BlinkSlotName, WinkSlotName);
            _profileSo.WritableAdapterBindings.Add(CreateBinding(BlinkSlotName));
            SerializedProperty bindingProperty = CreateProfileBindingProperty(_profileSo);

            var row = new VisualElement();
            InvokeBindExpressionBindingRow(row, 0, bindingProperty);

            var dropdown = row.Q<DropdownField>(InputSystemAdapterBindingDrawer.OverlaySlotDropdownName);
            var help = row.Q<HelpBox>(InputSystemAdapterBindingDrawer.OverlaySlotHelpName);
            Assert.That(dropdown, Is.Not.Null, "overlaySlot は PropertyField ではなく DropdownField として描画されるべき。");
            Assert.That(dropdown.choices, Is.EqualTo(new[] { string.Empty, BlinkSlotName, WinkSlotName }));
            Assert.That(dropdown.value, Is.EqualTo(BlinkSlotName));
            Assert.That(dropdown.enabledSelf, Is.True);

            SetSlots(_profileSo, BlinkSlotName, WinkSlotName, SparkleSlotName);
            InvokeRefreshOverlaySlotChoices(
                dropdown,
                help,
                bindingProperty,
                FindOverlaySlotProperty(bindingProperty));

            Assert.That(dropdown.choices, Is.EqualTo(new[] { string.Empty, BlinkSlotName, WinkSlotName, SparkleSlotName }));
        }

        [Test]
        public void BindExpressionBindingRow_WhenProfileSoIsUnavailable_DisablesOverlaySlotDropdownAndShowsHelp()
        {
            var host = ScriptableObject.CreateInstance<NonProfileBindingHost>();
            _hostSo = host;
            host.binding = CreateBinding(BlinkSlotName);
            SerializedProperty bindingProperty = CreateHostBindingProperty(host);

            var row = new VisualElement();
            InvokeBindExpressionBindingRow(row, 0, bindingProperty);

            var dropdown = row.Q<DropdownField>(InputSystemAdapterBindingDrawer.OverlaySlotDropdownName);
            var help = row.Q<HelpBox>(InputSystemAdapterBindingDrawer.OverlaySlotHelpName);

            Assert.That(dropdown, Is.Not.Null);
            Assert.That(dropdown.enabledSelf, Is.False);
            Assert.That(dropdown.value, Is.EqualTo(BlinkSlotName));
            Assert.That(help, Is.Not.Null);
            Assert.That(help.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            StringAssert.Contains("FacialCharacterProfileSO", help.text);
            StringAssert.Contains("Slots", help.text);
        }

        private SerializedProperty CreateProfileBindingProperty(TestProfileSO so)
        {
            _serializedObject?.Dispose();
            _serializedObject = new SerializedObject(so);
            _serializedObject.Update();
            SerializedProperty list = _serializedObject.FindProperty("_adapterBindings");
            Assert.That(list, Is.Not.Null);
            Assert.That(list.arraySize, Is.EqualTo(1));
            return list.GetArrayElementAtIndex(0);
        }

        private SerializedProperty CreateHostBindingProperty(NonProfileBindingHost host)
        {
            _serializedObject?.Dispose();
            _serializedObject = new SerializedObject(host);
            _serializedObject.Update();
            SerializedProperty property = _serializedObject.FindProperty("binding");
            Assert.That(property, Is.Not.Null);
            return property;
        }

        private static InputSystemAdapterBinding CreateBinding(string overlaySlot)
        {
            var binding = new InputSystemAdapterBinding();
            binding.Configure(
                asset: null,
                actionMapName: "Expression",
                expressionBindings: new[]
                {
                    new ExpressionBindingEntry
                    {
                        bindingMode = InputBindingMode.Overlay,
                        actionName = "RightTrigger",
                        overlaySlot = overlaySlot,
                        overlayTargetLayer = "overlay",
                    },
                });
            return binding;
        }

        private static void InvokeBindExpressionBindingRow(
            VisualElement row,
            int index,
            SerializedProperty bindingProperty)
        {
            MethodInfo method = typeof(InputSystemAdapterBindingDrawer).GetMethod(
                "BindExpressionBindingRow",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new object[] { row, index, bindingProperty });
        }

        private static void InvokeRefreshOverlaySlotChoices(
            DropdownField dropdown,
            HelpBox help,
            SerializedProperty bindingProperty,
            SerializedProperty overlaySlotProperty)
        {
            MethodInfo method = typeof(InputSystemAdapterBindingDrawer).GetMethod(
                "RefreshOverlaySlotChoices",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            method.Invoke(null, new object[] { dropdown, help, bindingProperty, overlaySlotProperty });
        }

        private static SerializedProperty FindOverlaySlotProperty(SerializedProperty bindingProperty)
        {
            SerializedProperty list = bindingProperty.FindPropertyRelative("_expressionBindings");
            Assert.That(list, Is.Not.Null);
            Assert.That(list.arraySize, Is.EqualTo(1));
            SerializedProperty slot = list.GetArrayElementAtIndex(0).FindPropertyRelative("overlaySlot");
            Assert.That(slot, Is.Not.Null);
            return slot;
        }

        private static void SetSlots(FacialCharacterProfileSO so, params string[] slots)
        {
            FieldInfo field = typeof(FacialCharacterProfileSO).GetField(
                "_slots",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(so, new List<string>(slots));
        }

        private sealed class TestProfileSO : FacialCharacterProfileSO
        {
            public List<AdapterBindingBase> WritableAdapterBindings => _adapterBindings;
        }

        private sealed class NonProfileBindingHost : ScriptableObject
        {
            [SerializeReference] public InputSystemAdapterBinding binding;
        }
    }
}
