using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Ncm;
using Ryujinx.Horizon.Sdk.Sf;
using Ryujinx.Horizon.Sdk.Sf.Hipc;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Ryujinx.Horizon.Sdk.Lr
{
    partial class AddOnContentLocationResolverImpl : IAddOnContentLocationResolver
    {
        private readonly struct OwnedPath
        {
            public readonly RedirectionPath RedirectionPath;
            public readonly ProgramId OwnerId;

            public OwnedPath(in RedirectionPath redirectionPath, ProgramId ownerId)
            {
                RedirectionPath = redirectionPath;
                OwnerId = ownerId;
            }
        }

        private readonly IContentManager _contentManager;

        private readonly RegisteredData<DataId, StorageId> _registeredStorages;
        private readonly Dictionary<DataId, OwnedPath> _registeredPaths;
        private readonly Dictionary<DataId, OwnedPath> _registeredOtherPaths;

        public AddOnContentLocationResolverImpl(IContentManager contentManager)
        {
            _contentManager = contentManager;
            _registeredStorages = new(8);
            _registeredPaths = new();
            _registeredOtherPaths = new();
        }

        private Result ResolveAddOnContentPathImpl(
            out Path path,
            out RedirectionAttributes attributes,
            RegisteredData<DataId, StorageId> storages,
            DataId dataId)
        {
            path = default;
            attributes = default;

            if (!storages.Find(out StorageId storageId, dataId))
            {
                return LrResult.AddOnContentNotFound;
            }

            Result result = _contentManager.OpenContentMetaDatabase(out IContentMetaDatabase contentMetaDatabase, storageId);
            if (result.IsFailure)
            {
                return result;
            }

            result = contentMetaDatabase.GetLatestData(out ContentInfo dataContentInfo, dataId);
            if (result.IsFailure)
            {
                return result;
            }

            result = _contentManager.OpenContentStorage(out IContentStorage contentStorage, storageId);
            if (result.IsFailure)
            {
                return result;
            }

            result = contentStorage.GetPath(out path, dataContentInfo.ContentId);
            if (result.IsFailure)
            {
                return result;
            }

            attributes = new(dataContentInfo.ContentAttributes);

            return Result.Success;
        }

        public Result ResolveAddOnContentPath(out Path path, out RedirectionAttributes attributes, DataId dataId)
        {
            Result result = ResolveAddOnContentPathImpl(out path, out attributes, _registeredStorages, dataId);

            if (result != LrResult.AddOnContentNotFound)
            {
                return result;
            }

            if (_registeredPaths.TryGetValue(dataId, out OwnedPath ownedPath))
            {
                path = ownedPath.RedirectionPath.Path;
                attributes = ownedPath.RedirectionPath.Attributes;

                return Result.Success;
            }

            path = default;
            attributes = default;

            return LrResult.AddOnContentNotFound;
        }

        [CmifCommand(0)]
        public Result ResolveAddOnContentPath([Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer, 0x300)] out Path path, DataId dataId)
        {
            return ResolveAddOnContentPath(out path, out _, dataId);
        }

        [CmifCommand(1)]
        public Result RegisterAddOnContentStorage(DataId dataId, Ncm.ApplicationId applicationId, StorageId storageId)
        {
            if (_registeredStorages.Register(dataId, storageId, new(applicationId.Id)))
            {
                return Result.Success;
            }

            return LrResult.TooManyRegisteredPaths;
        }

        [CmifCommand(2)]
        public Result UnregisterAllAddOnContentPath()
        {
            _registeredStorages.Clear();
            _registeredPaths.Clear();
            _registeredOtherPaths.Clear();

            return Result.Success;
        }

        [CmifCommand(3)]
        public Result RefreshApplicationAddOnContent([Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] ReadOnlySpan<Ncm.ApplicationId> ids)
        {
            _registeredStorages.ClearExcluding(MemoryMarshal.Cast<Ncm.ApplicationId, ProgramId>(ids));

            RemoveExcept(_registeredPaths, ids);
            RemoveExcept(_registeredOtherPaths, ids);

            return Result.Success;
        }

        private static void RemoveExcept(Dictionary<DataId, OwnedPath> dictionary, ReadOnlySpan<Ncm.ApplicationId> ids)
        {
            List<DataId> toRemove = new();

            foreach ((DataId dataId, OwnedPath ownedPath) in dictionary)
            {
                if (!ids.Contains(new(ownedPath.OwnerId.Id)))
                {
                    toRemove.Add(dataId);
                }
            }

            foreach (DataId dataId in toRemove)
            {
                dictionary.Remove(dataId);
            }
        }

        [CmifCommand(4)]
        public Result UnregisterApplicationAddOnContent(Ncm.ApplicationId applicationId)
        {
            _registeredStorages.UnregisterOwnerProgram(new(applicationId.Id));

            Remove(_registeredPaths, applicationId);
            Remove(_registeredOtherPaths, applicationId);

            return Result.Success;
        }

        private static void Remove(Dictionary<DataId, OwnedPath> dictionary, Ncm.ApplicationId id)
        {
            List<DataId> toRemove = new();

            foreach ((DataId dataId, OwnedPath ownedPath) in dictionary)
            {
                if (ownedPath.OwnerId.Id == id.Id)
                {
                    toRemove.Add(dataId);
                }
            }

            foreach (DataId dataId in toRemove)
            {
                dictionary.Remove(dataId);
            }
        }

        public Result GetRegisteredAddOnContentPaths(
            out Path path,
            out RedirectionAttributes attributes,
            out Path path2,
            out RedirectionAttributes attributes2,
            DataId dataId)
        {
            path2 = default;
            attributes2 = default;

            if (!_registeredPaths.TryGetValue(dataId, out OwnedPath ownedPath))
            {
                return ResolveAddOnContentPathImpl(out path, out attributes, _registeredStorages, dataId);
            }

            path = ownedPath.RedirectionPath.Path;
            attributes = ownedPath.RedirectionPath.Attributes;

            if (_registeredOtherPaths.TryGetValue(dataId, out ownedPath))
            {
                path2 = ownedPath.RedirectionPath.Path;
                attributes2 = ownedPath.RedirectionPath.Attributes;
            }

            return Result.Success;
        }

        [CmifCommand(5)]
        public Result GetRegisteredAddOnContentPaths([Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer, 0x300)] out Path path, [Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer, 0x300)] out Path path2, DataId dataId)
        {
            return GetRegisteredAddOnContentPaths(out path, out _, out path2, out _, dataId);
        }

        public Result RegisterAddOnContentPaths(
            DataId dataId,
            Ncm.ApplicationId applicationId,
            in Path path,
            RedirectionAttributes attributes,
            in Path path2,
            RedirectionAttributes attributes2)
        {
            if (_registeredPaths.Count >= 8)
            {
                return LrResult.TooManyRegisteredPaths;
            }

            if (path.AsSpan()[0] == 0)
            {
                return LrResult.InvalidPath;
            }

            _registeredPaths[dataId] = new(new(path, attributes), new(applicationId.Id));

            if (path2.AsSpan()[0] != 0)
            {
                _registeredOtherPaths[dataId] = new(new(path2, attributes2), new(applicationId.Id));
            }
            else
            {
                _registeredOtherPaths.Remove(dataId);
            }

            return Result.Success;
        }

        [CmifCommand(6)]
        public Result RegisterAddOnContentPath(DataId dataId, Ncm.ApplicationId applicationId, [Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer, 0x300)] in Path path)
        {
            return RegisterAddOnContentPaths(
                dataId,
                applicationId,
                path,
                new(Fs.ContentAttributes.None),
                new(),
                new(Fs.ContentAttributes.None));
        }

        [CmifCommand(7)]
        public Result RegisterAddOnContentPaths(DataId dataId, Ncm.ApplicationId applicationId, [Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer, 0x300)] in Path path, [Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer, 0x300)] in Path path2)
        {
            return RegisterAddOnContentPaths(
                dataId,
                applicationId,
                path,
                new(Fs.ContentAttributes.None),
                path2,
                new(Fs.ContentAttributes.None));
        }
    }
}