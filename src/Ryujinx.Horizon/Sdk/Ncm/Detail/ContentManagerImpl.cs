using Ryujinx.Common.Memory;
using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Fs;
using Ryujinx.Horizon.Sdk.Kvdb;
using Ryujinx.Horizon.Sdk.Sf;
using System;

namespace Ryujinx.Horizon.Sdk.Ncm.Detail
{
    partial class ContentManagerImpl : IContentManager, IDisposable
    {
        private const ulong BuiltInSystemSaveDataId = 0x8000000000000120;
        private const SaveDataFlags BuiltInSystemSaveDataFlags = SaveDataFlags.KeepAfterResettingSystemSaveData | SaveDataFlags.KeepAfterRefurbishment;

        private readonly SystemSaveDataInfo _builtInSystemSystemSaveDataInfo = new(
            BuiltInSystemSaveDataId,
            0x6c000,
            0x6c000,
            BuiltInSystemSaveDataFlags,
            SaveDataSpaceId.System);

        private readonly SystemSaveDataInfo _builtInUserSystemSaveDataInfo = new(
            0x8000000000000121,
            0x29e000,
            0x29e000,
            SaveDataFlags.None,
            SaveDataSpaceId.System);

        private readonly SystemSaveDataInfo _sdCardSystemSaveDataInfo = new(
            0x8000000000000124,
            0xa08000,
            0xa08000,
            SaveDataFlags.None,
            SaveDataSpaceId.SdSystem);

        private const int SystemMaxContentMetaCount = 0x800;
        private const int GameCardMaxContentMetaCount = 0x800;
        private const int HostMaxContentMetaCount = 0x800;
        private const int UserMaxContentMetaCount = 0x2000;
        private const int SdCardMaxContentMetaCount = 0x2000;

        private const int MaxContentStorageRoots = 8;
        private const int MaxIntegratedContentStorageRoots = 8;
        private const int MaxContentMetaDatabaseRoots = 8;
        private const int MaxIntegratedContentMetaDatabaseRoots = 8;
        private const int MaxConfigs = 8;
        private const int MaxIntegratedConfigs = 8;

        private readonly MountNameGenerator _mng;
        private readonly IFsClient _fs;

        private readonly object _lock;

        private bool _initialized;

        private readonly struct SystemSaveDataInfo
        {
            public readonly ulong Id;
            public readonly ulong Size;
            public readonly ulong JournalSize;
            public readonly SaveDataFlags Flags;
            public readonly SaveDataSpaceId SpaceId;

            public SystemSaveDataInfo(ulong id, ulong size, ulong journalSize, SaveDataFlags flags, SaveDataSpaceId spaceId)
            {
                Id = id;
                Size = size;
                JournalSize = journalSize;
                Flags = flags;
                SpaceId = spaceId;
            }
        }

        private struct ContentStorageConfig
        {
            public ContentStorageId ContentStorageId;
            public bool SkipVerifyAndCreate;
            public bool SkipActivate;
        }

        private struct IntegratedContentStorageConfig
        {
            public StorageId StorageId;
            public Array8<ContentStorageId> ContentStorageIds;
            public int ContentStorageIdsCount;
            public bool IsIntegrated;

            public IntegratedContentStorageConfig(StorageId storageId, ReadOnlySpan<ContentStorageId> contentStorageIds, bool isIntegrated)
            {
                StorageId = storageId;
                contentStorageIds.CopyTo(ContentStorageIds.AsSpan()[..contentStorageIds.Length]);
                ContentStorageIdsCount = contentStorageIds.Length;
                IsIntegrated = isIntegrated;
            }
        }

        private struct ContentStorageRoot
        {
            public string MountName;
            public string Path;
            public StorageId StorageId;
            public ContentStorageConfig? Config;
            public IContentStorage ContentStorage;
        }

        private struct IntegratedContentStorageRoot
        {
            public IntegratedContentStorageConfig Config;

            private readonly MountNameGenerator _mng;
            private readonly IFsClient _fs;
            public ContentStorageRoot[] Roots;
            private IntegratedContentStorageImpl _integratedContentStorage;

            public IntegratedContentStorageRoot(MountNameGenerator mng, IFsClient fs)
            {
                _mng = mng;
                _fs = fs;
            }

            public readonly Result Create()
            {
                for (int i = 0; i < Roots.Length; i++)
                {
                    var root = Roots[i];

                    if (!root.Config.HasValue || root.Config.Value.SkipVerifyAndCreate)
                    {
                        continue;
                    }

                    Result result = _fs.MountContentStorage(root.MountName, root.Config.Value.ContentStorageId);

                    if (result.IsFailure)
                    {
                        return result;
                    }

                    try
                    {
                        result = _fs.EnsureDirectory(root.Path);

                        if (result.IsFailure)
                        {
                            return result;
                        }

                        result = ContentStorageImpl.InitializeBase(_fs, root.Path);

                        if (result.IsFailure)
                        {
                            return result;
                        }
                    }
                    finally
                    {
                        _fs.Unmount(root.MountName);
                    }
                }

                return Result.Success;
            }

            public readonly Result Verify()
            {
                for (int i = 0; i < Roots.Length; i++)
                {
                    var root = Roots[i];

                    if (!root.Config.HasValue || root.Config.Value.SkipVerifyAndCreate)
                    {
                        continue;
                    }

                    string mountName = _mng.CreateUniqueMountName();
                    ReplaceMountName(out string path, mountName, root.Path);

                    Result result = _fs.MountContentStorage(mountName, root.Config.Value.ContentStorageId);

                    if (result.IsFailure)
                    {
                        return result;
                    }

                    try
                    {
                        result = ContentStorageImpl.VerifyBase(_fs, path);

                        if (result.IsFailure)
                        {
                            return result;
                        }
                    }
                    finally
                    {
                        _fs.Unmount(mountName);
                    }
                }

                return Result.Success;
            }

            public readonly Result Open(out IContentStorage storage)
            {
                bool hasInterface = Config.IsIntegrated
                    ? _integratedContentStorage != null
                    : Roots[0].ContentStorage != null;

                if (!hasInterface)
                {
                    storage = null;
                    return GetContentStorageNotActiveResult(Config.StorageId);
                }

                storage = Config.IsIntegrated ? _integratedContentStorage : Roots[0].ContentStorage;
                return Result.Success;
            }

