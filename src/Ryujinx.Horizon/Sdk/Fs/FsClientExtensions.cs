
using Ryujinx.Horizon.Common;
using System;

namespace Ryujinx.Horizon.Sdk.Fs
{
    static class FsClientExtensions
    {
        public static Result CopyFile(this IFsClient fs, string dstPath, string srcPath)
        {
            Result result = fs.OpenFile(out FileHandle srcHandle, srcPath, OpenMode.Read);
            if (result.IsFailure)
            {
                return result;
            }

            FileHandle dstHandle = default;
            bool hasDstHandle = false;

            try
            {
                result = fs.GetFileSize(out long fileSize, srcHandle);
                if (result.IsFailure)
                {
                    return result;
                }

                result = fs.CreateFile(dstPath, fileSize);
                if (result.IsFailure)
                {
                    return result;
                }

                result = fs.OpenFile(out dstHandle, dstPath, OpenMode.Read);
                if (result.IsFailure)
                {
                    return result;
                }

                hasDstHandle = true;

                byte[] buffer = new byte[0x1000];

                long offset = 0;
                while (offset < fileSize)
                {
                    long readSize = Math.Min(fileSize - offset, buffer.Length);

                    Span<byte> readBuffer = buffer.AsSpan()[..(int)readSize];

                    result = fs.ReadFile(srcHandle, offset, readBuffer);
                    if (result.IsFailure)
                    {
                        return result;
                    }

                    result = fs.WriteFile(dstHandle, offset, readBuffer, WriteOption.None);
                    if (result.IsFailure)
                    {
                        return result;
                    }

                    offset += readSize;
                }

                result = fs.FlushFile(dstHandle);
                if (result.IsFailure)
                {
                    return result;
                }
            }
            finally
            {
                fs.CloseFile(srcHandle);

                if (hasDstHandle)
                {
                    fs.CloseFile(dstHandle);
                }
            }

            return Result.Success;
        }
    }
}