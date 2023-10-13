using Ryujinx.Horizon.Sdk.Ncm;
using System;

namespace Ryujinx.Horizon.Sdk.Lr
{
    class RegisteredData<TKey, TValue> where TKey : IEquatable<TKey>
    {
        private struct Entry
        {
            public readonly TValue Value;
            public readonly ProgramId OwnerId;
            public readonly TKey Key;
            public bool IsValid;

            public Entry(TValue value, ProgramId ownerId, TKey key)
            {
                Value = value;
                OwnerId = ownerId;
                Key = key;
                IsValid = true;
            }
        }

        private readonly Entry[] _entries;
        private readonly int _capacity;

        public RegisteredData(int maxEntries)
        {
            _entries = new Entry[maxEntries];
            _capacity = maxEntries;
        }

        public bool Register(TKey key, TValue value, ProgramId programId)
        {
            for (int i = 0; i < _capacity; i++)
            {
                ref Entry entry = ref _entries[i];

                if (entry.IsValid && entry.Key.Equals(key))
                {
                    entry = new(value, programId, key);

                    return true;
                }
            }

            for (int i = 0; i < _capacity; i++)
            {
                ref Entry entry = ref _entries[i];

                if (!entry.IsValid)
                {
                    entry = new(value, programId, key);

                    return true;
                }
            }

            return false;
        }

        public void Unregister(TKey key)
        {
            for (int i = 0; i < _capacity; i++)
            {
                ref Entry entry = ref _entries[i];

                if (entry.IsValid && entry.Key.Equals(key))
                {
                    entry.IsValid = false;
                }
            }
        }

        public void UnregisterOwnerProgram(ProgramId ownerId)
        {
            for (int i = 0; i < _capacity; i++)
            {
                ref Entry entry = ref _entries[i];

                if (entry.OwnerId == ownerId)
                {
                    entry.IsValid = false;
                }
            }
        }

        public bool Find(out TValue value, TKey key)
        {
            for (int i = 0; i < _capacity; i++)
            {
                ref Entry entry = ref _entries[i];

                if (entry.IsValid && entry.Key.Equals(key))
                {
                    value = entry.Value;

                    return true;
                }
            }

            value = default;

            return false;
        }

        public void Clear()
        {
            for (int i = 0; i < _capacity; i++)
            {
                _entries[i].IsValid = false;
            }
        }

        public void ClearExcluding(ReadOnlySpan<ProgramId> excludingIds)
        {
            for (int i = 0; i < _capacity; i++)
            {
                ref Entry entry = ref _entries[i];

                if (!excludingIds.Contains(entry.OwnerId))
                {
                    entry.IsValid = false;
                }
            }
        }
    }
}