            public Result Activate(RightsIdCache rightsIdCache, RegisteredHostContent registeredHostContent)
            {
                if (!Config.IsIntegrated && _integratedContentStorage == null)
                {
                    _integratedContentStorage = new();
                }

                for (int i = 0; i < Roots.Length; i++)
                {
                    ref ContentStorageRoot root = ref Roots[i];

                    if (root.Config.HasValue && root.Config.Value.SkipActivate)
                    {
                        continue;
                    }

                    Activate(ref root, rightsIdCache, registeredHostContent);

                    if (Config.IsIntegrated)
                    {
                        int index;
                        for (index = 0; index < Config.ContentStorageIdsCount; index++)
                        {
                            if (Config.ContentStorageIds[index] == root.Config.Value.ContentStorageId)
                            {
                                break;
                            }
                        }

                        _integratedContentStorage.Add(root.ContentStorage, (byte)(index + 1));
                    }
                }

                return Result.Success;
            }

            public Result Inactivate(RegisteredHostContent registeredHostContent)
            {
                if (_integratedContentStorage != null)
                {
                    Result result = _integratedContentStorage.DisableForcibly();

                    if (result.IsFailure)
                    {
                        return result;
                    }

                    _integratedContentStorage = null;
                }

                for (int i = 0; i < Roots.Length; i++)
                {
                    var root = Roots[i];

                    if (root.ContentStorage != null)
                    {
                        root.ContentStorage.DisableForcibly();
                        root.ContentStorage = null;

                        if (root.StorageId == StorageId.Host)
                        {
                            registeredHostContent.ClearPaths();
                        }
                        else
                        {
                            _fs.Unmount(root.MountName);
                        }
                    }
                }

                return Result.Success;
            }

            public Result Activate(RightsIdCache rightsIdCache, RegisteredHostContent registeredHostContent, ContentStorageId contentStorageId)
            {
                try
                {
                    ref ContentStorageRoot root = ref GetRoot(contentStorageId);

                    if (Config.IsIntegrated && _integratedContentStorage == null)
                    {
                        _integratedContentStorage = new();
                    }

                    Result result = Activate(ref root, rightsIdCache, registeredHostContent);

                    if (result.IsFailure)
                    {
                        return result;
                    }

                    if (Config.IsIntegrated)
                    {
                        int index;
                        for (index = 0; index < Config.ContentStorageIdsCount; index++)
                        {
                            if (Config.ContentStorageIds[index] == root.Config.Value.ContentStorageId)
                            {
                                break;
                            }
                        }

                        _integratedContentStorage.Add(root.ContentStorage, (byte)(index + 1));
                    }
                }
                catch (ArgumentException)
                {
                    return NcmResult.UnknownStorage;
                }

                return Result.Success;
            }

            public readonly Result Activate(ref ContentStorageRoot root, RightsIdCache rightsIdCache, RegisteredHostContent registeredHostContent)
            {
                if (root.ContentStorage != null)
                {
                    return Result.Success;
                }

                if (root.Config.HasValue)
                {
                    Result result = _fs.MountContentStorage(root.MountName, root.Config.Value.ContentStorageId);

                    if (result.IsFailure)
                    {
                        return result;
                    }

                    try
                    {
                        ContentStorageImpl contentStorage = new(_fs);

                        switch (root.StorageId)
                        {
                            case StorageId.BuiltInSystem:
                                result = contentStorage.Initialize(
                                    root.Path,
                                    MakePath.MakeFlatContentFilePath,
                                    MakePath.MakeFlatPlaceHolderFilePath,
                                    false,
                                    rightsIdCache);
                                break;
                            case StorageId.SdCard:
                                result = contentStorage.Initialize(
                                    root.Path,
                                    MakePath.MakeSha256HierarchicalContentFilePathForFat16KCluster,
                                    MakePath.MakeSha256HierarchicalPlaceHolderFilePathForFat16KCluster,
                                    true,
                                    rightsIdCache);
                                break;
                            default:
                                result = contentStorage.Initialize(
                                    root.Path,
                                    MakePath.MakeSha256HierarchicalContentFilePathForFat16KCluster,
                                    MakePath.MakeSha256HierarchicalPlaceHolderFilePathForFat16KCluster,
                                    false,
                                    rightsIdCache);
                                break;
                        }

                        if (result.IsFailure)
                        {
                            return result;
                        }

                        root.ContentStorage = contentStorage;
                    }
                    finally
                    {
                        if (result.IsFailure)
                        {
                            _fs.Unmount(root.MountName);
                        }
                    }
                }
                else
                {
                    switch (root.StorageId)
                    {
                        case StorageId.Host:
                            root.ContentStorage = new HostContentStorageImpl(_fs, registeredHostContent);
                            break;
                        case StorageId.GameCard:
                            Result result = _fs.GetGameCardHandle(out GameCardHandle gameCardHandle);

                            if (result.IsFailure)
                            {
                                return result;
                            }

                            result = _fs.MountGameCardPartition(root.MountName, gameCardHandle, GameCardPartition.Secure);

                            if (result.IsFailure)
                            {
                                return result;
                            }

                            try
                            {
                                ReadOnlyContentStorageImpl contentStorage = new(_fs);
                                result = contentStorage.Initialize(root.Path, MakePath.MakeFlatContentFilePath);

                                if (result.IsFailure)
                                {
                                    return result;
                                }

                                root.ContentStorage = contentStorage;
                            }
                            finally
                            {
                                if (result.IsFailure)
                                {
                                    _fs.Unmount(root.MountName);
                                }
                            }
                            break;
                    }
                }

                return Result.Success;
            }

            private readonly ref ContentStorageRoot GetRoot(ContentStorageId contentStorageId)
            {
                for (int i = 0; i < Roots.Length; i++)
                {
                    ref ContentStorageRoot root = ref Roots[i];

                    if (root.Config.HasValue && root.Config.Value.ContentStorageId == contentStorageId)
                    {
                        return ref root;
                    }
                }

                throw new ArgumentException($"Root for \"{contentStorageId}\" not found.");
            }
        }

