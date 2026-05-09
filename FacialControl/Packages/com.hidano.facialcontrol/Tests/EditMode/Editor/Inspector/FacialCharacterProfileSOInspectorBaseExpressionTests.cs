using System.Collections.Generic;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Editor.Inspector;
using NUnit.Framework;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Hidano.FacialControl.Tests.EditMode.Editor.Inspector
{
    [TestFixture]
    public class FacialCharacterProfileSOInspectorBaseExpressionTests
    {
        private FacialCharacterProfileSO _so;
        private UnityEditor.Editor _editor;
        private AnimationClip _clip;

        [TearDown]
        public void TearDown()
        {
            if (_editor != null)
            {
                Object.DestroyImmediate(_editor);
                _editor = null;
            }

            if (_clip != null)
            {
                Object.DestroyImmediate(_clip);
                _clip = null;
            }

            if (_so != null)
            {
                Object.DestroyImmediate(_so);
                _so = null;
            }
        }

        [Test]
        public void CreateInspectorGUI_RendersBaseExpressionSectionBetweenLayersAndGazeConfigs()
        {
            _so = CreateProfile();

            var root = BuildInspectorRoot();
            var layers = root.Q<Foldout>(FacialCharacterProfileSOInspector.LayersFoldoutName);
            var baseExpression = root.Q<Foldout>(FacialCharacterProfileSOInspector.BaseExpressionFoldoutName);
            var gaze = root.Q<Foldout>(FacialCharacterProfileSOInspector.GazeConfigsFoldoutName);

            Assert.That(layers, Is.Not.Null);
            Assert.That(baseExpression, Is.Not.Null);
            Assert.That(baseExpression.text, Is.EqualTo("ベース表情"));
            Assert.That(gaze, Is.Not.Null);
            Assert.That(DirectChildIndex(root, layers), Is.LessThan(DirectChildIndex(root, baseExpression)));
            Assert.That(DirectChildIndex(root, baseExpression), Is.LessThan(DirectChildIndex(root, gaze)));
        }

        [Test]
        public void CreateInspectorGUI_BaseExpressionSectionContainsAnimationClipObjectFieldAndUnsetHelp()
        {
            _so = CreateProfile();

            var root = BuildInspectorRoot();
            var section = root.Q<Foldout>(FacialCharacterProfileSOInspector.BaseExpressionFoldoutName);
            var clipField = section.Q<ObjectField>(FacialCharacterProfileSOInspector.BaseExpressionClipFieldName);
            var help = section.Q<HelpBox>(FacialCharacterProfileSOInspector.BaseExpressionUnsetHelpName);

            Assert.That(clipField, Is.Not.Null);
            Assert.That(clipField.objectType, Is.EqualTo(typeof(AnimationClip)));
            Assert.That(clipField.allowSceneObjects, Is.False);
            Assert.That(help, Is.Not.Null);
            Assert.That(help.style.display.value, Is.EqualTo(DisplayStyle.Flex));
            StringAssert.Contains("常時固定表情", help.text);
            StringAssert.Contains("衣装固定 BlendShape", help.text);
            AssertNoBakeButton(section);
        }

        [Test]
        public void CreateInspectorGUI_BaseExpressionClipAssigned_HidesUnsetHelp()
        {
            _so = CreateProfile();
            _clip = new AnimationClip { name = "BaseExpression_InspectorClip" };
            _so.BaseExpression.animationClip = _clip;

            var root = BuildInspectorRoot();
            var help = root.Q<HelpBox>(FacialCharacterProfileSOInspector.BaseExpressionUnsetHelpName);

            Assert.That(help, Is.Not.Null);
            Assert.That(help.style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        private VisualElement BuildInspectorRoot()
        {
            _editor = UnityEditor.Editor.CreateEditor(_so, typeof(FacialCharacterProfileSOInspector));
            Assert.That(_editor, Is.Not.Null);
            return _editor.CreateInspectorGUI();
        }

        private static FacialCharacterProfileSO CreateProfile()
        {
            var so = ScriptableObject.CreateInstance<FacialCharacterProfileSO>();
            so.name = "BaseExpressionInspectorTest";
            return so;
        }

        private static int DirectChildIndex(VisualElement root, VisualElement child)
        {
            int index = 0;
            foreach (var current in root.Children())
            {
                if (ReferenceEquals(current, child))
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        private static void AssertNoBakeButton(VisualElement section)
        {
            var buttonTexts = new List<string>();
            section.Query<Button>().ForEach(button => buttonTexts.Add(button.text ?? string.Empty));

            foreach (string text in buttonTexts)
            {
                StringAssert.DoesNotContain("bake", text.ToLowerInvariant());
                StringAssert.DoesNotContain("rebake", text.ToLowerInvariant());
                StringAssert.DoesNotContain("ベイク", text);
                StringAssert.DoesNotContain("再ベイク", text);
            }
        }
    }
}
