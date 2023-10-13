using Ryujinx.Common.Memory;
using System;
using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Common
{
    public class TinyMt
    {
        private const int StateWordsCount = 4;

        private struct State
        {
            public Array4<uint> Data;
        }

        private State _state;

        private const uint ParamMat1 = 0x8F7011EE;
        private const uint ParamMat2 = 0xFC78FF1F;
        private const uint ParamTmat = 0x3793FDFF;

        private const uint ParamMult = 0x6C078965;
        private const uint ParamPlus = 0x0019660D;
        private const uint ParamXor = 0x5D588B65;

        private const uint TopBitmask = 0x7FFFFFFF;

        private const int MinimumInitIterations = 8;
        private const int NumDiscardedInitOutputs = 8;

        private static uint XorByShifted27(uint value)
        {
            return value ^ (value >> 27);
        }

        private static uint XorByShifted30(uint value)
        {
            return value ^ (value >> 30);
        }

        public void Initialize(uint seed)
        {
            _state.Data[0] = seed;
            _state.Data[1] = ParamMat1;
            _state.Data[2] = ParamMat2;
            _state.Data[3] = ParamTmat;

            for (int i = 1; i < MinimumInitIterations; i++)
            {
                uint mixed = XorByShifted30(_state.Data[(i - 1) % StateWordsCount]);
                _state.Data[i % StateWordsCount] ^= (uint)(mixed * ParamMult + i);
            }

            FinalizeInitialization();
        }

        public void Initialize(ReadOnlySpan<uint> seed)
        {
            _state.Data[0] = 0;
            _state.Data[1] = ParamMat1;
            _state.Data[2] = ParamMat2;
            _state.Data[3] = ParamTmat;

            int initIterationsCount = Math.Max(seed.Length + 1, MinimumInitIterations) - 1;

            GenerateInitialValuePlus(ref _state, 0, (uint)seed.Length);

            for (int i = 0; i < initIterationsCount; i++)
            {
                GenerateInitialValuePlus(ref _state, (i + 1) % StateWordsCount, (i < seed.Length) ? seed[i] : 0);
            }

            for (int i = 0; i < StateWordsCount; i++)
            {
                GenerateInitialValueXor(ref _state, (i + 1 + initIterationsCount) % StateWordsCount);
            }

            FinalizeInitialization();
        }

        private void FinalizeInitialization()
        {
            uint state0 = _state.Data[0] & TopBitmask;
            uint state1 = _state.Data[1];
            uint state2 = _state.Data[2];
            uint state3 = _state.Data[3];

            if (state0 == 0 && state1 == 0 && state2 == 0 && state3 == 0)
            {
                _state.Data[0] = 'T';
                _state.Data[1] = 'I';
                _state.Data[2] = 'N';
                _state.Data[3] = 'Y';
            }

            for (int i = 0; i < NumDiscardedInitOutputs; i++)
            {
                GenerateRandomUInt32();
            }
        }

        public void GenerateRandomBytes(Span<byte> destination)
        {
            Span<uint> destinationUint = MemoryMarshal.Cast<byte, uint>(destination);

            for (int i = 0; i < destinationUint.Length; i++)
            {
                destinationUint[i] = GenerateRandomUInt32();
            }

            if ((destination.Length & 3) != 0)
            {
                uint lastRandom = GenerateRandomUInt32();

                for (int i = destinationUint.Length * sizeof(uint); i < destination.Length; i++)
                {
                    destination[i] = (byte)lastRandom;
                    lastRandom >>= 8;
                }
            }
        }

        public uint GenerateRandomUInt32()
        {
            uint x0 = (_state.Data[0] & TopBitmask) ^ _state.Data[1] ^ _state.Data[2];
            uint y0 = _state.Data[3];
            uint x1 = x0 ^ (x0 << 1);
            uint y1 = y0 ^ (y0 >> 1) ^ x1;

            uint state0 = _state.Data[1];
            uint state1 = _state.Data[2];
            uint state2 = x1 ^ (y1 << 10);
            uint state3 = y1;

            if ((y1 & 1) != 0)
            {
                state1 ^= ParamMat1;
                state2 ^= ParamMat2;
            }

            _state.Data[0] = state0;
            _state.Data[1] = state1;
            _state.Data[2] = state2;
            _state.Data[3] = state3;

            uint t1 = state0 + (state2 >> 8);
            uint t0 = state3 ^ t1;

            if ((t1 & 1) != 0)
            {
                t0 ^= ParamTmat;
            }

            return t0;
        }

        public ulong GenerateRandomUInt64()
        {
            uint lo = GenerateRandomUInt32();
            uint hi = GenerateRandomUInt32();
            return ((ulong)hi << 32) | lo;
        }

        private static void GenerateInitialValuePlus(ref State state, int index, uint value)
        {
            ref uint state0 = ref state.Data[(index + 0) % StateWordsCount];
            ref uint state1 = ref state.Data[(index + 1) % StateWordsCount];
            ref uint state2 = ref state.Data[(index + 2) % StateWordsCount];
            ref uint state3 = ref state.Data[(index + 3) % StateWordsCount];

            uint x = XorByShifted27(state0 ^ state1 ^ state3) * ParamPlus;
            uint y = (uint)(x + index + value);

            state0 = y;
            state1 += x;
            state2 += y;
        }

        private void GenerateInitialValueXor(ref State state, int index)
        {
            ref uint state0 = ref state.Data[(index + 0) % StateWordsCount];
            ref uint state1 = ref state.Data[(index + 1) % StateWordsCount];
            ref uint state2 = ref state.Data[(index + 2) % StateWordsCount];
            ref uint state3 = ref state.Data[(index + 3) % StateWordsCount];

            uint x = XorByShifted27(state0 + state1 + state3) * ParamXor;
            uint y = (uint)(x - index);

            state0 = y;
            state1 ^= x;
            state2 ^= y;
        }
    }
}