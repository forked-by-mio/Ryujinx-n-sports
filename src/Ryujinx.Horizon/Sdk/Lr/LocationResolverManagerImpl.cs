using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Ncm;
using Ryujinx.Horizon.Sdk.Sf;
using Ryujinx.Horizon.Sdk.Sf.Hipc;
using System;
using System.Collections.Generic;

namespace Ryujinx.Horizon.Sdk.Lr
{
    partial class LocationResolverManagerImpl : ILocationResolverManager
    {
        private readonly IContentManager _contentManager;
        private readonly object _lock;
        private readonly Dictionary<StorageId, ILocationResolver> _locationResolvers;
        private readonly Dictionary<StorageId, bool> _locationResolversEnabled;
        private bool _defaultEnabled;
        private IRegisteredLocationResolver _registeredLocationResolver;
        private IAddOnContentLocationResolver _addOnContentLocationResolver;

        public LocationResolverManagerImpl(IContentManager contentManager)
        {
            _contentManager = contentManager;
            _lock = new();
            _locationResolvers = new();
            _locationResolversEnabled = new();
            _defaultEnabled = true;
        }

        private static bool IsAcceptableStorage(StorageId storageId)
        {
            if (storageId.IsInstallableStorage())
            {
                return storageId != StorageId.Any;
            }
            else
            {
                return storageId == StorageId.Host || storageId == StorageId.GameCard;
            }
        }

        [CmifCommand(0)]
        public Result OpenLocationResolver(out ILocationResolver locationResolver, StorageId storageId)
        {
            lock (_lock)
            {
                if (_locationResolvers.TryGetValue(storageId, out locationResolver))
                {
                    return Result.Success;
                }

                if (storageId == StorageId.Host)
                {
                    _locationResolvers.Add(storageId, locationResolver = new RedirectOnlyLocationResolverImpl());
                }
                else
                {
                    bool isEnabled = _locationResolversEnabled.TryGetValue(storageId, out bool enabled) ? enabled : _defaultEnabled;

                    ContentLocationResolverImpl contentResolver = new(_contentManager, storageId, isEnabled);
                    Result result = contentResolver.Refresh();
                    if (result.IsFailure)
                    {
                        return result;
                    }

                    _locationResolvers.Add(storageId, locationResolver = contentResolver);
                }
            }

            return Result.Success;
        }

        [CmifCommand(1)]
        public Result OpenRegisteredLocationResolver(out IRegisteredLocationResolver locationResolver)
        {
            lock (_lock)
            {
                _registeredLocationResolver ??= new RegisteredLocationResolverImpl();

                locationResolver = _registeredLocationResolver;
            }

            return Result.Success;
        }

        [CmifCommand(2)]
        public Result RefreshLocationResolver(StorageId storageId)
        {
            lock (_lock)
            {
                if (!_locationResolvers.TryGetValue(storageId, out ILocationResolver resolver))
                {
                    return LrResult.UnknownStorageId;
                }

                if (storageId != StorageId.Host)
                {
                    resolver.Refresh();
                }
            }

            return Result.Success;
        }

        [CmifCommand(3)]
        public Result OpenAddOnContentLocationResolver(out IAddOnContentLocationResolver locationResolver)
        {
            lock (_lock)
            {
                _addOnContentLocationResolver ??= new AddOnContentLocationResolverImpl(_contentManager);

                locationResolver = _addOnContentLocationResolver;
            }

            return Result.Success;
        }

        [CmifCommand(4)]
        public Result SetEnabled([Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] ReadOnlySpan<StorageId> storageIds)
        {
            lock (_lock)
            {
                _defaultEnabled = false;

                for (int i = 0; i < storageIds.Length; i++)
                {
                    StorageId storageId = storageIds[i];

                    if (!IsAcceptableStorage(storageId))
                    {
                        return LrResult.UnknownStorageId;
                    }

                    _locationResolversEnabled[storageId] = true;
                }

                foreach ((StorageId storageId, ILocationResolver resolver) in _locationResolvers)
                {
                    bool isEnabled = false;

                    foreach (StorageId enabledStorageId in storageIds)
                    {
                        if (enabledStorageId == storageId)
                        {
                            isEnabled = true;
                            break;
                        }
                    }

                    if (!isEnabled)
                    {
                        resolver.Disable().AbortOnFailure();
                    }
                }
            }

            return Result.Success;
        }
    }
}