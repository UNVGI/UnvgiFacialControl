using System;
using Hidano.FacialControl.LipSync.Adapters.Devices;

namespace Hidano.FacialControl.LipSync.Tests.Shared
{
    internal sealed class FakePlayerPrefsBackend : IPlayerPrefsBackend
    {
        public string GetString(string key, string defaultValue)
        {
            throw new NotImplementedException();
        }

        public int GetInt(string key, int defaultValue)
        {
            throw new NotImplementedException();
        }

        public void SetString(string key, string value)
        {
            throw new NotImplementedException();
        }

        public void SetInt(string key, int value)
        {
            throw new NotImplementedException();
        }

        public void Save()
        {
            throw new NotImplementedException();
        }
    }
}
