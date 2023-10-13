using Ryujinx.Common.Collections;
using Ryujinx.Horizon.Sdk.Ncm;
using System;

namespace Ryujinx.Horizon.Sdk.Lr
{
    class LocationRedirector
    {
        private class Redirection : IIntrusiveListNode<Redirection>
        {
            public ProgramId ProgramId { get; }
            public ProgramId OwnerId { get; }
            public Path Path { get; }
            public RedirectionAttributes Attributes { get; }
            public RedirectionFlags Flags { get; set; }

            private Redirection _previous;
            private Redirection _next;

            Redirection IIntrusiveListNode<Redirection>.ListPrevious
            {
                get => _previous;
                set => _previous = value;
            }

            Redirection IIntrusiveListNode<Redirection>.ListNext
            {
                get => _next;
                set => _next = value;
            }

            public Redirection ListPrevious => _previous;
            public Redirection ListNext => _next;

            public Redirection(ProgramId programId, ProgramId ownerId, in Path path, RedirectionAttributes attributes, RedirectionFlags flags)
            {
                ProgramId = programId;
                OwnerId = ownerId;
                Path = path;
                Attributes = attributes;
                Flags = flags;
            }
        }

        private readonly IntrusiveList<Redirection> _redirections = new();

        public bool FindRedirection(out Path path, out RedirectionAttributes attributes, ProgramId programId)
        {
            foreach (Redirection redirection in _redirections)
            {
                if (redirection.ProgramId == programId)
                {
                    path = redirection.Path;
                    attributes = redirection.Attributes;

                    return true;
                }
            }

            path = default;
            attributes = default;

            return false;
        }

        public void SetRedirection(ProgramId programId, in Path path, RedirectionAttributes attributes, RedirectionFlags flags = RedirectionFlags.None)
        {
            SetRedirection(programId, new(0), path, attributes, flags);
        }

        public void SetRedirection(ProgramId programId, ProgramId ownerId, in Path path, RedirectionAttributes attributes, RedirectionFlags flags = RedirectionFlags.None)
        {
            EraseRedirection(programId);
            _redirections.AddLast(new(programId, ownerId, path, attributes, flags));
        }

        public void EraseRedirection(ProgramId programId)
        {
            for (Redirection redirection = _redirections.First; redirection != null; redirection = redirection.ListNext)
            {
                if (redirection.ProgramId == programId)
                {
                    _redirections.Remove(redirection);
                    break;
                }
            }
        }

        public void ClearRedirections(RedirectionFlags flags)
        {
            for (Redirection redirection = _redirections.First; redirection != null;)
            {
                if ((redirection.Flags & flags) == flags)
                {
                    Redirection next = redirection.ListNext;
                    _redirections.Remove(redirection);
                    redirection = next;
                }
                else
                {
                    redirection = redirection.ListNext;
                }
            }
        }

        public void ClearRedirectionsExcludingOwners(ReadOnlySpan<ProgramId> excludingIds)
        {
            for (Redirection redirection = _redirections.First; redirection != null;)
            {
                if (excludingIds.Contains(redirection.ProgramId))
                {
                    redirection = redirection.ListNext;
                    continue;
                }

                Redirection next = redirection.ListNext;
                _redirections.Remove(redirection);
                redirection = next;
            }
        }
    }
}