using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Hidano.FacialControl.Adapters.Playable;
using Hidano.FacialControl.Adapters.ScriptableObject.Serializable;
using Hidano.FacialControl.Domain.Models;

namespace Hidano.FacialControl.Tests.EditMode.Adapters.Playable
{
    /// <summary>
    /// <see cref="FacialController"/> の新 <c>_characterSO</c> 経路を検証する。
    /// SerializeField の存在 / プロパティのアクセシビリティ / 旧 ProfileSO との共存を確認する。
    /// </summary>
    [TestFixture]
    public class FacialControllerCharacterSOTests
    {
        private sealed class TestCharacterSO : FacialCharacterProfileSO { }

        [Test]
        public void CharacterSO_Setter_PersistsValue()
        {
            var go = new GameObject("FacialControllerHost");
            try
            {
                go.AddComponent<Animator>();
                var controller = go.AddComponent<FacialController>();
                var so = ScriptableObject.CreateInstance<TestCharacterSO>();
                so.name = "MyChar";

                controller.CharacterSO = so;

                Assert.That(controller.CharacterSO, Is.SameAs(so));
                Object.DestroyImmediate(so);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CharacterSO_SerializeField_IsExposedToInspector()
        {
            var go = new GameObject("FacialControllerHost");
            try
            {
                go.AddComponent<Animator>();
                var controller = go.AddComponent<FacialController>();
                var serialized = new SerializedObject(controller);
                var prop = serialized.FindProperty("_characterSO");

                Assert.That(prop, Is.Not.Null,
                    "FacialController に _characterSO の SerializeField が存在するはず。");
                Assert.That(prop.propertyType, Is.EqualTo(SerializedPropertyType.ObjectReference));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Initialize_NoSOConfigured_DoesNothing()
        {
            var go = new GameObject("FacialControllerHost");
            try
            {
                go.AddComponent<Animator>();
                var controller = go.AddComponent<FacialController>();

                // _characterSO も _profileSO も未設定。Initialize は警告も出さず単に return する。
                controller.Initialize();

                Assert.That(controller.IsInitialized, Is.False);
                Assert.That(controller.CurrentProfile, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
