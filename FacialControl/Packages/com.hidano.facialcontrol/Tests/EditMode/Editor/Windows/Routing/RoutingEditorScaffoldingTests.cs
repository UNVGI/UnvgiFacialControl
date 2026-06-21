using Hidano.FacialControl.Editor.Windows.Routing;
using Hidano.FacialControl.Editor.Windows.Routing.Graph;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hidano.FacialControl.Tests.EditMode.Editor.Windows.Routing
{
    [TestFixture]
    public class RoutingEditorScaffoldingTests
    {
        [Test]
        public void CreateInstance_RoutingEditorWindow_CanBeConstructed()
        {
            var window = ScriptableObject.CreateInstance<RoutingEditorWindow>();

            Assert.That(window, Is.Not.Null);

            Object.DestroyImmediate(window);
        }

        [Test]
        public void Construct_GraphView_SetsStableElementName()
        {
            var graphView = new RoutingGraphView();

            Assert.That(graphView, Is.InstanceOf<VisualElement>());
            Assert.That(graphView.name, Is.EqualTo("routing-graph-view"));
        }
    }
}
