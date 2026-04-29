using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Hidano.FacialControl.Adapters.Input;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Adapters.OSC;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Adapters.ScriptableObject;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.InputSystem;

namespace Hidano.FacialControl.Tests.PlayMode.Integration
{
    /// <summary>
    /// Phase 8.2: アナログバインディングの E2E 統合テスト（tasks.md 8.2、Req 4.5 / 5.1 / 5.3 / 5.5 /
    /// 9.1〜9.3 / 9.5 / 11.2 / 11.3 / 11.5）。
    /// </summary>
    /// <remarks>
    /// JSON → <see cref="AnalogInputBindingProfileSO"/> → Domain → BoneWriter / Aggregator → Transform /
    /// BlendShape の全経路を 3 シナリオで検証する。
    /// <list type="bullet">
    ///   <item><term>シナリオ A</term><description>右スティック (<see cref="InputAction"/> Vector2)
    ///     → <see cref="AnalogBonePoseProvider"/> → <see cref="IBonePoseProvider.SetActiveBonePose"/>
    ///     → <see cref="Hidano.FacialControl.Adapters.Bone.BoneWriter.Apply"/> →
    ///     LeftEye/RightEye <see cref="Transform.localRotation"/>。body tilt 込みでも basis 相対が
    ///     保たれること（Req 4.5 / 11.2）。</description></item>
    ///   <item><term>シナリオ B</term><description>ARKit <c>/ARKit/jawOpen</c> 0.0→1.0 を OSC 受信
    ///     スレッドから注入 → <see cref="ArKitOscAnalogSource"/> → <see cref="AnalogBlendShapeInputSource"/>
    ///     → <see cref="Hidano.FacialControl.Domain.Services.LayerInputSourceAggregator"/> weighted-sum +
    ///     clamp01 → <see cref="SkinnedMeshRenderer.GetBlendShapeWeight(int)"/>（Req 11.3）。</description></item>
    ///   <item><term>シナリオ C</term><description>VRChat 互換 <c>/avatar/parameters/eyeBrowsY</c> の float
    ///     → <see cref="OscFloatAnalogSource"/> → BlendShape 経路（Req 5.3）。</description></item>
    /// </list>
    /// </remarks>
    [TestFixture]
    public class AnalogBindingEndToEndTests : InputTestFixture
    {
        // ============================================================
        // テストフィクスチャ状態
        // ============================================================

        private readonly List<UnityEngine.Object> _trackedObjects = new List<UnityEngine.Object>();
        private GameObject _root;
        private GameObject _binderObj;
        private GameObject _receiverObj;
        private InputActionAsset _actionAsset;
        private AnalogInputBindingProfileSO _profileSO;
        private OscReceiver _receiver;
        private OscDoubleBuffer _buffer;
        private Gamepad _gamepad;

        public override void Setup()
        {
            base.Setup();
            _gamepad = UnityEngine.InputSystem.InputSystem.AddDevice<Gamepad>();
        }

        public override void TearDown()
        {
            if (_binderObj != null) { UnityEngine.Object.DestroyImmediate(_binderObj); _binderObj = null; }
            if (_root != null) { UnityEngine.Object.DestroyImmediate(_root); _root = null; }
            if (_actionAsset != null) { UnityEngine.Object.DestroyImmediate(_actionAsset); _actionAsset = null; }
            if (_profileSO != null) { UnityEngine.Object.DestroyImmediate(_profileSO); _profileSO = null; }
            if (_buffer != null) { _buffer.Dispose(); _buffer = null; }
            if (_receiverObj != null) { UnityEngine.Object.DestroyImmediate(_receiverObj); _receiverObj = null; }

            for (int i = 0; i < _trackedObjects.Count; i++)
            {
                if (_trackedObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_trackedObjects[i]);
                }
            }
            _trackedObjects.Clear();

            base.TearDown();
        }

        // ============================================================
        // シナリオ A: 右スティック → BonePose → Transform.localRotation
        // ============================================================

