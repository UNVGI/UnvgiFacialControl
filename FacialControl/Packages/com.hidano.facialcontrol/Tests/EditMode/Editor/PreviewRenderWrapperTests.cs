using NUnit.Framework;
using SceneViewStyleCameraController;
using UnityEngine;
using Hidano.FacialControl.Editor.Common;

namespace Hidano.FacialControl.Tests.EditMode.Editor
{
    [TestFixture]
    public class PreviewRenderWrapperTests
    {
        private PreviewRenderWrapper _wrapper;
        private Rect _rect;

        [SetUp]
        public void SetUp()
        {
            _wrapper = new PreviewRenderWrapper();
            _rect = new Rect(0, 0, 512, 512);
        }

        [TearDown]
        public void TearDown()
        {
            _wrapper.Dispose();
        }

        [Test]
        public void HandleInput_AltLeftDrag_OrbitApplied()
        {
            var frame = new PreviewInputFrame(
                EventType.MouseDrag,
                button: 0,
                mousePosition: new Vector2(256, 256),
                delta: new Vector2(10f, 5f),
                scrollDelta: Vector2.zero,
                alt: true);

            var result = _wrapper.HandleInput(_rect, frame);

            Assert.IsTrue(result);
        }

        [Test]
        public void HandleInput_MiddleDrag_PanApplied()
        {
            var frame = new PreviewInputFrame(
                EventType.MouseDrag,
                button: 2,
                mousePosition: new Vector2(256, 256),
                delta: new Vector2(10f, 5f),
                scrollDelta: Vector2.zero,
                alt: false);

            var result = _wrapper.HandleInput(_rect, frame);

            Assert.IsTrue(result);
        }

        [Test]
        public void HandleInput_ScrollWheel_DollyApplied()
        {
            var frame = new PreviewInputFrame(
                EventType.ScrollWheel,
                button: 0,
                mousePosition: new Vector2(256, 256),
                delta: Vector2.zero,
                scrollDelta: new Vector2(0f, 3f),
                alt: false);

            var result = _wrapper.HandleInput(_rect, frame);

            Assert.IsTrue(result);
        }

        [Test]
        public void HandleInput_AltRightDrag_DollyApplied()
        {
            var frame = new PreviewInputFrame(
                EventType.MouseDrag,
                button: 1,
                mousePosition: new Vector2(256, 256),
                delta: new Vector2(0f, 10f),
                scrollDelta: Vector2.zero,
                alt: true);

            var result = _wrapper.HandleInput(_rect, frame);

            Assert.IsTrue(result);
        }

        [Test]
        public void HandleInput_OutsideRect_ReturnsFalse()
        {
            var frame = new PreviewInputFrame(
                EventType.MouseDrag,
                button: 0,
                mousePosition: new Vector2(600, 600),
                delta: new Vector2(10f, 5f),
                scrollDelta: Vector2.zero,
                alt: true);

            var result = _wrapper.HandleInput(_rect, frame);

            Assert.IsFalse(result);
        }
    }
}
