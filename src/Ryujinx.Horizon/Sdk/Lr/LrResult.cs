using Ryujinx.Horizon.Common;

namespace Ryujinx.Horizon.Sdk.Lr
{
    static class LrResult
    {
        private const int ModuleId = 8;

        public static Result ProgramNotFound => new(ModuleId, 2);
        public static Result DataNotFound => new(ModuleId, 3);
        public static Result UnknownStorageId => new(ModuleId, 4);
        public static Result HtmlDocumentNotFound => new(ModuleId, 6);
        public static Result AddOnContentNotFound => new(ModuleId, 7);
        public static Result ControlNotFound => new(ModuleId, 8);
        public static Result LegalInformationNotFound => new(ModuleId, 9);
        public static Result DebugProgramNotFound => new(ModuleId, 10);

        public static Result TooManyRegisteredPaths => new(ModuleId, 90);

        public static Result InvalidPath => new(ModuleId, 140);
    }
}
