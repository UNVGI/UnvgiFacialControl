using System.Collections;
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
using Hidano.FacialControl.InputSystem;

namespace Hidano.FacialControl.Tests.PlayMode.Adapters.Input
{
    /// <summary>
    /// Phase 6.1: <see cref="FacialAnalogInputBinder"/> の PlayMode 統合テスト
    /// (Req 7.1〜7.7, 9.3〜9.5)。
    /// </summary>
    [TestFixture]
    public class FacialAnalogInputBinderTests : InputTestFixture
    {
        private GameObject _root;
        private GameObject _binderObj;
        private InputActionAsset _actionAsset;
        private AnalogInputBindingProfileSO _profileSO;
        private Gamepad _gamepad;
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _gamepad = UnityEngine.InputSystem.InputSystem.AddDevice<Gamepad>();
            _keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<Keyboard>();
        }

        public override void TearDown()
        {
            if (_binderObj != null)
            {
                Object.DestroyImmediate(_binderObj);
                _binderObj = null;
            }
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
                _root = null;
            }
            if (_actionAsset != null)
            {
                Object.DestroyImmediate(_actionAsset);
                _actionAsset = null;
            }
            if (_profileSO != null)
            {
                Object.DestroyImmediate(_profileSO);
                _profileSO = null;
            }
            base.TearDown();
        }

        // ============================================================
        // 入力検証 / null safety
        // ============================================================

        [Test]
        public void OnEnable_NullController_LogsWarningWithoutThrow()
        {
            _binderObj = new GameObject("AnalogBinder");
            _binderObj.SetActive(false);
            _binderObj.AddComponent<FacialAnalogInputBinder>();

            LogAssert.Expect(LogType.Warning, new Regex(".*"));
            Assert.DoesNotThrow(() => _binderObj.SetActive(true));
        }

        [Test]
        public void OnEnable_NullProfile_LogsWarningWithoutThrow()
        {
            BuildHierarchy(out _, out _, out _);
            var controller = _root.GetComponent<FacialController>();
            controller.InitializeWithProfile(CreateMinimalProfile());

            _binderObj = new GameObject("AnalogBinder");
            _binderObj.SetActive(false);
            var binder = _binderObj.AddComponent<FacialAnalogInputBinder>();
            SetPrivateField(binder, "_facialController", controller);

            LogAssert.Expect(LogType.Warning, new Regex(".*"));
            Assert.DoesNotThrow(() => _binderObj.SetActive(true));
        }

        // ============================================================
        // BonePose binding: 右スティック → LeftEye の Y 軸へ反映 (Req 7.1, 7.5)
        // ============================================================

        [UnityTest]
        public IEnumerator BonePoseBinding_DrivesLeftEyeRotation()
        {
            BuildHierarchy(out _, out var leftEye, out _);
            var controller = _root.GetComponent<FacialController>();
            controller.InitializeWithProfile(CreateMinimalProfile());

            _actionAsset = CreateAnalogActionAsset();
            _profileSO = CreateBonePoseProfile("right_stick", "LeftEye", AnalogTargetAxis.Y, scale: 30f);

            CreateBinder(controller, _profileSO, _actionAsset);
            _binderObj.SetActive(true);

            // 1 fr で source.Tick / BuildAndPush / SetActiveBonePose、
            // さらに 1 fr で BoneWriter.Apply が走る
            Set(_gamepad.leftStick, new Vector2(0.5f, 0f));
            yield return null;
            yield return null;

            Assert.AreNotEqual(Quaternion.identity, leftEye.localRotation,
                "右スティック X 入力で LeftEye の localRotation が更新されること");
        }

        // ============================================================
        // OnDisable: BonePose Provider が dispose され、ActionMap が無効化される (Req 7.3)
        // ============================================================

        [UnityTest]
        public IEnumerator OnDisable_StopsDrivingLeftEyeRotation()
        {
            BuildHierarchy(out _, out var leftEye, out _);
            var controller = _root.GetComponent<FacialController>();
            controller.InitializeWithProfile(CreateMinimalProfile());

            _actionAsset = CreateAnalogActionAsset();
            _profileSO = CreateBonePoseProfile("right_stick", "LeftEye", AnalogTargetAxis.Y, scale: 30f);

            CreateBinder(controller, _profileSO, _actionAsset);
            _binderObj.SetActive(true);

            Set(_gamepad.leftStick, new Vector2(0.7f, 0f));
            yield return null;
            yield return null;

            var rotatedAfterEnable = leftEye.localRotation;
            Assert.AreNotEqual(Quaternion.identity, rotatedAfterEnable,
                "OnEnable 後は BonePose 駆動で localRotation が変化していること");

            _binderObj.SetActive(false);

            // 別のスティック値を注入しても変化しないことを観測する
            Set(_gamepad.leftStick, new Vector2(-0.7f, 0f));
            var beforeStick = leftEye.localRotation;
            yield return null;
            yield return null;

            // OnDisable 後は BuildAndPush が走らないため、LeftEye は前回の状態か、
            // あるいは BoneWriter の Restore で initial に戻る。
            // いずれにせよ「新しいスティック値」を反映していないことを契約とする。
            Assert.AreEqual(beforeStick, leftEye.localRotation,
                "OnDisable 後は新規スティック値が LeftEye に反映されないこと");
        }

        // ============================================================
        // FacialInputBinder と並走 (Req 7.6, 9.3)
        // ============================================================

        [UnityTest]
        public IEnumerator Coexists_WithFacialInputBinder()
        {
            BuildHierarchy(out _, out var leftEye, out _);
            var controller = _root.GetComponent<FacialController>();
            controller.InitializeWithProfile(CreateProfileWithExpression("expr-001"));

            _actionAsset = CreateAnalogActionAsset();
            _profileSO = CreateBonePoseProfile("right_stick", "LeftEye", AnalogTargetAxis.Y, scale: 30f);

            CreateBinder(controller, _profileSO, _actionAsset);

            // 並走する FacialInputBinder（離散）。InputBindingProfileSO は null でも
            // FacialInputBinder.OnEnable は警告のみで例外を投げない（既存契約）。
            var triggerAsset = ScriptableObject.CreateInstance<InputActionAsset>();
            var triggerMap = new InputActionMap("Expression");
            var triggerAction = triggerMap.AddAction("Trigger1", InputActionType.Button);
            triggerAction.AddBinding("<Keyboard>/1");
            triggerAsset.AddActionMap(triggerMap);

            var triggerProfile = ScriptableObject.CreateInstance<InputBindingProfileSO>();
            SetPrivateField(triggerProfile, "_actionAsset", triggerAsset);
            // _bindings はデフォルトで空配列扱い

            var triggerBinderObj = new GameObject("FacialInputBinder");
            triggerBinderObj.SetActive(false);
            var triggerBinder = triggerBinderObj.AddComponent<FacialInputBinder>();
            SetPrivateField(triggerBinder, "_facialController", controller);
            SetPrivateField(triggerBinder, "_bindingProfile", triggerProfile);

            try
            {
                _binderObj.SetActive(true);
                triggerBinderObj.SetActive(true);

                Set(_gamepad.leftStick, new Vector2(0.6f, 0f));
                yield return null;
                yield return null;

                Assert.AreNotEqual(Quaternion.identity, leftEye.localRotation,
                    "FacialInputBinder が併置されていてもアナログ駆動が継続すること");
            }
            finally
            {
                Object.DestroyImmediate(triggerBinderObj);
                Object.DestroyImmediate(triggerProfile);
                Object.DestroyImmediate(triggerAsset);
            }
        }

        // ============================================================
        // SetProfile: 進行中の Expression を中断しない (Req 7.4, 9.4)
        // ============================================================

        [UnityTest]
        public IEnumerator SetProfile_PreservesActiveExpressionsOnOtherLayer()
        {
            BuildHierarchy(out _, out _, out _);
            var controller = _root.GetComponent<FacialController>();
            var profile = CreateProfileWithExpression("expr-001");
            controller.InitializeWithProfile(profile);

            _actionAsset = CreateAnalogActionAsset();
            _profileSO = CreateBonePoseProfile("right_stick", "LeftEye", AnalogTargetAxis.Y, scale: 30f);

            var binder = CreateBinder(controller, _profileSO, _actionAsset);
            _binderObj.SetActive(true);

            yield return null;

            var expr = profile.Expressions.Span[0];
            controller.Activate(expr);

            yield return null;

            Assert.AreEqual(1, controller.GetActiveExpressions().Count,
                "アナログ binder 起動中でも Expression を Activate できること");

            // 別プロファイル (異なる scale) に差し替える。
            var nextProfile = CreateBonePoseProfile("right_stick", "LeftEye", AnalogTargetAxis.X, scale: 15f);
            try
            {
                binder.SetProfile(nextProfile);

                yield return null;

                var stillActive = controller.GetActiveExpressions();
                Assert.AreEqual(1, stillActive.Count,
                    "SetProfile 後も他レイヤーで Active な Expression は維持される (Req 7.4, 9.4)");
                Assert.AreEqual(expr.Id, stillActive[0].Id);
            }
            finally
            {
                Object.DestroyImmediate(nextProfile);
            }
        }

        // ============================================================
        // Helpers
        // ============================================================

        private void BuildHierarchy(out Transform head, out Transform leftEye, out Transform rightEye)
        {
            _root = new GameObject("FacialAnalogBinderTestRoot");
            _root.transform.position = Vector3.zero;
            _root.AddComponent<Animator>();
            _root.AddComponent<FacialController>();

            var meshObj = new GameObject("Mesh");
            meshObj.transform.SetParent(_root.transform, worldPositionStays: false);
            meshObj.AddComponent<SkinnedMeshRenderer>();

            var hips = MakeChild(_root.transform, "Hips");
            var spine = MakeChild(hips, "Spine");
            var neck = MakeChild(spine, "Neck");
            head = MakeChild(neck, "Head");
            leftEye = MakeChild(head, "LeftEye");
            rightEye = MakeChild(head, "RightEye");
        }

        private static Transform MakeChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        private static FacialProfile CreateMinimalProfile()
        {
            var layers = new[]
            {
                new LayerDefinition("emotion", 0, ExclusionMode.LastWins)
            };
            return new FacialProfile("1.0.0", layers, null, null, null, System.Array.Empty<BonePose>());
        }

        private static FacialProfile CreateProfileWithExpression(string expressionId)
        {
            var layers = new[]
            {
                new LayerDefinition("emotion", 0, ExclusionMode.LastWins)
            };
            var expressions = new[]
            {
                new Expression(expressionId, "TestExpr", "emotion")
            };
            return new FacialProfile("1.0.0", layers, expressions, null, null, System.Array.Empty<BonePose>());
        }

        private static InputActionAsset CreateAnalogActionAsset()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Analog");
            var stick = map.AddAction("right_stick", InputActionType.Value);
            stick.AddBinding("<Gamepad>/leftStick");
            // expectedControlType を Vector2 で確定させる（コンストラクタ引数が無いため
            // SerializedObject 経由で設定する代わりに、controls の解決後に判定する）。
            asset.AddActionMap(map);
            return asset;
        }

        private static AnalogInputBindingProfileSO CreateBonePoseProfile(
            string sourceId,
            string boneName,
            AnalogTargetAxis targetAxis,
            float scale)
        {
            var so = ScriptableObject.CreateInstance<AnalogInputBindingProfileSO>();
            // ToDomain は AnalogInputBindingJsonLoader を経由するため、JSON を手書きする。
            // dead-zone=0, scale=scale, offset=0, curve=Linear, invert=false, min=-180, max=180
            string json =
                "{\n" +
                "  \"version\": \"1.0.0\",\n" +
                "  \"bindings\": [\n" +
                "    {\n" +
                "      \"sourceId\": \"" + sourceId + "\",\n" +
                "      \"sourceAxis\": 0,\n" +
                "      \"targetKind\": \"bonepose\",\n" +
                "      \"targetIdentifier\": \"" + boneName + "\",\n" +
                "      \"targetAxis\": \"" + targetAxis + "\",\n" +
                "      \"mapping\": {\n" +
                "        \"deadZone\": 0.0,\n" +
                "        \"scale\": " + scale.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\n" +
                "        \"offset\": 0.0,\n" +
                "        \"curveType\": \"Linear\",\n" +
                "        \"curveKeyFrames\": [],\n" +
                "        \"invert\": false,\n" +
                "        \"min\": -180.0,\n" +
                "        \"max\": 180.0\n" +
                "      }\n" +
                "    }\n" +
                "  ]\n" +
                "}";
            so.JsonText = json;
            return so;
        }

        private FacialAnalogInputBinder CreateBinder(
            FacialController controller,
            AnalogInputBindingProfileSO profile,
            InputActionAsset actionAsset)
        {
            _binderObj = new GameObject("FacialAnalogInputBinder");
            _binderObj.SetActive(false);
            var binder = _binderObj.AddComponent<FacialAnalogInputBinder>();
            SetPrivateField(binder, "_facialController", controller);
            SetPrivateField(binder, "_profile", profile);
            SetPrivateField(binder, "_actionAsset", actionAsset);
            SetPrivateField(binder, "_actionMapName", "Analog");
            return binder;
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