        private struct ContentMetaDatabaseRoot
        {
            public string MountName;
            public string Path;
            public StorageId StorageId;
            public ContentStorageConfig? StorageConfig;
            public SystemSaveDataInfo? SaveDataInfo;
            public MemoryKeyValueStore<ContentMetaKey> Kvs;
            public IContentMetaDatabase ContentMetaDatabase;
            public ContentMetaMemoryResource MemoryResource;
            public uint MaxContentMetas;
        }

        private struct IntegratedContentMetaDatabaseRoot
        {
            public IntegratedContentStorageConfig Config;

            private readonly MountNameGenerator _mng;
            private readonly IFsClient _fs;
            public ContentMetaDatabaseRoot[] Roots;
            private IntegratedContentMetaDatabaseImpl _integratedContentMetaDatabase;

            public IntegratedContentMetaDatabaseRoot(MountNameGenerator mng, IFsClient fs)
            {
                _mng = mng;
                _fs = fs;
            }

            public readonly Result Create()
            {
                for (int i = 0; i < Roots.Length; i++)
                {
                    var root = Roots[i];

                    if (!root.SaveDataInfo.HasValue)
                    {
                        continue;
                    }

                    Result result = EnsureAndMountSystemSaveData(_fs, root.MountName, root.SaveDataInfo.Value);
                    if (result.IsFailure)
                    {
                        return result;
                    }

                    try
                    {
                        result = _fs.EnsureDirectory(root.Path);
                        if (result.IsFailure)
                        {
                            return result;
                        }

                        result = _fs.CommitSaveData(root.MountName);
                        if (result.IsFailure)
                        {
                            return result;
                        }
                    }
                    finally
                    {
                        _fs.Unmount(root.MountName);
                    }
                }

                return Result.Success;
            }

            public readonly Result Verify()
            {
                for (int i = 0; i < Roots.Length; i++)
                {
                    var root = Roots[i];

                    if (!root.SaveDataInfo.HasValue)
                    {
                        continue;
                    }

                    Result result;

                    bool mount = root.ContentMetaDatabase == null;
                    if (mount)
                    {
                        result = _fs.MountSystemSaveData(root.MountName, root.SaveDataInfo.Value.SpaceId, root.SaveDataInfo.Value.Id);
                        if (result.IsFailure)
                        {
                            return result;
                        }
                    }

                    try
                    {
                        result = _fs.HasDirectory(out bool hasDir, root.Path);
                        if (result.IsFailure)
                        {
                            return result;
                        }

                        if (!hasDir)
                        {
                            return NcmResult.InvalidContentMetaDatabase;
                        }
                    }
                    finally
                    {
                        if (mount)
                        {
                            _fs.Unmount(root.MountName);
                        }
                    }
                }

                return Result.Success;
            }

            public readonly Result Open(out IContentMetaDatabase storage)
            {
                bool hasInterface = Config.IsIntegrated
                    ? _integratedContentMetaDatabase != null
                    : Roots[0].ContentMetaDatabase != null;

                if (!hasInterface)
                {
                    storage = null;

                    return GetContentMetaDatabaseNotActiveResult(Config.StorageId);
                }

                storage = Config.IsIntegrated ? _integratedContentMetaDatabase : Roots[0].ContentMetaDatabase;

                return Result.Success;
            }

            public readonly Result Cleanup()
            {
                for (int i = 0; i < Roots.Length; i++)
                {
                    var root = Roots[i];

                    if (!root.SaveDataInfo.HasValue)
                    {
                        continue;
                    }

                    _fs.DeleteSaveData(root.SaveDataInfo.Value.SpaceId, root.SaveDataInfo.Value.Id);
                }

                return Result.Success;
            }

            public Result Activate()
            {
                if (Config.IsIntegrated && _integratedContentMetaDatabase == null)
                {
                    _integratedContentMetaDatabase = new();
                }

                for (int i = 0; i < Roots.Length; i++)
                {
                    ref ContentMetaDatabaseRoot root = ref Roots[i];

                    if (root.StorageConfig.HasValue && root.StorageConfig.Value.SkipActivate)
                    {
                        continue;
                    }

                    Result result = Activate(ref root);

                    if (result.IsFailure)
                    {
                        return result;
                    }

                    if (Config.IsIntegrated)
                    {
                        int index;
                        for (index = 0; index < Config.ContentStorageIdsCount; index++)
                        {
                            if (Config.ContentStorageIds[index] == root.StorageConfig.Value.ContentStorageId)
                            {
                                break;
                            }
                        }

                        _integratedContentMetaDatabase.Add(root.ContentMetaDatabase, (byte)(index + 1));
                    }
                }

                return Result.Success;
            }

            public Result Inactivate()
            {
                if (_integratedContentMetaDatabase != null)
                {
                    Result result = _integratedContentMetaDatabase.DisableForcibly();
                    if (result.IsFailure)
                    {
                        return result;
                    }

                    _integratedContentMetaDatabase = null;
                }

                for (int i = 0; i < Roots.Length; i++)
                {
                    var root = Roots[i];

                    if (root.ContentMetaDatabase != null)
                    {
                        root.ContentMetaDatabase.DisableForcibly();
                        root.ContentMetaDatabase = null;
                        root.Kvs = null;

                        if (root.SaveDataInfo.HasValue)
                        {
                            _fs.Unmount(root.MountName);
                        }
                    }
                }

                return Result.Success;
            }

