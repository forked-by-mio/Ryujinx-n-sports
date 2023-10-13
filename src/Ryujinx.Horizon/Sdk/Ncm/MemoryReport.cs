using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Ncm
{
    [StructLayout(LayoutKind.Sequential, Size = 0x20)]
    readonly struct MemoryResourceState
    {
        public readonly ulong PeakTotalAllocatedSize;
        public readonly ulong PeakAllocationSize;
        public readonly ulong AllocatableSize;
        public readonly ulong TotalFreeSize;

        public MemoryResourceState(
            ulong peakTotalAllocatedSize,
            ulong peakAllocationSize,
            ulong allocatableSize,
            ulong totalFreeSize)
        {
            PeakTotalAllocatedSize = peakTotalAllocatedSize;
            PeakAllocationSize = peakAllocationSize;
            AllocatableSize = allocatableSize;
            TotalFreeSize = totalFreeSize;
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 0x80)]
    readonly struct MemoryReport
    {
        public readonly MemoryResourceState SystemContentMetaResourceState;
        public readonly MemoryResourceState SdAndUserContentMetaResourceState;
        public readonly MemoryResourceState GameCardContentMetaResourceState;
        public readonly MemoryResourceState HeapResourceState;

        public MemoryReport(
            MemoryResourceState systemContentMetaResourceState,
            MemoryResourceState sdAndUserContentMetaResourceState,
            MemoryResourceState gameCardContentMetaResourceState,
            MemoryResourceState heapResourceState)
        {
            SystemContentMetaResourceState = systemContentMetaResourceState;
            SdAndUserContentMetaResourceState = sdAndUserContentMetaResourceState;
            GameCardContentMetaResourceState = gameCardContentMetaResourceState;
            HeapResourceState = heapResourceState;
        }
    }
}