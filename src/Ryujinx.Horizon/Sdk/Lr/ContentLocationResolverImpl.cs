using Ryujinx.Horizon.Common;
using Ryujinx.Horizon.Sdk.Ncm;
using Ryujinx.Horizon.Sdk.Sf;
using Ryujinx.Horizon.Sdk.Sf.Hipc;
using System;

namespace Ryujinx.Horizon.Sdk.Lr
{
    partial class ContentLocationResolverImpl : LocationResolverImplBase, ILocationResolver
    {
        private readonly IContentManager _contentManager;
        private readonly StorageId _storageId;
        private bool _enabled;

        private IContentMetaDatabase _contentMetaDatabase;
        private IContentStorage _contentStorage;

        public ContentLocationResolverImpl(IContentManager contentManager, StorageId storageId, bool enabled)
        {
            _contentManager = contentManager;
            _storageId = storageId;
            _enabled = enabled;
        }

        private void GetContentStoragePath([Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer, 0x300)] out Path path, ContentId contentId)
        {
            _contentStorage.GetPath(out path, contentId);
        }

        public Result ResolveProgramPath([Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer, 0x300)] out Path path, out RedirectionAttributes attributes, ProgramId programId)
        {
            if (ProgramRedirector.FindRedirection(out path, out attributes, programId))
            {
                return Result.Success;
            }

            if (!_enabled)
            {
                return LrResult.ProgramNotFound;
            }

            Result result = _contentMetaDatabase.GetLatestProgram(out ContentInfo programContentInfo, programId);

            if (result == NcmResult.ContentMetaNotFound)
            {
                return LrResult.ProgramNotFound;
            }
            else if (result.IsFailure)
            {
                return result;
            }

            GetContentStoragePath(out path, programContentInfo.ContentId);
            attributes = new(programContentInfo.ContentAttributes);

            return Result.Success;
        }

        [CmifCommand(0)]
        public Result ResolveProgramPath([Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer, 0x300)] out Path path, ProgramId programId)
        {
            return ResolveProgramPath(out path, out _, programId);
        }

        [CmifCommand(1)]
        public Result RedirectProgramPath([Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer, 0x300)] in Path path, ProgramId programId)
        {
            ProgramRedirector.SetRedirection(programId, path, new(Fs.ContentAttributes.None));

            return Result.Success;
        }

        [CmifCommand(2)]
        public Result ResolveApplicationControlPath([Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer, 0x300)] out Path path, ProgramId programId)
        {
            if (AppControlRedirector.FindRedirection(out path, out _, programId))
            {
                return Result.Success;
            }

            return LrResult.ControlNotFound;
        }

        [CmifCommand(3)]
        public Result ResolveApplicationHtmlDocumentPath([Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer, 0x300)] out Path path, ProgramId programId)
        {
            if (HtmlDocsRedirector.FindRedirection(out path, out _, programId))
            {
                return Result.Success;
            }

            return LrResult.HtmlDocumentNotFound;
        }

        [CmifCommand(4)]
        public Result ResolveDataPath([Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer, 0x300)] out Path path, DataId dataId)
        {
            path = default;

            if (!_enabled)
            {
                return LrResult.DataNotFound;
            }

            Result result = _contentMetaDatabase.GetLatestData(out ContentInfo dataContentInfo, dataId);
            if (result.IsFailure)
            {
                return result;
            }

            GetContentStoragePath(out path, dataContentInfo.ContentId);

            return Result.Success;
        }

        [CmifCommand(5)]
        public Result RedirectApplicationControlPath([Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer, 0x300)] in Path path, ProgramId programId, ProgramId ownerId)
        {
            AppControlRedirector.SetRedirection(programId, ownerId, path, new(Fs.ContentAttributes.None), RedirectionFlags.Application);

            return Result.Success;
        }

        [CmifCommand(6)]
        public Result RedirectApplicationHtmlDocumentPath([Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer, 0x300)] in Path path, ProgramId programId, ProgramId ownerId)
        {
            HtmlDocsRedirector.SetRedirection(programId, ownerId, path, new(Fs.ContentAttributes.None), RedirectionFlags.Application);

            return Result.Success;
        }

        [CmifCommand(7)]
        public Result ResolveApplicationLegalInformationPath([Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer, 0x300)] out Path path, ProgramId programId)
        {
            if (LegalInfoRedirector.FindRedirection(out path, out _, programId))
            {
                return Result.Success;
            }

            return LrResult.LegalInformationNotFound;
        }

        [CmifCommand(8)]
        public Result RedirectApplicationLegalInformationPath([Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer, 0x300)] in Path path, ProgramId programId, ProgramId ownerId)
        {
            LegalInfoRedirector.SetRedirection(programId, ownerId, path, new(Fs.ContentAttributes.None), RedirectionFlags.Application);

            return Result.Success;
        }

        [CmifCommand(9)]
        public Result Refresh()
        {
            Result result = _contentManager.OpenContentMetaDatabase(out IContentMetaDatabase contentMetaDatabase, _storageId);
            if (result.IsFailure)
            {
                return result;
            }

            result = _contentManager.OpenContentStorage(out IContentStorage contentStorage, _storageId);
            if (result.IsFailure)
            {
                return result;
            }

            _contentMetaDatabase = contentMetaDatabase;
            _contentStorage = contentStorage;

            ClearRedirections();

            return Result.Success;
        }

        [CmifCommand(10)]
        public Result RedirectApplicationProgramPath([Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer, 0x300)] in Path path, ProgramId programId, ProgramId ownerId)
        {
            ProgramRedirector.SetRedirection(programId, ownerId, path, new(Fs.ContentAttributes.None), RedirectionFlags.Application);

            return Result.Success;
        }

        [CmifCommand(11)]
        public Result ClearApplicationRedirection([Buffer(HipcBufferFlags.In | HipcBufferFlags.MapAlias)] ReadOnlySpan<ProgramId> excludingIds)
        {
            ClearRedirections(excludingIds);

            return Result.Success;
        }

        [CmifCommand(12)]
        public Result EraseProgramRedirection(ProgramId programId)
        {
            ProgramRedirector.EraseRedirection(programId);

            return Result.Success;
        }

        [CmifCommand(13)]
        public Result EraseApplicationControlRedirection(ProgramId programId)
        {
            AppControlRedirector.EraseRedirection(programId);

            return Result.Success;
        }

        [CmifCommand(14)]
        public Result EraseApplicationHtmlDocumentRedirection(ProgramId programId)
        {
            HtmlDocsRedirector.EraseRedirection(programId);

            return Result.Success;
        }

        [CmifCommand(15)]
        public Result EraseApplicationLegalInformationRedirection(ProgramId programId)
        {
            LegalInfoRedirector.EraseRedirection(programId);

            return Result.Success;
        }

        [CmifCommand(16)]
        public Result ResolveProgramPathForDebug([Buffer(HipcBufferFlags.Out | HipcBufferFlags.Pointer, 0x300)] out Path path, ProgramId programId)
        {
            if (DebugProgramRedirector.FindRedirection(out path, out _, programId))
            {
                return Result.Success;
            }

            if (!_enabled)
            {
                return LrResult.DebugProgramNotFound;
            }

            Result result = ResolveProgramPath(out path, out _, programId);

            if (result == LrResult.ProgramNotFound)
            {
                return LrResult.DebugProgramNotFound;
            }

            return result;
        }

        [CmifCommand(17)]
        public Result RedirectProgramPathForDebug([Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer, 0x300)] in Path path, ProgramId programId)
        {
            DebugProgramRedirector.SetRedirection(programId, path, new(Fs.ContentAttributes.None));

            return Result.Success;
        }

        [CmifCommand(18)]
        public Result RedirectApplicationProgramPathForDebug([Buffer(HipcBufferFlags.In | HipcBufferFlags.Pointer, 0x300)] in Path path, ProgramId programId, ProgramId ownerId)
        {
            DebugProgramRedirector.SetRedirection(programId, ownerId, path, new(Fs.ContentAttributes.None), RedirectionFlags.Application);

            return Result.Success;
        }

        [CmifCommand(19)]
        public Result EraseProgramRedirectionForDebug(ProgramId programId)
        {
            DebugProgramRedirector.EraseRedirection(programId);

            return Result.Success;
        }

        [CmifCommand(20)]
        public Result Disable()
        {
            _enabled = false;

            return Result.Success;
        }
    }
}