            private readonly Result Activate(ref ContentMetaDatabaseRoot root)
            {
                if (root.ContentMetaDatabase != null)
                {
                    return Result.Success;
                }

                root.Kvs = new(_fs);

                if (root.SaveDataInfo.HasValue)
                {
                    Result result = _fs.MountSystemSaveData(root.MountName, root.SaveDataInfo.Value.SpaceId, root.SaveDataInfo.Value.Id);
                    if (result.IsFailure)
                    {
                        return result;
                    }

                    try
                    {
                        result = root.Kvs.Initialize(root.Path, (int)root.MaxContentMetas, root.MemoryResource);
                        if (result.IsFailure)
                        {
                            return result;
                        }

                        result = root.Kvs.Load();
                        if (result.IsFailure)
                        {
                            return result;
                        }

                        root.ContentMetaDatabase = new ContentMetaDatabaseImpl(_fs, root.Kvs, root.MountName);
                    }
                    finally
                    {
                        if (result.IsFailure)
                        {
                            _fs.Unmount(root.MountName);
                        }
                    }
                }
                else
                {
                    if (root.StorageId == StorageId.BuiltInSystem)
                    {
                        string tmpMountName = _mng.CreateUniqueMountName();

                        Result result = Result.Success;

                        switch (root.StorageConfig.Value.ContentStorageId)
                        {
                            case ContentStorageId.System:
                                result = _fs.MountBis(tmpMountName, BisPartitionId.System);
                                break;
                            case ContentStorageId.System0:
                                result = _fs.MountBis(tmpMountName, BisPartitionId.System0);
                                break;
                            default:
                                DebugUtil.Unreachable();
                                break;
                        }

                        if (result.IsFailure)
                        {
                            return result;
                        }

                        try
                        {
                            string path = $"{tmpMountName}/cnmtdb.arc";
                            result = root.Kvs.InitializeForReadOnlyArchiveFile(path, (int)root.MaxContentMetas, root.MemoryResource);
                            if (result.IsFailure)
                            {
                                return result;
                            }

                            result = root.Kvs.Load();
                            if (result.IsFailure)
                            {
                                return result;
                            }

                            root.ContentMetaDatabase = new ContentMetaDatabaseImpl(_fs, root.Kvs);
                        }
                        finally
                        {
                            _fs.Unmount(tmpMountName);
                        }
                    }
                    else
                    {
                        Result result = root.Kvs.Initialize((int)root.MaxContentMetas, root.MemoryResource);
                        if (result.IsFailure)
                        {
                            return result;
                        }

                        root.ContentMetaDatabase = new ContentMetaDatabaseImpl(_fs, root.Kvs);
                    }
                }

                return Result.Success;
            }

            public Result Activate(ContentStorageId contentStorageId)
            {
                try
                {
                    ref ContentMetaDatabaseRoot root = ref GetRoot(contentStorageId);

                    if (Config.IsIntegrated && _integratedContentMetaDatabase == null)
                    {
                        _integratedContentMetaDatabase = new();
                    }

                    Result result = Activate(ref root);
                    if (result.IsFailure)
                    {
                        return result;
                    }

                    if (Config.IsIntegrated)
                    {
                        int index;
                        for (index = 0; index < Config.ContentStorageIdsCount; index++)
                        {
                            if (Config.ContentStorageIds[index] == root.StorageConfig.Value.ContentStorageId)
                            {
                                break;
                            }
                        }

                        _integratedContentMetaDatabase.Add(root.ContentMetaDatabase, (byte)(index + 1));
                    }
                }
                catch (ArgumentException)
                {
                    return NcmResult.UnknownStorage;
                }

                return Result.Success;
            }

            public readonly ref ContentMetaDatabaseRoot GetRoot(ContentStorageId contentStorageId)
            {
                for (int i = 0; i < Roots.Length; i++)
                {
                    ref ContentMetaDatabaseRoot root = ref Roots[i];

                    if (root.StorageConfig.HasValue && root.StorageConfig.Value.ContentStorageId == contentStorageId)
                    {
                        return ref root;
                    }
                }

                throw new ArgumentException($"Root for \"{contentStorageId}\" not found.");
            }
        }

        private readonly IntegratedContentStorageRoot[] _integratedContentStorageRoots;
        private readonly ContentStorageRoot[] _contentStorageRoots;
        private readonly IntegratedContentMetaDatabaseRoot[] _integratedContentMetaDatabaseRoots;
        private readonly ContentMetaDatabaseRoot[] _contentMetaDatabaseRoots;
        private readonly IntegratedContentStorageConfig[] _integratedConfigs;
        private readonly ContentStorageConfig[] _configs;

        private int _integratedContentStorageEntriesCount;
        private int _contentStorageEntriesCount;
        private int _integratedContentMetaEntriesCount;
        private int _contentMetaEntriesCount;
        private int _integratedConfigsCount;
        private int _configsCount;

        private readonly RightsIdCache _rightsIdCache;
        private readonly RegisteredHostContent _registeredHostContent;

        private readonly ContentMetaMemoryResource _systemContentMetaMemoryResource;
        private readonly ContentMetaMemoryResource _gameCardContentMetaMemoryResource;
        private readonly ContentMetaMemoryResource _sdAndUserContentMetaMemoryResource;

        public ContentManagerImpl(MountNameGenerator mng, IFsClient fs)
        {
            _mng = mng;
            _fs = fs;
            _lock = new();
            _integratedContentStorageRoots = new IntegratedContentStorageRoot[MaxIntegratedContentStorageRoots];
            _contentStorageRoots = new ContentStorageRoot[MaxContentStorageRoots];
            _integratedContentMetaDatabaseRoots = new IntegratedContentMetaDatabaseRoot[MaxIntegratedContentMetaDatabaseRoots];
            _contentMetaDatabaseRoots = new ContentMetaDatabaseRoot[MaxContentMetaDatabaseRoots];
            _integratedConfigs = new IntegratedContentStorageConfig[MaxIntegratedConfigs];
            _configs = new ContentStorageConfig[MaxConfigs];

            _rightsIdCache = new();
            _registeredHostContent = new();

            _systemContentMetaMemoryResource = new(0x80000); // 512 KB
            _gameCardContentMetaMemoryResource = new(0x80000); // 512 KB
            _sdAndUserContentMetaMemoryResource = new(0x200000); // 2 MB
        }

        private Result EnsureBuiltInSystemSaveDataFlags()
        {
            Result result = _fs.GetSaveDataFlags(out SaveDataFlags flags, BuiltInSystemSaveDataId);
            if (result.IsFailure)
            {
                return result;
            }

            if (flags != BuiltInSystemSaveDataFlags)
            {
                result = _fs.SetSaveDataFlags(BuiltInSystemSaveDataId, SaveDataSpaceId.System, BuiltInSystemSaveDataFlags);
                if (result.IsFailure)
                {
                    return result;
                }
            }

            return Result.Success;
        }

        private ref ContentStorageConfig GetContentStorageConfig(ContentStorageId contentStorageId)
        {
            for (int i = 0; i < _configsCount; i++)
            {
                ref ContentStorageConfig config = ref _configs[i];

                if (config.ContentStorageId == contentStorageId)
                {
                    return ref config;
                }
            }

            throw new ArgumentException($"Invalid content storage ID '{contentStorageId}'.");
        }

