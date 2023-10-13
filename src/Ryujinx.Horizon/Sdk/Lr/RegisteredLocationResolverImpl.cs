using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Ncm;
using Ryujinx.Horizon.Sdk.Sf;
using Ryujinx.Horizon.Sdk.Sf.Hipc;
using System;

namespace Ryujinx.Horizon.Sdk.Lr
{
    partial class RegisteredLocationResolverImpl : IRegisteredLocationResolver
    {
        private const int MaxRegisteredLocations = 0x20;

        private readonly LocationRedirector _programRedirector;
        private readonly RegisteredData<ProgramId, RedirectionPath> _registeredProgramLocations;
        private readonly LocationRedirector _htmlDocsRedirector;
        private readonly RegisteredData<ProgramId, RedirectionPath> _registeredHtmlDocsLocations;

        public RegisteredLocationResolverImpl()
        {
            _programRedirector = new();
            _registeredProgramLocations = new(MaxRegisteredLocations);
            _htmlDocsRedirector = new();
            _registeredHtmlDocsLocations = new(MaxRegisteredLocations);
        }

        private static bool ResolvePath(out Path path, LocationRedirector redirector, RegisteredData<ProgramId, RedirectionPath> locations, ProgramId programId)
        {
            if (!redirector.FindRedirection(out path, out _, programId))
            {
                if (!locations.Find(out RedirectionPath redirectionPath, programId))
                {
                    path = default;

                    return false;
                }

                path = redirectionPath.Path;
            }

            return true;
        }

        private static void RegisterPath(RegisteredData<ProgramId, RedirectionPath> locations, ProgramId programId, in Path path, ProgramId ownerId)
        {
            RedirectionPath redirectionPath = new(path, new(Fs.ContentAttributes.None));

            if (locations.Register(programId, redirectionPath, ownerId))
            {
                return;
            }

            locations.Clear();
            locations.Register(programId, redirectionPath, ownerId);
        }

        private void ClearRedirections(RedirectionFlags flags = RedirectionFlags.None)
        {
            _htmlDocsRedirector.ClearRedirections(flags);
            _programRedirector.ClearRedirections(flags);
        }

        private Result RefreshImpl(ReadOnlySpan<ProgramId> excludingIds)
        {
            if (excludingIds.Length != 0)
            {
                _registeredProgramLocations.ClearExcluding(excludingIds);
                _registeredHtmlDocsLocations.ClearExcluding(excludingIds);
            }
            else
            {
                ClearRedirections();
            }

            _programRedirector.ClearRedirectionsExcludingOwners(excludingIds);
            _htmlDocsRedirector.ClearRedirectionsExcludingOwners(excludingIds);

            return Result.Success;
        }

        [CmifCommand(0)]
        public Result ResolveProgramPath([Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer, 0x300)] out Path path, ProgramId programId)
        {
            if (ResolvePath(out path, _programRedirector, _registeredProgramLocations, programId))
            {
                return Result.Success;
            }

            path = default;

            return LrResult.ProgramNotFound;
        }

        [CmifCommand(1)]
        public Result RegisterProgramPath([Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer, 0x300)] in Path path, ProgramId programId, ProgramId ownerId)
        {
            RegisterPath(_registeredProgramLocations, programId, path, ownerId);

            return Result.Success;
        }

        [CmifCommand(2)]
        public Result UnregisterProgramPath(ProgramId programId)
        {
            _registeredProgramLocations.Unregister(programId);

            return Result.Success;
        }

        [CmifCommand(3)]
        public Result RedirectProgramPath([Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer, 0x300)] in Path path, ProgramId programId, ProgramId ownerId)
        {
            _programRedirector.SetRedirection(programId, ownerId, path, new(Fs.ContentAttributes.None));

            return Result.Success;
        }

        [CmifCommand(4)]
        public Result ResolveHtmlDocumentPath([Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer, 0x300)] out Path path, ProgramId programId)
        {
            if (ResolvePath(out path, _htmlDocsRedirector, _registeredHtmlDocsLocations, programId))
            {
                return Result.Success;
            }

            path = default;

            return LrResult.HtmlDocumentNotFound;
        }

        [CmifCommand(5)]
        public Result RegisterHtmlDocumentPath([Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer, 0x300)] in Path path, ProgramId programId, ProgramId ownerId)
        {
            RegisterPath(_registeredHtmlDocsLocations, programId, path, ownerId);

            return Result.Success;
        }

        [CmifCommand(6)]
        public Result UnregisterHtmlDocumentPath(ProgramId programId)
        {
            _registeredHtmlDocsLocations.Unregister(programId);

            return Result.Success;
        }

        [CmifCommand(7)]
        public Result RedirectHtmlDocumentPath([Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer, 0x300)] in Path path, ProgramId programId, ProgramId ownerId)
        {
            _htmlDocsRedirector.SetRedirection(programId, ownerId, path, new(Fs.ContentAttributes.None));

            return Result.Success;
        }

        [CmifCommand(8)]
        public Result Refresh()
        {
            return RefreshImpl(ReadOnlySpan<ProgramId>.Empty);
        }

        [CmifCommand(9)]
        public Result RefreshExcluding([Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] ReadOnlySpan<ProgramId> excludingIds)
        {
            return RefreshImpl(excludingIds);
        }
    }
}