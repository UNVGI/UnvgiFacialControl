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
        public void GetExpressionBindings_FilteredByCategory_ReturnsOnlyMatching()
        {
            var so = ScriptableObject.CreateInstance<FacialCharacterSO>();
            try
            {
                so.ExpressionBindings.Add(new ExpressionBindingEntry
                {
                    actionName = "Trigger1", expressionId = "smile", category = InputSourceCategory.Controller,
                });
                so.ExpressionBindings.Add(new ExpressionBindingEntry
                {
                    actionName = "Trigger2", expressionId = "angry", category = InputSourceCategory.Keyboard,
                });
                so.ExpressionBindings.Add(new ExpressionBindingEntry
                {
                    actionName = "Trigger3", expressionId = "joy", category = InputSourceCategory.Controller,
                });

                var controllerBindings = so.GetExpressionBindings(InputSourceCategory.Controller);
                var keyboardBindings = so.GetExpressionBindings(InputSourceCategory.Keyboard);

                Assert.That(controllerBindings.Count, Is.EqualTo(2));
                Assert.That(keyboardBindings.Count, Is.EqualTo(1));
                Assert.That(keyboardBindings[0].ActionName, Is.EqualTo("Trigger2"));
                Assert.That(keyboardBindings[0].ExpressionId, Is.EqualTo("angry"));
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
                    actionName = "", expressionId = "smile", category = InputSourceCategory.Controller,
                });
                so.ExpressionBindings.Add(new ExpressionBindingEntry
                {
                    actionName = "Trigger1", expressionId = "", category = InputSourceCategory.Controller,
                });
                so.ExpressionBindings.Add(new ExpressionBindingEntry
                {
                    actionName = "Trigger2", expressionId = "joy", category = InputSourceCategory.Controller,
                });

                var bindings = so.GetExpressionBindings(InputSourceCategory.Controller);

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
