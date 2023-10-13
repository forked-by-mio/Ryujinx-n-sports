using Ryujinx.Horizon.Common;

namespace Ryujinx.Horizon.Sdk.Ncm
{
    static class NcmResult
    {
        private const int ModuleId = 5;

        public static Result InvalidContentStorageBase => new(ModuleId, 1);
        public static Result PlaceHolderAlreadyExists => new(ModuleId, 2);
        public static Result PlaceHolderNotFound => new(ModuleId, 3);
        public static Result ContentAlreadyExists => new(ModuleId, 4);
        public static Result ContentNotFound => new(ModuleId, 5);
        public static Result ContentMetaNotFound => new(ModuleId, 7);
        public static Result AllocationFailed => new(ModuleId, 8);
        public static Result UnknownStorage => new(ModuleId, 12);
        public static Result InvalidContentStorage => new(ModuleId, 100);
        public static Result InvalidContentMetaDatabase => new(ModuleId, 110);
        public static Result InvalidPackageFormat => new(ModuleId, 130);
        public static Result InvalidContentHash => new(ModuleId, 140);
        public static Result InvalidInstallTaskState => new(ModuleId, 160);
        public static Result InvalidPlaceHolderFile => new(ModuleId, 170);
        public static Result BufferInsufficient => new(ModuleId, 180);
        public static Result WriteToReadOnlyContentStorage => new(ModuleId, 190);
        public static Result NotEnoughInstallSpace => new(ModuleId, 200);
        public static Result SystemUpdateNotFoundInPackage => new(ModuleId, 210);
        public static Result ContentInfoNotFound => new(ModuleId, 220);
        public static Result DeltaNotFound => new(ModuleId, 237);
        public static Result InvalidContentMetaKey => new(ModuleId, 240);
        public static Result GameCardContentStorageNotActive => new(ModuleId, 251);
        public static Result BuiltInSystemContentStorageNotActive => new(ModuleId, 252);
        public static Result BuiltInUserContentStorageNotActive => new(ModuleId, 253);
        public static Result SdCardContentStorageNotActive => new(ModuleId, 254);
        public static Result UnknownContentStorageNotActive => new(ModuleId, 258);
        public static Result GameCardContentMetaDatabaseNotActive => new(ModuleId, 261);
        public static Result BuiltInSystemContentMetaDatabaseNotActive => new(ModuleId, 262);
        public static Result BuiltInUserContentMetaDatabaseNotActive => new(ModuleId, 263);
        public static Result SdCardContentMetaDatabaseNotActive => new(ModuleId, 264);
        public static Result UnknownContentMetaDatabaseNotActive => new(ModuleId, 268);
        public static Result IgnorableInstallTicketFailure => new(ModuleId, 280);
        public static Result CreatePlaceHolderCancelled => new(ModuleId, 291);
        public static Result WritePlaceHolderCancelled => new(ModuleId, 292);
        public static Result ContentStorageBaseNotFound => new(ModuleId, 310);
        public static Result ListPartiallyNotCommitted => new(ModuleId, 330);
        public static Result UnexpectedContentMetaPrepared => new(ModuleId, 360);
        public static Result InvalidFirmwareVariation => new(ModuleId, 380);
        public static Result InvalidAddOnContentMetaExtendedHeader = new(ModuleId, 400);
        public static Result InvalidContentMetaDirectory = new(ModuleId, 430);
        public static Result InvalidOperation => new(ModuleId, 8180);
        public static Result InvalidOffset => new(ModuleId, 8182);
    }
}
