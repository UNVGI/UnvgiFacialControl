using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.InputSystem.Adapters.Input;
using Hidano.FacialControl.InputSystem.Adapters.ScriptableObject;

namespace Hidano.FacialControl.InputSystem.Tests.PlayMode.Integration
{
    /// <summary>
    /// <see cref="FacialCharacterInputExtension"/> の PlayMode 統合テスト (Task 4)。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 旧 <c>FacialInputBinder</c> の責務を <c>FacialCharacterSO</c> 経由で結線するパターンに対し、
    /// 以下を End-to-End で検証する。
    /// <list type="bullet">
    ///   <item>Controller カテゴリの Press → Expression Activate</item>
    ///   <item>Keyboard カテゴリの Press → Expression Activate</item>
    ///   <item>Controller / Keyboard 両カテゴリ混在時の独立駆動</item>
    ///   <item>OnDisable 後はバインディングが解除されること</item>
    ///   <item>SO に analog binding が存在する場合 InputSourceFactory に
    ///         <c>analog-blendshape</c> 予約 ID が登録され、Aggregator スナップショットに現れること</item>
    /// </list>
    /// </para>
    /// </remarks>
    [TestFixture]
    public class FacialCharacterInputExtensionTests : InputTestFixture
    {
        private GameObject _rigGameObject;
        private FacialCharacterSO _characterSO;
        private InputActionAsset _actionAsset;
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
            if (_rigGameObject != null)
            {
                Object.DestroyImmediate(_rigGameObject);
                _rigGameObject = null;
            }
            if (_characterSO != null)
            {
                Object.DestroyImmediate(_characterSO);
                _characterSO = null;
            }
            if (_actionAsset != null)
            {
                Object.DestroyImmediate(_actionAsset);
                _actionAsset = null;
            }
            base.TearDown();
        }

        // ================================================================
        // Controller カテゴリの結線
        // ================================================================

        [UnityTest]
        public IEnumerator OnEnable_ControllerCategory_ActivatesExpressionOnPress()
        {
            var profile = CreateTestProfile();
            _actionAsset = CreateActionAsset(new[] { ("ControllerTrigger", "<Gamepad>/buttonSouth") });
            _characterSO = CreateCharacterSO(_actionAsset, new[]
            {
                ("ControllerTrigger", "expr-001", InputSourceCategory.Controller),
            });

            BuildRig(profile, _characterSO, out var controller, out _);

            yield return null;

            Press(_gamepad.buttonSouth);
            yield return null;

            var active = controller.GetActiveExpressions();
            Assert.AreEqual(1, active.Count, "Controller カテゴリの Press で Expression がアクティブ化されること");
            Assert.AreEqual("expr-001", active[0].Id);
        }

        // ================================================================
        // Keyboard カテゴリの結線
        // ================================================================

        [UnityTest]
        public IEnumerator OnEnable_KeyboardCategory_ActivatesExpressionOnPress()
        {
            var profile = CreateTestProfile();
            _actionAsset = CreateActionAsset(new[] { ("KeyboardTrigger", "<Keyboard>/1") });
            _characterSO = CreateCharacterSO(_actionAsset, new[]
            {
                ("KeyboardTrigger", "expr-001", InputSourceCategory.Keyboard),
            });

            BuildRig(profile, _characterSO, out var controller, out _);

            yield return null;

            Press(_keyboard.digit1Key);
            yield return null;

            var active = controller.GetActiveExpressions();
            Assert.AreEqual(1, active.Count, "Keyboard カテゴリの Press で Expression がアクティブ化されること");
            Assert.AreEqual("expr-001", active[0].Id);
        }

        // ================================================================
        // 両カテゴリ混在
        // ================================================================

        [UnityTest]
        public IEnumerator OnEnable_BothCategories_ActivateRespectiveExpressions()
        {
            var profile = CreateTestProfile();
            _actionAsset = CreateActionAsset(new[]
            {
                ("ControllerTrigger", "<Gamepad>/buttonSouth"),
                ("KeyboardTrigger", "<Keyboard>/2"),
            });
            _characterSO = CreateCharacterSO(_actionAsset, new[]
            {
                ("ControllerTrigger", "expr-001", InputSourceCategory.Controller),
                ("KeyboardTrigger", "expr-002", InputSourceCategory.Keyboard),
            });

            BuildRig(profile, _characterSO, out var controller, out _);

            yield return null;

            Press(_gamepad.buttonSouth);
            yield return null;

            var afterController = controller.GetActiveExpressions();
            Assert.IsTrue(ContainsId(afterController, "expr-001"),
                "Controller カテゴリの Press で expr-001 がアクティブ化されること");

            // 同レイヤー LastWins モードで二度目の Press は前を退ける。次の検証のため Release も挟む。
            Release(_gamepad.buttonSouth);
            yield return null;
            // Release により expr-001 は非アクティブ化されている (LastWins, Toggle Off)。
            Press(_keyboard.digit2Key);
            yield return null;

            var afterKeyboard = controller.GetActiveExpressions();
            Assert.IsTrue(ContainsId(afterKeyboard, "expr-002"),
                "Keyboard カテゴリの Press で expr-002 がアクティブ化されること");
        }

