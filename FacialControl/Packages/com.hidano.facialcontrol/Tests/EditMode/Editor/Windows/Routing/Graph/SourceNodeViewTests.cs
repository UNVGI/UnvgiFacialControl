using System.Reflection;
using Hidano.FacialControl.Editor.Windows.Routing.Graph;
using Hidano.FacialControl.Editor.Windows.Routing.Logic;
using NUnit.Framework;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Hidano.FacialControl.Tests.EditMode.Editor.Windows.Routing.Graph
{
    [TestFixture]
    public class SourceNodeViewTests
    {
        [Test]
        public void Construct_RendersLabelOnlyAndStoresCanonicalIdInternally()
        {
            var descriptor = new SourceNodeDescriptor("lipsync-overlay:a", "a", "ulipsync");

            var view = new SourceNodeView(descriptor, _ => { });

            Assert.That(view.title, Is.EqualTo("ulipsync"));
            Assert.That(view.OutputPort, Is.Not.Null);
            Assert.That(view.OutputPort.direction, Is.EqualTo(Direction.Output));
            Assert.That(view.OutputPort.portName, Is.EqualTo("a"));
            Assert.That(view.OutputPort.tooltip, Is.EqualTo("lipsync-overlay:a"));
            Assert.That(view.OutputPort.userData, Is.EqualTo("lipsync-overlay:a"));
            Assert.That(view.Query<TextField>().ToList(), Is.Empty);
        }

        [Test]
        public void Click_AutoWireButton_InvokesCallbackWithDescriptor()
        {
            var descriptor = new SourceNodeDescriptor("lipsync-overlay:i", "i", "ulipsync");
            SourceNodeDescriptor invokedDescriptor = default;
            bool invoked = false;

            var view = new SourceNodeView(descriptor, nodeDescriptor =>
            {
                invoked = true;
                invokedDescriptor = nodeDescriptor;
            });

            InvokeButton(view.AutoWireButton);

            Assert.That(invoked, Is.True);
            Assert.That(invokedDescriptor.CanonicalId, Is.EqualTo("lipsync-overlay:i"));
            Assert.That(invokedDescriptor.Label, Is.EqualTo("i"));
            Assert.That(invokedDescriptor.BindingSlug, Is.EqualTo("ulipsync"));
        }

        [Test]
        public void SetSourceNodes_RebuildsGraphWithSourceNodeViews()
        {
            var graphView = new RoutingGraphView();
            var sourceNodes = new[]
            {
                new SourceNodeDescriptor("lipsync-overlay:a", "a", "ulipsync"),
                new SourceNodeDescriptor("ulipsync", "ulipsync", "ulipsync"),
            };

            graphView.SetSourceNodes(sourceNodes, _ => { });

            Assert.That(graphView.SourceNodeViews, Has.Count.EqualTo(2));
            Assert.That(graphView.Query<SourceNodeView>().ToList(), Has.Count.EqualTo(2));
            Assert.That(graphView.SourceNodeViews[0].OutputPort.portName, Is.EqualTo("a"));
            Assert.That(graphView.SourceNodeViews[1].OutputPort.tooltip, Is.EqualTo("ulipsync"));
        }

        private static void InvokeButton(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.text, Is.EqualTo(SourceNodeView.AutoWireButtonText));

            MethodInfo invoke = button.clickable.GetType().GetMethod(
                "Invoke",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(EventBase) },
                modifiers: null);
            Assert.That(invoke, Is.Not.Null);
            invoke.Invoke(button.clickable, new object[] { null });
        }
    }
}
