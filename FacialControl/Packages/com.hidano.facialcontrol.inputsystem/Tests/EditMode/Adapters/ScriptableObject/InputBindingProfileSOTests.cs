using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Hidano.FacialControl.Adapters.ScriptableObject;
using InputBinding = Hidano.FacialControl.Domain.Models.InputBinding;

namespace Hidano.FacialControl.Tests.EditMode.Adapters
{
    [TestFixture]
    public class InputBindingProfileSOTests
    {
        private InputBindingProfileSO _so;
        private InputActionAsset _actionAsset;

        [TearDown]
        public void TearDown()
        {
            if (_so != null)
            {
                UnityEngine.Object.DestroyImmediate(_so);
                _so = null;
            }

            if (_actionAsset != null)
            {
                UnityEngine.Object.DestroyImmediate(_actionAsset);
                _actionAsset = null;
            }
        }

        [Test]
        public void GetBindings_ActionAssetIsNull_ReturnsEmptyList()
        {
            _so = UnityEngine.ScriptableObject.CreateInstance<InputBindingProfileSO>();

            IReadOnlyList<InputBinding> bindings = _so.GetBindings();

            Assert.IsNotNull(bindings);
            Assert.AreEqual(0, bindings.Count);
        }

        [Test]
        public void GetBindings_WithSerializedEntries_ReturnsReadOnlyListOfInputBinding()
        {
            _so = UnityEngine.ScriptableObject.CreateInstance<InputBindingProfileSO>();
            _actionAsset = UnityEngine.ScriptableObject.CreateInstance<InputActionAsset>();
            SetActionAsset(_so, _actionAsset);
            SetBindings(_so, new[]
            {
                ("Trigger1", "expression-id-001"),
                ("Trigger2", "expression-id-002"),
            });

            IReadOnlyList<InputBinding> bindings = _so.GetBindings();

            Assert.IsNotNull(bindings);
            Assert.IsInstanceOf<IReadOnlyList<InputBinding>>(bindings);
            Assert.AreEqual(2, bindings.Count);
        }

        [Test]
        public void GetBindings_WithSerializedEntries_PreservesActionNameAndExpressionId()
        {
            _so = UnityEngine.ScriptableObject.CreateInstance<InputBindingProfileSO>();
            _actionAsset = UnityEngine.ScriptableObject.CreateInstance<InputActionAsset>();
            SetActionAsset(_so, _actionAsset);
            SetBindings(_so, new[]
            {
                ("Trigger1", "expression-id-001"),
                ("Trigger2", "expression-id-002"),
            });

            IReadOnlyList<InputBinding> bindings = _so.GetBindings();

            Assert.AreEqual("Trigger1", bindings[0].ActionName);
            Assert.AreEqual("expression-id-001", bindings[0].ExpressionId);
            Assert.AreEqual("Trigger2", bindings[1].ActionName);
            Assert.AreEqual("expression-id-002", bindings[1].ExpressionId);
        }

        [Test]
        public void InputSourceCategory_DefaultOnFreshInstance_IsController()
        {
            _so = UnityEngine.ScriptableObject.CreateInstance<InputBindingProfileSO>();

            Assert.AreEqual(InputSourceCategory.Controller, _so.InputSourceCategory);
        }

        [Test]
        public void OnValidate_ControllerCategoryWithKeyboardOnlyBindings_EmitsWarning()
        {
            _so = UnityEngine.ScriptableObject.CreateInstance<InputBindingProfileSO>();
            _actionAsset = CreateActionAssetWithBinding("Trigger1", "<Keyboard>/a");
            SetActionAsset(_so, _actionAsset);
            SetActionMapName(_so, "Expression");
            SetInputSourceCategory(_so, InputSourceCategory.Controller);

            LogAssert.Expect(LogType.Warning, new Regex(@"InputSourceCategory が Controller.*Keyboard"));
            InvokeOnValidate(_so);
        }

