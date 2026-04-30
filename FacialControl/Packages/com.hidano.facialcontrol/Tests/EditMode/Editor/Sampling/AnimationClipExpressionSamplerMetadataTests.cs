using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Editor.Sampling;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hidano.FacialControl.Tests.EditMode.Editor.Sampling
{
    /// <summary>
    /// Phase 2.2: <see cref="AnimationClipExpressionSampler"/> の AnimationEvent 経由
    /// メタデータ抽出（TransitionDuration / TransitionCurvePreset）を検証する。
    /// 予約 functionName: <c>FacialControlMeta_Set</c>、
    /// stringParameter で key 識別（transitionDuration / transitionCurvePreset）、
    /// floatParameter で値運搬（preset enum 整数も float として）。
    /// _Requirements: 2.4, 2.5, 2.6
    /// </summary>
    [TestFixture]
    public class AnimationClipExpressionSamplerMetadataTests
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
            // BlendShape カーブを 1 本入れて clip が空にならないようにする（warning 回避目的ではないがテストの意図を明確にする）
            SetFloatCurve(clip, "Body/Face", typeof(SkinnedMeshRenderer), "blendShape.Smile", 0.5f);
            return clip;
        }

        [Test]
        public void SampleSnapshot_NoMetadata_FallsBackToDefaults()
        {
            var clip = CreateTrackedClip();
            // AnimationEvent を一切設定しない
            var sampler = new AnimationClipExpressionSampler();

            var snapshot = sampler.SampleSnapshot("expr-meta-default", clip);

            Assert.AreEqual(0.25f, snapshot.TransitionDuration);
            Assert.AreEqual(TransitionCurvePreset.Linear, snapshot.TransitionCurvePreset);
        }

        [Test]
        public void SampleSnapshot_DurationEvent_AppliesDuration()
        {
            var clip = CreateTrackedClip();
            SetMetaEvents(clip, new[]
            {
                CreateMetaEvent("transitionDuration", 0.5f),
            });

            var sampler = new AnimationClipExpressionSampler();
            var snapshot = sampler.SampleSnapshot("expr-meta-duration", clip);

            Assert.AreEqual(0.5f, snapshot.TransitionDuration, 1e-5f);
            // CurvePreset は不在なので Linear のまま
            Assert.AreEqual(TransitionCurvePreset.Linear, snapshot.TransitionCurvePreset);
        }

        [Test]
        public void SampleSnapshot_CurvePresetEvent_AppliesPreset()
        {
            var clip = CreateTrackedClip();
            SetMetaEvents(clip, new[]
            {
                CreateMetaEvent("transitionCurvePreset", (float)(int)TransitionCurvePreset.EaseInOut),
            });

            var sampler = new AnimationClipExpressionSampler();
            var snapshot = sampler.SampleSnapshot("expr-meta-curve", clip);

            Assert.AreEqual(TransitionCurvePreset.EaseInOut, snapshot.TransitionCurvePreset);
            // Duration は不在なので 0.25 のまま
            Assert.AreEqual(0.25f, snapshot.TransitionDuration);
        }

        [Test]
        public void SampleSnapshot_DuplicateKey_LogsWarningAndUsesFirst()
        {
            var clip = CreateTrackedClip();
            SetMetaEvents(clip, new[]
            {
                CreateMetaEvent("transitionDuration", 0.4f),
                CreateMetaEvent("transitionDuration", 0.7f),
            });

            var sampler = new AnimationClipExpressionSampler();

            LogAssert.Expect(LogType.Warning, new Regex("Duplicate"));
            var snapshot = sampler.SampleSnapshot("expr-meta-dup", clip);

            // 最初の値のみ採用
            Assert.AreEqual(0.4f, snapshot.TransitionDuration, 1e-5f);
        }

        [Test]
        public void MetaSetFunctionName_IsExposedAsConstant()
        {
            // 予約 functionName を public 定数として外部から参照可能にする（Refactor 完了基準）
            Assert.AreEqual("FacialControlMeta_Set", AnimationClipExpressionSampler.MetaSetFunctionName);
        }

        private static AnimationEvent CreateMetaEvent(string key, float value)
        {
            return new AnimationEvent
            {
                time = 0f,
                functionName = AnimationClipExpressionSampler.MetaSetFunctionName,
                stringParameter = key,
                floatParameter = value,
            };
        }

        private static void SetMetaEvents(AnimationClip clip, AnimationEvent[] events)
        {
            AnimationUtility.SetAnimationEvents(clip, events);
        }

        private static void SetFloatCurve(AnimationClip clip, string path, Type type, string propertyName, float value)
        {
            var binding = new EditorCurveBinding
            {
                path = path,
                type = type,
                propertyName = propertyName,
            };
            var curve = AnimationCurve.Constant(0f, 1f, value);
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }
    }
}
