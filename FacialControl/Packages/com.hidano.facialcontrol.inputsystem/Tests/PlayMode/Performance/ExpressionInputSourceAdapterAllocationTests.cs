using System;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.InputSystem.Tests.PlayMode.Performance
{
    /// <summary>
    /// tasks.md 4.8: <see cref="ExpressionInputSourceAdapter"/> の
    /// <c>InputAction.performed</c> dispatch ホットパスが 100 回連続呼出で 0-alloc であることを
    /// 検証する PlayMode/Performance テスト（Req 11.3, 11.5, 12.7）。
    /// </summary>
    /// <remarks>
    /// <para>
    /// adapter の <c>BindingEntry</c> は構築時に delegate を 1 度だけキャッシュし、
    /// 購読 / 解除に伴う closure 確保が無いことを保証する。dispatch 経路の値読取は
    /// <c>InputAction.CallbackContext.ReadValue&lt;float&gt;</c> を struct context 経由で呼ぶため
    /// boxing が発生しない。本テストは <see cref="InputTestFixture"/> の <c>Press</c> / <c>Release</c>
    /// で keyboard / gamepad 両 device 経由の dispatch を 100 回繰り返し、ProfilerRecorder で
    /// adapter 経路に GC アロケーションが発生しないことを検証する。
    /// </para>
    /// <para>
    /// InputSystem 内部のイベントキュー処理は本 adapter の責務外だが、
    /// Press / Release を <c>InputState.Change</c> 経由で driving した場合の総アロケーションが
    /// 0 であることを期待する（InputSystem 1.x の steady-state 振る舞い）。
    /// </para>
    /// </remarks>
    [TestFixture]
    public class ExpressionInputSourceAdapterAllocationTests : InputTestFixture
    {
        private const int DispatchCount = 100;

        private static readonly string[] BlendShapeNames =
        {
            "smile", "angry", "sad", "surprised",
        };

        private GameObject _gameObject;
        private ExpressionInputSourceAdapter _adapter;
        private ExpressionTriggerInputSource _keyboardSink;
        private ExpressionTriggerInputSource _controllerSink;
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
                UnityEngine.Object.DestroyImmediate(_gameObject);
                _gameObject = null;
            }
            base.TearDown();
        }

        [Test]
        public void KeyboardPerformed_100Dispatches_ZeroGCAllocation()
        {
            CreateAdapter();
            using var action = new InputAction("Smile", InputActionType.Button, "<Keyboard>/1");
            _adapter.BindExpression(action, "smile");
            action.Enable();

            // ウォームアップ: 1 回 Press/Release で JIT 等の初期化を済ませる
            Press(_keyboard.digit1Key);
            Release(_keyboard.digit1Key);

            using var recorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory, "GC.Alloc");

            for (int i = 0; i < DispatchCount; i++)
            {
                Press(_keyboard.digit1Key);
                Release(_keyboard.digit1Key);
            }

            long gcAlloc = recorder.LastValue;

            Assert.AreEqual(0, gcAlloc,
                $"Keyboard dispatch で GC アロケーションが検出されました: {gcAlloc} bytes (100 回 dispatch)");
        }

        [Test]
        public void GamepadPerformed_100Dispatches_ZeroGCAllocation()
        {
            CreateAdapter();
            using var action = new InputAction("Smile", InputActionType.Button, "<Gamepad>/buttonSouth");
            _adapter.BindExpression(action, "smile");
            action.Enable();

            // ウォームアップ
            Press(_gamepad.buttonSouth);
            Release(_gamepad.buttonSouth);

            using var recorder = ProfilerRecorder.StartNew(
                ProfilerCategory.Memory, "GC.Alloc");

            for (int i = 0; i < DispatchCount; i++)
            {
                Press(_gamepad.buttonSouth);
                Release(_gamepad.buttonSouth);
            }

            long gcAlloc = recorder.LastValue;

            Assert.AreEqual(0, gcAlloc,
                $"Gamepad dispatch で GC アロケーションが検出されました: {gcAlloc} bytes (100 回 dispatch)");
        }

        [Test]
        public void Tick_100Frames_ZeroGCAllocation()
        {
            CreateAdapter();
            using var action = new InputAction("Smile", InputActionType.Button, "<Keyboard>/1");
            _adapter.BindExpression(action, "smile");
            action.Enable();

            // 1 件アクティブにしてから Tick の遷移進行を計測する
            Press(_keyboard.digit1Key);

            // ウォームアップ
            _adapter.Tick(0.016f);

            long allocBefore = GC.GetTotalMemory(false);
            for (int frame = 0; frame < DispatchCount; frame++)
            {
                _adapter.Tick(0.016f);
            }
            long allocAfter = GC.GetTotalMemory(false);

            long allocated = allocAfter - allocBefore;
            Assert.LessOrEqual(allocated, 0,
                $"adapter.Tick で GC アロケーションが検出されました: {allocated} bytes (100 frames)");
        }

        [Test]
        public void BindUnbind_DoesNotLeakAfterWarmup()
        {
            CreateAdapter();

            // ウォームアップ: bind → unbind を 1 回行い、内部 Dictionary の容量確保等を済ませる
            using var warmupAction = new InputAction("Warmup", InputActionType.Button, "<Keyboard>/1");
            _adapter.BindExpression(warmupAction, "smile");
            _adapter.UnbindExpression(warmupAction);

            long allocBefore = GC.GetTotalMemory(false);

            for (int i = 0; i < DispatchCount; i++)
            {
                using var action = new InputAction("Bind" + (i % 2), InputActionType.Button, "<Keyboard>/1");
                _adapter.BindExpression(action, "smile");
                _adapter.UnbindExpression(action);
            }

            long allocAfter = GC.GetTotalMemory(false);

            // InputAction の生成 / Dispose 自体は InputSystem 内部でアロケーションが発生し得るため、
            // adapter 自体の bind/unbind が「秤量できるほどの」漏洩を起こしていないことを確認する
            // 緩い境界条件で検証する（アクション生成は InputSystem 側の責務）。
            long allocated = allocAfter - allocBefore;
            Assert.That(allocated, Is.LessThan(64 * 1024),
                $"BindExpression / UnbindExpression で予想外の GC アロケーションが検出されました: {allocated} bytes");
        }

        // ============================================================
        // ヘルパー
        // ============================================================

        private void CreateAdapter()
        {
            _gameObject = new GameObject("ExpressionInputSourceAdapterAllocationTest");
            _adapter = _gameObject.AddComponent<ExpressionInputSourceAdapter>();

            var profile = BuildProfile();

            _keyboardSink = new ExpressionTriggerInputSource(
                id: InputSourceId.Parse(ExpressionTriggerInputSource.KeyboardReservedId),
                blendShapeCount: BlendShapeNames.Length,
                maxStackDepth: 8,
                exclusionMode: ExclusionMode.LastWins,
                blendShapeNames: BlendShapeNames,
                profile: profile);

            _controllerSink = new ExpressionTriggerInputSource(
                id: InputSourceId.Parse(ExpressionTriggerInputSource.ControllerReservedId),
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
            };

            return new FacialProfile("1.0", layers: layers, expressions: expressions);
        }
    }
}
