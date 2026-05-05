using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hidano.FacialControl.Adapters.InputSources;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Adapters;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.PlayMode.Adapters.Playable
{
    /// <summary>
    /// task 6.1: <see cref="FacialController"/> Phase 1 並走仕様の PlayMode テスト（Red）。
    /// design.md の DD-2 / `## Migration Strategy > Phase 動作契約マトリクス` および
    /// tasks.md task 6.2 で定義される下記契約を assert する：
    /// <list type="bullet">
    ///   <item><description>(a) <c>_adapterBindings.Count == 0</c> の SO では旧
    ///   <see cref="IFacialControllerExtension"/> 経路のみ駆動され、既存 PlayMode テスト群が
    ///   全 green を維持する（DD-2 注記）。</description></item>
    ///   <item><description>(b) <c>_adapterBindings.Count &gt; 0</c> の SO では
    ///   <c>FacialControllerLifetimeScope</c> による child scope build が走り、Mock binding の
    ///   <see cref="AdapterBindingBase.OnStart"/> / <see cref="AdapterBindingBase.OnLateTick"/> /
    ///   <see cref="AdapterBindingBase.Dispose"/> が VContainer 経由で呼ばれる。</description></item>
    ///   <item><description>(c) 旧 <see cref="IFacialControllerExtension"/> コンポーネントと
    ///   <c>_adapterBindings.Count &gt; 0</c> を同時検出した場合、
    ///   <see cref="Debug.LogWarning(object)"/> が出力される（DD-2 衝突防御）。</description></item>
    /// </list>
    /// 本ファイルは task 6.1 の Red 段階であり、task 6.2 で <c>FacialController.Initialize</c> /
    /// <c>Cleanup</c> に VContainer child scope build が追加されるまで (b) (c) は fail する契約。
    /// _Requirements: 4.7, 6.8, 6.9, 9.1, 13.5, 10.3, 10.5_
    /// </summary>
    [TestFixture]
    public class FacialControllerAdapterBindingsPhase1Tests
    {
        private static readonly Regex DualPathWarningPattern =
            new Regex("FacialController", RegexOptions.IgnoreCase);

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
        // (a) DD-2 注記: _adapterBindings.Count == 0 の SO では旧 IFacialControllerExtension 経路のみ駆動
        // ---------------------------------------------------------------

        [Test]
        public void Initialize_AdapterBindingsEmpty_DrivesLegacyExtensionConfigureFactoryPath()
        {
            // 既存 PlayMode テスト群が全 green を維持することの代表 case。
            // _adapterBindings が空であれば child scope build は skip され、
            // 旧 ApplyExtensions → IFacialControllerExtension.ConfigureFactory がそのまま呼ばれる。
            _controllerGameObject = CreateControllerHost();
            var extension = _controllerGameObject.AddComponent<TrackingFacialControllerExtension>();
            var controller = _controllerGameObject.AddComponent<FacialController>();
            var so = ScriptableObject.CreateInstance<TestablePhase1ProfileSO>();
            try
            {
                Assert.That(so.AdapterBindings.Count, Is.EqualTo(0),
                    "Test 前提: AdapterBindings は空 list で開始するはず。");

                controller.CharacterSO = so;
                controller.Initialize();

                Assert.That(controller.IsInitialized, Is.True,
                    "SkinnedMeshRenderer + Animator を備えた host で Initialize は完了するはず。");
                Assert.That(extension.ConfigureFactoryCount, Is.EqualTo(1),
                    "AdapterBindings.Count == 0 の Phase 1 並走期は旧 IFacialControllerExtension.ConfigureFactory が 1 回だけ呼ばれること。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(so);
            }
        }

        // ---------------------------------------------------------------
        // (b) _adapterBindings.Count > 0 の SO で child scope build → binding 各 lifecycle が VContainer 経由で呼ばれる
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
                    "AdapterBindings.Count > 0 で FacialControllerLifetimeScope.Build が走り、binding.OnStart が VContainer の IStartable 経由で 1 回呼ばれるはず。");

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
        // (c) 旧 IFacialControllerExtension + _adapterBindings.Count > 0 の同時検出 → Debug.LogWarning
        // ---------------------------------------------------------------

        [Test]
        public void Initialize_LegacyExtensionAndAdapterBindingsCoexist_LogsDualPathWarning()
        {
            // DD-2 衝突防御: Phase 1 並走期に旧 Extension コンポーネントと新 _adapterBindings の
            // 両方が同時に存在する場合は runtime warning を出して slug 衝突や挙動不整合を早期検出する。
            // 警告本文は実装側で確定する想定のため "FacialController" を含むことだけを assert する。
            LogAssert.Expect(LogType.Warning, DualPathWarningPattern);

            _controllerGameObject = CreateControllerHost();
            _controllerGameObject.AddComponent<TrackingFacialControllerExtension>();
            var controller = _controllerGameObject.AddComponent<FacialController>();
            var so = ScriptableObject.CreateInstance<TestablePhase1ProfileSO>();
            so.WritableAdapterBindings.Add(new TrackingAdapterBinding { Slug = "tracking-2" });

            try
            {
                controller.CharacterSO = so;
                controller.Initialize();
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
        /// 旧 <see cref="IFacialControllerExtension"/> 経路の駆動回数を観測するための Mock。
        /// </summary>
        public sealed class TrackingFacialControllerExtension : MonoBehaviour, IFacialControllerExtension
        {
            public int ConfigureFactoryCount;

            public void ConfigureFactory(
                InputSourceFactory factory,
                FacialProfile profile,
                IReadOnlyList<string> blendShapeNames)
            {
                ConfigureFactoryCount++;
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
