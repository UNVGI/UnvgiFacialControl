using System.Reflection;
using Hidano.FacialControl.Editor.Windows.Routing.Graph;
using Hidano.FacialControl.Editor.Windows.Routing.Logic;
using NUnit.Framework;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace Hidano.FacialControl.Tests.EditMode.Editor.Windows.Routing.Graph
{
    [TestFixture]
    public class AdapterNodeViewTests
    {
        [Test]
        public void Construct_RendersDisplayNameBadgeAndMultipleOutputPorts()
        {
            var data = new AdapterNodeData(
                "input-system",
                "input-system",
                supportsAutoWire: false,
                new[]
                {
                    new AdapterOutputData("input-system", "input-system"),
                    new AdapterOutputData("input-system:analog-expression", "analog-expression"),
                    new AdapterOutputData("input-system:overlay:blink", "blink"),
                });

            var view = new AdapterNodeView(data, _ => { });

            Assert.That(view.title, Is.EqualTo("input-system"));
            Assert.That(view.TypeBadge, Is.Not.Null);
            Assert.That(view.TypeBadge.text, Is.EqualTo(AdapterNodeView.TypeBadgeText));

            Port analogPort = view.GetOutputPort("input-system:analog-expression");
            Assert.That(analogPort, Is.Not.Null);
            Assert.That(analogPort.direction, Is.EqualTo(Direction.Output));
            Assert.That(analogPort.portName, Is.EqualTo("analog-expression"));
            Assert.That(analogPort.userData, Is.EqualTo("input-system:analog-expression"));

            Assert.That(view.GetOutputPort("input-system:overlay:blink"), Is.Not.Null);
            Assert.That(view.GetOutputPort("does-not-exist"), Is.Null);
        }

        [Test]
        public void Construct_HidesAutoWireButton_WhenBindingDoesNotSupportIt()
        {
            var data = new AdapterNodeData(
                "input-system",
                "input-system",
                supportsAutoWire: false,
                new[] { new AdapterOutputData("input-system", "input-system") });

            var view = new AdapterNodeView(data, _ => { });

            Assert.That(view.AutoWireButton, Is.Null);
        }

        [Test]
        public void Click_AutoWireButton_InvokesCallbackWithBindingSlug()
        {
            var data = new AdapterNodeData(
                "ulipsync",
                "ulipsync",
                supportsAutoWire: true,
                new[] { new AdapterOutputData("lipsync-overlay:a", "a") });
            string invokedSlug = null;

            var view = new AdapterNodeView(data, slug => invokedSlug = slug);

            Assert.That(view.AutoWireButton, Is.Not.Null);
            InvokeButton(view.AutoWireButton);

            Assert.That(invokedSlug, Is.EqualTo("ulipsync"));
        }

        [Test]
        public void SetAdapterNodes_RebuildsGraphWithAdapterNodeViews()
        {
            var graphView = new RoutingGraphView();
            var adapterNodes = new[]
            {
                new AdapterNodeData(
                    "ulipsync",
                    "ulipsync",
                    supportsAutoWire: true,
                    new[] { new AdapterOutputData("lipsync-overlay:a", "a") }),
                new AdapterNodeData(
                    "input-system",
                    "input-system",
                    supportsAutoWire: false,
                    new[]
                    {
                        new AdapterOutputData("input-system", "input-system"),
                        new AdapterOutputData("input-system:analog-expression", "analog-expression"),
                    }),
            };

            graphView.SetAdapterNodes(adapterNodes, _ => { });

            Assert.That(graphView.AdapterNodeViews, Has.Count.EqualTo(2));
            Assert.That(graphView.Query<AdapterNodeView>().ToList(), Has.Count.EqualTo(2));
            Assert.That(graphView.AdapterNodeViews[0].GetOutputPort("lipsync-overlay:a"), Is.Not.Null);
            Assert.That(
                graphView.AdapterNodeViews[1].GetOutputPort("input-system:analog-expression").portName,
                Is.EqualTo("analog-expression"));
        }

        private static void InvokeButton(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.text, Is.EqualTo(AdapterNodeView.AutoWireButtonText));

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