        private Result GetIntegratedContentStorageConfig(out IntegratedContentStorageConfig config, ContentStorageId contentStorageId)
        {
            for (int i = 0; i < _integratedConfigsCount; i++)
            {
                var integratedConfig = _integratedConfigs[i];

                for (int n = 0; n < integratedConfig.ContentStorageIdsCount; n++)
                {
                    if (integratedConfig.ContentStorageIds[n] == contentStorageId)
                    {
                        config = integratedConfig;

                        return Result.Success;
                    }
                }
            }

            // This should throw.

            config = default;

            return NcmResult.UnknownStorage;
        }

        private Result GetIntegratedContentStorageRoot(out IntegratedContentStorageRoot root, StorageId storageId)
        {
            if (storageId.IsUniqueStorage())
            {
                for (int i = 0; i < _integratedContentStorageEntriesCount; i++)
                {
                    if (_integratedContentStorageRoots[i].Config.StorageId == storageId)
                    {
                        root = _integratedContentStorageRoots[i];

                        return Result.Success;
                    }
                }

                NcmResult.UnknownStorage.AbortOnFailure();
            }

            root = default;

            return NcmResult.UnknownStorage;
        }

        private Result GetIntegratedContentMetaDatabaseRoot(out IntegratedContentMetaDatabaseRoot root, StorageId storageId)
        {
            if (storageId.IsUniqueStorage())
            {
                for (int i = 0; i < _integratedContentMetaEntriesCount; i++)
                {
                    if (_integratedContentMetaDatabaseRoots[i].Config.StorageId == storageId)
                    {
                        root = _integratedContentMetaDatabaseRoots[i];

                        return Result.Success;
                    }
                }

                NcmResult.UnknownStorage.AbortOnFailure();
            }

            root = default;

            return NcmResult.UnknownStorage;
        }

        private Result InitializeContentStorageRoot(out ContentStorageRoot contentStorageRoot, StorageId storageId, ContentStorageConfig? config)
        {
            contentStorageRoot = new()
            {
                StorageId = storageId,
                Config = config,
                ContentStorage = null,
                MountName = _mng.CreateUniqueMountName(),
            };

            contentStorageRoot.Path = $"{contentStorageRoot.MountName}:/";

            return Result.Success;
        }

        private Result InitializeContentMetaDatabaseRoot(out ContentMetaDatabaseRoot contentMetaDatabaseRoot, StorageId storageId, ContentStorageConfig? config)
        {
            contentMetaDatabaseRoot = new()
            {
                StorageId = storageId,
                StorageConfig = config,
            };

            switch (storageId)
            {
                case StorageId.Host:
                    contentMetaDatabaseRoot.SaveDataInfo = null;
                    contentMetaDatabaseRoot.MaxContentMetas = HostMaxContentMetaCount;
                    contentMetaDatabaseRoot.MemoryResource = _sdAndUserContentMetaMemoryResource;
                    break;
                case StorageId.GameCard:
                    contentMetaDatabaseRoot.SaveDataInfo = null;
                    contentMetaDatabaseRoot.MaxContentMetas = GameCardMaxContentMetaCount;
                    contentMetaDatabaseRoot.MemoryResource = _gameCardContentMetaMemoryResource;
                    break;
                case StorageId.BuiltInSystem:
                    if (config.HasValue && (config.Value.ContentStorageId != ContentStorageId.System || config.Value.SkipVerifyAndCreate))
                    {
                        contentMetaDatabaseRoot.SaveDataInfo = null;
                    }
                    else
                    {
                        contentMetaDatabaseRoot.SaveDataInfo = _builtInSystemSystemSaveDataInfo;
                    }

                    contentMetaDatabaseRoot.MaxContentMetas = SystemMaxContentMetaCount;
                    contentMetaDatabaseRoot.MemoryResource = _systemContentMetaMemoryResource;
                    break;
                case StorageId.BuiltInUser:
                    contentMetaDatabaseRoot.SaveDataInfo = _builtInUserSystemSaveDataInfo;
                    contentMetaDatabaseRoot.MaxContentMetas = UserMaxContentMetaCount;
                    contentMetaDatabaseRoot.MemoryResource = _sdAndUserContentMetaMemoryResource;
                    break;
                case StorageId.SdCard:
                    contentMetaDatabaseRoot.SaveDataInfo = _sdCardSystemSaveDataInfo;
                    contentMetaDatabaseRoot.MaxContentMetas = SdCardMaxContentMetaCount;
                    contentMetaDatabaseRoot.MemoryResource = _sdAndUserContentMetaMemoryResource;
                    break;
            }

            contentMetaDatabaseRoot.Kvs = null;
            contentMetaDatabaseRoot.MountName = _mng.CreateUniqueMountName();
            contentMetaDatabaseRoot.MountName = '#' + contentMetaDatabaseRoot.MountName[1..];
            contentMetaDatabaseRoot.Path = $"{contentMetaDatabaseRoot.MountName}:/meta";

            return Result.Success;
        }

        private Result InitializeIntegratedContentStorageRoot(out IntegratedContentStorageRoot root, ref IntegratedContentStorageConfig config, int rootIndex, int rootCount)
        {
            root = new(_mng, _fs)
            {
                Config = config,
                Roots = _contentStorageRoots.AsSpan().Slice(rootIndex, rootCount).ToArray(),
            };

            return Result.Success;
        }

        private Result InitializeIntegratedContentMetaDatabaseRoot(out IntegratedContentMetaDatabaseRoot root, ref IntegratedContentStorageConfig config, int rootIndex, int rootCount)
        {
            root = new(_mng, _fs)
            {
                Config = config,
                Roots = _contentMetaDatabaseRoots.AsSpan().Slice(rootIndex, rootCount).ToArray(),
            };

            return Result.Success;
        }

        private Result ImportContentMetaDatabaseImpl(ref ContentMetaDatabaseRoot root, string importMountName)
        {
            DebugUtil.Assert(root.StorageId == StorageId.BuiltInSystem);

            lock (_lock)
            {
                string saveDataDbPath = $"{root.Path}/imkvdb.arc";
                string bisDbPath = $"{root.Path}/cnmtdb.arc";

                Result result = _fs.MountSystemSaveData(root.MountName, root.SaveDataInfo.Value.SpaceId, root.SaveDataInfo.Value.Id);
                if (result.IsFailure)
                {
                    return result;
                }

                try
                {
                    result = _fs.EnsureDirectory(root.Path);
                    if (result.IsFailure)
                    {
                        return result;
                    }

                    result = _fs.CopyFile(saveDataDbPath, bisDbPath);
                    if (result.IsFailure)
                    {
                        return result;
                    }

                    return _fs.CommitSaveData(root.MountName);
                }
                finally
                {
                    _fs.Unmount(root.MountName);
                }
            }
        }