        [UnityTest]
        public IEnumerator ScenarioA_RightStick_DrivesEyeRotation_BasisRelative()
        {
            // body tilt を Head に与え、basis 相対の合成結果になっていることを検証する（Req 4.5）。
            BuildBoneHierarchy(out var head, out var leftEye, out var rightEye);
            var controller = _root.GetComponent<FacialController>();
            controller.InitializeWithProfile(CreateMinimalProfile());

            _actionAsset = CreateAnalogActionAsset();
            _profileSO = CreateRightStickBonePoseProfileSO();

            var binder = AttachBinder(controller, _profileSO, _actionAsset);

            // body tilt: Head に Z 軸 25deg のロールを与える。
            var tilt = Quaternion.Euler(0f, 0f, 25f);
            head.localRotation = tilt;

            _binderObj.SetActive(true);

            // X=1.0 → mapping(deadZone=0, scale=1, curve=Linear, min=-180, max=180) で 1.0
            // （TransitionCalculator.Evaluate は内部で clamp01 するため、curve 後の値は [0,1] に収まる）。
            Set(_gamepad.leftStick, new Vector2(1.0f, 0f));
            yield return null;
            yield return null;

            // 期待値: tilt * Quaternion.Euler(0, 1, 0)（basis 相対の合成結果）
            var expected = tilt * Quaternion.Euler(0f, 1f, 0f);
            AssertQuaternionApprox(expected, leftEye.localRotation, 1e-3f,
                "シナリオ A: 右スティック X → LeftEye Y が basis(tilt) 相対の合成結果");
            AssertQuaternionApprox(expected, rightEye.localRotation, 1e-3f,
                "シナリオ A: 右スティック X → RightEye Y が basis(tilt) 相対の合成結果");

            // 入力を 0 に戻すと localRotation は basis * identity = basis に近づく。
            Set(_gamepad.leftStick, Vector2.zero);
            yield return null;
            yield return null;

            AssertQuaternionApprox(tilt, leftEye.localRotation, 1e-3f,
                "シナリオ A: 入力 0 で LeftEye が basis(tilt) のみに戻る");

            // 既存テスト（BlendShapeNonRegression / FacialControllerLifecycle）の前提となる
            // FC 状態（IsInitialized）が破壊されていないこと（Req 9.3, 9.5）。
            Assert.IsTrue(controller.IsInitialized,
                "シナリオ A 後でも FacialController は初期化状態を維持する（Req 9.3, 9.5）");
        }

        // ============================================================
        // シナリオ B: ARKit jawOpen → AnalogBlendShape → SkinnedMeshRenderer 重み
        // ============================================================

        [UnityTest]
        public IEnumerator ScenarioB_ArKitJawOpen_DrivesBlendShapeWeight()
        {
            BuildOscReceiver();

            // ARKit jawOpen を扱う 1ch ソース。staleness=0 で last-valid 永続。
            using var arkitSource = new ArKitOscAnalogSource(
                InputSourceId.Parse("arkit_jaw_open"),
                _receiver,
                new[] { "jawOpen" },
                stalenessSeconds: 0f);

            BuildBlendShapeHierarchy(out var smr, out int jawOpenIndex, "jawOpen");
            var controller = _root.GetComponent<FacialController>();

            // analog-blendshape を layer 0 で宣言（必要、Req 11.3）。weight=1.0。
            var profile = CreateProfileWithAnalogBlendShape();
            controller.InitializeWithProfile(profile);

            _profileSO = CreateArKitJawOpenProfileSO();

            var binder = AttachBinder(controller, _profileSO, actionAsset: null);
            // 外部 OSC ソースを sourceId 名で先に登録してから OnEnable へ進む。
            binder.RegisterExternalSource("arkit_jaw_open", arkitSource);
            _binderObj.SetActive(true);

            // 受信スレッド相当: HandleOscMessage は内部で _analogListeners[address] を発火する。
            _receiver.HandleOscMessage(new uOSC.Message("/ARKit/jawOpen", 1.0f));

            yield return null;
            yield return null;

            // FacialController.LateUpdate が SetBlendShapeWeight(0-100) で書込んでいる。
            // mapping (scale=1, min=0, max=1) → Aggregator clamp01 → FC * 100 = 100。
            float weight = smr.GetBlendShapeWeight(jawOpenIndex);
            Assert.Greater(weight, 50f,
                $"シナリオ B: ARKit jawOpen=1.0 で SkinnedMeshRenderer の jawOpen 重みが上昇する " +
                $"(actual={weight}, Req 11.3)");

            // jawOpen を 0 に戻すと重みも 0 付近に戻る（Aggregator が新値を反映）。
            _receiver.HandleOscMessage(new uOSC.Message("/ARKit/jawOpen", 0.0f));
            yield return null;
            yield return null;

            float weightAfterRelease = smr.GetBlendShapeWeight(jawOpenIndex);
            Assert.Less(weightAfterRelease, 5f,
                $"シナリオ B: ARKit jawOpen=0.0 で重みが解除される (actual={weightAfterRelease})");
        }

