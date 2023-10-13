using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Fs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Kvdb
{
    class MemoryKeyValueStore<TKey> : IEnumerable<MemoryKeyValueStore<TKey>.Entry> where TKey : unmanaged, IComparable<TKey>, IEquatable<TKey>
    {
        // Each entry has a pointer and size field, in addition to the key. Each one of those fields takes 8 bytes.
        private static readonly int _entrySizeInBytes = Unsafe.SizeOf<TKey>() + 0x10;

        public readonly struct Entry
        {
            public TKey Key { get; }
            public byte[] Value { get; }

            public Entry(in TKey key, byte[] value)
            {
                Key = key;
                Value = value;
            }
        }

        private class Index : IEnumerable<Entry>
        {
            private int _count;
            private int _capacity;
            private Entry[] _entries;
            private MemoryResource _memoryResource;

            public ref Entry this[int index] => ref _entries[index];

            public int Count => _count;

            public Result Initialize(int capacity, MemoryResource mr)
            {
                _entries = mr.Allocate<Entry>(capacity, _entrySizeInBytes);

                if (_entries == null)
                {
                    return KvdbResult.AllocationFailed;
                }

                _capacity = capacity;
                _memoryResource = mr;

                return Result.Success;
            }

            public Result Set(in TKey key, ReadOnlySpan<byte> value)
            {
                int entryIndex = GetLowerBoundIndex(key);

                if ((uint)entryIndex < (uint)_count && _entries[entryIndex].Key.Equals(key))
                {
                    _memoryResource.Deallocate(_entries[entryIndex].Value);
                }
                else
                {
                    if (_count >= _capacity)
                    {
                        return KvdbResult.OutOfKeyResource;
                    }

                    Array.Copy(_entries, entryIndex, _entries, entryIndex + 1, _count - entryIndex);
                    _count++;
                }

                byte[] newValue = _memoryResource.Allocate(value);

                if (newValue == null)
                {
                    return KvdbResult.AllocationFailed;
                }

                _entries[entryIndex] = new(key, newValue);

                return Result.Success;
            }

            public Result AddUnsafe(in TKey key, byte[] value)
            {
                if (_count >= _capacity)
                {
                    return KvdbResult.OutOfKeyResource;
                }

                _entries[_count++] = new(key, value);

                return Result.Success;
            }

            public Result Remove(in TKey key)
            {
                int entryIndex = FindIndex(key);

                if (entryIndex < 0)
                {
                    return KvdbResult.KeyNotFound;
                }

                ref Entry entry = ref _entries[entryIndex];

                _memoryResource.Deallocate(entry.Value);
                Array.Copy(_entries, entryIndex + 1, _entries, entryIndex, _count - (entryIndex + 1));
                _count--;

                return Result.Success;
            }

            public void Destroy()
            {
                if (_entries != null)
                {
                    ResetEntries();
                    _memoryResource.Deallocate(_entries, _entrySizeInBytes);
                    _entries = null;
                }
            }

            public void ResetEntries()
            {
                for (int i = 0; i < _count; i++)
                {
                    _memoryResource.Deallocate(_entries[i].Value);
                }

                _count = 0;
            }

            public int FindIndex(in TKey key)
            {
                int entryIndex = GetLowerBoundIndex(key);

                if ((uint)entryIndex < (uint)_count && _entries[entryIndex].Key.Equals(key))
                {
                    return entryIndex;
                }

                return -1;
            }

            public int GetLowerBoundIndex(in TKey key)
            {
                ReadOnlySpan<Entry> entries = _entries.AsSpan(0, _count);

                int left = 0;
                int right = entries.Length - 1;

                while (left <= right)
                {
                    int middle = (int)(((uint)right + (uint)left) >> 1);

                    int c = key.CompareTo(entries[middle].Key);
                    if (c == 0)
                    {
                        return middle;
                    }
                    else if (c > 0)
                    {
                        left = middle + 1;
                    }
                    else
                    {
                        right = middle - 1;
                    }
                }

                // If not found, return the index of the first element that is greater than key.
                return left;
            }

            public IEnumerator<Entry> GetEnumerator()
            {
                for (int index = 0; index < _count; index++)
                {
                    yield return _entries[index];
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        private readonly IFsClient _fs;
        private Index _index;
        private string _path;
        private string _tempPath;
        private MemoryResource _memoryResource;

        public int Count => _index.Count;

        public ref Entry this[int index] => ref _index[index];

        public MemoryKeyValueStore(IFsClient fs)
        {
            _fs = fs;
        }

        public Result Initialize(string dir, int capacity, MemoryResource mr)
        {
            Result result = _fs.GetEntryType(out DirectoryEntryType entryType, dir);

            if (result.IsFailure)
            {
                return result;
            }

            if (entryType != DirectoryEntryType.Directory)
            {
                return FsResult.PathNotFound;
            }

            _path = $"{dir}/imkvdb.arc";
            _tempPath = $"{dir}/imkvdb.tmp";

            _index = new();
            result = _index.Initialize(capacity, mr);

            if (result.IsFailure)
            {
                return result;
            }

            _memoryResource = mr;

            return Result.Success;
        }

        public Result InitializeForReadOnlyArchiveFile(string path, int capacity, MemoryResource mr)
        {
            Result result = _fs.GetEntryType(out DirectoryEntryType entryType, path);

            if (result.IsFailure)
            {
                return result;
            }

            if (entryType != DirectoryEntryType.File)
            {
                return FsResult.PathNotFound;
            }

            _path = path;
            _tempPath = string.Empty;

            _index = new();
            result = _index.Initialize(capacity, mr);

            if (result.IsFailure)
            {
                return result;
            }

            _memoryResource = mr;

            return Result.Success;
        }

        public Result Initialize(int capacity, MemoryResource mr)
        {
            _path = string.Empty;
            _tempPath = string.Empty;

            _index = new();
            Result result = _index.Initialize(capacity, mr);

            if (result.IsFailure)
            {
                return result;
            }

            _memoryResource = mr;

            return Result.Success;
        }

        public Result Load()
        {
            _index.ResetEntries();

            Result result = ReadArchiveFile(out byte[] archive);

            if (result == FsResult.PathNotFound)
            {
                // If there is no file, no entries were saved yet. That case is fine too.
                return Result.Success;
            }
            else if (result.IsFailure)
            {
                return result;
            }

            ArchiveReader reader = new(archive.AsSpan());

            result = reader.ReadEntryCount(out uint entryCount);

            if (result.IsFailure)
            {
                return result;
            }

            for (uint i = 0; i < entryCount; i++)
            {
                result = reader.GetEntrySize(out uint keySize, out uint valueSize);

                if (result.IsFailure)
                {
                    return result;
                }

                byte[] newValue = _memoryResource.Allocate(valueSize);

                if (newValue == null)
                {
                    return KvdbResult.AllocationFailed;
                }

                try
                {
                    result = reader.ReadEntry(out TKey key, newValue.AsSpan());

                    if (result.IsFailure)
                    {
                        return result;
                    }

                    result = _index.AddUnsafe(key, newValue);

                    if (result.IsFailure)
                    {
                        return result;
                    }
                }
                finally
                {
                    if (result.IsFailure)
                    {
                        _memoryResource.Deallocate(newValue);
                    }
                }
            }

            return Result.Success;
        }

        public Result Save(bool destructive = false)
        {
            byte[] buffer = new byte[GetArchiveSize()];

            ArchiveWriter writer = new(buffer);

            writer.WriteHeader((uint)Count);

            foreach (Entry entry in _index)
            {
                writer.WriteEntry(entry.Key, entry.Value);
            }

            return Commit(buffer, destructive);
        }

        public Result Set<TValue>(in TKey key, TValue value) where TValue : unmanaged
        {
            return _index.Set(key, MemoryMarshal.Cast<TValue, byte>(MemoryMarshal.CreateSpan(ref value, 1)));
        }

        public Result Set(in TKey key, ReadOnlySpan<byte> value)
        {
            return _index.Set(key, value);
        }

        public Result Get(out int size, in TKey key, Span<byte> destination)
        {
            int entryIndex = _index.FindIndex(key);

            if (entryIndex < 0)
            {
                size = 0;
                return KvdbResult.KeyNotFound;
            }

            ref Entry entry = ref _index[entryIndex];

            size = Math.Min(entry.Value.Length, destination.Length);
            entry.Value.AsSpan()[..size].CopyTo(destination[..size]);

            return Result.Success;
        }

        public Result GetValue(out byte[] value, in TKey key)
        {
            int entryIndex = _index.FindIndex(key);

            if (entryIndex < 0)
            {
                value = default;
                return KvdbResult.KeyNotFound;
            }

            ref Entry entry = ref _index[entryIndex];

            value = entry.Value;
            return Result.Success;
        }

        public Result GetValue<TValue>(out TValue value, in TKey key) where  TValue : unmanaged
        {
            int entryIndex = _index.FindIndex(key);

            if (entryIndex < 0)
            {
                value = default;
                return KvdbResult.KeyNotFound;
            }

            ref Entry entry = ref _index[entryIndex];

            value = MemoryMarshal.Cast<byte, TValue>(entry.Value.AsSpan())[0];
            return Result.Success;
        }

        public Result GetValueSize(out int size, in TKey key)
        {
            int entryIndex = _index.FindIndex(key);

            if (entryIndex < 0)
            {
                size = 0;
                return KvdbResult.KeyNotFound;
            }

            ref Entry entry = ref _index[entryIndex];

            size = entry.Value.Length;
            return Result.Success;
        }

        public Result Remove(in TKey key)
        {
            return _index.Remove(key);
        }

        private Result SaveArchiveToFile(string path, ReadOnlySpan<byte> data)
        {
            _fs.DeleteFile(path);

            Result result = _fs.CreateFile(path, data.Length);

            if (result.IsFailure)
            {
                return result;
            }

            result = _fs.OpenFile(out FileHandle handle, path, OpenMode.Write);

            if (result.IsFailure)
            {
                return result;
            }

            try
            {
                result = _fs.WriteFile(handle, 0, data, WriteOption.Flush);

                if (result.IsFailure)
                {
                    return result;
                }
            }
            finally
            {
                _fs.CloseFile(handle);
            }

            return Result.Success;
        }

        private Result Commit(ReadOnlySpan<byte> data, bool destructive)
        {
            if (destructive)
            {
                Result result = SaveArchiveToFile(_path, data);

                if (result.IsFailure)
                {
                    return result;
                }
            }
            else
            {
                Result result = SaveArchiveToFile(_tempPath, data);

                if (result.IsFailure)
                {
                    return result;
                }

                _fs.DeleteFile(_path);

                result = _fs.RenameFile(_tempPath, _path);

                if (result.IsFailure)
                {
                    return result;
                }
            }

            return Result.Success;
        }

        private int GetArchiveSize()
        {
            int size = Unsafe.SizeOf<ArchiveHeader>();

            foreach (Entry entry in _index)
            {
                size += Unsafe.SizeOf<ArchiveEntryHeader>() + Unsafe.SizeOf<TKey>() + entry.Value.Length;
            }

            return size;
        }

        private Result ReadArchiveFile(out byte[] buffer)
        {
            buffer = null;

            Result result = _fs.OpenFile(out FileHandle handle, _path, OpenMode.Read);

            if (result.IsFailure)
            {
                return result;
            }

            try
            {
                result = _fs.GetFileSize(out long archiveSize, handle);

                if (result.IsFailure)
                {
                    return result;
                }

                // Make sure we won't try to create a buffer with nonsensical size.
                if (archiveSize < 0 || archiveSize >= int.MaxValue)
                {
                    return KvdbResult.AllocationFailed;
                }

                buffer = new byte[archiveSize];

                result = _fs.ReadFile(handle, 0, buffer.AsSpan());

                if (result.IsFailure)
                {
                    return result;
                }
            }
            finally
            {
                _fs.CloseFile(handle);
            }

            return Result.Success;
        }

        public int GetLowerBoundIndex(in TKey key)
        {
            return _index.GetLowerBoundIndex(key);
        }

        public IEnumerator<Entry> GetEnumerator()
        {
            return _index.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}