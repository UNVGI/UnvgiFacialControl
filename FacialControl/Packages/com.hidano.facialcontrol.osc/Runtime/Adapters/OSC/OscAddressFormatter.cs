using System;

namespace Hidano.FacialControl.Adapters.OSC
{
    public static class OscAddressFormatter
    {
        public const string VRChatParameterPrefix = "/avatar/parameters/";
        public const char VRChatGazeXAxis = 'X';
        public const char VRChatGazeYAxis = 'Y';

        public static int GetBlendShapeAddressLength(
            AddressPresetKind preset,
            ReadOnlySpan<char> blendShapeName)
        {
            return GetBlendShapePrefix(preset).Length + blendShapeName.Length;
        }

        public static bool TryFormatBlendShapeAddress(
            AddressPresetKind preset,
            ReadOnlySpan<char> blendShapeName,
            Span<char> destination,
            out int charsWritten)
        {
            ReadOnlySpan<char> prefix = GetBlendShapePrefix(preset);
            charsWritten = prefix.Length + blendShapeName.Length;

            if (destination.Length < charsWritten)
            {
                return false;
            }

            prefix.CopyTo(destination);
            blendShapeName.CopyTo(destination.Slice(prefix.Length));
            return true;
        }

        public static string FormatBlendShapeAddress(AddressPresetKind preset, string blendShapeName)
        {
            switch (preset)
            {
                case AddressPresetKind.VRChat:
                    return VRChatParameterPrefix + (blendShapeName ?? string.Empty);
                default:
                    throw new NotSupportedException($"{preset} address preset is not implemented for sender output yet.");
            }
        }

        public static int GetGazeAddressLength(
            AddressPresetKind preset,
            ReadOnlySpan<char> expressionId,
            char axis)
        {
            ValidateGazeAxis(axis);
            return GetGazePrefix(preset).Length + expressionId.Length + 1;
        }

        public static bool TryFormatGazeAddress(
            AddressPresetKind preset,
            ReadOnlySpan<char> expressionId,
            char axis,
            Span<char> destination,
            out int charsWritten)
        {
            ValidateGazeAxis(axis);
            ReadOnlySpan<char> prefix = GetGazePrefix(preset);
            charsWritten = prefix.Length + expressionId.Length + 1;

            if (destination.Length < charsWritten)
            {
                return false;
            }

            prefix.CopyTo(destination);
            expressionId.CopyTo(destination.Slice(prefix.Length));
            destination[charsWritten - 1] = axis;
            return true;
        }

        public static string FormatGazeAddress(AddressPresetKind preset, string expressionId, char axis)
        {
            ValidateGazeAxis(axis);

            switch (preset)
            {
                case AddressPresetKind.VRChat:
                    return VRChatParameterPrefix + (expressionId ?? string.Empty) + axis;
                default:
                    throw new NotSupportedException($"{preset} address preset is not implemented for gaze sender output yet.");
            }
        }

        private static ReadOnlySpan<char> GetBlendShapePrefix(AddressPresetKind preset)
        {
            switch (preset)
            {
                case AddressPresetKind.VRChat:
                    return VRChatParameterPrefix.AsSpan();
                default:
                    throw new NotSupportedException($"{preset} address preset is not implemented for sender output yet.");
            }
        }

        private static ReadOnlySpan<char> GetGazePrefix(AddressPresetKind preset)
        {
            switch (preset)
            {
                case AddressPresetKind.VRChat:
                    return VRChatParameterPrefix.AsSpan();
                default:
                    throw new NotSupportedException($"{preset} address preset is not implemented for gaze sender output yet.");
            }
        }

        private static void ValidateGazeAxis(char axis)
        {
            if (axis != VRChatGazeXAxis && axis != VRChatGazeYAxis)
            {
                throw new ArgumentException("VRChat gaze axis must be X or Y.", nameof(axis));
            }
        }
    }
}
