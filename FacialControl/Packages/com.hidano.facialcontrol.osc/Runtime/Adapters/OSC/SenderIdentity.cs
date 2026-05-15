using System;

namespace Hidano.FacialControl.Adapters.OSC
{
    /// <summary>
    /// OSC 送信元を識別する UUID と起動時刻の組。
    /// </summary>
    public readonly struct SenderIdentity : IEquatable<SenderIdentity>
    {
        public readonly Guid SenderId;
        public readonly long StartedAtUnixMs;

        public SenderIdentity(Guid senderId, long startedAtUnixMs)
        {
            if (senderId == Guid.Empty)
            {
                throw new ArgumentException("Sender UUID must not be empty.", nameof(senderId));
            }

            if (startedAtUnixMs < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(startedAtUnixMs), startedAtUnixMs,
                    "StartedAtUnixMs must be greater than or equal to zero.");
            }

            SenderId = senderId;
            StartedAtUnixMs = startedAtUnixMs;
        }

        public bool Equals(SenderIdentity other)
        {
            return SenderId.Equals(other.SenderId)
                && StartedAtUnixMs == other.StartedAtUnixMs;
        }

        public override bool Equals(object obj)
        {
            return obj is SenderIdentity other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (SenderId.GetHashCode() * 397) ^ StartedAtUnixMs.GetHashCode();
            }
        }

        public override string ToString()
        {
            return $"{SenderId:D}@{StartedAtUnixMs}";
        }

        public static bool operator ==(SenderIdentity left, SenderIdentity right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(SenderIdentity left, SenderIdentity right)
        {
            return !left.Equals(right);
        }
    }
}
