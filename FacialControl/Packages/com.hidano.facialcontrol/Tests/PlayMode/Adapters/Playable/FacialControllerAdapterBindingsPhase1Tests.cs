using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Adapters;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.PlayMode.Adapters.Playable
{
    /// <summary>
    /// Phase 2 完了後の <see cref="FacialController"/> + <c>_adapterBindings</c> 経路 PlayMode テスト。
    /// Phase 1 並走期に存在した「empty-list ゲート」「旧 Extension 経路駆動」「両経路同時検出時の
    /// dual-path warning」の 3 シナリオは tasks.md 11.1 で全て廃止され、残るのは「無条件
    /// child scope build → binding lifecycle が VContainer 経由で駆動される」契約のみ。
    /// </summary>
    [TestFixture]
    public class FacialControllerAdapterBindingsPhase1Tests
    {
        private GameObject _controllerGameObject;

        [TearDown]
        public void TearDown()
        {
            if (_controllerGameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_controllerGameObject);
                _controllerGameObject = null;
            }
        }

        // ---------------------------------------------------------------
        // _adapterBindings に登録された binding は child scope build → VContainer 経由で
        // OnStart / OnLateTick / Dispose が呼ばれる。
        // ---------------------------------------------------------------

        [UnityTest]
        public IEnumerator Initialize_AdapterBindingsNonEmpty_VContainerInvokesBindingLifecycle()
        {
            _controllerGameObject = CreateControllerHost();
            var controller = _controllerGameObject.AddComponent<FacialController>();
            var so = ScriptableObject.CreateInstance<TestablePhase1ProfileSO>();
            var binding = new TrackingAdapterBinding { Slug = "tracking-1" };
            so.WritableAdapterBindings.Add(binding);

            try
            {
                controller.CharacterSO = so;
                controller.Initialize();

                Assert.That(controller.IsInitialized, Is.True,
                    "Initialize は AdapterBindings 経路でも完了するはず。");
                Assert.That(binding.OnStartCount, Is.EqualTo(1),
                    "child scope build 後に binding.OnStart が VContainer の IInitializable 経由で 1 回呼ばれるはず。");

                // VContainer の ILateTickable は Unity の LateUpdate と同じ PlayerLoop bucket。
                // 1〜2 frame 進めて少なくとも 1 回 dispatch されたことを観測する。
                yield return null;
                yield return null;

                Assert.That(binding.OnLateTickCount, Is.GreaterThanOrEqualTo(1),
                    "binding.OnLateTick は VContainer の ILateTickable として PlayerLoop.LateUpdate 経由で駆動されるはず。");

                // OnDisable → Cleanup → child scope.Dispose() → binding.Dispose() の連鎖を検証。
                _controllerGameObject.SetActive(false);

                Assert.That(binding.DisposeCount, Is.EqualTo(1),
                    "Cleanup で child scope を Dispose() し、binding.Dispose が VContainer の IDisposable 経由で 1 回呼ばれるはず。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        // ---------------------------------------------------------------
        // _adapterBindings が空でも child scope build は走る（tasks.md 11.1: 無条件 build）。
        // 旧 Extension 経路は完全削除済みのため、空 list でも Initialize は完了する。
        // ---------------------------------------------------------------

        [Test]
        public void Initialize_AdapterBindingsEmpty_BuildsChildScopeUnconditionally()
        {
            _controllerGameObject = CreateControllerHost();
            var controller = _controllerGameObject.AddComponent<FacialController>();
            var so = ScriptableObject.CreateInstance<TestablePhase1ProfileSO>();
            try
            {
                Assert.That(so.AdapterBindings.Count, Is.EqualTo(0),
                    "Test 前提: AdapterBindings は空 list で開始するはず。");

                controller.CharacterSO = so;
                controller.Initialize();

                Assert.That(controller.IsInitialized, Is.True,
                    "AdapterBindings が空でも child scope build は走り Initialize は完了するはず。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        // ---------------------------------------------------------------
        // Helpers / Mocks
        // ---------------------------------------------------------------

        private static GameObject CreateControllerHost()
        {
            var go = new GameObject("FacialControllerAdapterBindingsPhase1TestsHost");
            go.AddComponent<Animator>();
            // ResolveSkinnedMeshRenderers が空配列を返すと Initialize が早期 return するため、
            // sharedMesh を持たない Renderer を 1 件だけぶら下げて Initialize を成立させる。
            var meshGo = new GameObject("Mesh");
            meshGo.transform.SetParent(go.transform);
            meshGo.AddComponent<SkinnedMeshRenderer>();
            return go;
        }

        /// <summary>
        /// task 4.2 で追加された <c>protected List&lt;AdapterBindingBase&gt; _adapterBindings</c> field に
        /// PlayMode テスト側から直接書き込めるよう公開する concrete <see cref="FacialCharacterProfileSO"/>。
        /// <see cref="LoadProfile"/> は StreamingAssets 探索を経由せず最小の <see cref="FacialProfile"/> を返す。
        /// </summary>
        public sealed class TestablePhase1ProfileSO : FacialCharacterProfileSO
        {
            public List<AdapterBindingBase> WritableAdapterBindings => _adapterBindings;

            public override FacialProfile LoadProfile()
            {
                var layers = new[]
                {
                    new LayerDefinition("emotion", 0, ExclusionMode.LastWins)
                };
                return new FacialProfile("2.0", layers);
            }
        }

        /// <summary>
        /// VContainer 経由の lifecycle 駆動を観測するための <see cref="AdapterBindingBase"/> 派生 Mock。
        /// 同 instance に対する <see cref="OnStart"/> / <see cref="OnLateTick"/> / <see cref="Dispose"/>
        /// の呼出回数を <c>NonSerialized</c> field で集計する。
        /// </summary>
        [Serializable]
        public sealed class TrackingAdapterBinding : AdapterBindingBase
        {
            [NonSerialized] public int OnStartCount;
            [NonSerialized] public int OnLateTickCount;
            [NonSerialized] public int DisposeCount;

            public override void OnStart(in AdapterBuildContext ctx)
            {
                OnStartCount++;
            }

            public override void OnLateTick(float deltaTime)
            {
                OnLateTickCount++;
            }

            public override void Dispose()
            {
                DisposeCount++;
            }
        }
    }
}