        private Result BuildContentMetaDatabase(StorageId storageId)
        {
            return ImportContentMetaDatabase(storageId, false);
        }

        private Result ImportContentMetaDatabase(StorageId storageId, bool fromSignedPartition)
        {
            DebugUtil.Assert(storageId == StorageId.BuiltInSystem);

            Result result = GetIntegratedContentMetaDatabaseRoot(out IntegratedContentMetaDatabaseRoot integratedRoot, storageId);
            if (result.IsFailure)
            {
                return result;
            }

            try
            {
                ref ContentMetaDatabaseRoot root = ref integratedRoot.GetRoot(ContentStorageId.System);

                string bisMountName = _mng.CreateUniqueMountName();

                result = _fs.MountBis(bisMountName, BisPartitionId.System);
                if (result.IsFailure)
                {
                    return result;
                }

                try
                {
                    if (!fromSignedPartition || _fs.IsSignedSystemPartitionOnSdCardValid($"{bisMountName}:/"))
                    {
                        result = ImportContentMetaDatabaseImpl(ref root, bisMountName);
                        if (result.IsFailure)
                        {
                            return result;
                        }
                    }
                }
                finally
                {
                    _fs.Unmount(bisMountName);
                }
            }
            catch (ArgumentException)
            {
                return NcmResult.UnknownStorage;
            }

            return Result.Success;
        }

        public Result Initialize(ContentManagerConfig managerConfig)
        {
            if (managerConfig.IsIntegratedSystemContentEnabled)
            {
                IntegratedContentStorageConfig[] integratedConfigs = new IntegratedContentStorageConfig[]
                {
                    new(StorageId.BuiltInSystem, stackalloc[] { ContentStorageId.System, ContentStorageId.System0 }, true),
                    new(StorageId.BuiltInUser, stackalloc[] { ContentStorageId.User }, false),
                    new(StorageId.SdCard, stackalloc[] { ContentStorageId.SdCard }, false),
                    new(StorageId.GameCard, ReadOnlySpan<ContentStorageId>.Empty, false),
                    new(StorageId.Host, ReadOnlySpan<ContentStorageId>.Empty, false),
                };

                ContentStorageConfig[] contentStorageConfigs = new ContentStorageConfig[]
                {
                    new() { ContentStorageId = ContentStorageId.System, SkipVerifyAndCreate = true, SkipActivate = true },
                    new() { ContentStorageId = ContentStorageId.System0, SkipVerifyAndCreate = true, SkipActivate = false },
                    new() { ContentStorageId = ContentStorageId.User, SkipVerifyAndCreate = false, SkipActivate = false },
                    new() { ContentStorageId = ContentStorageId.SdCard, SkipVerifyAndCreate = false, SkipActivate = false },
                };

                StorageId[] activatedStorages = new StorageId[]
                {
                    StorageId.BuiltInSystem,
                };

                return Initialize(managerConfig, integratedConfigs, contentStorageConfigs, activatedStorages);
            }
            else
            {
                IntegratedContentStorageConfig[] integratedConfigs = new IntegratedContentStorageConfig[]
                {
                    new(StorageId.BuiltInSystem, stackalloc[] { ContentStorageId.System, }, false),
                    new(StorageId.BuiltInUser, stackalloc[] { ContentStorageId.User }, false),
                    new(StorageId.SdCard, stackalloc[] { ContentStorageId.SdCard }, false),
                    new(StorageId.GameCard, ReadOnlySpan<ContentStorageId>.Empty, false),
                    new(StorageId.Host, ReadOnlySpan<ContentStorageId>.Empty, false),
                };

                ContentStorageConfig[] contentStorageConfigs = new ContentStorageConfig[]
                {
                    new() { ContentStorageId = ContentStorageId.System, SkipVerifyAndCreate = false, SkipActivate = false },
                    new() { ContentStorageId = ContentStorageId.User, SkipVerifyAndCreate = false, SkipActivate = false },
                    new() { ContentStorageId = ContentStorageId.SdCard, SkipVerifyAndCreate = false, SkipActivate = false },
                };

                StorageId[] activatedStorages = new StorageId[]
                {
                    StorageId.BuiltInSystem,
                };

                return Initialize(managerConfig, integratedConfigs, contentStorageConfigs, activatedStorages);
            }
        }

