using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Ryujinx.Audio
{
    public static class Downmixing
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Channel51FormatPCM16
        {
            public short FrontLeft;
            public short FrontRight;
            public short FrontCenter;
            public short LowFrequency;
            public short BackLeft;
            public short BackRight;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct ChannelStereoFormatPCM16
        {
            public short Left;
            public short Right;
        }

        private const int Q15Bits = 16;
        private const int RawQ15One = 1 << Q15Bits;
        private const int RawQ15HalfOne = (int)(0.5f * RawQ15One);
        private const int Minus3dBInQ15 = (int)(0.707f * RawQ15One);
        private const int Minus6dBInQ15 = (int)(0.501f * RawQ15One);
        private const int Minus12dBInQ15 = (int)(0.251f * RawQ15One);

        private static int[] DefaultSurroundToStereoCoefficients = new int[4]
        {
            RawQ15One,
            Minus3dBInQ15,
            Minus12dBInQ15,
            Minus3dBInQ15,
        };

        private static int[] DefaultStereoToMonoCoefficients = new int[2]
        {
            Minus6dBInQ15,
            Minus6dBInQ15,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReadOnlySpan<Channel51FormatPCM16> GetSurroundBuffer(ReadOnlySpan<short> data)
        {
            return MemoryMarshal.Cast<short, Channel51FormatPCM16>(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ReadOnlySpan<ChannelStereoFormatPCM16> GetStereoBuffer(ReadOnlySpan<short> data)
        {
            return MemoryMarshal.Cast<short, ChannelStereoFormatPCM16>(data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static short DownMixStereoToMono(ReadOnlySpan<int> coefficients, short left, short right)
        {
            return (short)((left * coefficients[0] + right * coefficients[1]) >> Q15Bits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static short DownMixSurroundToStereo(ReadOnlySpan<int> coefficients, short back, short lfe, short center, short front)
        {
            return (short)((coefficients[3] * back + coefficients[2] * lfe + coefficients[1] * center + coefficients[0] * front + RawQ15HalfOne) >> Q15Bits);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float[] ConvertCoefficients(ReadOnlySpan<int> coefficients)
        {
            float[] coeffs = new float[coefficients.Length];

            for (int i = 0; i < coefficients.Length; i++)
            {
                coeffs[i] = (float)coefficients[i] / (float)RawQ15One;
            }

            return coeffs;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static short[] DownMixSurroundToStereo(ReadOnlySpan<int> coefficients, ReadOnlySpan<short> data)
        {
            const int SurroundChannelCount = 6;
            const int StereoChannelCount = 2;

            int samplePerChannelCount = data.Length / SurroundChannelCount;

            short[] downmixedBuffer = new short[samplePerChannelCount * StereoChannelCount];

            int i = 0;

            ReadOnlySpan<Channel51FormatPCM16> channels = GetSurroundBuffer(data);

            if (Fma.IsSupported)
            {
                float[] coeffs = ConvertCoefficients(coefficients);

                Vector256<float> backCoefficient = Vector256.Create(coeffs[3]);
                Vector256<float> lowFrequencyCoefficient = Vector256.Create(coeffs[2]);
                Vector256<float> centerCoefficient = Vector256.Create(coeffs[1]);
                Vector256<float> frontCoefficient = Vector256.Create(coeffs[0]);

                unsafe
                {
                    fixed (short* ptr = downmixedBuffer)
                    {
                        for (; i < samplePerChannelCount / 4; i += 4)
                        {
                            ReadOnlySpan<Channel51FormatPCM16> currentChannels = channels.Slice(i, 4);

                            Vector256<float> center = Avx.ConvertToVector256Single(
                                Vector256.Create(currentChannels[0].FrontCenter, currentChannels[0].FrontCenter,
                                                 currentChannels[1].FrontCenter, currentChannels[1].FrontCenter,
                                                 currentChannels[2].FrontCenter, currentChannels[2].FrontCenter,
                                                 currentChannels[3].FrontCenter, currentChannels[3].FrontCenter));

                            Vector256<float> lowFrequency = Avx.ConvertToVector256Single(
                                Vector256.Create(currentChannels[0].LowFrequency, currentChannels[0].LowFrequency,
                                                 currentChannels[1].LowFrequency, currentChannels[1].LowFrequency,
                                                 currentChannels[2].LowFrequency, currentChannels[2].LowFrequency,
                                                 currentChannels[3].LowFrequency, currentChannels[3].LowFrequency));

                            Vector256<float> front = Avx.ConvertToVector256Single(
                                Vector256.Create(currentChannels[0].FrontLeft, currentChannels[0].FrontRight,
                                                 currentChannels[1].FrontLeft, currentChannels[1].FrontRight,
                                                 currentChannels[2].FrontLeft, currentChannels[2].FrontRight,
                                                 currentChannels[3].FrontLeft, currentChannels[3].FrontRight));

                            Vector256<float> back = Avx.ConvertToVector256Single(
                                Vector256.Create(currentChannels[0].BackLeft, currentChannels[0].BackRight,
                                                 currentChannels[1].BackLeft, currentChannels[1].BackRight,
                                                 currentChannels[2].BackLeft, currentChannels[2].BackRight,
                                                 currentChannels[3].BackLeft, currentChannels[3].BackRight));

                            Vector256<float> resultFloat = Vector256.Create(0.5f);

                            resultFloat = Fma.MultiplyAdd(back, backCoefficient, resultFloat);
                            resultFloat = Fma.MultiplyAdd(lowFrequency, lowFrequencyCoefficient, resultFloat);
                            resultFloat = Fma.MultiplyAdd(center, centerCoefficient, resultFloat);
                            resultFloat = Fma.MultiplyAdd(front, frontCoefficient, resultFloat);

                            Vector256<int> result = Avx.ConvertToVector256Int32(resultFloat);

                            Sse2.Store(ptr + i * 2, Sse2.PackSignedSaturate(result.GetLower(), result.GetUpper()));
                        }
                    }
                }
            }
            else if (Sse2.IsSupported)
            {
                float[] coeffs = ConvertCoefficients(coefficients);

                Vector128<float> backCoefficient = Vector128.Create(coeffs[3]);
                Vector128<float> lowFrequencyCoefficient = Vector128.Create(coeffs[2]);
                Vector128<float> centerCoefficient = Vector128.Create(coeffs[1]);
                Vector128<float> frontCoefficient = Vector128.Create(coeffs[0]);

                unsafe
                {
                    fixed (short* ptr = downmixedBuffer)
                    {
                        // NOTE: divide by 4 as we write 4 samples in total (the 2 last are zeroed)
                        for (; i < samplePerChannelCount / 4; i += 2)
                        {
                            ReadOnlySpan<Channel51FormatPCM16> currentChannels = channels.Slice(i, 2);

                            Vector128<float> center = Sse2.ConvertToVector128Single(
                                Vector128.Create(currentChannels[0].FrontCenter,
                                                 currentChannels[0].FrontCenter,
                                                 currentChannels[1].FrontCenter,
                                                 currentChannels[1].FrontCenter));

                            Vector128<float> lowFrequency = Sse2.ConvertToVector128Single(
                                Vector128.Create(currentChannels[0].LowFrequency,
                                                 currentChannels[0].LowFrequency,
                                                 currentChannels[1].LowFrequency,
                                                 currentChannels[1].LowFrequency));

                            Vector128<float> front = Sse2.ConvertToVector128Single(
                                Vector128.Create(currentChannels[0].FrontLeft,
                                                 currentChannels[0].FrontRight,
                                                 currentChannels[1].FrontLeft,
                                                 currentChannels[1].FrontRight));

                            Vector128<float> back = Sse2.ConvertToVector128Single(
                                Vector128.Create(currentChannels[0].BackLeft,
                                                 currentChannels[0].BackRight,
                                                 currentChannels[1].BackLeft,
                                                 currentChannels[1].BackRight));

                            Vector128<float> result = Vector128.Create(0.5f);

                            result = Sse.Add(result, Sse.Multiply(back, backCoefficient));
                            result = Sse.Add(result, Sse.Multiply(lowFrequency, lowFrequencyCoefficient));
                            result = Sse.Add(result, Sse.Multiply(center, centerCoefficient));
                            result = Sse.Add(result, Sse.Multiply(front, frontCoefficient));

                            Sse2.Store(ptr + i * 2,
                                       Sse2.PackSignedSaturate(Sse2.ConvertToVector128Int32(result), Vector128<int>.Zero));
                        }
                    }
                }
            }

            // The rest or fallback
            for (; i < samplePerChannelCount; i++)
            {
                Channel51FormatPCM16 channel = channels[i];

                downmixedBuffer[i * 2] = DownMixSurroundToStereo(coefficients, channel.BackLeft, channel.LowFrequency, channel.FrontCenter, channel.FrontLeft);
                downmixedBuffer[i * 2 + 1] = DownMixSurroundToStereo(coefficients, channel.BackRight, channel.LowFrequency, channel.FrontCenter, channel.FrontRight);
            }

            return downmixedBuffer;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static short[] DownMixStereoToMono(ReadOnlySpan<int> coefficients, ReadOnlySpan<short> data)
        {
            const int SurroundChannelCount = 2;
            const int StereoChannelCount = 1;

            int samplePerChannelCount = data.Length / SurroundChannelCount;

            short[] downmixedBuffer = new short[samplePerChannelCount * StereoChannelCount];

            int i = 0;

            ReadOnlySpan<ChannelStereoFormatPCM16> channels = GetStereoBuffer(data);

            if (Sse2.IsSupported)
            {
                float[] coeffs = ConvertCoefficients(coefficients);

                Vector128<float> leftCoefficient = Vector128.Create(coeffs[0]);
                Vector128<float> rightCoefficient = Vector128.Create(coeffs[1]);

                unsafe
                {
                    fixed (short* ptr = downmixedBuffer)
                    {
                        for (; i < samplePerChannelCount / 8; i += 8)
                        {
                            ReadOnlySpan<ChannelStereoFormatPCM16> currentChannels = channels.Slice(i, 8);

                            Vector128<float> left0 = Sse2.ConvertToVector128Single(
                                Vector128.Create(currentChannels[0].Left,
                                                 currentChannels[1].Left,
                                                 currentChannels[2].Left,
                                                 currentChannels[3].Left));

                            Vector128<float> right0 = Sse2.ConvertToVector128Single(
                                Vector128.Create(currentChannels[0].Right,
                                                 currentChannels[1].Right,
                                                 currentChannels[2].Right,
                                                 currentChannels[3].Right));

                            Vector128<float> left1 = Sse2.ConvertToVector128Single(
                                Vector128.Create(currentChannels[4].Left,
                                                 currentChannels[5].Left,
                                                 currentChannels[6].Left,
                                                 currentChannels[7].Left));

                            Vector128<float> right1 = Sse2.ConvertToVector128Single(
                                Vector128.Create(currentChannels[4].Right,
                                                 currentChannels[5].Right,
                                                 currentChannels[6].Right,
                                                 currentChannels[7].Right));

                            Vector128<float> result0 = Sse.Add(Sse.Multiply(left0, leftCoefficient), Sse.Multiply(right0, rightCoefficient));
                            Vector128<float> result1 = Sse.Add(Sse.Multiply(left1, leftCoefficient), Sse.Multiply(right1, rightCoefficient));

                            Sse2.Store(ptr + i, Sse2.PackSignedSaturate(Sse2.ConvertToVector128Int32(result0), Sse2.ConvertToVector128Int32(result1)));
                        }
                    }
                }
            }

            // The rest or fallback
            for (; i < samplePerChannelCount; i++)
            {
                ChannelStereoFormatPCM16 channel = channels[i];

                downmixedBuffer[i] = DownMixStereoToMono(coefficients, channel.Left, channel.Right);
            }

            return downmixedBuffer;
        }

        public static short[] DownMixStereoToMono(ReadOnlySpan<short> data)
        {
            return DownMixStereoToMono(DefaultStereoToMonoCoefficients, data);
        }

        public static short[] DownMixSurroundToStereo(ReadOnlySpan<short> data)
        {
            return DownMixSurroundToStereo(DefaultSurroundToStereoCoefficients, data);
        }
    }
}
