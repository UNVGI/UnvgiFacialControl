using System;
using System.Collections.Generic;
using Hidano.FacialControl.Adapters.AdapterBindings.InputSystem;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Domain.Adapters;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.InputSystem.Adapters.ScriptableObject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Hidano.FacialControl.InputSystem.Tests.PlayMode.Integration
{
    /// <summary>
    /// task 10.1 PlayMode 観測可能完了条件:
    /// <see cref="InputSystemAdapterBinding"/> が <c>OnStart</c> で
    /// <c>InputActionAsset.Instantiate</c> + <c>ActionMap.Enable</c> を実行し、
    /// InputAction 仮想 device → ExpressionTrigger / Analog / Gaze の 3 経路（D-8 集約）で
    /// 入力源が登録 / 解決可能になることを assert する（Req 6.1, 6.5, 6.8, 7.4, 7.5）。
    /// <c>Dispose</c> で <c>ActionMap.Disable</c> + 内部 Asset destroy + provider dispose が
    /// 走り再 <c>Dispose</c> 呼び出しが冪等であることも検証する。
    /// </summary>
    /// <remarks>
    /// 本ファイルは Red 段階のテストであり、
    /// <c>Hidano.FacialControl.Adapters.AdapterBindings.InputSystem.InputSystemAdapterBinding</c>
    /// が未実装のためコンパイル時に CS0246 / CS0234 が発生して Red 状態となる
    /// （task 10.2 の Green 化対象）。
    /// </remarks>
    [TestFixture]
    public class InputSystemAdapterBindingIntegrationTests
    {
        private GameObject _hostGameObject;
        private InputSourceRegistry _registry;
        private InputSystemAdapterBinding _binding;
        private InputActionAsset _sourceAsset;
        private bool _bindingStarted;
        private Keyboard _keyboard;
        private Gamepad _gamepad;

        [SetUp]
        public void SetUp()
        {
            _registry = new InputSourceRegistry();
            _hostGameObject = new GameObject("InputSystemAdapterBindingIntegrationTestsHost");
            _bindingStarted = false;

            // 仮想 device を test runtime に register。
            _keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<Keyboard>();
            _gamepad = UnityEngine.InputSystem.InputSystem.AddDevice<Gamepad>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_binding != null && _bindingStarted)
            {
                try
                {
                    _binding.Dispose();
                }
                catch (Exception)
                {
                    // TearDown では例外を握り潰し、テスト本体の assertion を優先する。
                }
            }
            _binding = null;
            _bindingStarted = false;

            if (_sourceAsset != null)
            {
                UnityEngine.Object.DestroyImmediate(_sourceAsset);
                _sourceAsset = null;
            }

            if (_hostGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_hostGameObject);
                _hostGameObject = null;
            }

            if (_keyboard != null)
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(_keyboard);
                _keyboard = null;
            }
            if (_gamepad != null)
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(_gamepad);
                _gamepad = null;
            }
        }

        // ---------------------------------------------------------------
        // OnStart: ActionMap.Enable + InputSourceRegistry に primary 登録
        // ---------------------------------------------------------------

        [Test]
        public void OnStart_EnablesActionMapOnInstantiatedRuntimeAsset()
        {
            _sourceAsset = CreateExpressionActionAsset(
                actionMapName: "Expression",
                buttonActionName: "TriggerHappy",
                buttonBinding: "<Keyboard>/digit1");
            _binding = CreateBinding(
                slug: "input-system-action-map-enable",
                asset: _sourceAsset,
                actionMapName: "Expression",
                expressionBindings: new List<ExpressionBindingEntry>
                {
                    new ExpressionBindingEntry { actionName = "TriggerHappy", expressionId = "expr-001" },
                });

            AdapterBuildContext ctx = CreateContext();

            _binding.OnStart(in ctx);
            _bindingStarted = true;

            InputActionMap runtimeMap = _binding.RuntimeActionMap;
            Assert.IsNotNull(runtimeMap,
                "OnStart 後は RuntimeActionMap が解決済みであるべき（Req 6.1）。");
            Assert.IsTrue(runtimeMap.enabled,
                "OnStart は instantiate した runtime ActionMap を Enable するべき（Req 6.1, D-10）。");
        }

        [Test]
        public void OnStart_DoesNotEnableSourceAssetActionMap_OnlyRuntimeClone()
        {
            // Source asset は未変更で、Instantiate された clone のみが Enable される（旧 FacialCharacterInputExtension の挙動を維持）。
            _sourceAsset = CreateExpressionActionAsset(
                actionMapName: "Expression",
                buttonActionName: "TriggerHappy",
                buttonBinding: "<Keyboard>/digit1");
            _binding = CreateBinding(
                slug: "input-system-no-source-mutation",
                asset: _sourceAsset,
                actionMapName: "Expression",
                expressionBindings: new List<ExpressionBindingEntry>
                {
                    new ExpressionBindingEntry { actionName = "TriggerHappy", expressionId = "expr-001" },
                });

            AdapterBuildContext ctx = CreateContext();

            _binding.OnStart(in ctx);
            _bindingStarted = true;

            InputActionMap sourceMap = _sourceAsset.FindActionMap("Expression");
            Assert.IsNotNull(sourceMap);
            Assert.IsFalse(sourceMap.enabled,
                "Source InputActionAsset 側の ActionMap は Enable されないべき（Instantiate された clone のみ Enable される）。");
        }

        [Test]
        public void OnStart_RegistersPrimaryInputSourceUnderSlug()
        {
            const string slug = "input-system-primary-resolve";
            _sourceAsset = CreateExpressionActionAsset(
                actionMapName: "Expression",
                buttonActionName: "TriggerHappy",
                buttonBinding: "<Keyboard>/digit1");
            _binding = CreateBinding(
                slug: slug,
                asset: _sourceAsset,
                actionMapName: "Expression",
                expressionBindings: new List<ExpressionBindingEntry>
                {
                    new ExpressionBindingEntry { actionName = "TriggerHappy", expressionId = "expr-001" },
                });

            AdapterBuildContext ctx = CreateContext();

            _binding.OnStart(in ctx);
            _bindingStarted = true;

            bool resolved = _registry.TryResolve(slug, out IInputSource source);
            Assert.IsTrue(resolved,
                $"InputSourceRegistry.TryResolve(\"{slug}\") は OnStart 後に true を返すべき（Req 6.1, D-3）。");
            Assert.IsNotNull(source,
                "解決結果の IInputSource は non-null であるべき。");
        }

        // ---------------------------------------------------------------
        // OnStart: ExpressionTrigger / Analog / Gaze の 3 経路集約（D-8）
        // ---------------------------------------------------------------

        [Test]
        public void OnStart_ExpressionTriggerPath_AddsExpressionInputSourceAdapterToHostGameObject()
        {
            // D-8 集約: ExpressionTrigger 経路は host GameObject 上の ExpressionInputSourceAdapter 経由で実装される。
            _sourceAsset = CreateExpressionActionAsset(
                actionMapName: "Expression",
                buttonActionName: "TriggerHappy",
                buttonBinding: "<Keyboard>/digit1");
            _binding = CreateBinding(
                slug: "input-system-trigger-path",
                asset: _sourceAsset,
                actionMapName: "Expression",
                expressionBindings: new List<ExpressionBindingEntry>
                {
                    new ExpressionBindingEntry { actionName = "TriggerHappy", expressionId = "expr-001" },
                });

            AdapterBuildContext ctx = CreateContext();

            _binding.OnStart(in ctx);
            _bindingStarted = true;

            ExpressionInputSourceAdapter adapter =
                _hostGameObject.GetComponent<ExpressionInputSourceAdapter>();
            Assert.IsNotNull(adapter,
                "OnStart は ExpressionTrigger 経路のために ExpressionInputSourceAdapter を host GameObject に AddComponent するべき（Req 6.8, D-8）。");
        }

        [Test]
        public void OnStart_AnalogPath_RegistersAnalogSourceUnderCompositeSlug()
        {
            // D-8 集約: Analog 経路は GazeConfig.inputAction.action.name を sub-id とした composite slug 登録となる
            // （旧 FacialCharacterInputExtension.EnsureAnalogReady 内の InputActionAnalogSource 構築相当）。
            const string slug = "input-system-analog-path";
            _sourceAsset = CreateGazeActionAsset(
                actionMapName: "Expression",
                gazeActionName: "GazeLook");

            var gazeConfig = new GazeExpressionConfig
            {
                expressionId = "expr-gaze",
                inputAction = InputActionReference.Create(_sourceAsset.FindActionMap("Expression").FindAction("GazeLook")),
            };

            _binding = CreateBinding(
                slug: slug,
                asset: _sourceAsset,
                actionMapName: "Expression",
                expressionBindings: null,
                gazeConfigs: new List<GazeExpressionConfig> { gazeConfig });

            AdapterBuildContext ctx = CreateContext();

            _binding.OnStart(in ctx);
            _bindingStarted = true;

            // Composite slug `<slug>:<actionName>` で analog source が解決できるべき（Req 12.4 (b)）。
            bool resolved = _registry.TryResolve(slug + ":GazeLook", out IInputSource source);
            Assert.IsTrue(resolved,
                $"Analog 経路の InputSource は \"{slug}:GazeLook\" で解決できるべき（Req 12.4, D-8）。");
            Assert.IsNotNull(source);
        }

        [Test]
        public void OnStart_GazePath_RegistersGazeBoneProviderForGazeConfig()
        {
            // Gaze 経路: GazeConfig が 1 件以上あれば binding は GazeBonePoseProvider を保持し公開する。
            _sourceAsset = CreateGazeActionAsset(
                actionMapName: "Expression",
                gazeActionName: "GazeLook");

            var gazeConfig = new GazeExpressionConfig
            {
                expressionId = "expr-gaze",
                inputAction = InputActionReference.Create(_sourceAsset.FindActionMap("Expression").FindAction("GazeLook")),
            };

            _binding = CreateBinding(
                slug: "input-system-gaze-path",
                asset: _sourceAsset,
                actionMapName: "Expression",
                expressionBindings: null,
                gazeConfigs: new List<GazeExpressionConfig> { gazeConfig });

            AdapterBuildContext ctx = CreateContext();

            _binding.OnStart(in ctx);
            _bindingStarted = true;

            Assert.IsTrue(_binding.HasGazeProvider,
                "GazeConfig が 1 件以上ある場合は OnStart 後に Gaze provider が構築されるべき（Req 6.1, D-8）。");
        }

        // ---------------------------------------------------------------
        // Dispose: ActionMap.Disable + Asset destroy + provider dispose
        // ---------------------------------------------------------------

        [Test]
        public void Dispose_DisablesRuntimeActionMap()
        {
            _sourceAsset = CreateExpressionActionAsset(
                actionMapName: "Expression",
                buttonActionName: "TriggerHappy",
                buttonBinding: "<Keyboard>/digit1");
            _binding = CreateBinding(
                slug: "input-system-dispose-disable",
                asset: _sourceAsset,
                actionMapName: "Expression",
                expressionBindings: new List<ExpressionBindingEntry>
                {
                    new ExpressionBindingEntry { actionName = "TriggerHappy", expressionId = "expr-001" },
                });

            AdapterBuildContext ctx = CreateContext();
            _binding.OnStart(in ctx);
            _bindingStarted = true;

            InputActionMap runtimeMap = _binding.RuntimeActionMap;
            Assert.IsNotNull(runtimeMap);
            Assert.IsTrue(runtimeMap.enabled, "Sanity: OnStart 後は ActionMap が Enable されている。");

            _binding.Dispose();
            _bindingStarted = false;

            Assert.IsFalse(runtimeMap.enabled,
                "Dispose は runtime ActionMap を Disable するべき（Req 6.1, D-10）。");
        }

        [UnityTest]
        public System.Collections.IEnumerator Dispose_DestroysInstantiatedRuntimeActionAsset()
        {
            _sourceAsset = CreateExpressionActionAsset(
                actionMapName: "Expression",
                buttonActionName: "TriggerHappy",
                buttonBinding: "<Keyboard>/digit1");
            _binding = CreateBinding(
                slug: "input-system-dispose-destroy",
                asset: _sourceAsset,
                actionMapName: "Expression",
                expressionBindings: new List<ExpressionBindingEntry>
                {
                    new ExpressionBindingEntry { actionName = "TriggerHappy", expressionId = "expr-001" },
                });

            AdapterBuildContext ctx = CreateContext();
            _binding.OnStart(in ctx);
            _bindingStarted = true;

            InputActionAsset runtimeAsset = _binding.RuntimeActionAsset;
            Assert.IsNotNull(runtimeAsset, "Sanity: OnStart 後は RuntimeActionAsset が Instantiate 済み。");
            Assert.AreNotSame(_sourceAsset, runtimeAsset,
                "RuntimeActionAsset は source asset の Instantiate clone であるべき。");

            _binding.Dispose();
            _bindingStarted = false;

            yield return null;

            // Unity の Object 等価性: Destroy 後の参照は == null となる。
            Assert.IsTrue(runtimeAsset == null,
                "Dispose は Instantiate した runtime ActionAsset を Destroy するべき（Req 6.1, D-10）。");
        }

        [Test]
        public void Dispose_DoesNotDestroySourceAsset()
        {
            _sourceAsset = CreateExpressionActionAsset(
                actionMapName: "Expression",
                buttonActionName: "TriggerHappy",
                buttonBinding: "<Keyboard>/digit1");
            _binding = CreateBinding(
                slug: "input-system-dispose-source-preserved",
                asset: _sourceAsset,
                actionMapName: "Expression",
                expressionBindings: new List<ExpressionBindingEntry>
                {
                    new ExpressionBindingEntry { actionName = "TriggerHappy", expressionId = "expr-001" },
                });

            AdapterBuildContext ctx = CreateContext();
            _binding.OnStart(in ctx);
            _bindingStarted = true;

            _binding.Dispose();
            _bindingStarted = false;

            Assert.IsTrue(_sourceAsset != null,
                "Dispose は source InputActionAsset を破棄しないべき（外部参照のため）。");
        }

        [Test]
        public void Dispose_TogglesIsStartedToFalse()
        {
            _sourceAsset = CreateExpressionActionAsset(
                actionMapName: "Expression",
                buttonActionName: "TriggerHappy",
                buttonBinding: "<Keyboard>/digit1");
            _binding = CreateBinding(
                slug: "input-system-dispose-flag",
                asset: _sourceAsset,
                actionMapName: "Expression",
                expressionBindings: new List<ExpressionBindingEntry>
                {
                    new ExpressionBindingEntry { actionName = "TriggerHappy", expressionId = "expr-001" },
                });

            AdapterBuildContext ctx = CreateContext();
            _binding.OnStart(in ctx);
            _bindingStarted = true;
            Assert.IsTrue(_binding.IsStarted, "Sanity: OnStart 後は IsStarted == true。");

            _binding.Dispose();
            _bindingStarted = false;

            Assert.IsFalse(_binding.IsStarted,
                "Dispose 後は IsStarted == false に戻るべき。");
        }

        [Test]
        public void Dispose_IsIdempotent_DoesNotThrowOnSecondCall()
        {
            _sourceAsset = CreateExpressionActionAsset(
                actionMapName: "Expression",
                buttonActionName: "TriggerHappy",
                buttonBinding: "<Keyboard>/digit1");
            _binding = CreateBinding(
                slug: "input-system-dispose-idempotent",
                asset: _sourceAsset,
                actionMapName: "Expression",
                expressionBindings: new List<ExpressionBindingEntry>
                {
                    new ExpressionBindingEntry { actionName = "TriggerHappy", expressionId = "expr-001" },
                });

            AdapterBuildContext ctx = CreateContext();
            _binding.OnStart(in ctx);
            _bindingStarted = true;

            _binding.Dispose();
            _bindingStarted = false;

            Assert.DoesNotThrow(() => _binding.Dispose(),
                "Dispose は冪等で 2 回目以降の呼び出しでも例外を投げないべき（Req 13.5）。");
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private InputSystemAdapterBinding CreateBinding(
            string slug,
            InputActionAsset asset,
            string actionMapName,
            IReadOnlyList<ExpressionBindingEntry> expressionBindings,
            IReadOnlyList<GazeExpressionConfig> gazeConfigs = null)
        {
            var binding = new InputSystemAdapterBinding();
            binding.Slug = slug;
            binding.Configure(asset, actionMapName, expressionBindings, gazeConfigs);
            return binding;
        }

        private AdapterBuildContext CreateContext(IReadOnlyList<string> blendShapeNames = null)
        {
            return new AdapterBuildContext(
                profile: new FacialProfile("1.0"),
                blendShapeNames: blendShapeNames ?? new List<string> { "smile", "frown" },
                inputSourceRegistry: _registry,
                timeProvider: new UnityTimeProvider(),
                hostGameObject: _hostGameObject,
                lipSyncProvider: null);
        }

        private static InputActionAsset CreateExpressionActionAsset(
            string actionMapName,
            string buttonActionName,
            string buttonBinding)
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = asset.AddActionMap(actionMapName);
            var action = map.AddAction(buttonActionName, InputActionType.Button);
            action.AddBinding(buttonBinding);
            return asset;
        }

        private static InputActionAsset CreateGazeActionAsset(
            string actionMapName,
            string gazeActionName)
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = asset.AddActionMap(actionMapName);
            var action = map.AddAction(gazeActionName, InputActionType.Value, expectedControlType: "Vector2");
            action.AddBinding("<Gamepad>/leftStick");
            return asset;
        }
    }
}
