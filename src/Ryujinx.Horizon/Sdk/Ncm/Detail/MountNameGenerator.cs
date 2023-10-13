using System.Threading;

namespace Ryujinx.Horizon.Sdk.Ncm.Detail
{
    class MountNameGenerator
    {
        private int _mountNameCount;

        public string CreateUniqueMountName()
        {
            return $"@ncm{Interlocked.Increment(ref _mountNameCount) - 1:x8}";
        }
    }
}