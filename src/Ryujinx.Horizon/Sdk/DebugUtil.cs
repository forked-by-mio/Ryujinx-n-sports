using System.Diagnostics;

namespace Ryujinx.Horizon.Sdk
{
    static class DebugUtil
    {
        public static void Abort()
        {
            Debug.Fail("Aborted.");
        }

        public static void Assert(bool condition)
        {
            Debug.Assert(condition);
        }

        public static void Unreachable()
        {
            Debug.Fail("Tried to execute unreachable code.");
        }
    }
}
