namespace Ryujinx.Horizon.Sdk.Ncm.Detail
{
    readonly struct ContentManagerConfig
    {
        private readonly bool _buildSystemDatabase;
        private readonly bool _importDatabaseFromSystemOnSd;
        private readonly bool _enableIntegratedSystemContent;

        public bool HasAnyConfig => _buildSystemDatabase || _importDatabaseFromSystemOnSd || _enableIntegratedSystemContent;
        public bool ShouldBuildDatabase => _buildSystemDatabase;
        public bool ShouldImportDatabaseFromSignedSystemPartitionOnSd => _importDatabaseFromSystemOnSd;
        public bool IsIntegratedSystemContentEnabled => _enableIntegratedSystemContent;

        public ContentManagerConfig(bool buildSystemDatabase, bool importDatabaseFromSystemOnSd, bool enableIntegratedSystemContent)
        {
            _buildSystemDatabase = buildSystemDatabase;
            _importDatabaseFromSystemOnSd = importDatabaseFromSystemOnSd;
            _enableIntegratedSystemContent = enableIntegratedSystemContent;
        }
    }
}