        // ================================================================
        // OnDisable で解除
        // ================================================================

        [UnityTest]
        public IEnumerator OnDisable_UnbindsAllBindings()
        {
            var profile = CreateTestProfile();
            _actionAsset = CreateActionAsset(new[] { ("KeyboardTrigger", "<Keyboard>/1") });
            _characterSO = CreateCharacterSO(_actionAsset, new[]
            {
                ("KeyboardTrigger", "expr-001", InputSourceCategory.Keyboard),
            });

            BuildRig(profile, _characterSO, out var controller, out var extension);

            yield return null;

            // 拡張だけを無効化する。FacialController は active を維持する。
            extension.enabled = false;
            yield return null;

            Press(_keyboard.digit1Key);
            yield return null;

            var active = controller.GetActiveExpressions();
            Assert.AreEqual(0, active.Count, "OnDisable 後はバインディングが解除されていること");
        }

        // ================================================================
        // analog-blendshape の registration
        // ================================================================

        [UnityTest]
        public IEnumerator ConfigureFactory_RegistersAnalogBlendShapeReservedSource()
        {
            // analog-blendshape を layer 0 で宣言したプロファイルを使い、FC が Factory.TryCreate
            // 経由で AnalogBlendShapeInputSource を組込んだことを Aggregator スナップショットで確認する。
            var profile = CreateProfileWithAnalogBlendShape();
            _actionAsset = CreateActionAsset(System.Array.Empty<(string, string)>());
            _characterSO = CreateCharacterSOWithAnalogBlendShapeBinding(_actionAsset, "x-jaw-open", "jawOpen");

            // BlendShape 'jawOpen' が SkinnedMeshRenderer 上に存在せず、Action 'x-jaw-open' も
            // ActionMap に存在しないため複数の警告ログが想定される。本テストの主眼ではないので無視する。
            LogAssert.ignoreFailingMessages = true;

            BuildRig(profile, _characterSO, out var controller, out _);

            // 1 フレーム LateUpdate を回して Aggregate を 1 度走らせる。
            yield return null;
            yield return null;

            var snapshot = controller.GetInputSourceWeightsSnapshot();
            bool foundAnalogBlendShape = false;
            for (int i = 0; i < snapshot.Count; i++)
            {
                var entry = snapshot[i];
                if (entry.LayerIdx == 0 && entry.SourceId.Value == "analog-blendshape")
                {
                    foundAnalogBlendShape = true;
                    break;
                }
            }
            Assert.IsTrue(foundAnalogBlendShape,
                "ConfigureFactory が呼ばれた結果、analog-blendshape が layer 0 のスナップショットに現れること");
        }

        // ================================================================
        // ヘルパー: シーン構築
        // ================================================================

        private void BuildRig(
            FacialProfile profile,
            FacialCharacterSO so,
            out FacialController controller,
            out FacialCharacterInputExtension extension)
        {
            _rigGameObject = new GameObject("FacialCharacterInputExtensionTest");
            _rigGameObject.SetActive(false);

            _rigGameObject.AddComponent<Animator>();

            // FacialController は SkinnedMeshRenderer を子から検索するので 1 個ぶら下げる。
            var meshObj = new GameObject("Mesh");
            meshObj.transform.SetParent(_rigGameObject.transform, worldPositionStays: false);
            meshObj.AddComponent<SkinnedMeshRenderer>();

            controller = _rigGameObject.AddComponent<FacialController>();
            // FacialCharacterInputExtension が GetComponent<FacialController>() で参照する前に
            // _characterSO を SerializeField 相当の経路でセットする (private field 直接代入)。
            SetPrivateField(controller, "_characterSO", so);

            extension = _rigGameObject.AddComponent<FacialCharacterInputExtension>();
            SetPrivateField(extension, "_facialController", controller);

            // 拡張は最初は無効化しておき、profile を直接注入後に有効化する。
            // これによって ApplyExtensions は profile 確定後の状態で動く。
            extension.enabled = false;

            // SetActive(true) で FC.OnEnable -> Initialize (SO の StreamingAssets 不在で BuildFallback) が走る。
            _rigGameObject.SetActive(true);

            // テスト目的でコアパスのバインディングは一致させたいので、profile を直接注入で初期化し直す
            // (StreamingAssets JSON 不在時の SO フォールバック経路は別テスト責務とする)。
            controller.InitializeWithProfile(profile);

            // 拡張を有効化 -> OnEnable で EnsureAnalogReady (factory はもう次回 Initialize で再呼ばれるが、
            // 本テストでは Aggregator スナップショットの分析用に再 Initialize して registration を確定させる)。
            extension.enabled = true;

            // FC を再 Initialize して ApplyExtensions が Extension.ConfigureFactory を再呼出する。
            // これによって _activeSources / bindings が「Extension 有効化後の値」で factory に登録される。
            controller.InitializeWithProfile(profile);
        }

