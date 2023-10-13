
using Ryujinx.Horizon.Common;
using System.Collections.Generic;

namespace Ryujinx.Horizon.Sdk.Ncm.Detail
{
    class RegisteredHostContent
    {
        private class RegisteredPath
        {
            public ContentId ContentId { get; }
            public string Path { get; set; }

            public RegisteredPath(ContentId contentId, string path)
            {
                ContentId = contentId;
                Path = path;
            }
        }

        private readonly List<RegisteredPath> _pathList;

        public RegisteredHostContent()
        {
            _pathList = new();
        }

        public Result RegisterPath(ContentId contentId, string path)
        {
            lock (_pathList)
            {
                foreach (RegisteredPath rp in _pathList)
                {
                    if (rp.ContentId == contentId)
                    {
                        rp.Path = path;
                        return Result.Success;
                    }
                }

                RegisteredPath registeredPath = new(contentId, path);
                _pathList.Add(registeredPath);
            }

            return Result.Success;
        }

        public Result GetPath(out string path, ContentId contentId)
        {
            lock (_pathList)
            {
                foreach (RegisteredPath rp in _pathList)
                {
                    if (rp.ContentId == contentId)
                    {
                        path = rp.Path;
                        return Result.Success;
                    }
                }
            }

            path = null;
            return NcmResult.ContentNotFound;
        }

        public void ClearPaths()
        {
            lock (_pathList)
            {
                _pathList.Clear();
            }
        }
    }
}