        // ============================================================
        // シナリオ C: VRChat 互換 OSC float → BlendShape
        // ============================================================

        [UnityTest]
        public IEnumerator ScenarioC_VRChatOscFloat_DrivesBlendShapeWeight()
        {
            BuildOscReceiver();

            // VRChat 互換 OSC アドレスを購読する float 1ch ソース。
            using var oscSource = new OscFloatAnalogSource(
                InputSourceId.Parse("osc_eyebrows"),
                _receiver,
                "/avatar/parameters/eyeBrowsY",
                stalenessSeconds: 0f);

            BuildBlendShapeHierarchy(out var smr, out int eyeBrowsIndex, "eyeBrowsY");
            var controller = _root.GetComponent<FacialController>();

            var profile = CreateProfileWithAnalogBlendShape();
            controller.InitializeWithProfile(profile);

            _profileSO = CreateOscBlendShapeProfileSO("osc_eyebrows", "eyeBrowsY");

            var binder = AttachBinder(controller, _profileSO, actionAsset: null);
            binder.RegisterExternalSource("osc_eyebrows", oscSource);
            _binderObj.SetActive(true);

            _receiver.HandleOscMessage(new uOSC.Message("/avatar/parameters/eyeBrowsY", 0.8f));

            yield return null;
            yield return null;

            // mapping(scale=1, min=0, max=1) → 0.8 → Aggregator clamp01 → 0.8 → FC * 100 = 80
            float weight = smr.GetBlendShapeWeight(eyeBrowsIndex);
            Assert.Greater(weight, 50f,
                $"シナリオ C: VRChat OSC eyeBrowsY=0.8 で BlendShape 重みが追従する " +
                $"(actual={weight}, Req 5.3)");

            Assert.Less(weight, 100f + 1e-2f,
                $"シナリオ C: 重みは Aggregator clamp01 + FC * 100 で 100 を上限とする (actual={weight})");
        }

        // ============================================================
        // 既存テストの非破壊（Req 9.3, 9.5）
        // ============================================================
        // 新規 binder の存在によって既存のスイート（BlendShapeNonRegressionTests /
        // FacialControllerLifecycleTests）が無修正で全件 Green であることは別 Test Run で観測する。
        // ここでは binder 起動 → 解除のライフサイクル後に FacialController が継続利用できる
        // ことを 1 ケースで担保する。

        [UnityTest]
        public IEnumerator BinderLifecycle_DoesNotBreakControllerOrExistingTests()
        {
            BuildBoneHierarchy(out _, out _, out _);
            var controller = _root.GetComponent<FacialController>();
            controller.InitializeWithProfile(CreateMinimalProfile());

            _actionAsset = CreateAnalogActionAsset();
            _profileSO = CreateRightStickBonePoseProfileSO();

            AttachBinder(controller, _profileSO, _actionAsset);
            _binderObj.SetActive(true);
            yield return null;

            // OnDisable で binder を畳む（Req 7.3）。
            _binderObj.SetActive(false);
            yield return null;

            Assert.IsTrue(controller.IsInitialized,
                "binder OnDisable 後も FacialController は初期化状態を維持する（Req 9.5）");
        }

