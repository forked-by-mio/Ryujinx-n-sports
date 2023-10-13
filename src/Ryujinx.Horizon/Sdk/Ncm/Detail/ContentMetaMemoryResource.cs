using System;

namespace Ryujinx.Horizon.Sdk.Ncm.Detail
{
    class ContentMetaMemoryResource : MemoryResource
    {
        public uint PeakTotalAllocatedSize { get; private set; }
        public uint PeakAllocationSize { get; private set; }

        public ContentMetaMemoryResource(uint capacity) : base(capacity)
        {
        }

        protected override bool CanAllocate(uint length)
        {
            bool canAllocate = base.CanAllocate(length);

            PeakTotalAllocatedSize = Math.Max(PeakTotalAllocatedSize, AllocatedSize);
            PeakAllocationSize = Math.Max(PeakAllocationSize, length);

            return canAllocate;
        }

        public MemoryResourceState ToMemoryResourceState()
        {
            return new(PeakTotalAllocatedSize, PeakAllocationSize, AllocatableSize, TotalFreeSize);
        }
    }
}