using System;

namespace Hidano.FacialControl.LipSync.Tests.Shared
{
    public sealed class FakeAsioDriverEnumerator
    {
        private readonly string[] _driverNames;

        public FakeAsioDriverEnumerator(params string[] driverNames)
        {
            if (driverNames == null)
            {
                throw new ArgumentNullException(nameof(driverNames));
            }

            _driverNames = (string[])driverNames.Clone();
        }

        public string[] GetDriverNames()
        {
            return _driverNames;
        }
    }
}
