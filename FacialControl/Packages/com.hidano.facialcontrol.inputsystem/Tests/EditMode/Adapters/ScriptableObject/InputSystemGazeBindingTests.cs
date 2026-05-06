using System;
using System.Linq;
using System.Reflection;
using Hidano.FacialControl.Adapters.ScriptableObject;
using Hidano.FacialControl.InputSystem.Adapters.ScriptableObject;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Hidano.FacialControl.InputSystem.Tests.EditMode.Adapters.ScriptableObject
{
    [TestFixture]
    public class InputSystemGazeBindingTests
    {
        [Test]
        public void Type_IsSerializableSealedAndDoesNotInheritGazeBindingConfig()
        {
            Type type = typeof(InputSystemGazeBinding);

            Assert.That(type.GetCustomAttributes(typeof(SerializableAttribute), inherit: false).Length, Is.EqualTo(1));
            Assert.That(type.IsSealed, Is.True);
            Assert.That(typeof(GazeBindingConfig).IsAssignableFrom(type), Is.False);
        }

        [Test]
        public void Fields_AreOnlyExpressionIdAndInputActionReference()
        {
            FieldInfo[] fields = typeof(InputSystemGazeBinding)
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .OrderBy(field => field.Name, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(new[] { "expressionId", "inputActionRef" }, fields.Select(field => field.Name).ToArray());
            Assert.That(fields.Single(field => field.Name == "expressionId").FieldType, Is.EqualTo(typeof(string)));
            Assert.That(fields.Single(field => field.Name == "inputActionRef").FieldType, Is.EqualTo(typeof(InputActionReference)));
        }
    }
}