        private Result Initialize(
            ContentManagerConfig managerConfig,
            IntegratedContentStorageConfig[] integratedConfigs,
            ContentStorageConfig[] configs,
            StorageId[] activatedStorages)
        {
            lock (_lock)
            {
                if (_initialized)
                {
                    return Result.Success;
                }

                for (int i = 0; i < integratedConfigs.Length; i++)
                {
                    _integratedConfigs[i] = integratedConfigs[i];
                }

                _integratedConfigsCount = integratedConfigs.Length;

                for (int i = 0; i < configs.Length; i++)
                {
                    _configs[i] = configs[i];
                }

                _configsCount = configs.Length;

                _integratedContentStorageEntriesCount = 0;
                _contentStorageEntriesCount = 0;
                _integratedContentMetaEntriesCount = 0;
                _contentMetaEntriesCount = 0;

                for (int i = 0; i < _integratedConfigsCount; i++)
                {
                    ref IntegratedContentStorageConfig integratedConfig = ref _integratedConfigs[i];

                    int contentStorageRootIndex = _contentStorageEntriesCount;
                    int contentMetaRootIndex = _contentMetaEntriesCount;

                    Result result;

                    if (integratedConfig.ContentStorageIdsCount > 0)
                    {
                        for (int n = 0; n < integratedConfig.ContentStorageIdsCount; n++)
                        {
                            ref var config = ref GetContentStorageConfig(integratedConfig.ContentStorageIds[n]);

                            result = InitializeContentStorageRoot(out _contentStorageRoots[_contentStorageEntriesCount++], integratedConfig.StorageId, config);
                            if (result.IsFailure)
                            {
                                return result;
                            }

                            result = InitializeContentMetaDatabaseRoot(out _contentMetaDatabaseRoots[_contentMetaEntriesCount++], integratedConfig.StorageId, config);
                            if (result.IsFailure)
                            {
                                return result;
                            }
                        }
                    }
                    else
                    {
                        result = InitializeContentStorageRoot(out _contentStorageRoots[_contentStorageEntriesCount++], integratedConfig.StorageId, null);
                        if (result.IsFailure)
                        {
                            return result;
                        }

                        result = InitializeContentMetaDatabaseRoot(out _contentMetaDatabaseRoots[_contentMetaEntriesCount++], integratedConfig.StorageId, null);
                        if (result.IsFailure)
                        {
                            return result;
                        }
                    }

                    result = InitializeIntegratedContentStorageRoot(
                        out _integratedContentStorageRoots[_integratedContentStorageEntriesCount++],
                        ref integratedConfig,
                        contentStorageRootIndex,
                        _contentStorageEntriesCount - contentStorageRootIndex);

                    if (result.IsFailure)
                    {
                        return result;
                    }

                    result = InitializeIntegratedContentMetaDatabaseRoot(
                        out _integratedContentMetaDatabaseRoots[_integratedContentMetaEntriesCount++],
                        ref integratedConfig,
                        contentMetaRootIndex,
                        _contentMetaEntriesCount - contentMetaRootIndex);

                    if (result.IsFailure)
                    {
                        return result;
                    }
                }

                for (int i = 0; i < activatedStorages.Length; i++)
                {
                    StorageId storageId = activatedStorages[i];

                    if (storageId == StorageId.BuiltInSystem)
                    {
                        Result result = InitializeStorageBuiltInSystem(managerConfig);
                        if (result.IsFailure)
                        {
                            return result;
                        }
                    }
                    else
                    {
                        Result result = InitializeStorage(storageId);
                        if (result.IsFailure)
                        {
                            return result;
                        }
                    }
                }

                _initialized = true;
            }

            return Result.Success;
        }

        private Result InitializeStorageBuiltInSystem(ContentManagerConfig managerConfig)
        {
            Result result;

            if (VerifyContentStorage(StorageId.BuiltInSystem).IsFailure)
            {
                result = CreateContentStorage(StorageId.BuiltInSystem);
                if (result.IsFailure)
                {
                    return result;
                }
            }

            result = ActivateContentStorage(StorageId.BuiltInSystem);
            if (result.IsFailure)
            {
                return result;
            }

            if (VerifyContentMetaDatabase(StorageId.BuiltInSystem).IsFailure)
            {
                result = CreateContentMetaDatabase(StorageId.BuiltInSystem);
                if (result.IsFailure)
                {
                    return result;
                }

                if (managerConfig.ShouldBuildDatabase)
                {
                    result = BuildContentMetaDatabase(StorageId.BuiltInSystem);
                    if (result.IsFailure)
                    {
                        return result;
                    }
                }
                else
                {
                    result = ImportContentMetaDatabase(StorageId.BuiltInSystem, true);
                    if (result.IsFailure)
                    {
                        return result;
                    }
                }

                result = VerifyContentMetaDatabase(StorageId.BuiltInSystem);
                if (result.IsFailure)
                {
                    return result;
                }
            }

            EnsureBuiltInSystemSaveDataFlags();

            return ActivateContentMetaDatabase(StorageId.BuiltInSystem);
        }

        private Result InitializeStorage(StorageId storageId)
        {
            Result result;

            if (VerifyContentStorage(storageId).IsFailure)
            {
                result = CreateContentStorage(storageId);
                if (result.IsFailure)
                {
                    return result;
                }
            }

            result = ActivateContentStorage(storageId);
            if (result.IsFailure)
            {
                return result;
            }

            if (VerifyContentMetaDatabase(storageId).IsFailure)
            {
                result = CreateContentMetaDatabase(storageId);
                if (result.IsFailure)
                {
                    return result;
                }
            }

            return ActivateContentMetaDatabase(storageId);
        }

        [CmifCommand(0)]
        public Result CreateContentStorage(StorageId storageId)
        {
            lock (_lock)
            {
                Result result = GetIntegratedContentStorageRoot(out IntegratedContentStorageRoot root, storageId);
                if (result.IsFailure)
                {
                    return result;
                }

                return root.Create();
            }
        }

        [CmifCommand(1)]
        public Result CreateContentMetaDatabase(StorageId storageId)
        {
            lock (_lock)
            {
                Result result = GetIntegratedContentMetaDatabaseRoot(out IntegratedContentMetaDatabaseRoot root, storageId);
                if (result.IsFailure)
                {
                    return result;
                }

                return root.Create();
            }
        }

        [CmifCommand(2)]
        public Result VerifyContentStorage(StorageId storageId)
        {
            lock (_lock)
            {
                Result result = GetIntegratedContentStorageRoot(out IntegratedContentStorageRoot root, storageId);
                if (result.IsFailure)
                {
                    return result;
                }

                return root.Verify();
            }
        }

        [CmifCommand(3)]
        public Result VerifyContentMetaDatabase(StorageId storageId)
        {
            lock (_lock)
            {
                Result result = GetIntegratedContentMetaDatabaseRoot(out IntegratedContentMetaDatabaseRoot root, storageId);
                if (result.IsFailure)
                {
                    return result;
                }

                return root.Verify();
            }
        }

        [CmifCommand(4)]
        public Result OpenContentStorage(out IContentStorage storage, StorageId storageId)
        {
            lock (_lock)
            {
                Result result = GetIntegratedContentStorageRoot(out IntegratedContentStorageRoot root, storageId);
                if (result.IsFailure)
                {
                    storage = null;

                    return result;
                }

                return root.Open(out storage);
            }
        }

        [CmifCommand(5)]
        public Result OpenContentMetaDatabase(out IContentMetaDatabase metaDatabase, StorageId storageId)
        {
            lock (_lock)
            {
                Result result = GetIntegratedContentMetaDatabaseRoot(out IntegratedContentMetaDatabaseRoot root, storageId);
                if (result.IsFailure)
                {
                    metaDatabase = null;

                    return result;
                }

                return root.Open(out metaDatabase);
            }
        }

