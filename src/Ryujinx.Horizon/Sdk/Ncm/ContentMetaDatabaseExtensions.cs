using Ryujinx.Horizon.Common;

namespace Ryujinx.Horizon.Sdk.Ncm
{
    static class ContentMetaDatabaseExtensions
    {
        public static Result GetProgram(this IContentMetaDatabase contentMetaDatabase, out ContentId contentId, ProgramId programId, uint version)
        {
            return contentMetaDatabase.GetContentIdByType(out contentId, ContentMetaKey.CreateUnknwonType(programId.Id, version), ContentType.Program);
        }

        public static Result GetProgram(this IContentMetaDatabase contentMetaDatabase, out ContentInfo contentInfo, ProgramId programId, uint version)
        {
            return contentMetaDatabase.GetContentInfoByType(out contentInfo, ContentMetaKey.CreateUnknwonType(programId.Id, version), ContentType.Program);
        }

        public static Result GetLatestProgram(this IContentMetaDatabase contentMetaDatabase, out ContentId contentId, ProgramId programId)
        {
            contentId = default;

            Result result = contentMetaDatabase.GetLatestContentMetaKey(out ContentMetaKey key, programId.Id);
            if (result.IsFailure)
            {
                return result;
            }

            return contentMetaDatabase.GetContentIdByType(out contentId, key, ContentType.Program);
        }

        public static Result GetLatestData(this IContentMetaDatabase contentMetaDatabase, out ContentId contentId, DataId dataId)
        {
            contentId = default;

            Result result = contentMetaDatabase.GetLatestContentMetaKey(out ContentMetaKey key, dataId.Id);
            if (result.IsFailure)
            {
                return result;
            }

            return contentMetaDatabase.GetContentIdByType(out contentId, key, ContentType.Data);
        }

        public static Result GetLatestControl(this IContentMetaDatabase contentMetaDatabase, out ContentId contentId, ApplicationId applicationId)
        {
            contentId = default;

            Result result = contentMetaDatabase.GetLatestContentMetaKey(out ContentMetaKey key, applicationId.Id);
            if (result.IsFailure)
            {
                return result;
            }

            return contentMetaDatabase.GetContentIdByType(out contentId, key, ContentType.Control);
        }

        public static Result GetLatestHtmlDocument(this IContentMetaDatabase contentMetaDatabase, out ContentId contentId, ApplicationId applicationId)
        {
            contentId = default;

            Result result = contentMetaDatabase.GetLatestContentMetaKey(out ContentMetaKey key, applicationId.Id);
            if (result.IsFailure)
            {
                return result;
            }

            return contentMetaDatabase.GetContentIdByType(out contentId, key, ContentType.HtmlDocument);
        }

        public static Result GetLatestLegalInformation(this IContentMetaDatabase contentMetaDatabase, out ContentId contentId, ApplicationId applicationId)
        {
            contentId = default;

            Result result = contentMetaDatabase.GetLatestContentMetaKey(out ContentMetaKey key, applicationId.Id);
            if (result.IsFailure)
            {
                return result;
            }

            return contentMetaDatabase.GetContentIdByType(out contentId, key, ContentType.LegalInformation);
        }

        public static Result GetLatestProgram(this IContentMetaDatabase contentMetaDatabase, out ContentInfo contentInfo, ProgramId programId)
        {
            contentInfo = default;

            Result result = contentMetaDatabase.GetLatestContentMetaKey(out ContentMetaKey key, programId.Id);
            if (result.IsFailure)
            {
                return result;
            }

            return contentMetaDatabase.GetContentInfoByType(out contentInfo, key, ContentType.Program);
        }

        public static Result GetLatestData(this IContentMetaDatabase contentMetaDatabase, out ContentInfo contentInfo, DataId dataId)
        {
            contentInfo = default;

            Result result = contentMetaDatabase.GetLatestContentMetaKey(out ContentMetaKey key, dataId.Id);
            if (result.IsFailure)
            {
                return result;
            }

            return contentMetaDatabase.GetContentInfoByType(out contentInfo, key, ContentType.Data);
        }

        public static Result GetLatestControl(this IContentMetaDatabase contentMetaDatabase, out ContentInfo contentInfo, ApplicationId applicationId)
        {
            contentInfo = default;

            Result result = contentMetaDatabase.GetLatestContentMetaKey(out ContentMetaKey key, applicationId.Id);
            if (result.IsFailure)
            {
                return result;
            }

            return contentMetaDatabase.GetContentInfoByType(out contentInfo, key, ContentType.Control);
        }

        public static Result GetLatestHtmlDocument(this IContentMetaDatabase contentMetaDatabase, out ContentInfo contentInfo, ApplicationId applicationId)
        {
            contentInfo = default;

            Result result = contentMetaDatabase.GetLatestContentMetaKey(out ContentMetaKey key, applicationId.Id);
            if (result.IsFailure)
            {
                return result;
            }

            return contentMetaDatabase.GetContentInfoByType(out contentInfo, key, ContentType.HtmlDocument);
        }

        public static Result GetLatestLegalInformation(this IContentMetaDatabase contentMetaDatabase, out ContentInfo contentInfo, ApplicationId applicationId)
        {
            contentInfo = default;

            Result result = contentMetaDatabase.GetLatestContentMetaKey(out ContentMetaKey key, applicationId.Id);
            if (result.IsFailure)
            {
                return result;
            }

            return contentMetaDatabase.GetContentInfoByType(out contentInfo, key, ContentType.LegalInformation);
        }
    }
}