using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Hidano.FacialControl.Adapters.Input;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Adapters.ScriptableObject;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.PlayMode.Integration
{
    /// <summary>
    /// FacialInputBinder の PlayMode テスト（Red フェーズ）。
    /// OnEnable でバインディングが登録され、InputAction の Press で Expression がアクティブ化されること、
    /// null プロファイル/不在 ExpressionId の警告ログ、OnDisable での解除を検証する。
    /// </summary>
    [TestFixture]
    public class FacialInputBinderTests : InputTestFixture
    {
        private GameObject _controllerGameObject;
        private GameObject _binderGameObject;
        private InputBindingProfileSO _bindingProfile;
        private InputActionAsset _actionAsset;
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
        }

        public override void TearDown()
        {
            if (_binderGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_binderGameObject);
                _binderGameObject = null;
            }
            if (_controllerGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_controllerGameObject);
                _controllerGameObject = null;
            }
            if (_bindingProfile != null)
            {
                UnityEngine.Object.DestroyImmediate(_bindingProfile);
                _bindingProfile = null;
            }
            if (_actionAsset != null)
            {
                UnityEngine.Object.DestroyImmediate(_actionAsset);
                _actionAsset = null;
            }
            base.TearDown();
        }

        // ================================================================
        // OnEnable: バインディング登録
        // ================================================================

        [Test]
        public void OnEnable_WithValidProfile_RegistersBindingsAndActivatesExpressionOnPress()
        {
            var controller = CreateController(out var profile);
            var expression = profile.Expressions.Span[0]; // expr-001 / Happy

            _actionAsset = CreateActionAssetWithTrigger("Trigger1", "<Keyboard>/1");
            _bindingProfile = CreateBindingProfile(_actionAsset, new[]
            {
                ("Trigger1", expression.Id),
            });

            var binder = CreateBinder(controller, _bindingProfile);
            _binderGameObject.SetActive(true); // OnEnable 発火

            Assert.IsNotNull(binder);

            Press(_keyboard.digit1Key);

            var active = controller.GetActiveExpressions();
            Assert.AreEqual(1, active.Count);
            Assert.AreEqual(expression.Id, active[0].Id);
        }

        // ================================================================
        // OnEnable: _bindingProfile が null のときの警告ログ
        // ================================================================

        [Test]
        public void OnEnable_WithNullProfile_LogsWarningWithoutException()
        {
            var controller = CreateController(out _);

            LogAssert.Expect(LogType.Warning, new Regex(".*"));

            Assert.DoesNotThrow(() =>
            {
                CreateBinder(controller, null);
                _binderGameObject.SetActive(true);
            });
        }

        // ================================================================
        // OnEnable: ExpressionId 不在時は警告＋スキップ、残りは登録される
        // ================================================================

        [Test]
        public void OnEnable_WithUnknownExpressionId_LogsWarningAndRegistersRemainingBindings()
        {
            var controller = CreateBlendController(out var profile);
            var expression = profile.Expressions.Span[0]; // expr-001 / Happy

            _actionAsset = CreateActionAsset(new[]
            {
                ("Trigger1", "<Keyboard>/1"),
                ("Trigger2", "<Keyboard>/2"),
            });
            _bindingProfile = CreateBindingProfile(_actionAsset, new[]
            {
                ("Trigger1", "nonexistent-id"),
                ("Trigger2", expression.Id),
            });

            LogAssert.Expect(LogType.Warning, new Regex(".*"));

            CreateBinder(controller, _bindingProfile);
            _binderGameObject.SetActive(true);

            Press(_keyboard.digit1Key);
            var active1 = controller.GetActiveExpressions();
            Assert.AreEqual(0, active1.Count, "不在 ExpressionId のバインディングは登録されないこと");

            Press(_keyboard.digit2Key);
            var active2 = controller.GetActiveExpressions();
            Assert.AreEqual(1, active2.Count, "有効なバインディングは登録されていること");
            Assert.AreEqual(expression.Id, active2[0].Id);
        }

        // ================================================================
        // OnDisable: 全バインディング解除と ActionMap 無効化
        // ================================================================

        [Test]
        public void OnDisable_UnbindsAllBindingsAndDisablesActionMap()
        {
            var controller = CreateController(out var profile);
            var expression = profile.Expressions.Span[0];

            _actionAsset = CreateActionAssetWithTrigger("Trigger1", "<Keyboard>/1");
            _bindingProfile = CreateBindingProfile(_actionAsset, new[]
            {
                ("Trigger1", expression.Id),
            });

            CreateBinder(controller, _bindingProfile);
            _binderGameObject.SetActive(true);

            // OnDisable 発火
            _binderGameObject.SetActive(false);

            Press(_keyboard.digit1Key);

            var active = controller.GetActiveExpressions();
            Assert.AreEqual(0, active.Count, "OnDisable 後はバインディングが解除されていること");
        }

        // ================================================================
        // ヘルパー
        // ================================================================

        private FacialController CreateController(out FacialProfile profile)
        {
            profile = CreateTestProfile();
            _controllerGameObject = CreateGameObjectWithAnimatorAndRenderer();
            var controller = _controllerGameObject.AddComponent<FacialController>();
            controller.InitializeWithProfile(profile);
            return controller;
        }

        private FacialController CreateBlendController(out FacialProfile profile)
        {
            profile = CreateBlendProfile();
            _controllerGameObject = CreateGameObjectWithAnimatorAndRenderer();
            var controller = _controllerGameObject.AddComponent<FacialController>();
            controller.InitializeWithProfile(profile);
            return controller;
        }

        private FacialInputBinder CreateBinder(FacialController controller, InputBindingProfileSO profile)
        {
            _binderGameObject = new GameObject("FacialInputBinder");
            _binderGameObject.SetActive(false); // OnEnable を意図的に遅延
            var binder = _binderGameObject.AddComponent<FacialInputBinder>();
            SetPrivateField(binder, "_facialController", controller);
            SetPrivateField(binder, "_bindingProfile", profile);
            return binder;
        }

        private static GameObject CreateGameObjectWithAnimatorAndRenderer()
        {
            var go = new GameObject("FacialInputBinderTest");
            go.AddComponent<Animator>();
            var childObj = new GameObject("Mesh");
            childObj.transform.SetParent(go.transform);
            childObj.AddComponent<SkinnedMeshRenderer>();
            return go;
        }

        private static FacialProfile CreateTestProfile()
        {
            var layers = new LayerDefinition[]
            {
                new LayerDefinition("emotion", 0, ExclusionMode.LastWins)
            };
            var expressions = new Expression[]
            {
                new Expression("expr-001", "Happy", "emotion", 0.25f,
                    TransitionCurve.Linear,
                    new BlendShapeMapping[]
                    {
                        new BlendShapeMapping("smile", 1.0f)
                    }),
                new Expression("expr-002", "Sad", "emotion", 0.25f,
                    TransitionCurve.Linear,
                    new BlendShapeMapping[]
                    {
                        new BlendShapeMapping("frown", 0.8f)
                    })
            };
            return new FacialProfile("1.0.0", layers, expressions);
        }

        private static FacialProfile CreateBlendProfile()
        {
            var layers = new LayerDefinition[]
            {
                new LayerDefinition("emotion", 0, ExclusionMode.Blend)
            };
            var expressions = new Expression[]
            {
                new Expression("expr-001", "Happy", "emotion", 0.25f,
                    TransitionCurve.Linear,
                    new BlendShapeMapping[]
                    {
                        new BlendShapeMapping("smile", 1.0f)
                    }),
                new Expression("expr-002", "Sad", "emotion", 0.25f,
                    TransitionCurve.Linear,
                    new BlendShapeMapping[]
                    {
                        new BlendShapeMapping("frown", 0.8f)
                    })
            };
            return new FacialProfile("1.0.0", layers, expressions);
        }

        private static InputActionAsset CreateActionAssetWithTrigger(string actionName, string binding)
        {
            return CreateActionAsset(new[] { (actionName, binding) });
        }

        private static InputActionAsset CreateActionAsset((string actionName, string binding)[] actions)
        {
            var asset = UnityEngine.ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Expression");
            foreach (var (actionName, binding) in actions)
            {
                var action = map.AddAction(actionName, InputActionType.Button);
                action.AddBinding(binding);
            }
            asset.AddActionMap(map);
            return asset;
        }

        private static InputBindingProfileSO CreateBindingProfile(
            InputActionAsset asset,
            (string actionName, string expressionId)[] entries)
        {
            var so = UnityEngine.ScriptableObject.CreateInstance<InputBindingProfileSO>();

            FieldInfo assetField = typeof(InputBindingProfileSO).GetField(
                "_actionAsset", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(assetField, "_actionAsset フィールドが InputBindingProfileSO に見つかりません。");
            assetField.SetValue(so, asset);

            Type entryType = typeof(InputBindingProfileSO).GetNestedType(
                "InputBindingEntry", BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(entryType, "InputBindingEntry ネストクラスが見つかりません。");

            Type listType = typeof(List<>).MakeGenericType(entryType);
            System.Collections.IList list = (System.Collections.IList)Activator.CreateInstance(listType);

            FieldInfo actionNameField = entryType.GetField("actionName");
            FieldInfo expressionIdField = entryType.GetField("expressionId");

            foreach (var (actionName, expressionId) in entries)
            {
                object entry = Activator.CreateInstance(entryType);
                actionNameField.SetValue(entry, actionName);
                expressionIdField.SetValue(entry, expressionId);
                list.Add(entry);
            }

            FieldInfo bindingsField = typeof(InputBindingProfileSO).GetField(
                "_bindings", BindingFlags.NonPublic | BindingFlags.Instance);
            bindingsField.SetValue(so, list);

            return so;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"{fieldName} フィールドが {target.GetType().Name} に見つかりません。");
            field.SetValue(target, value);
        }
    }
}