        [Test]
        public void OnValidate_KeyboardCategoryWithKeyboardOnlyBindings_NoWarning()
        {
            _so = UnityEngine.ScriptableObject.CreateInstance<InputBindingProfileSO>();
            _actionAsset = CreateActionAssetWithBinding("Trigger1", "<Keyboard>/a");
            SetActionAsset(_so, _actionAsset);
            SetActionMapName(_so, "Expression");
            SetInputSourceCategory(_so, InputSourceCategory.Keyboard);

            InvokeOnValidate(_so);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void OnValidate_ControllerCategoryWithGamepadBindings_NoWarning()
        {
            _so = UnityEngine.ScriptableObject.CreateInstance<InputBindingProfileSO>();
            _actionAsset = CreateActionAssetWithBinding("Trigger1", "<Gamepad>/buttonSouth");
            SetActionAsset(_so, _actionAsset);
            SetActionMapName(_so, "Expression");
            SetInputSourceCategory(_so, InputSourceCategory.Controller);

            InvokeOnValidate(_so);
            LogAssert.NoUnexpectedReceived();
        }

        private static InputActionAsset CreateActionAssetWithBinding(string actionName, string bindingPath)
        {
            var asset = UnityEngine.ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Expression");
            var action = map.AddAction(actionName, InputActionType.Button);
            action.AddBinding(bindingPath);
            asset.AddActionMap(map);
            return asset;
        }

        private static void SetActionMapName(InputBindingProfileSO so, string actionMapName)
        {
            FieldInfo field = typeof(InputBindingProfileSO).GetField(
                "_actionMapName", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "_actionMapName フィールドが InputBindingProfileSO に見つかりません。");
            field.SetValue(so, actionMapName);
        }

        private static void SetInputSourceCategory(InputBindingProfileSO so, InputSourceCategory category)
        {
            FieldInfo field = typeof(InputBindingProfileSO).GetField(
                "_inputSourceCategory", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "_inputSourceCategory フィールドが InputBindingProfileSO に見つかりません。");
            field.SetValue(so, category);
        }

        private static void InvokeOnValidate(InputBindingProfileSO so)
        {
            MethodInfo method = typeof(InputBindingProfileSO).GetMethod(
                "OnValidate", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "OnValidate メソッドが InputBindingProfileSO に見つかりません。");
            method.Invoke(so, null);
        }

        private static void SetActionAsset(InputBindingProfileSO so, InputActionAsset asset)
        {
            FieldInfo field = typeof(InputBindingProfileSO).GetField(
                "_actionAsset", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "_actionAsset フィールドが InputBindingProfileSO に見つかりません。");
            field.SetValue(so, asset);
        }

        private static void SetBindings(InputBindingProfileSO so, (string actionName, string expressionId)[] entries)
        {
            Type entryType = typeof(InputBindingProfileSO).GetNestedType(
                "InputBindingEntry", BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(entryType, "InputBindingEntry ネストクラスが InputBindingProfileSO に見つかりません。");

            Type listType = typeof(List<>).MakeGenericType(entryType);
            IList list = (IList)Activator.CreateInstance(listType);

            FieldInfo actionNameField = entryType.GetField("actionName");
            FieldInfo expressionIdField = entryType.GetField("expressionId");
            Assert.IsNotNull(actionNameField, "InputBindingEntry.actionName フィールドが見つかりません。");
            Assert.IsNotNull(expressionIdField, "InputBindingEntry.expressionId フィールドが見つかりません。");

            foreach (var (actionName, expressionId) in entries)
            {
                object entry = Activator.CreateInstance(entryType);
                actionNameField.SetValue(entry, actionName);
                expressionIdField.SetValue(entry, expressionId);
                list.Add(entry);
            }

            FieldInfo bindingsField = typeof(InputBindingProfileSO).GetField(
                "_bindings", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(bindingsField, "_bindings フィールドが InputBindingProfileSO に見つかりません。");
            bindingsField.SetValue(so, list);
        }
    }
}
