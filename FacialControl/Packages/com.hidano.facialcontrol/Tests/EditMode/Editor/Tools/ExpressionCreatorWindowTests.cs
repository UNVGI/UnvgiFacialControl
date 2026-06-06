using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Editor.Sampling;
using Hidano.FacialControl.Editor.Tools;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
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
        private readonly List<string> _trackedFiles = new List<string>();
        private readonly List<string> _trackedAssetPaths = new List<string>();

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

            for (int i = 0; i < _trackedFiles.Count; i++)
            {
                if (File.Exists(_trackedFiles[i]))
                    File.Delete(_trackedFiles[i]);
            }
            _trackedFiles.Clear();

            for (int i = 0; i < _trackedAssetPaths.Count; i++)
            {
                AssetDatabase.DeleteAsset(_trackedAssetPaths[i]);
            }
            _trackedAssetPaths.Clear();
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
        public void BakeButton_HasFlexShrinkZeroAndMinWidth()
        {
            var window = ScriptableObject.CreateInstance<ExpressionCreatorWindow>();
            _trackedObjects.Add(window);

            var createGui = typeof(ExpressionCreatorWindow).GetMethod(
                "CreateGUI",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(createGui);

            createGui.Invoke(window, null);

            var bakeButton = window.rootVisualElement.Q<Button>("expression-creator-bake-button");
            Assert.IsNotNull(bakeButton);
            Assert.AreEqual(0f, bakeButton.style.flexShrink.value, 1e-5f);
            Assert.AreEqual(140f, bakeButton.style.minWidth.value.value, 1e-5f);
        }

        [Test]
        public void CreateGUI_AddsSavePreviewPngButtonToLeftPanel()
        {
            var window = ScriptableObject.CreateInstance<ExpressionCreatorWindow>();
            _trackedObjects.Add(window);

            InvokeCreateGUI(window);

            var leftPanel = window.rootVisualElement.Q<VisualElement>("expression-creator-left-panel");
            Assert.IsNotNull(leftPanel);

            var saveButton = leftPanel.Q<Button>("expression-creator-save-preview-png-button");
            Assert.IsNotNull(saveButton);
            Assert.AreEqual("プレビューを PNG として保存", saveButton.text);
            Assert.IsNotNull(saveButton.clickable);
        }

        [Test]
        public void CreateGUI_AddsCreateNewClipButtonNextToClipField()
        {
            var window = ScriptableObject.CreateInstance<ExpressionCreatorWindow>();
            _trackedObjects.Add(window);

            InvokeCreateGUI(window);

            var clipField = (ObjectField)GetPrivateField(window, "_clipField");
            Assert.IsNotNull(clipField);

            var parent = clipField.parent;
            Assert.IsNotNull(parent);

            var createButton = parent.Q<Button>("expression-creator-create-new-clip-button");
            Assert.IsNotNull(createButton);
            Assert.AreEqual("新規作成", createButton.text);
            Assert.IsNotNull(createButton.clickable);
        }

        [Test]
        public void SavePreviewPngHandler_WithSpecifiedPath_WritesPngFile()
        {
            var window = ScriptableObject.CreateInstance<ExpressionCreatorWindow>();
            _trackedObjects.Add(window);
            InvokeCreateGUI(window);

            var outputPath = Path.Combine(Path.GetTempPath(), $"expression-preview-{Guid.NewGuid():N}.png");
            _trackedFiles.Add(outputPath);
            var capturedWidth = 0;
            var capturedHeight = 0;

            SetPrivateField(window, "_savePreviewPathProvider", (Func<string>)(() => outputPath));
            SetPrivateField(window, "_previewTextureCapture", (Func<int, int, Texture2D>)((width, height) =>
            {
                capturedWidth = width;
                capturedHeight = height;
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.SetPixels(new[] { Color.red, Color.green, Color.blue, Color.white });
                texture.Apply();
                return texture;
            }));
            SetPrivateField(window, "_pngFileWriter", (Action<string, byte[]>)File.WriteAllBytes);

            var handler = typeof(ExpressionCreatorWindow).GetMethod(
                "OnSavePreviewClicked",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(handler);

            handler.Invoke(window, null);

            Assert.AreEqual(256, capturedWidth);
            Assert.AreEqual(256, capturedHeight);
            Assert.IsTrue(File.Exists(outputPath));

            var bytes = File.ReadAllBytes(outputPath);
            Assert.Greater(bytes.Length, 8);
            Assert.AreEqual((byte)0x89, bytes[0]);
            Assert.AreEqual((byte)'P', bytes[1]);
            Assert.AreEqual((byte)'N', bytes[2]);
            Assert.AreEqual((byte)'G', bytes[3]);
        }

        [Test]
        public void CreateNewClipHandler_WithSpecifiedPath_CreatesAndAssignsClip()
        {
            var window = ScriptableObject.CreateInstance<ExpressionCreatorWindow>();
            _trackedObjects.Add(window);
            InvokeCreateGUI(window);

            var assetPath = $"Assets/expression-creator-created-{Guid.NewGuid():N}.anim";
            _trackedAssetPaths.Add(assetPath);
            SetPrivateField(window, "_createClipPathProvider", (Func<string>)(() => assetPath));

            InvokePrivateMethod(window, "OnCreateNewClipClicked");

            var createdClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
            Assert.IsNotNull(createdClip);

            var clipField = (ObjectField)GetPrivateField(window, "_clipField");
            Assert.AreSame(createdClip, clipField.value);
            Assert.AreSame(createdClip, GetPrivateField(window, "_targetClip"));
        }

        [Test]
        public void CreateNewClipHandler_DialogCancelled_DoesNotCreateOrChangeClip()
        {
            var window = ScriptableObject.CreateInstance<ExpressionCreatorWindow>();
            _trackedObjects.Add(window);
            InvokeCreateGUI(window);

            var existingClip = CreateTrackedClip();
            var clipField = (ObjectField)GetPrivateField(window, "_clipField");
            clipField.value = existingClip;
            SetPrivateField(window, "_targetClip", existingClip);

            var createAssetCalled = false;
            SetPrivateField(window, "_createClipPathProvider", (Func<string>)(() => string.Empty));
            SetPrivateField(window, "_clipAssetCreator", (Action<AnimationClip, string>)((_, _) => createAssetCalled = true));

            InvokePrivateMethod(window, "OnCreateNewClipClicked");

            Assert.IsFalse(createAssetCalled);
            Assert.AreSame(existingClip, clipField.value);
            Assert.AreSame(existingClip, GetPrivateField(window, "_targetClip"));
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

            // 入力エントリは正規化 0..1。AnimationClip カーブは Unity 標準 0..100 スケールで書き込まれる。
            Assert.AreEqual(50f, byKey["Body/Face|blendShape.Smile"], 1e-5f);
            Assert.AreEqual(25f, byKey["Body/Face|blendShape.Anger"], 1e-5f);
            Assert.AreEqual(100f, byKey["Body/Head|blendShape.Surprise"], 1e-5f);
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

        private static void InvokeCreateGUI(ExpressionCreatorWindow window)
        {
            var createGui = typeof(ExpressionCreatorWindow).GetMethod(
                "CreateGUI",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(createGui);

            createGui.Invoke(window, null);
        }

        private static void SetPrivateField(ExpressionCreatorWindow window, string fieldName, object value)
        {
            var field = typeof(ExpressionCreatorWindow).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);

            field.SetValue(window, value);
        }

        private static object GetPrivateField(ExpressionCreatorWindow window, string fieldName)
        {
            var field = typeof(ExpressionCreatorWindow).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);

            return field.GetValue(window);
        }

        private static void InvokePrivateMethod(ExpressionCreatorWindow window, string methodName)
        {
            var method = typeof(ExpressionCreatorWindow).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);

            method.Invoke(window, null);
        }
    }
}
