using System.Collections.Generic;
using System.Reflection;
using Hidano.FacialControl.Adapters.Json.Dto;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Editor.Inspector;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Hidano.FacialControl.Tests.EditMode.Editor.Inspector
{
    [TestFixture]
    public class OverlaysTabUITests
    {
        private const string EmotionLayerName = "Emotion";
        private const string SecondaryLayerName = "Detail";
        private const string BlinkSlotName = "blink";
        private const string WinkSlotName = "wink";

        private FacialCharacterProfileSO _so;
        private UnityEditor.Editor _editor;
        private readonly List<AnimationClip> _clips = new List<AnimationClip>();

        [TearDown]
        public void TearDown()
        {
            if (_editor != null)
            {
                Object.DestroyImmediate(_editor);
                _editor = null;
            }

            for (int i = 0; i < _clips.Count; i++)
            {
                if (_clips[i] != null)
                {
                    Object.DestroyImmediate(_clips[i]);
                }
            }
            _clips.Clear();

            if (_so != null)
            {
                Object.DestroyImmediate(_so);
                _so = null;
            }
        }

        [Test]
        public void CreateInspectorGUI_OverlaysStateRadioInitialValue_MatchesSerializableGetState()
        {
            _so = CreateProfileWithSlots(BlinkSlotName);
            var binding = new OverlaySlotBindingSerializable
            {
                slot = BlinkSlotName,
                suppress = true,
            };
            _so.Expressions.Add(CreateExpression(binding));

            var root = BuildInspectorRoot();
            AssertSixTabAndBuilderContracts(root);

            var radio = root.Q<RadioButtonGroup>(FacialCharacterProfileSOInspector.ExpressionOverlayStateRadioName);
            Assert.That(radio, Is.Not.Null, "Expression row の Overlays 3 状態ラジオが見つかりません。");
            Assert.That(radio.choices, Is.EqualTo(new[] { "Default", "Suppress", "Override" }));
            Assert.That(radio.value, Is.EqualTo(ToRadioIndex(binding.GetState())));
        }

        [Test]
        public void CreateInspectorGUI_ExpressionRowLayerDropdown_UsesLayerChoicesAndUpdatesExpressionLayer()
        {
            _so = CreateProfileWithSlots(BlinkSlotName);
            _so.Layers.Add(new LayerDefinitionSerializable
            {
                name = SecondaryLayerName,
                priority = 1,
            });
            _so.Expressions.Add(CreateExpression(new OverlaySlotBindingSerializable { slot = BlinkSlotName }));

            var root = BuildInspectorRoot();
            var dropdown = root.Q<DropdownField>(FacialCharacterProfileSOInspector.ExpressionRowLayerDropdownName);

            Assert.That(dropdown, Is.Not.Null, "Expression row の Layer DropdownField が見つかりません。");
            Assert.That(dropdown.choices, Is.EqualTo(new[] { EmotionLayerName, SecondaryLayerName }));
            Assert.That(dropdown.value, Is.EqualTo(EmotionLayerName));

            dropdown.value = SecondaryLayerName;

            Assert.That(_so.Expressions[0].layer, Is.EqualTo(SecondaryLayerName));
        }

        [Test]
        public void CreateInspectorGUI_LayersTab_DoesNotRenderExpressionRows()
        {
            _so = CreateProfileWithSlots(BlinkSlotName);
            _so.Expressions.Add(CreateExpression(new OverlaySlotBindingSerializable { slot = BlinkSlotName }));

            var root = BuildInspectorRoot();
            var layersTab = root.Q<VisualElement>(FacialCharacterProfileSOInspector.TabLayersName);

            Assert.That(layersTab, Is.Not.Null, "Layers tab was not found.");
            Assert.That(
                layersTab.Q<TextField>(FacialCharacterProfileSOInspector.ExpressionRowNameFieldName),
                Is.Null,
                "Layers tab must not render Expression row name fields.");
            Assert.That(
                layersTab.Q<DropdownField>(FacialCharacterProfileSOInspector.ExpressionRowLayerDropdownName),
                Is.Null,
                "Layers tab must not render Expression row layer dropdowns.");
            Assert.That(
                layersTab.Q<RadioButtonGroup>(FacialCharacterProfileSOInspector.ExpressionOverlayStateRadioName),
                Is.Null,
                "Layers tab must not render Expression overlay controls.");
        }

        [Test]
        public void CreateInspectorGUI_OverrideSelected_ShowsAnimationClipFieldOnlyForOverride()
        {
            _so = CreateProfileWithSlots(BlinkSlotName);
            _so.Expressions.Add(CreateExpression(new OverlaySlotBindingSerializable { slot = BlinkSlotName }));

            var root = BuildInspectorRoot();
            var radio = root.Q<RadioButtonGroup>(FacialCharacterProfileSOInspector.ExpressionOverlayStateRadioName);
            var clipField = root.Q<ObjectField>(FacialCharacterProfileSOInspector.ExpressionOverlayAnimationClipFieldName);
            Assert.That(radio, Is.Not.Null);
            Assert.That(clipField, Is.Not.Null);
            Assert.That(clipField.objectType, Is.EqualTo(typeof(AnimationClip)));

            radio.value = ToRadioIndex(OverlaySlotBindingState.DefaultFallback);
            Assert.That(clipField.style.display.value, Is.EqualTo(DisplayStyle.None));

            radio.value = ToRadioIndex(OverlaySlotBindingState.Override);
            Assert.That(clipField.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            radio.value = ToRadioIndex(OverlaySlotBindingState.Suppress);
            Assert.That(clipField.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void OverlayStateRadioSelection_UpdatesSerializableFieldsAndClearsOverrideData()
        {
            _so = CreateProfileWithSlots(BlinkSlotName);
            var clip = CreateClip("OverlayStateRadioSelection_OverrideClip");
            var binding = new OverlaySlotBindingSerializable
            {
                slot = BlinkSlotName,
                animationClip = clip,
                cachedSnapshot = CreateCachedSnapshot(),
            };
            _so.Expressions.Add(CreateExpression(binding));

            var root = BuildInspectorRoot();
            var radio = root.Q<RadioButtonGroup>(FacialCharacterProfileSOInspector.ExpressionOverlayStateRadioName);
            Assert.That(radio, Is.Not.Null);

            radio.value = ToRadioIndex(OverlaySlotBindingState.Suppress);
            Assert.That(binding.suppress, Is.True);
            Assert.That(binding.animationClip, Is.Null);
            Assert.That(IsEmptySnapshot(binding.cachedSnapshot), Is.True);

            binding.animationClip = clip;
            binding.cachedSnapshot = CreateCachedSnapshot();

            radio.value = ToRadioIndex(OverlaySlotBindingState.DefaultFallback);
            Assert.That(binding.suppress, Is.False);
            Assert.That(binding.animationClip, Is.Null);
            Assert.That(IsEmptySnapshot(binding.cachedSnapshot), Is.True);

            radio.value = ToRadioIndex(OverlaySlotBindingState.Override);
            Assert.That(binding.suppress, Is.False);
        }

        [Test]
        public void CreateInspectorGUI_UndeclaredSlotReference_RendersWarningHelpBoxOnOverlayRow()
        {
            _so = CreateProfileWithSlots();
            _so.Expressions.Add(CreateExpression(new OverlaySlotBindingSerializable { slot = BlinkSlotName }));

            var root = BuildInspectorRoot();
            var help = root.Q<HelpBox>(FacialCharacterProfileSOInspector.ExpressionOverlayUndeclaredSlotHelpName);

            Assert.That(help, Is.Not.Null, "未宣言 slot を参照する overlay row の警告 HelpBox が見つかりません。");
            Assert.That(help.messageType, Is.EqualTo(HelpBoxMessageType.Warning));
            StringAssert.Contains(BlinkSlotName, help.text);
        }

        [Test]
        public void SlotsPropertyChanged_RebuildsDefaultOverlayDropdownChoices()
        {
            _so = CreateProfileWithSlots(BlinkSlotName);
            _so.DefaultOverlays.Add(new OverlaySlotBindingSerializable { slot = BlinkSlotName });

            var root = BuildInspectorRoot();
            var dropdown = root.Q<DropdownField>(FacialCharacterProfileSOInspector.DefaultOverlaySlotDropdownName);
            Assert.That(dropdown, Is.Not.Null, "Default Overlays の slot DropdownField が見つかりません。");
            Assert.That(dropdown.choices, Is.EqualTo(new[] { BlinkSlotName }));

            SetSlots(_so, BlinkSlotName, WinkSlotName);
            root = RebuildInspectorRoot();
            dropdown = root.Q<DropdownField>(FacialCharacterProfileSOInspector.DefaultOverlaySlotDropdownName);

            Assert.That(dropdown, Is.Not.Null);
            Assert.That(dropdown.choices, Is.EqualTo(new[] { BlinkSlotName, WinkSlotName }));
        }

        [Test]
        public void CreateInspectorGUI_DefaultOverlayRow_RendersSlotAndAlwaysVisibleClipFieldWithoutStateRadio()
        {
            _so = CreateProfileWithSlots(BlinkSlotName);
            var binding = new OverlaySlotBindingSerializable
            {
                slot = BlinkSlotName,
                suppress = true,
                cachedSnapshot = CreateCachedSnapshot(),
            };
            _so.DefaultOverlays.Add(binding);

            var root = BuildInspectorRoot();
            var foldout = root.Q<VisualElement>(FacialCharacterProfileSOInspector.DefaultOverlaysFoldoutName);
            var dropdown = foldout.Q<DropdownField>(FacialCharacterProfileSOInspector.DefaultOverlaySlotDropdownName);
            var radio = foldout.Q<RadioButtonGroup>();
            var clipField = foldout.Q<ObjectField>(FacialCharacterProfileSOInspector.DefaultOverlayAnimationClipFieldName);

            Assert.That(dropdown, Is.Not.Null, "Default Overlays row の slot DropdownField が見つかりません。");
            Assert.That(radio, Is.Null, "Default Overlays row に 3 状態ラジオを表示してはいけません。");
            Assert.That(clipField, Is.Not.Null, "Default Overlays row の AnimationClip フィールドが見つかりません。");
            Assert.That(clipField.objectType, Is.EqualTo(typeof(AnimationClip)));
            Assert.That(clipField.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(binding.suppress, Is.False);
            Assert.That(IsEmptySnapshot(binding.cachedSnapshot), Is.True);
        }

        [Test]
        public void DefaultOverlayAnimationClipFieldSelection_NormalizesSuppressAndClearsSnapshotWhenUnset()
        {
            _so = CreateProfileWithSlots(BlinkSlotName);
            var initialClip = CreateClip("DefaultOverlayAnimationClipFieldSelection_InitialClip");
            var binding = new OverlaySlotBindingSerializable
            {
                slot = BlinkSlotName,
                suppress = true,
                animationClip = initialClip,
                cachedSnapshot = CreateCachedSnapshot(),
            };
            _so.DefaultOverlays.Add(binding);

            var root = BuildInspectorRoot();
            var clipField = root.Q<ObjectField>(FacialCharacterProfileSOInspector.DefaultOverlayAnimationClipFieldName);
            Assert.That(clipField, Is.Not.Null);
            Assert.That(clipField.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(binding.suppress, Is.False);
            Assert.That(binding.animationClip, Is.SameAs(initialClip));

            var replacementClip = CreateClip("DefaultOverlayAnimationClipFieldSelection_ReplacementClip");
            clipField.value = replacementClip;
            Assert.That(binding.suppress, Is.False);
            Assert.That(binding.animationClip, Is.SameAs(replacementClip));
            Assert.That(clipField.style.display.value, Is.EqualTo(DisplayStyle.Flex));

            binding.cachedSnapshot = CreateCachedSnapshot();
            clipField.value = null;
            Assert.That(binding.suppress, Is.False);
            Assert.That(binding.animationClip, Is.Null);
            Assert.That(IsEmptySnapshot(binding.cachedSnapshot), Is.True);
            Assert.That(clipField.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void CreateInspectorGUI_DefaultOverlayUndeclaredSlot_RendersWarningHelpBoxOnRow()
        {
            _so = CreateProfileWithSlots();
            _so.DefaultOverlays.Add(new OverlaySlotBindingSerializable { slot = BlinkSlotName });

            var root = BuildInspectorRoot();
            var help = root.Q<HelpBox>(FacialCharacterProfileSOInspector.DefaultOverlayUndeclaredSlotHelpName);

            Assert.That(help, Is.Not.Null, "未宣言 slot を参照する Default Overlay row の警告 HelpBox が見つかりません。");
            Assert.That(help.messageType, Is.EqualTo(HelpBoxMessageType.Warning));
            StringAssert.Contains(BlinkSlotName, help.text);
        }

        private VisualElement BuildInspectorRoot()
        {
            _editor = UnityEditor.Editor.CreateEditor(_so, typeof(FacialCharacterProfileSOInspector));
            Assert.That(_editor, Is.Not.Null);
            return _editor.CreateInspectorGUI();
        }

        private VisualElement RebuildInspectorRoot()
        {
            if (_editor != null)
            {
                Object.DestroyImmediate(_editor);
                _editor = null;
            }

            return BuildInspectorRoot();
        }

        private static void AssertSixTabAndBuilderContracts(VisualElement root)
        {
            Assert.That(root.Q<VisualElement>(FacialCharacterProfileSOInspector.TabExpressionLibraryName), Is.Not.Null);
            Assert.That(root.Q<VisualElement>(FacialCharacterProfileSOInspector.TabLayersName), Is.Not.Null);
            Assert.That(root.Q<VisualElement>(FacialCharacterProfileSOInspector.TabBaseExpressionName), Is.Not.Null);
            Assert.That(root.Q<VisualElement>(FacialCharacterProfileSOInspector.TabGazeName), Is.Not.Null);
            Assert.That(root.Q<VisualElement>(FacialCharacterProfileSOInspector.TabAdapterBindingsName), Is.Not.Null);
            Assert.That(root.Q<VisualElement>(FacialCharacterProfileSOInspector.TabDebugName), Is.Not.Null);

            AssertPrivateBuilderExists("BuildSlotsDeclarationSection", typeof(VisualElement));
            AssertPrivateBuilderExists("BuildDefaultOverlaysSection", typeof(VisualElement));
            AssertPrivateBuilderExists("BuildOverlaysSectionForExpression", typeof(SerializedProperty), typeof(int));

            Assert.That(
                root.Q<VisualElement>(FacialCharacterProfileSOInspector.SlotsDeclarationFoldoutName),
                Is.Not.Null,
                "表情ライブラリタブに Slots 宣言セクションが必要です。");
            Assert.That(
                root.Q<VisualElement>(FacialCharacterProfileSOInspector.DefaultOverlaysFoldoutName),
                Is.Not.Null,
                "表情ライブラリタブに Default Overlays セクションが必要です。");
            Assert.That(
                root.Q<VisualElement>(FacialCharacterProfileSOInspector.ExpressionOverlaysSectionName),
                Is.Not.Null,
                "Expression row に Overlays セクションが必要です。");
        }

        private static void AssertPrivateBuilderExists(string methodName, params System.Type[] parameterTypes)
        {
            var method = typeof(FacialCharacterProfileSOInspector).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: parameterTypes,
                modifiers: null);

            Assert.That(method, Is.Not.Null, $"{methodName} が FacialCharacterProfileSOInspector に未実装です。");
        }

        private static FacialCharacterProfileSO CreateProfileWithSlots(params string[] slots)
        {
            var so = ScriptableObject.CreateInstance<FacialCharacterProfileSO>();
            so.name = "OverlaysTabUITestProfile";
            so.Layers.Add(new LayerDefinitionSerializable
            {
                name = EmotionLayerName,
                priority = 0,
            });
            SetSlots(so, slots);
            return so;
        }

        private static ExpressionSerializable CreateExpression(OverlaySlotBindingSerializable binding)
        {
            return new ExpressionSerializable
            {
                id = "smile",
                name = "Smile",
                layer = EmotionLayerName,
                isGaze = false,
                overlays = new List<OverlaySlotBindingSerializable> { binding },
            };
        }

        private AnimationClip CreateClip(string name)
        {
            var clip = new AnimationClip { name = name };
            _clips.Add(clip);
            return clip;
        }

        private static ExpressionSnapshotDto CreateCachedSnapshot()
        {
            return new ExpressionSnapshotDto
            {
                blendShapes = new List<BlendShapeSnapshotDto>
                {
                    new BlendShapeSnapshotDto
                    {
                        rendererPath = "Body",
                        name = "Blink_L",
                        value = 1f,
                    },
                },
                bones = new List<BoneSnapshotDto>(),
                rendererPaths = new List<string> { "Body" },
            };
        }

        private static bool IsEmptySnapshot(ExpressionSnapshotDto snapshot)
        {
            if (snapshot == null) return true;
            bool hasBlendShapes = snapshot.blendShapes != null && snapshot.blendShapes.Count > 0;
            bool hasBones = snapshot.bones != null && snapshot.bones.Count > 0;
            bool hasRendererPaths = snapshot.rendererPaths != null && snapshot.rendererPaths.Count > 0;
            bool hasOverlays = snapshot.overlays != null && snapshot.overlays.Count > 0;
            return !hasBlendShapes && !hasBones && !hasRendererPaths && !hasOverlays;
        }

        private static int ToRadioIndex(OverlaySlotBindingState state)
        {
            switch (state)
            {
                case OverlaySlotBindingState.DefaultFallback:
                    return 0;
                case OverlaySlotBindingState.Suppress:
                    return 1;
                case OverlaySlotBindingState.Override:
                    return 2;
                default:
                    Assert.Fail($"未知の OverlaySlotBindingState です: {state}");
                    return -1;
            }
        }

        private static void SetSlots(FacialCharacterProfileSO so, params string[] slots)
        {
            var serialized = new SerializedObject(so);
            serialized.Update();
            var slotsProperty = serialized.FindProperty("_slots");
            Assert.That(slotsProperty, Is.Not.Null, "_slots SerializedProperty が見つかりません。");
            slotsProperty.ClearArray();

            for (int i = 0; i < slots.Length; i++)
            {
                slotsProperty.InsertArrayElementAtIndex(i);
                slotsProperty.GetArrayElementAtIndex(i).stringValue = slots[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
