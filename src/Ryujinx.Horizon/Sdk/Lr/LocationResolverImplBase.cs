using Ryujinx.Horizon.Sdk.Ncm;
using System;

namespace Ryujinx.Horizon.Sdk.Lr
{
    class LocationResolverImplBase
    {
        protected readonly LocationRedirector ProgramRedirector = new();
        protected readonly LocationRedirector DebugProgramRedirector = new();
        protected readonly LocationRedirector AppControlRedirector = new();
        protected readonly LocationRedirector HtmlDocsRedirector = new();
        protected readonly LocationRedirector LegalInfoRedirector = new();

        public void ClearRedirections(RedirectionFlags flags = RedirectionFlags.None)
        {
            ProgramRedirector.ClearRedirections(flags);
            DebugProgramRedirector.ClearRedirections(flags);
            AppControlRedirector.ClearRedirections(flags);
            HtmlDocsRedirector.ClearRedirections(flags);
            LegalInfoRedirector.ClearRedirections(flags);
        }

        public void ClearRedirections(ReadOnlySpan<ProgramId> excludingIds)
        {
            ProgramRedirector.ClearRedirectionsExcludingOwners(excludingIds);
            DebugProgramRedirector.ClearRedirectionsExcludingOwners(excludingIds);
            AppControlRedirector.ClearRedirectionsExcludingOwners(excludingIds);
            HtmlDocsRedirector.ClearRedirectionsExcludingOwners(excludingIds);
            LegalInfoRedirector.ClearRedirectionsExcludingOwners(excludingIds);
        }
    }
}