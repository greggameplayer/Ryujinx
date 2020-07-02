using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ryujinx.Audio
{
    public static class DspUtils
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Channel51FormatPCM16
        {
            public short FrontLeft;
            public short FrontRight;
            public short FrontCenter;
            public short LowFrequency;
            public short BackLeft;
            public short BackRight;
        }

        public static short Saturate(int Value)
        {
            if (Value > short.MaxValue)
                Value = short.MaxValue;

            if (Value < short.MinValue)
                Value = short.MinValue;

            return (short)Value;
        }

        private const int RawQ15One = 1 << 16;
        private const int Minus3dBInQ15 = (int)(0.707f * RawQ15One);
        private const int Minus12dBInQ15 = (int)(0.251f * RawQ15One);

        private static int[] DefaultSurroundToStereoCoefficients = new int[4]
        {
            RawQ15One,
            Minus3dBInQ15,
            Minus12dBInQ15,
            Minus3dBInQ15,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReadOnlySpan<Channel51FormatPCM16> GetBuffer(ReadOnlySpan<short> data)
        {
            return MemoryMarshal.Cast<short, Channel51FormatPCM16>(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short DownMixSurroundToStereo(ReadOnlySpan<int> coefficients, short back, short lfe, short center, short front)
        {
            return (short)((coefficients[3] * back + coefficients[2] * lfe + coefficients[1] * center + coefficients[0] * front + 0x8000) >> 16);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short[] DownMixSurroundToStereo(ReadOnlySpan<int> coefficients, ReadOnlySpan<short> data)
        {
            const int SurroundChannelCount = 6;
            const int StereoChannelCount = 2;

            int samplePerChannelCount = data.Length / SurroundChannelCount;

            short[] downmixedBuffer = new short[samplePerChannelCount * StereoChannelCount];

            ReadOnlySpan<Channel51FormatPCM16> channels = GetBuffer(data);

            for (int i = 0; i < samplePerChannelCount; i++)
            {
                Channel51FormatPCM16 channel = channels[i];

                downmixedBuffer[i * 2] = DownMixSurroundToStereo(coefficients, channel.BackLeft, channel.LowFrequency, channel.FrontCenter, channel.FrontLeft);
                downmixedBuffer[i * 2 + 1] = DownMixSurroundToStereo(coefficients, channel.BackRight, channel.LowFrequency, channel.FrontCenter, channel.FrontRight);
            }

            return downmixedBuffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static short[] DownMixSurroundToStereo(ReadOnlySpan<short> data)
        {
            return DownMixSurroundToStereo(DefaultSurroundToStereoCoefficients, data);
        }
    }
}