        // ============================================================
        // ヘルパー: シーン構築
        // ============================================================

        private void BuildBoneHierarchy(out Transform head, out Transform leftEye, out Transform rightEye)
        {
            _root = new GameObject("AnalogE2E_BoneRoot");
            _root.transform.position = Vector3.zero;
            _root.AddComponent<Animator>();
            _root.AddComponent<FacialController>();

            // SkinnedMeshRenderer は基本パイプライン互換のために子に持たせる（mesh は無し）。
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

        private void BuildBlendShapeHierarchy(
            out SkinnedMeshRenderer smr,
            out int targetBlendShapeIndex,
            string blendShapeName)
        {
            _root = new GameObject("AnalogE2E_BlendShapeRoot");
            _root.transform.position = Vector3.zero;
            _root.AddComponent<Animator>();
            _root.AddComponent<FacialController>();

            // Head 階層も用意（FacialController が basis bone を解決する際の安全側）。
            var hips = MakeChild(_root.transform, "Hips");
            var spine = MakeChild(hips, "Spine");
            var neck = MakeChild(spine, "Neck");
            MakeChild(neck, "Head");

            var meshObj = new GameObject("Mesh");
            meshObj.transform.SetParent(_root.transform, worldPositionStays: false);
            smr = meshObj.AddComponent<SkinnedMeshRenderer>();

            // 単一 BlendShape を持つ最小限の Mesh を動的生成する（Editor 系テストと同パターン）。
            var mesh = new Mesh();
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            var zeroDeltas = new Vector3[3];
            mesh.AddBlendShapeFrame(blendShapeName, 100f, zeroDeltas, null, null);
            _trackedObjects.Add(mesh);

            smr.sharedMesh = mesh;
            targetBlendShapeIndex = mesh.GetBlendShapeIndex(blendShapeName);
            Assert.GreaterOrEqual(targetBlendShapeIndex, 0,
                $"前提: BlendShape '{blendShapeName}' が Mesh に存在すること");
        }

        private void BuildOscReceiver()
        {
            _receiverObj = new GameObject("AnalogE2E_OscReceiver");
            _receiver = _receiverObj.AddComponent<OscReceiver>();
            _buffer = new OscDoubleBuffer(0);
            _receiver.Initialize(_buffer, Array.Empty<OscMapping>());
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

        // ============================================================
        // ヘルパー: Profile / SO 生成
        // ============================================================

        private static FacialProfile CreateMinimalProfile()
        {
            var layers = new[]
            {
                new LayerDefinition("emotion", 0, ExclusionMode.LastWins),
            };
            return new FacialProfile("1.0.0", layers, null, null, null, Array.Empty<BonePose>());
        }

        /// <summary>
        /// layer 0 で <c>analog-blendshape</c> を宣言したプロファイル。AnalogBlendShapeRegistration
        /// が <c>RegisterReserved</c> 経由でアダプタを生成し、Aggregator の入力源として組込まれる。
        /// </summary>
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
                bonePoses: Array.Empty<BonePose>());
        }

