using System.Collections.Generic;
using System.Reflection;
using Hidano.FacialControl.LipSync.Adapters.PhonemeEntries;
using Hidano.FacialControl.LipSync.Editor.Inspector;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hidano.FacialControl.LipSync.Tests.EditMode.Editor
{
    public class PhonemeEntryListViewTests
    {
        private PhonemeEntryListViewTestAsset _asset;
        private SerializedObject _serializedObject;
        private SerializedProperty _entriesProperty;

        [SetUp]
        public void SetUp()
        {
            Undo.ClearAll();

            _asset = ScriptableObject.CreateInstance<PhonemeEntryListViewTestAsset>();
            _serializedObject = new SerializedObject(_asset);
            _entriesProperty =
                _serializedObject.FindProperty(nameof(PhonemeEntryListViewTestAsset.Entries));
        }

        [TearDown]
        public void TearDown()
        {
            _serializedObject.Dispose();
            Object.DestroyImmediate(_asset);
            Undo.ClearAll();
        }

        [Test]
        public void Create_ArrayProperty_BuildsReorderableListWithStandardFooter()
        {
            var view = CreateView();
            var listView = view.Q<ListView>(PhonemeEntryListView.ListViewName);

            Assert.That(listView, Is.Not.Null);
            Assert.That(listView.reorderable, Is.True);
            Assert.That(listView.showAddRemoveFooter, Is.True);
        }

        [Test]
        public void AddEntry_BlendShapeAndAnimationClip_AppendsConcreteManagedReferences()
        {
            var view = CreateView();

            view.AddEntry(PhonemeEntryListView.EntryKind.BlendShape);
            view.AddEntry(PhonemeEntryListView.EntryKind.AnimationClip);

            _serializedObject.Update();
            Assert.That(_entriesProperty.arraySize, Is.EqualTo(2));
            Assert.That(
                _entriesProperty.GetArrayElementAtIndex(0).managedReferenceValue,
                Is.InstanceOf<BlendShapePhonemeEntry>());
            Assert.That(
                _entriesProperty.GetArrayElementAtIndex(1).managedReferenceValue,
                Is.InstanceOf<AnimationClipPhonemeEntry>());
        }

        [Test]
        public void SetEntryKind_FromBlendShapeToAnimationClip_PreservesCommonFields()
        {
            var view = CreateView();
            view.AddEntry(PhonemeEntryListView.EntryKind.BlendShape);

            _serializedObject.Update();
            SerializedProperty entry = _entriesProperty.GetArrayElementAtIndex(0);
            entry.FindPropertyRelative(nameof(PhonemeEntryBase.PhonemeId)).stringValue = "A";
            entry.FindPropertyRelative(nameof(PhonemeEntryBase.MaxWeight)).floatValue = 80f;
            entry.FindPropertyRelative(nameof(BlendShapePhonemeEntry.BlendShapeName)).stringValue = "Mouth_A";
            _serializedObject.ApplyModifiedProperties();

            view.SetEntryKind(0, PhonemeEntryListView.EntryKind.AnimationClip);

            _serializedObject.Update();
            entry = _entriesProperty.GetArrayElementAtIndex(0);
            Assert.That(entry.managedReferenceValue, Is.InstanceOf<AnimationClipPhonemeEntry>());
            Assert.That(
                entry.FindPropertyRelative(nameof(PhonemeEntryBase.PhonemeId)).stringValue,
                Is.EqualTo("A"));
            Assert.That(
                entry.FindPropertyRelative(nameof(PhonemeEntryBase.MaxWeight)).floatValue,
                Is.EqualTo(80f).Within(1e-6f));
        }

        [Test]
        public void MoveEntry_ReordersSerializedArray()
        {
            var view = CreateView();
            view.AddEntry(PhonemeEntryListView.EntryKind.BlendShape);
            view.AddEntry(PhonemeEntryListView.EntryKind.AnimationClip);

            _serializedObject.Update();
            SetPhonemeId(0, "A");
            SetPhonemeId(1, "I");
            _serializedObject.ApplyModifiedProperties();

            view.MoveEntry(0, 1);

            _serializedObject.Update();
            Assert.That(GetPhonemeId(0), Is.EqualTo("I"));
            Assert.That(GetPhonemeId(1), Is.EqualTo("A"));
        }

        [Test]
        public void RemoveEntryAt_RemovesElement()
        {
            var view = CreateView();
            view.AddEntry(PhonemeEntryListView.EntryKind.BlendShape);
            view.AddEntry(PhonemeEntryListView.EntryKind.AnimationClip);

            view.RemoveEntryAt(0);

            _serializedObject.Update();
            Assert.That(_entriesProperty.arraySize, Is.EqualTo(1));
            Assert.That(
                _entriesProperty.GetArrayElementAtIndex(0).managedReferenceValue,
                Is.InstanceOf<AnimationClipPhonemeEntry>());
        }

        [Test]
        public void BlendShapeRow_EmptyBlendShapeName_ShowsWarningHelpBox()
        {
            _asset.Entries.Add(new BlendShapePhonemeEntry
            {
                PhonemeId = "A",
                BlendShapeName = string.Empty,
                MaxWeight = 100f,
            });
            _serializedObject.Update();
            var view = CreateView();

            VisualElement row = view.CreateBoundRowForIndex(0);
            var selector = row.Q<DropdownField>(PhonemeEntryListView.EntryTypeSelectorName);
            var warning = row.Q<HelpBox>(PhonemeEntryListView.BlendShapeWarningName);

            Assert.That(selector, Is.Not.Null);
            Assert.That(selector.value, Is.EqualTo(PhonemeEntryListView.BlendShapeLabel));
            Assert.That(warning, Is.Not.Null);
            Assert.That(warning.style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void AnimationClipRow_RendersClipFieldAndNoBlendShapeWarning()
        {
            _asset.Entries.Add(new AnimationClipPhonemeEntry
            {
                PhonemeId = "O",
                MaxWeight = 100f,
            });
            _serializedObject.Update();
            var view = CreateView();

            VisualElement row = view.CreateBoundRowForIndex(0);
            var selector = row.Q<DropdownField>(PhonemeEntryListView.EntryTypeSelectorName);

            Assert.That(selector, Is.Not.Null);
            Assert.That(selector.value, Is.EqualTo(PhonemeEntryListView.AnimationClipLabel));
            Assert.That(row.Q<ObjectField>(), Is.Not.Null);
            Assert.That(row.Q<HelpBox>(PhonemeEntryListView.BlendShapeWarningName), Is.Null);
        }

        [Test]
        public void UndoAfterSetEntryKind_DisplaysSerializedPropertyCurrentValue()
        {
            _asset.Entries.Add(new BlendShapePhonemeEntry
            {
                PhonemeId = "A",
                BlendShapeName = "Mouth_A",
                MaxWeight = 100f,
            });
            _serializedObject.Update();
            var view = CreateView();

            Undo.RecordObject(_asset, "Change Phoneme Entry Kind");
            view.SetEntryKind(0, PhonemeEntryListView.EntryKind.AnimationClip);
            Undo.FlushUndoRecordObjects();

            Assert.That(GetBoundEntryTypeSelectorValue(view), Is.EqualTo(PhonemeEntryListView.AnimationClipLabel));

            Undo.PerformUndo();
            InvokeUndoRedoPerformed(view);

            _serializedObject.Update();
            SerializedProperty entry = _entriesProperty.GetArrayElementAtIndex(0);
            Assert.That(entry.managedReferenceValue, Is.InstanceOf<BlendShapePhonemeEntry>());
            Assert.That(
                GetBoundEntryTypeSelectorValue(view),
                Is.EqualTo(PhonemeEntryListView.BlendShapeLabel));
        }

        [Test]
        public void UndoRedoPerformed_WhenFiredRepeatedly_RebuildsWithoutStaleDisplay()
        {
            _asset.Entries.Add(new BlendShapePhonemeEntry
            {
                PhonemeId = "I",
                BlendShapeName = "Mouth_I",
                MaxWeight = 90f,
            });
            _serializedObject.Update();
            var view = CreateView();

            Undo.RecordObject(_asset, "Change Phoneme Entry Kind To AnimationClip");
            view.SetEntryKind(0, PhonemeEntryListView.EntryKind.AnimationClip);
            Undo.FlushUndoRecordObjects();

            Undo.RecordObject(_asset, "Change Phoneme Entry Kind To BlendShape");
            view.SetEntryKind(0, PhonemeEntryListView.EntryKind.BlendShape);
            Undo.FlushUndoRecordObjects();

            Undo.PerformUndo();
            Assert.DoesNotThrow(() => InvokeUndoRedoPerformed(view));
            Assert.That(
                GetBoundEntryTypeSelectorValue(view),
                Is.EqualTo(PhonemeEntryListView.AnimationClipLabel));

            Undo.PerformUndo();
            Assert.DoesNotThrow(() =>
            {
                InvokeUndoRedoPerformed(view);
                InvokeUndoRedoPerformed(view);
            });
            Assert.That(
                GetBoundEntryTypeSelectorValue(view),
                Is.EqualTo(PhonemeEntryListView.BlendShapeLabel));
        }

        private PhonemeEntryListView CreateView()
        {
            _serializedObject.Update();
            _entriesProperty =
                _serializedObject.FindProperty(nameof(PhonemeEntryListViewTestAsset.Entries));
            return new PhonemeEntryListView(_entriesProperty);
        }

        private static void InvokeUndoRedoPerformed(PhonemeEntryListView view)
        {
            MethodInfo method = typeof(PhonemeEntryListView).GetMethod(
                "OnUndoRedoPerformed",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(view, null);
        }

        private static string GetBoundEntryTypeSelectorValue(PhonemeEntryListView view)
        {
            VisualElement row = view.CreateBoundRowForIndex(0);
            var selector = row.Q<DropdownField>(PhonemeEntryListView.EntryTypeSelectorName);
            Assert.That(selector, Is.Not.Null);
            return selector.value;
        }

        private void SetPhonemeId(int index, string phonemeId)
        {
            _entriesProperty.GetArrayElementAtIndex(index)
                .FindPropertyRelative(nameof(PhonemeEntryBase.PhonemeId))
                .stringValue = phonemeId;
        }

        private string GetPhonemeId(int index)
        {
            return _entriesProperty.GetArrayElementAtIndex(index)
                .FindPropertyRelative(nameof(PhonemeEntryBase.PhonemeId))
                .stringValue;
        }

        private sealed class PhonemeEntryListViewTestAsset : ScriptableObject
        {
            [SerializeReference]
            public List<PhonemeEntryBase> Entries = new List<PhonemeEntryBase>();
        }
    }
}
