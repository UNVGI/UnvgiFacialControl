using NUnit.Framework;
using Hidano.FacialControl.Application.UseCases;
using Hidano.FacialControl.Domain.Interfaces;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Application
{
    /// <summary>
    /// <see cref="ExpressionUseCase"/> が <see cref="IActiveExpressionProvider"/> を
    /// 正しく実装することを保証するテスト。OverlayInputSource の解決ロジックは
    /// 本 provider に依存するため、レイヤー別 top の取得が確実に動くことを保証する。
    /// </summary>
    [TestFixture]
    public class ExpressionUseCaseActiveProviderTests
    {
        private static FacialProfile CreateLastWinsProfile()
        {
            var layers = new[]
            {
                new LayerDefinition("emotion", 0, ExclusionMode.LastWins),
                new LayerDefinition("eye", 1, ExclusionMode.LastWins),
            };
            return new FacialProfile("1.0", layers);
        }

        private static FacialProfile CreateBlendProfile()
        {
            var layers = new[]
            {
                new LayerDefinition("emotion", 0, ExclusionMode.Blend),
            };
            return new FacialProfile("1.0", layers);
        }

        [Test]
        public void TryGetTop_NoActive_ReturnsNull()
        {
            var sut = new ExpressionUseCase(CreateLastWinsProfile());
            Assert.IsNull(((IActiveExpressionProvider)sut).TryGetTopActiveExpression("emotion"));
        }

        [Test]
        public void TryGetTop_AfterActivate_ReturnsThatExpression()
        {
            var sut = new ExpressionUseCase(CreateLastWinsProfile());
            var smile = new Expression("smile", "Smile", "emotion");
            sut.Activate(smile);

            var top = ((IActiveExpressionProvider)sut).TryGetTopActiveExpression("emotion");
            Assert.IsNotNull(top);
            Assert.AreEqual("smile", top.Value.Id);
        }

        [Test]
        public void TryGetTop_LastWins_ReplacementReplacesTop()
        {
            var sut = new ExpressionUseCase(CreateLastWinsProfile());
            sut.Activate(new Expression("smile", "Smile", "emotion"));
            sut.Activate(new Expression("anger", "Anger", "emotion"));

            var top = ((IActiveExpressionProvider)sut).TryGetTopActiveExpression("emotion");
            Assert.IsNotNull(top);
            Assert.AreEqual("anger", top.Value.Id, "LastWins では最後 Activate が top");
        }

        [Test]
        public void TryGetTop_Blend_ReturnsLastAddedAsTop()
        {
            var sut = new ExpressionUseCase(CreateBlendProfile());
            sut.Activate(new Expression("smile", "Smile", "emotion"));
            sut.Activate(new Expression("anger", "Anger", "emotion"));

            var top = ((IActiveExpressionProvider)sut).TryGetTopActiveExpression("emotion");
            Assert.IsNotNull(top);
            Assert.AreEqual("anger", top.Value.Id, "Blend モードでも最後 Activate を top として返す");
        }

        [Test]
        public void TryGetTop_AfterDeactivate_ReturnsNullWhenLayerEmpties()
        {
            var sut = new ExpressionUseCase(CreateLastWinsProfile());
            var smile = new Expression("smile", "Smile", "emotion");
            sut.Activate(smile);
            sut.Deactivate(smile);

            Assert.IsNull(((IActiveExpressionProvider)sut).TryGetTopActiveExpression("emotion"));
        }

        [Test]
        public void TryGetTop_DifferentLayer_IndependentlyTracked()
        {
            var sut = new ExpressionUseCase(CreateLastWinsProfile());
            sut.Activate(new Expression("smile", "Smile", "emotion"));
            sut.Activate(new Expression("blink", "Blink", "eye"));

            var emotion = ((IActiveExpressionProvider)sut).TryGetTopActiveExpression("emotion");
            var eye = ((IActiveExpressionProvider)sut).TryGetTopActiveExpression("eye");
            Assert.AreEqual("smile", emotion?.Id);
            Assert.AreEqual("blink", eye?.Id);
        }

        [TestCase(null)]
        [TestCase("")]
        public void TryGetTop_NullOrEmptyLayer_ReturnsNull(string layerName)
        {
            var sut = new ExpressionUseCase(CreateLastWinsProfile());
            sut.Activate(new Expression("smile", "Smile", "emotion"));
            Assert.IsNull(((IActiveExpressionProvider)sut).TryGetTopActiveExpression(layerName));
        }

        [Test]
        public void TryGetTop_UnknownLayer_ReturnsNull()
        {
            var sut = new ExpressionUseCase(CreateLastWinsProfile());
            sut.Activate(new Expression("smile", "Smile", "emotion"));
            Assert.IsNull(((IActiveExpressionProvider)sut).TryGetTopActiveExpression("does_not_exist"));
        }

        [Test]
        public void TryGetTop_AfterSetProfile_IsCleared()
        {
            var sut = new ExpressionUseCase(CreateLastWinsProfile());
            sut.Activate(new Expression("smile", "Smile", "emotion"));

            sut.SetProfile(CreateLastWinsProfile());

            Assert.IsNull(((IActiveExpressionProvider)sut).TryGetTopActiveExpression("emotion"));
        }
    }
}
