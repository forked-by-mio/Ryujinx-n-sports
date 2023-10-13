using Ryujinx.Horizon.Common;
using System;

namespace Ryujinx.Horizon.Sdk.Util
{
    static class MtRandom
    {
        private static readonly TinyMt _mt;

        static MtRandom()
        {
            // Official implementation uses random entropy from kernel as seed.
            // For now we don't bother.
            _mt = new();
            _mt.Initialize(21);
        }

        public static void GenerateRandomBytes(Span<byte> destination)
        {
            lock (_mt)
            {
                _mt.GenerateRandomBytes(destination);
            }
        }
    }
}