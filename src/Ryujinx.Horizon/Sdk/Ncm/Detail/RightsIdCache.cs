
using Ryujinx.Common.Memory;
using System;

namespace Ryujinx.Horizon.Sdk.Ncm.Detail
{
    class RightsIdCache
    {
        private struct Entry
        {
            public UInt128 Uuid;
            public RightsId RightsId;
            public ulong LastAccessed;
        }

        private Array128<Entry> _entries;
        private ulong _counter;
        private readonly object _lock;

        public RightsIdCache()
        {
            _lock = new();
            Invalidate();
        }

        public void Invalidate()
        {
            _counter = 2;

            for (int i = 0; i < _entries.Length; i++)
            {
                _entries[i].LastAccessed = 1;
            }
        }

        public void Store(ContentId contentId, RightsId rightsId)
        {
            lock (_lock)
            {
                ref Entry evictionCandidate = ref _entries[0];

                for (int i = 1; i < _entries.Length; i++)
                {
                    ref Entry entry = ref _entries[i];

                    if (contentId.Id == entry.Uuid ||
                        (contentId.Id != evictionCandidate.Uuid && entry.LastAccessed < evictionCandidate.LastAccessed))
                    {
                        evictionCandidate = ref entry;
                    }
                }

                evictionCandidate.Uuid = contentId.Id;
                evictionCandidate.RightsId = rightsId;
                evictionCandidate.LastAccessed = _counter++;
            }
        }

        public bool Find(out RightsId rightsId, ContentId contentId)
        {
            lock (_lock)
            {
                ref Entry evictionCandidate = ref _entries[0];

                for (int i = 1; i < _entries.Length; i++)
                {
                    ref Entry entry = ref _entries[i];

                    if (entry.LastAccessed != 1 && entry.Uuid == contentId.Id)
                    {
                        entry.LastAccessed = _counter++;
                        rightsId = entry.RightsId;
                        return true;
                    }
                }
            }

            rightsId = default;
            return false;
        }
    }
}