using System;
using System.Collections.Generic;
using System.Reflection;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Editor.Sampling;
using Hidano.FacialControl.Editor.Tools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hidano.FacialControl.Tests.EditMode.Editor.Tools
{
    /// <summary>
    /// ExpressionCreatorWindow の AnimationClip ベイク経路テスト。
    /// ベイクロジックは <see cref="ExpressionClipBakery"/> static helper に抽出済み。
    /// </summary>
    [TestFixture]
    public class ExpressionCreatorWindowTests
    {
        private readonly List<UnityEngine.Object> _trackedObjects = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _trackedObjects.Count; i++)
            {
                if (_trackedObjects[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_trackedObjects[i]);
                }
            }
            _trackedObjects.Clear();
        }

        private AnimationClip CreateTrackedClip()
        {
            var clip = new AnimationClip();
            _trackedObjects.Add(clip);
            return clip;
        }

        [Test]
        public void CreateGUI_DoesNotShowTransitionMetadataFoldout()
        {
            var window = ScriptableObject.CreateInstance<ExpressionCreatorWindow>();
            _trackedObjects.Add(window);

            var createGui = typeof(ExpressionCreatorWindow).GetMethod(
                "CreateGUI",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(createGui);

            createGui.Invoke(window, null);

            var foldouts = window.rootVisualElement.Query<Foldout>().ToList();
            Assert.AreEqual(0, foldouts.Count);
        }

        [Test]
        public void Bake_BlendShapeSliders_WritesEditorCurves()
        {
            var clip = CreateTrackedClip();
            var entries = new List<ExpressionClipBakery.BlendShapeBakeEntry>
            {
                new ExpressionClipBakery.BlendShapeBakeEntry("Body/Face", "Smile", 0.5f),
                new ExpressionClipBakery.BlendShapeBakeEntry("Body/Face", "Anger", 0.25f),
                new ExpressionClipBakery.BlendShapeBakeEntry("Body/Head", "Surprise", 1.0f),
            };

            ExpressionClipBakery.Bake(clip, entries, 0.25f, TransitionCurvePreset.Linear);

            var bindings = AnimationUtility.GetCurveBindings(clip);
            // BlendShape 3 本のみ（メタデータは AnimationEvent 側で運搬）
            Assert.AreEqual(3, bindings.Length);

            var byKey = new Dictionary<string, float>();
            for (int i = 0; i < bindings.Length; i++)
            {
                var b = bindings[i];
                Assert.AreEqual(typeof(SkinnedMeshRenderer), b.type);
                Assert.IsTrue(b.propertyName.StartsWith("blendShape."),
                    $"Unexpected propertyName: {b.propertyName}");
                var curve = AnimationUtility.GetEditorCurve(clip, b);
                byKey[$"{b.path}|{b.propertyName}"] = curve.Evaluate(0f);
            }

            Assert.AreEqual(0.5f, byKey["Body/Face|blendShape.Smile"], 1e-5f);
            Assert.AreEqual(0.25f, byKey["Body/Face|blendShape.Anger"], 1e-5f);
            Assert.AreEqual(1.0f, byKey["Body/Head|blendShape.Surprise"], 1e-5f);
        }

        [Test]
        public void Bake_DoesNotWriteAnimationEvents()
        {
            var clip = CreateTrackedClip();
            var entries = new List<ExpressionClipBakery.BlendShapeBakeEntry>
            {
                new ExpressionClipBakery.BlendShapeBakeEntry("Body/Face", "Smile", 0.5f),
            };

            ExpressionClipBakery.Bake(clip, entries, 0.7f, TransitionCurvePreset.EaseInOut);

            var events = AnimationUtility.GetAnimationEvents(clip);
            Assert.IsNotNull(events);
            Assert.AreEqual(0, events.Length);
        }

        [Test]
        public void LoadExistingClip_RestoresSliderValues()
        {
            var clip = CreateTrackedClip();
            var entries = new List<ExpressionClipBakery.BlendShapeBakeEntry>
            {
                new ExpressionClipBakery.BlendShapeBakeEntry("Body/Face", "Smile", 0.5f),
                new ExpressionClipBakery.BlendShapeBakeEntry("Body/Face", "Anger", 0.25f),
                new ExpressionClipBakery.BlendShapeBakeEntry("Body/Head", "Surprise", 1.0f),
            };
            ExpressionClipBakery.Bake(clip, entries, 0.25f, TransitionCurvePreset.Linear);

            var sampler = new AnimationClipExpressionSampler();
            var loaded = ExpressionClipBakery.LoadBlendShapeValues(clip, sampler);

            Assert.AreEqual(3, loaded.Count);
            Assert.AreEqual(0.5f, loaded[("Body/Face", "Smile")], 1e-5f);
            Assert.AreEqual(0.25f, loaded[("Body/Face", "Anger")], 1e-5f);
            Assert.AreEqual(1.0f, loaded[("Body/Head", "Surprise")], 1e-5f);
        }

        [Test]
        public void Bake_NullClip_Throws()
        {
            var entries = new List<ExpressionClipBakery.BlendShapeBakeEntry>();
            Assert.Throws<ArgumentNullException>(() =>
                ExpressionClipBakery.Bake(null, entries, 0.25f, TransitionCurvePreset.Linear));
        }

        [Test]
        public void Bake_NullEntries_Throws()
        {
            var clip = CreateTrackedClip();
            Assert.Throws<ArgumentNullException>(() =>
                ExpressionClipBakery.Bake(clip, null, 0.25f, TransitionCurvePreset.Linear));
        }

        [Test]
        public void Bake_RebakeOverwritesExistingCurves()
        {
            var clip = CreateTrackedClip();
            // 1 回目のベイク: Smile + Anger
            var first = new List<ExpressionClipBakery.BlendShapeBakeEntry>
            {
                new ExpressionClipBakery.BlendShapeBakeEntry("Body/Face", "Smile", 0.4f),
                new ExpressionClipBakery.BlendShapeBakeEntry("Body/Face", "Anger", 0.8f),
            };
            ExpressionClipBakery.Bake(clip, first, 0.25f, TransitionCurvePreset.Linear);

            // 2 回目のベイク: Surprise のみ。旧 Smile / Anger は削除されるべき
            var second = new List<ExpressionClipBakery.BlendShapeBakeEntry>
            {
                new ExpressionClipBakery.BlendShapeBakeEntry("Body/Head", "Surprise", 1.0f),
            };
            ExpressionClipBakery.Bake(clip, second, 0.25f, TransitionCurvePreset.Linear);

            var bindings = AnimationUtility.GetCurveBindings(clip);
            Assert.AreEqual(1, bindings.Length, "再ベイクで旧 BlendShape カーブが削除されること");
            Assert.AreEqual("blendShape.Surprise", bindings[0].propertyName);
        }
    }
}
