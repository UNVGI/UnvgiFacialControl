using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.InputSystem.Tests.PlayMode.Adapters.InputSources
{
    /// <summary>
    /// tasks.md 4.5: <see cref="ExpressionInputSourceAdapter"/> の PlayMode 契約テスト
    /// (Req 7.2-7.5, 8.1, 8.3, 8.4, 6.6, 11.3)。
    /// </summary>
    /// <remarks>
    /// Keyboard / Gamepad の実 device を <see cref="InputTestFixture"/> 経由でシミュレートし、
    /// <see cref="InputDeviceCategorizer"/> による自動分類で keyboard / controller sink に
    /// 正しく dispatch されることを検証する。
    /// </remarks>
    [TestFixture]
    public class ExpressionInputSourceAdapterTests : InputTestFixture
    {
        private static readonly string[] BlendShapeNames = { "smile", "angry", "sad", "surprised" };

        private GameObject _gameObject;
        private ExpressionInputSourceAdapter _adapter;
        private KeyboardExpressionInputSource _keyboardSink;
        private ControllerExpressionInputSource _controllerSink;
        private Keyboard _keyboard;
        private Gamepad _gamepad;

        public override void Setup()
        {
            base.Setup();
            _keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<Keyboard>();
            _gamepad = UnityEngine.InputSystem.InputSystem.AddDevice<Gamepad>();
        }

        public override void TearDown()
        {
            if (_adapter != null)
            {
                _adapter.UnbindAll();
                _adapter = null;
            }
            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
                _gameObject = null;
            }
            base.TearDown();
        }

        // ============================================================
        // Keyboard / Controller 自動分類 dispatch
        // ============================================================

        [Test]
        public void OnKeyboardAction_ActivatesExpression_ViaKeyboardSink()
        {
            CreateAdapter();
            using var action = new InputAction("Smile", InputActionType.Button, "<Keyboard>/1");
            _adapter.BindExpression(action, "smile");
            action.Enable();

            Press(_keyboard.digit1Key);

            Assert.AreEqual(1, _keyboardSink.ActiveExpressionIds.Count,
                "Keyboard 由来の入力は keyboard sink に dispatch される");
            Assert.AreEqual("smile", _keyboardSink.ActiveExpressionIds[0]);
            Assert.AreEqual(0, _controllerSink.ActiveExpressionIds.Count,
                "controller sink には dispatch されない");
        }

        [Test]
        public void OnGamepadAction_ActivatesExpression_ViaControllerSink()
        {
            CreateAdapter();
            using var action = new InputAction("Smile", InputActionType.Button, "<Gamepad>/buttonSouth");
            _adapter.BindExpression(action, "smile");
            action.Enable();

            Press(_gamepad.buttonSouth);

            Assert.AreEqual(1, _controllerSink.ActiveExpressionIds.Count,
                "Gamepad 由来の入力は controller sink に dispatch される");
            Assert.AreEqual("smile", _controllerSink.ActiveExpressionIds[0]);
            Assert.AreEqual(0, _keyboardSink.ActiveExpressionIds.Count,
                "keyboard sink には dispatch されない");
        }

        [Test]
        public void OnUnknownDevice_LogsWarning_AndUsesControllerSink()
        {
            CreateAdapter();

            // bindings[0] には未認識 prefix を、bindings[1] には実 device 用 binding を持たせる。
            // Categorizer は bindings[0].path を判定材料にするため、未認識 → fallback 警告 +
            // controller sink への dispatch が起きる（Req 7.4 / 7.5）。
            using var action = new InputAction("Smile", InputActionType.Button);
            action.AddBinding("<UnknownDeviceLayout>/buttonA");
            action.AddBinding("<Keyboard>/1");
            _adapter.BindExpression(action, "smile");
            action.Enable();

            LogAssert.Expect(LogType.Warning,
                new Regex("ExpressionInputSourceAdapter.*unrecognized device category"));

            Press(_keyboard.digit1Key);

            Assert.AreEqual(1, _controllerSink.ActiveExpressionIds.Count,
                "未認識 device は controller sink に fallback dispatch される（Req 7.5）");
            Assert.AreEqual("smile", _controllerSink.ActiveExpressionIds[0]);
            Assert.AreEqual(0, _keyboardSink.ActiveExpressionIds.Count);
        }

        // ============================================================
        // Subscribe / Unsubscribe symmetry
        // ============================================================

        [Test]
        public void MultipleEnableDisable_DoesNotLeakSubscriptions()
        {
            CreateAdapter();
            using var action = new InputAction("Smile", InputActionType.Button, "<Keyboard>/1");
            _adapter.BindExpression(action, "smile");
            action.Enable();

            // 3 回 Disable / Enable を繰り返す。OnDisable で全解除、OnEnable で全再購読されるため、
            // Subscribe / Unsubscribe が対称である限り、SubscribedBindingCount は最終 Enable 後
            // バインディング数 (=1) のまま安定する。
            for (int i = 0; i < 3; i++)
            {
                _gameObject.SetActive(false);
                Assert.AreEqual(0, _adapter.SubscribedBindingCount,
                    $"OnDisable cycle {i}: 購読数は 0 に戻るべき");
                _gameObject.SetActive(true);
                Assert.AreEqual(1, _adapter.SubscribedBindingCount,
                    $"OnEnable cycle {i}: 購読数はバインディング数 (=1) と一致すべき");
            }

            Press(_keyboard.digit1Key);
            Assert.AreEqual(1, _keyboardSink.ActiveExpressionIds.Count,
                "1 回の Press で 1 件だけ Active 化される");
            Assert.AreEqual("smile", _keyboardSink.ActiveExpressionIds[0]);
        }

        [Test]
        public void OnDisable_UnsubscribesAll_PressDoesNotDispatch()
        {
            CreateAdapter();
            using var action = new InputAction("Smile", InputActionType.Button, "<Keyboard>/1");
            _adapter.BindExpression(action, "smile");
            action.Enable();

            _gameObject.SetActive(false);

            Press(_keyboard.digit1Key);

            Assert.AreEqual(0, _keyboardSink.ActiveExpressionIds.Count,
                "OnDisable 後は購読解除されているため keyboard sink は dispatch されない");
            Assert.AreEqual(0, _adapter.SubscribedBindingCount);
        }

        // ============================================================
        // ヘルパー
        // ============================================================

        private void CreateAdapter()
        {
            _gameObject = new GameObject("ExpressionInputSourceAdapterTest");
            _adapter = _gameObject.AddComponent<ExpressionInputSourceAdapter>();

            var profile = BuildProfile();
            _keyboardSink = new KeyboardExpressionInputSource(
                blendShapeCount: BlendShapeNames.Length,
                maxStackDepth: 8,
                exclusionMode: ExclusionMode.LastWins,
                blendShapeNames: BlendShapeNames,
                profile: profile);
            _controllerSink = new ControllerExpressionInputSource(
                blendShapeCount: BlendShapeNames.Length,
                maxStackDepth: 8,
                exclusionMode: ExclusionMode.LastWins,
                blendShapeNames: BlendShapeNames,
                profile: profile);

            _adapter.Initialize(_keyboardSink, _controllerSink);
        }

        private static FacialProfile BuildProfile()
        {
            var layers = new[]
            {
                new LayerDefinition("emotion", priority: 0, ExclusionMode.LastWins),
            };

            var expressions = new[]
            {
                new Expression(
                    id: "smile",
                    name: "smile",
                    layer: "emotion",
                    transitionDuration: 0.2f,
                    transitionCurve: TransitionCurve.Linear,
                    blendShapeValues: new[]
                    {
                        new BlendShapeMapping("smile", 1.0f),
                    }),
                new Expression(
                    id: "angry",
                    name: "angry",
                    layer: "emotion",
                    transitionDuration: 0.2f,
                    transitionCurve: TransitionCurve.Linear,
                    blendShapeValues: new[]
                    {
                        new BlendShapeMapping("angry", 1.0f),
                    }),
            };

            return new FacialProfile("1.0", layers: layers, expressions: expressions);
        }
    }
}
