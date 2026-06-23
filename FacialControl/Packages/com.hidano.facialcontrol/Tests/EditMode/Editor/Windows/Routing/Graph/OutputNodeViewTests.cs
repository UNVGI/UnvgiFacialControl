using System.Linq;
using Hidano.FacialControl.Editor.Windows.Routing.Graph;
using Hidano.FacialControl.Editor.Windows.Routing.Logic;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Hidano.FacialControl.Tests.EditMode.Editor.Windows.Routing.Graph
{
    [TestFixture]
    public class OutputNodeViewTests
    {
        [Test]
        public void Construct_RendersOrderedLayersAsReadOnlyRows()
        {
            var view = new OutputNodeView(new OutputNodeData(new[]
            {
                new OutputLayerData(1, "emotion", 0, new string[0]),
                new OutputLayerData(0, "overlay", 2, new[] { "emotion", "fx" }),
            }));

            Assert.That(view.title, Is.EqualTo(OutputNodeView.NodeTitle));
            Assert.That(view.Query<TextField>().ToList(), Is.Empty);

            Label[] rows = view.Query<Label>(className: OutputNodeView.LayerRowClassName).ToList().ToArray();
            Assert.That(rows, Has.Length.EqualTo(2));
            Assert.That(rows[0].text, Is.EqualTo("1. emotion | priority 0 | mask -"));
            Assert.That(rows[1].text, Is.EqualTo("2. overlay | priority 2 | mask emotion, fx"));
        }

        [Test]
        public void SetOutputNode_RebuildsGraphWithUpdatedOrderedLayers()
        {
            var graphView = new RoutingGraphView();

            graphView.SetOutputNode(new OutputNodeData(new[]
            {
                new OutputLayerData(0, "overlay", 5, new[] { "emotion" }),
            }));

            Assert.That(graphView.OutputNodeView, Is.Not.Null);
            CollectionAssert.AreEqual(
                new[] { "1. overlay | priority 5 | mask emotion" },
                graphView.OutputNodeView.Query<Label>(className: OutputNodeView.LayerRowClassName)
                    .ToList()
                    .Select(label => label.text)
                    .ToArray());

            graphView.SetOutputNode(new OutputNodeData(new[]
            {
                new OutputLayerData(1, "emotion", 1, new string[0]),
                new OutputLayerData(0, "overlay", 3, new[] { "emotion", "fx" }),
            }));

            CollectionAssert.AreEqual(
                new[]
                {
                    "1. emotion | priority 1 | mask -",
                    "2. overlay | priority 3 | mask emotion, fx",
                },
                graphView.OutputNodeView.Query<Label>(className: OutputNodeView.LayerRowClassName)
                    .ToList()
                    .Select(label => label.text)
                    .ToArray());
        }
    }
}
