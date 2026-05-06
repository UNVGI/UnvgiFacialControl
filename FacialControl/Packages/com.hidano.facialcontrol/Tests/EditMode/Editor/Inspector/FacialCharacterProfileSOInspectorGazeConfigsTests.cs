using System.Collections.Generic;
using System.Reflection;
using Hidano.FacialControl.Adapters.ScriptableObject;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Models;
using Hidano.FacialControl.Editor.Inspector;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hidano.FacialControl.Tests.EditMode.Editor.Inspector
{
    [TestFixture]
    public class FacialCharacterProfileSOInspectorGazeConfigsTests
    {
        private sealed class TestCharacterSO : FacialCharacterProfileSO
        {
            public List<GazeBindingConfig> WritableGazeConfigs => _gazeConfigs;
        }

        private TestCharacterSO _so;
        private UnityEditor.Editor _editor;

        [TearDown]
        public void TearDown()
        {
            if (_editor != null)
            {
                Object.DestroyImmediate(_editor);
                _editor = null;
            }

            if (_so != null)
            {
                Object.DestroyImmediate(_so);
                _so = null;
            }
        }

        [Test]
        public void GazeConfigsAddDropdown_ListsOnlyAnalogExpressionsWithoutExistingConfig()
        {
            _so = CreateProfile();
            _so.Expressions.Add(CreateExpression("analog-one", "Analog One", ExpressionKind.Analog));
            _so.Expressions.Add(CreateExpression("analog-two", "Analog Two", ExpressionKind.Analog));
            _so.Expressions.Add(CreateExpression("digital-one", "Digital One", ExpressionKind.Digital));
            _so.WritableGazeConfigs.Add(new GazeBindingConfig { expressionId = "analog-two" });

            var root = BuildInspectorRoot();

            var dropdown = root.Q<DropdownField>(FacialCharacterProfileSOInspector.GazeConfigAddDropdownName);
            Assert.That(dropdown, Is.Not.Null);
            Assert.That(dropdown.choices, Is.EqualTo(new[] { "Analog One [analog-one]" }));
        }

        [Test]
        public void GazeConfigsAddDropdown_SelectionAppendsConfigForExpressionId()
        {
            _so = CreateProfile();
            _so.Expressions.Add(CreateExpression("analog-one", "Analog One", ExpressionKind.Analog));

            var root = BuildInspectorRoot();
            var dropdown = root.Q<DropdownField>(FacialCharacterProfileSOInspector.GazeConfigAddDropdownName);

            Assert.That(_so.GazeConfigs, Is.Empty);
            Assert.That(dropdown.choices, Does.Contain("Analog One [analog-one]"));
            InvokeAddGazeConfig("analog-one");

            Assert.That(_so.GazeConfigs, Has.Count.EqualTo(1));
            Assert.That(_so.GazeConfigs[0].expressionId, Is.EqualTo("analog-one"));
        }

        [Test]
        public void GazeConfigsAddDropdown_WhenNoCandidatesRemain_IsDisabledWithLabel()
        {
            _so = CreateProfile();
            _so.Expressions.Add(CreateExpression("analog-one", "Analog One", ExpressionKind.Analog));
            _so.WritableGazeConfigs.Add(new GazeBindingConfig { expressionId = "analog-one" });

            var root = BuildInspectorRoot();

            var dropdown = root.Q<DropdownField>(FacialCharacterProfileSOInspector.GazeConfigAddDropdownName);
            Assert.That(dropdown, Is.Not.Null);
            Assert.That(dropdown.enabledSelf, Is.False);
            Assert.That(dropdown.value, Is.EqualTo(FacialCharacterProfileSOInspector.GazeConfigNoCandidatesLabel));
        }

        [Test]
        public void GazeConfigsRow_RendersRequiredSingleRowControls()
        {
            _so = CreateProfile();
            _so.Expressions.Add(CreateExpression("analog-one", "Analog One", ExpressionKind.Analog));
            _so.WritableGazeConfigs.Add(new GazeBindingConfig { expressionId = "analog-one" });

            var root = BuildInspectorRoot();
            var row = root.Q<VisualElement>(FacialCharacterProfileSOInspector.GazeConfigRowName);

            Assert.That(row, Is.Not.Null);
            Assert.That(row.Q<Label>(FacialCharacterProfileSOInspector.GazeConfigExpressionNameLabelName)?.text, Is.EqualTo("Analog One"));
            Assert.That(row.Q<TextField>(FacialCharacterProfileSOInspector.GazeConfigLeftBonePathFieldName), Is.Not.Null);
            Assert.That(row.Q<TextField>(FacialCharacterProfileSOInspector.GazeConfigRightBonePathFieldName), Is.Not.Null);
            Assert.That(row.Q<FloatField>(FacialCharacterProfileSOInspector.GazeConfigLookUpAngleFieldName), Is.Not.Null);
            Assert.That(row.Q<FloatField>(FacialCharacterProfileSOInspector.GazeConfigLookDownAngleFieldName), Is.Not.Null);
            Assert.That(row.Q<FloatField>(FacialCharacterProfileSOInspector.GazeConfigOuterYawAngleFieldName), Is.Not.Null);
            Assert.That(row.Q<FloatField>(FacialCharacterProfileSOInspector.GazeConfigInnerYawAngleFieldName), Is.Not.Null);
            Assert.That(row.Q<ObjectField>(FacialCharacterProfileSOInspector.GazeConfigLookLeftClipFieldName), Is.Not.Null);
            Assert.That(row.Q<ObjectField>(FacialCharacterProfileSOInspector.GazeConfigLookRightClipFieldName), Is.Not.Null);
            Assert.That(row.Q<ObjectField>(FacialCharacterProfileSOInspector.GazeConfigLookUpClipFieldName), Is.Not.Null);
            Assert.That(row.Q<ObjectField>(FacialCharacterProfileSOInspector.GazeConfigLookDownClipFieldName), Is.Not.Null);
            Assert.That(row.Q<Button>(FacialCharacterProfileSOInspector.GazeConfigAutoAssignButtonName), Is.Not.Null);
            Assert.That(row.Q<Button>(FacialCharacterProfileSOInspector.GazeConfigRemoveButtonName), Is.Not.Null);
            Assert.That(root.Q<Button>(FacialCharacterProfileSOInspector.GazeConfigBulkResolveButtonName), Is.Not.Null);
        }

        private VisualElement BuildInspectorRoot()
        {
            _editor = UnityEditor.Editor.CreateEditor(_so, typeof(FacialCharacterProfileSOInspector));
            Assert.That(_editor, Is.Not.Null);
            return _editor.CreateInspectorGUI();
        }

        private void InvokeAddGazeConfig(string expressionId)
        {
            var method = typeof(FacialCharacterProfileSOInspector).GetMethod(
                "AddGazeConfigFromCandidate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(_editor, new object[] { expressionId });
        }

        private static TestCharacterSO CreateProfile()
        {
            var so = ScriptableObject.CreateInstance<TestCharacterSO>();
            so.name = "GazeConfigInspectorTest";
            return so;
        }

        private static ExpressionSerializable CreateExpression(
            string id,
            string name,
            ExpressionKind kind)
        {
            return new ExpressionSerializable
            {
                id = id,
                name = name,
                layer = "base",
                kind = kind,
            };
        }
    }
}
