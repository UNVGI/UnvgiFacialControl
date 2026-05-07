using System.Reflection;
using Hidano.FacialControl.LipSync.Adapters.Devices;
using Hidano.FacialControl.LipSync.Editor.Inspector;
using Hidano.FacialControl.LipSync.Tests.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hidano.FacialControl.LipSync.Tests.EditMode.Editor
{
    public class DeviceDescriptorPopupTests
    {
        private DeviceDescriptorPopupTestAsset _asset;
        private SerializedObject _serializedObject;
        private SerializedProperty _descriptorProperty;

        [SetUp]
        public void SetUp()
        {
            _asset = ScriptableObject.CreateInstance<DeviceDescriptorPopupTestAsset>();
            _serializedObject = new SerializedObject(_asset);
            _descriptorProperty = _serializedObject.FindProperty(nameof(DeviceDescriptorPopupTestAsset.Descriptor));
        }

        [TearDown]
        public void TearDown()
        {
            _serializedObject.Dispose();
            Object.DestroyImmediate(_asset);
        }

        [Test]
        public void Create_EnumeratedAsioAndMicrophone_AddsAllNamesToPopupChoices()
        {
            var popup = CreatePopup(
                new FakeAsioDriverEnumerator("ASIO Main", "ASIO Backup"),
                new FakeMicrophoneDeviceEnumerator("Built-in Mic", "USB Mic"));

            var devicePopup = popup.Q<PopupField<string>>(DeviceDescriptorPopup.DevicePopupName);

            Assert.That(devicePopup, Is.Not.Null);
            CollectionAssert.Contains(devicePopup.choices, "ASIO Main");
            CollectionAssert.Contains(devicePopup.choices, "ASIO Backup");
            CollectionAssert.Contains(devicePopup.choices, "Built-in Mic");
            CollectionAssert.Contains(devicePopup.choices, "USB Mic");
        }

        [Test]
        public void Create_ValidDescriptor_DoesNotExposeAsioMicToggle()
        {
            var popup = CreatePopup(
                new FakeAsioDriverEnumerator("ASIO Main"),
                new FakeMicrophoneDeviceEnumerator("USB Mic"));

            Assert.That(popup.Q<Toggle>(), Is.Null);
        }

        [Test]
        public void ManualOverride_Changed_UpdatesDeviceName()
        {
            var popup = CreatePopup(
                new FakeAsioDriverEnumerator("ASIO Main"),
                new FakeMicrophoneDeviceEnumerator("USB Mic"));
            var manualOverride = popup.Q<TextField>(DeviceDescriptorPopup.ManualOverrideFieldName);

            manualOverride.SetValueWithoutNotify("Disconnected Mic");
            InvokePrivate(popup, "ApplyDeviceNameFromManualOverride", manualOverride.value);

            _serializedObject.Update();
            Assert.That(
                _descriptorProperty.FindPropertyRelative(nameof(DeviceDescriptor.DeviceName)).stringValue,
                Is.EqualTo("Disconnected Mic"));
        }

        [Test]
        public void PopupSelection_Changed_UpdatesDeviceNameAndManualOverride()
        {
            var popup = CreatePopup(
                new FakeAsioDriverEnumerator("ASIO Main"),
                new FakeMicrophoneDeviceEnumerator("USB Mic"));
            var devicePopup = popup.Q<PopupField<string>>(DeviceDescriptorPopup.DevicePopupName);
            var manualOverride = popup.Q<TextField>(DeviceDescriptorPopup.ManualOverrideFieldName);

            devicePopup.SetValueWithoutNotify("USB Mic");
            InvokePrivate(popup, "ApplyDeviceNameFromPopup", devicePopup.value);

            _serializedObject.Update();
            Assert.That(
                _descriptorProperty.FindPropertyRelative(nameof(DeviceDescriptor.DeviceName)).stringValue,
                Is.EqualTo("USB Mic"));
            Assert.That(manualOverride.value, Is.EqualTo("USB Mic"));
        }

        [Test]
        public void DisambiguatorIndex_Changed_UpdatesDescriptor()
        {
            var popup = CreatePopup(
                new FakeAsioDriverEnumerator("ASIO Main"),
                new FakeMicrophoneDeviceEnumerator("USB Mic"));
            var disambiguatorIndex =
                popup.Q<IntegerField>(DeviceDescriptorPopup.DisambiguatorIndexFieldName);

            disambiguatorIndex.SetValueWithoutNotify(2);
            InvokePrivate(popup, "ApplyDisambiguatorIndex", disambiguatorIndex.value);

            _serializedObject.Update();
            Assert.That(
                _descriptorProperty.FindPropertyRelative(nameof(DeviceDescriptor.DisambiguatorIndex)).intValue,
                Is.EqualTo(2));
        }

        private DeviceDescriptorPopup CreatePopup(
            IAsioDriverEnumerator asioEnumerator,
            IMicrophoneDeviceEnumerator microphoneEnumerator)
        {
            return new DeviceDescriptorPopup(
                _descriptorProperty,
                asioEnumerator,
                microphoneEnumerator);
        }

        private static void InvokePrivate(
            DeviceDescriptorPopup popup,
            string methodName,
            params object[] args)
        {
            MethodInfo method = typeof(DeviceDescriptorPopup).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(popup, args);
        }

        private sealed class DeviceDescriptorPopupTestAsset : ScriptableObject
        {
            public DeviceDescriptor Descriptor;
        }
    }
}
