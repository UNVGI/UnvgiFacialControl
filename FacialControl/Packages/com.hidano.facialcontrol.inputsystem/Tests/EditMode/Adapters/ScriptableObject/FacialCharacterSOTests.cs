using NUnit.Framework;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.InputSystem.Adapters.ScriptableObject;
using Hidano.FacialControl.Domain.Models;
using UnityEngine;

namespace Hidano.FacialControl.InputSystem.Tests.EditMode.Adapters.ScriptableObjectTests
{
    /// <summary>
    /// <see cref="FacialCharacterSO"/> の Inspector フィールド永続化と Domain 変換を検証する。
    /// </summary>
    [TestFixture]
    public class FacialCharacterSOTests
    {
        [Test]
        public void GetExpressionBindings_ReturnsAllNonEmptyEntries()
        {
            var so = ScriptableObject.CreateInstance<FacialCharacterSO>();
            try
            {
                so.ExpressionBindings.Add(new ExpressionBindingEntry
                {
                    actionName = "Trigger1", expressionId = "smile",
                });
                so.ExpressionBindings.Add(new ExpressionBindingEntry
                {
                    actionName = "Trigger2", expressionId = "angry",
                });
                so.ExpressionBindings.Add(new ExpressionBindingEntry
                {
                    actionName = "Trigger3", expressionId = "joy",
                });

                var bindings = so.GetExpressionBindings();

                Assert.That(bindings.Count, Is.EqualTo(3));
                Assert.That(bindings[0].ActionName, Is.EqualTo("Trigger1"));
                Assert.That(bindings[1].ActionName, Is.EqualTo("Trigger2"));
                Assert.That(bindings[2].ExpressionId, Is.EqualTo("joy"));
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void GetExpressionBindings_EmptyEntries_AreSkipped()
        {
            var so = ScriptableObject.CreateInstance<FacialCharacterSO>();
            try
            {
                so.ExpressionBindings.Add(new ExpressionBindingEntry
                {
                    actionName = "", expressionId = "smile",
                });
                so.ExpressionBindings.Add(new ExpressionBindingEntry
                {
                    actionName = "Trigger1", expressionId = "",
                });
                so.ExpressionBindings.Add(new ExpressionBindingEntry
                {
                    actionName = "Trigger2", expressionId = "joy",
                });

                var bindings = so.GetExpressionBindings();

                Assert.That(bindings.Count, Is.EqualTo(1));
                Assert.That(bindings[0].ActionName, Is.EqualTo("Trigger2"));
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void BuildAnalogProfile_ConvertsAllValidBindings()
        {
            var so = ScriptableObject.CreateInstance<FacialCharacterSO>();
            try
            {
                so.AnalogBindings.Add(new AnalogBindingEntrySerializable
                {
                    sourceId = "x-stick-x",
                    sourceAxis = 0,
                    targetKind = AnalogBindingTargetKind.BlendShape,
                    targetIdentifier = "EyeLeftBlink",
                });
                so.AnalogBindings.Add(new AnalogBindingEntrySerializable
                {
                    sourceId = "x-stick-y",
                    sourceAxis = 1,
                    targetKind = AnalogBindingTargetKind.BonePose,
                    targetIdentifier = "Head",
                    targetAxis = AnalogTargetAxis.Y,
                });

                var profile = so.BuildAnalogProfile();

                Assert.That(profile.Bindings.Length, Is.EqualTo(2));
                Assert.That(profile.Bindings.Span[0].TargetIdentifier, Is.EqualTo("EyeLeftBlink"));
                Assert.That(profile.Bindings.Span[1].TargetAxis, Is.EqualTo(AnalogTargetAxis.Y));
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void DefaultActionMapName_IsExpression()
        {
            var so = ScriptableObject.CreateInstance<FacialCharacterSO>();
            try
            {
                Assert.That(so.ActionMapName, Is.EqualTo("Expression"));
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }

        [Test]
        public void BuildFallbackProfile_RespectsInspectorData()
        {
            var so = ScriptableObject.CreateInstance<FacialCharacterSO>();
            try
            {
                so.Layers.Add(new LayerDefinitionSerializable
                {
                    name = "emotion",
                    priority = 0,
                    exclusionMode = ExclusionMode.LastWins,
                    inputSources = { new InputSourceDeclarationSerializable { id = "controller-expr", weight = 1.0f } },
                });
                so.Expressions.Add(new ExpressionSerializable
                {
                    id = "smile", name = "Smile", layer = "emotion",
                });

                var profile = so.BuildFallbackProfile();

                Assert.That(profile.Layers.Length, Is.EqualTo(1));
                Assert.That(profile.Expressions.Length, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(so);
            }
        }
    }
}