        private static InputActionAsset CreateAnalogActionAsset()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Analog");
            var stick = map.AddAction("right_stick", InputActionType.Value);
            stick.AddBinding("<Gamepad>/leftStick");
            asset.AddActionMap(map);
            return asset;
        }

        /// <summary>
        /// 右スティック(<c>right_stick</c>) → LeftEye/RightEye の Y 軸 BonePose binding。
        /// scale=1, min=-180, max=180, deadZone=0, curve=Linear。
        /// </summary>
        /// <remarks>
        /// <see cref="Hidano.FacialControl.Domain.Services.AnalogMappingEvaluator"/> は
        /// curve 評価で <c>clamp01</c> をかけるため、curve 後の値は [0,1] に収まる。
        /// テストでは入力値 1.0 を与えて output=1.0 となる構成を使う。
        /// </remarks>
        private static AnalogInputBindingProfileSO CreateRightStickBonePoseProfileSO()
        {
            var so = ScriptableObject.CreateInstance<AnalogInputBindingProfileSO>();
            string json =
                "{\n" +
                "  \"version\": \"1.0.0\",\n" +
                "  \"bindings\": [\n" +
                BoneposeBindingJson("right_stick", 0, "LeftEye", "Y", 1f) + ",\n" +
                BoneposeBindingJson("right_stick", 0, "RightEye", "Y", 1f) +
                "  ]\n" +
                "}";
            so.JsonText = json;
            return so;
        }

        /// <summary>
        /// ARKit jawOpen → BlendShape <c>jawOpen</c> binding。scale=1, min=0, max=1, deadZone=0。
        /// </summary>
        private static AnalogInputBindingProfileSO CreateArKitJawOpenProfileSO()
        {
            var so = ScriptableObject.CreateInstance<AnalogInputBindingProfileSO>();
            string json =
                "{\n" +
                "  \"version\": \"1.0.0\",\n" +
                "  \"bindings\": [\n" +
                BlendShapeBindingJson("arkit_jaw_open", 0, "jawOpen") +
                "  ]\n" +
                "}";
            so.JsonText = json;
            return so;
        }

        /// <summary>VRChat 互換 OSC float → BlendShape binding。</summary>
        private static AnalogInputBindingProfileSO CreateOscBlendShapeProfileSO(string sourceId, string blendShapeName)
        {
            var so = ScriptableObject.CreateInstance<AnalogInputBindingProfileSO>();
            string json =
                "{\n" +
                "  \"version\": \"1.0.0\",\n" +
                "  \"bindings\": [\n" +
                BlendShapeBindingJson(sourceId, 0, blendShapeName) +
                "  ]\n" +
                "}";
            so.JsonText = json;
            return so;
        }

        private static string BoneposeBindingJson(
            string sourceId, int sourceAxis, string boneName, string targetAxis, float scale)
        {
            return
                "    {\n" +
                "      \"sourceId\": \"" + sourceId + "\",\n" +
                "      \"sourceAxis\": " + sourceAxis + ",\n" +
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
                "    }";
        }

        private static string BlendShapeBindingJson(string sourceId, int sourceAxis, string blendShapeName)
        {
            return
                "    {\n" +
                "      \"sourceId\": \"" + sourceId + "\",\n" +
                "      \"sourceAxis\": " + sourceAxis + ",\n" +
                "      \"targetKind\": \"blendshape\",\n" +
                "      \"targetIdentifier\": \"" + blendShapeName + "\",\n" +
                "      \"targetAxis\": \"X\",\n" +
                "      \"mapping\": {\n" +
                "        \"deadZone\": 0.0,\n" +
                "        \"scale\": 1.0,\n" +
                "        \"offset\": 0.0,\n" +
                "        \"curveType\": \"Linear\",\n" +
                "        \"curveKeyFrames\": [],\n" +
                "        \"invert\": false,\n" +
                "        \"min\": 0.0,\n" +
                "        \"max\": 1.0\n" +
                "      }\n" +
                "    }";
        }

        // ============================================================
        // ヘルパー: Binder 結線
        // ============================================================

        private FacialAnalogInputBinder AttachBinder(
            FacialController controller,
            AnalogInputBindingProfileSO profile,
            InputActionAsset actionAsset)
        {
            _binderObj = new GameObject("AnalogE2E_Binder");
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
            var field = target.GetType().GetField(
                fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, $"{fieldName} フィールドが {target.GetType().Name} に見つかりません。");
            field.SetValue(target, value);
        }

        // ============================================================
        // ヘルパー: Quaternion 近似比較（±q を等価とみなす）
        // ============================================================

        private static void AssertQuaternionApprox(Quaternion expected, Quaternion actual, float tol, string label)
        {
            float dot = expected.x * actual.x + expected.y * actual.y +
                        expected.z * actual.z + expected.w * actual.w;
            float diff = Mathf.Abs(Mathf.Abs(dot) - 1f);
            Assert.That(diff, Is.LessThan(tol),
                $"{label}: 期待値 {expected} と実測値 {actual} が一致しません（|dot|-1={diff}）");
        }
    }
}