        [CmifCommand(6)]
        public Result CloseContentStorageForcibly(StorageId storageId)
        {
            return InactivateContentStorage(storageId);
        }

        [CmifCommand(7)]
        public Result CloseContentMetaDatabaseForcibly(StorageId storageId)
        {
            return InactivateContentMetaDatabase(storageId);
        }

        [CmifCommand(8)]
        public Result CleanupContentMetaDatabase(StorageId storageId)
        {
            lock (_lock)
            {
                Result result = GetIntegratedContentMetaDatabaseRoot(out IntegratedContentMetaDatabaseRoot root, storageId);
                if (result.IsFailure)
                {
                    return result;
                }

                return root.Cleanup();
            }
        }

        [CmifCommand(9)]
        public Result ActivateContentStorage(StorageId storageId)
        {
            lock (_lock)
            {
                Result result = GetIntegratedContentStorageRoot(out IntegratedContentStorageRoot root, storageId);
                if (result.IsFailure)
                {
                    return result;
                }

                return root.Activate(_rightsIdCache, _registeredHostContent);
            }
        }

        [CmifCommand(10)]
        public Result InactivateContentStorage(StorageId storageId)
        {
            lock (_lock)
            {
                Result result = GetIntegratedContentStorageRoot(out IntegratedContentStorageRoot root, storageId);
                if (result.IsFailure)
                {
                    return result;
                }

                return root.Inactivate(_registeredHostContent);
            }
        }

        [CmifCommand(11)]
        public Result ActivateContentMetaDatabase(StorageId storageId)
        {
            lock (_lock)
            {
                Result result = GetIntegratedContentMetaDatabaseRoot(out IntegratedContentMetaDatabaseRoot root, storageId);
                if (result.IsFailure)
                {
                    return result;
                }

                return root.Activate();
            }
        }

        [CmifCommand(12)]
        public Result InactivateContentMetaDatabase(StorageId storageId)
        {
            lock (_lock)
            {
                Result result = GetIntegratedContentMetaDatabaseRoot(out IntegratedContentMetaDatabaseRoot root, storageId);
                if (result.IsFailure)
                {
                    return result;
                }

                return root.Inactivate();
            }
        }

        [CmifCommand(13)]
        public Result InvalidateRightsIdCache()
        {
            _rightsIdCache.Invalidate();

            return Result.Success;
        }

        [CmifCommand(14)]
        public Result GetMemoryReport(out MemoryReport report)
        {
            report = new(
                _systemContentMetaMemoryResource.ToMemoryResourceState(),
                _sdAndUserContentMetaMemoryResource.ToMemoryResourceState(),
                _gameCardContentMetaMemoryResource.ToMemoryResourceState(),
                new()); // Last one is always empty since we don't use the emulated heap at all.

            return Result.Success;
        }

        [CmifCommand(15)]
        public Result ActivateFsContentStorage(ContentStorageId contentStorageId)
        {
            Result result = GetIntegratedContentStorageConfig(out IntegratedContentStorageConfig integratedConfig, contentStorageId);
            if (result.IsFailure)
            {
                return result;
            }

            result = GetIntegratedContentStorageRoot(out IntegratedContentStorageRoot storageRoot, integratedConfig.StorageId);
            if (result.IsFailure)
            {
                return result;
            }

            result = storageRoot.Activate(_rightsIdCache, _registeredHostContent, contentStorageId);
            if (result.IsFailure)
            {
                return result;
            }

            result = GetIntegratedContentMetaDatabaseRoot(out IntegratedContentMetaDatabaseRoot metaDatabaseRoot, integratedConfig.StorageId);
            if (result.IsFailure)
            {
                return result;
            }

            result = metaDatabaseRoot.Activate(contentStorageId);
            if (result.IsFailure)
            {
                return result;
            }

            return Result.Success;
        }

        private static Result EnsureAndMountSystemSaveData(IFsClient fs, string mountName, in SystemSaveDataInfo info)
        {
            const ulong OwnerId = 0;

            fs.DisableAutoSaveDataCreation();

            Result result = fs.MountSystemSaveData(mountName, info.SpaceId, info.Id);

            if (result == FsResult.TargetNotFound)
            {
                result = fs.CreateSystemSaveData(info.SpaceId, info.Id, OwnerId, info.Size, info.JournalSize, info.Flags);
                if (result.IsFailure)
                {
                    return result;
                }

                result = fs.MountSystemSaveData(mountName, info.SpaceId, info.Id);
                if (result.IsFailure)
                {
                    return result;
                }
            }

            return Result.Success;
        }

        private static void ReplaceMountName(out string outPath, string mountName, string path)
        {
            outPath = mountName + path[path.IndexOf(':')..];
        }

        private static Result GetContentStorageNotActiveResult(StorageId storageId)
        {
            return storageId switch
            {
                StorageId.GameCard => NcmResult.GameCardContentStorageNotActive,
                StorageId.BuiltInSystem => NcmResult.BuiltInSystemContentStorageNotActive,
                StorageId.BuiltInUser => NcmResult.BuiltInUserContentStorageNotActive,
                StorageId.SdCard => NcmResult.SdCardContentStorageNotActive,
                _ => NcmResult.UnknownContentStorageNotActive,
            };
        }

        private static Result GetContentMetaDatabaseNotActiveResult(StorageId storageId)
        {
            return storageId switch
            {
                StorageId.GameCard => NcmResult.GameCardContentMetaDatabaseNotActive,
                StorageId.BuiltInSystem => NcmResult.BuiltInSystemContentMetaDatabaseNotActive,
                StorageId.BuiltInUser => NcmResult.BuiltInUserContentMetaDatabaseNotActive,
                StorageId.SdCard => NcmResult.SdCardContentMetaDatabaseNotActive,
                _ => NcmResult.UnknownContentMetaDatabaseNotActive,
            };
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                lock (_lock)
                {
                    for (int i = 0; i < _integratedContentStorageEntriesCount; i++)
                    {
                        InactivateContentStorage(_integratedContentStorageRoots[i].Config.StorageId);
                    }

                    for (int i = 0; i < _integratedContentMetaEntriesCount; i++)
                    {
                        InactivateContentMetaDatabase(_integratedContentMetaDatabaseRoots[i].Config.StorageId);
                    }
                }
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}