        private static bool ContainsId(IReadOnlyList<Expression> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Id == id)
                {
                    return true;
                }
            }
            return false;
        }

        // ================================================================
        // ヘルパー: Profile / SO 生成
        // ================================================================

        private static FacialProfile CreateTestProfile()
        {
            var layers = new[]
            {
                new LayerDefinition("emotion", 0, ExclusionMode.LastWins),
            };
            var expressions = new[]
            {
                new Expression("expr-001", "Happy", "emotion", 0.0f,
                    TransitionCurve.Linear,
                    new[] { new BlendShapeMapping("smile", 1.0f) }),
                new Expression("expr-002", "Sad", "emotion", 0.0f,
                    TransitionCurve.Linear,
                    new[] { new BlendShapeMapping("frown", 0.8f) }),
            };
            return new FacialProfile("1.0.0", layers, expressions);
        }

        private static FacialProfile CreateProfileWithAnalogBlendShape()
        {
            var layers = new[]
            {
                new LayerDefinition("emotion", 0, ExclusionMode.LastWins),
            };
            var layerInputSources = new InputSourceDeclaration[][]
            {
                new InputSourceDeclaration[]
                {
                    new InputSourceDeclaration("analog-blendshape", 1.0f, null),
                },
            };
            return new FacialProfile(
                schemaVersion: "1.0.0",
                layers: layers,
                expressions: null,
                rendererPaths: null,
                layerInputSources: layerInputSources,
                bonePoses: System.Array.Empty<BonePose>());
        }

        private static InputActionAsset CreateActionAsset((string actionName, string binding)[] actions)
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Expression");
            for (int i = 0; i < actions.Length; i++)
            {
                var (actionName, binding) = actions[i];
                var action = map.AddAction(actionName, InputActionType.Button);
                action.AddBinding(binding);
            }
            asset.AddActionMap(map);
            return asset;
        }

        /// <summary>
        /// FacialCharacterSO を ScriptableObject.CreateInstance で構築し、
        /// _inputActionAsset / _expressionBindings を Reflection で書き込む。
        /// </summary>
        private static FacialCharacterSO CreateCharacterSO(
            InputActionAsset actionAsset,
            (string actionName, string expressionId, InputSourceCategory category)[] entries)
        {
            var so = ScriptableObject.CreateInstance<FacialCharacterSO>();
            so.InputActionAsset = actionAsset;
            so.ActionMapName = "Expression";

            so.ExpressionBindings.Clear();
            for (int i = 0; i < entries.Length; i++)
            {
                var (actionName, expressionId, category) = entries[i];
                so.ExpressionBindings.Add(new ExpressionBindingEntry
                {
                    actionName = actionName,
                    expressionId = expressionId,
                    category = category,
                });
            }
            return so;
        }

        /// <summary>
        /// analog binding を 1 件持つ FacialCharacterSO を作成する。
        /// </summary>
        private static FacialCharacterSO CreateCharacterSOWithAnalogBlendShapeBinding(
            InputActionAsset actionAsset, string sourceId, string blendShapeName)
        {
            var so = ScriptableObject.CreateInstance<FacialCharacterSO>();
            so.InputActionAsset = actionAsset;
            so.ActionMapName = "Expression";

            // ExpressionBindings は空でよい。AnalogBindings に 1 件追加。
            var binding = new AnalogBindingEntrySerializable
            {
                sourceId = sourceId,
                sourceAxis = 0,
                targetKind = AnalogBindingTargetKind.BlendShape,
                targetIdentifier = blendShapeName,
                targetAxis = AnalogTargetAxis.X,
                mapping = new AnalogMappingFunctionSerializable(),
            };
            // mapping は AnalogMappingFunctionSerializable のデフォルト (deadZone=0, scale=1, offset=0,
            // curveType=Linear, invert=false, min=0, max=1) を使う。
            so.AnalogBindings.Add(binding);
            return so;
        }

        // ================================================================
        // Reflection ヘルパー
        // ================================================================

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"{fieldName} フィールドが {target.GetType().Name} に見つかりません。");
            field.SetValue(target, value);
        }
    }
}
