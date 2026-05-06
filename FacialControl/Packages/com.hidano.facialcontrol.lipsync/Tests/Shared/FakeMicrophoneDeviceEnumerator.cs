using System;

namespace Hidano.FacialControl.LipSync.Tests.Shared
{
    public sealed class FakeMicrophoneDeviceEnumerator
    {
        private readonly string[] _deviceNames;

        public FakeMicrophoneDeviceEnumerator(params string[] deviceNames)
        {
            if (deviceNames == null)
            {
                throw new ArgumentNullException(nameof(deviceNames));
            }

            _deviceNames = (string[])deviceNames.Clone();
        }

        public string[] GetDeviceNames()
        {
            return _deviceNames;
        }
    }
}
