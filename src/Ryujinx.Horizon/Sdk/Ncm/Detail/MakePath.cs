
using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Ryujinx.Horizon.Sdk.Ncm.Detail
{
    static class MakePath
    {
        private static void MakeContentName(out string name, ContentId contentId)
        {
            name = $"{contentId.GetString()}.nca";
        }

        private static void MakePlaceHolderName(out string name, PlaceHolderId placeHolderId)
        {
            UInt128 id = placeHolderId.Id;
            ReadOnlySpan<byte> bytes = MemoryMarshal.Cast<UInt128, byte>(MemoryMarshal.CreateSpan(ref id, 1));
            string str = string.Empty;

            for (int i = 0; i < bytes.Length; i++)
            {
                str += bytes[i].ToString("x2");
            }

            name = str + ".nca";
        }

        private static byte Get8BitSha256HashPrefix(ContentId contentId)
        {
            UInt128 id = contentId.Id;
            return SHA256.HashData(MemoryMarshal.Cast<UInt128, byte>(MemoryMarshal.CreateSpan(ref id, 1)))[0];
        }

        private static byte Get8BitSha256HashPrefix(PlaceHolderId placeHolderId)
        {
            UInt128 id = placeHolderId.Id;
            return SHA256.HashData(MemoryMarshal.Cast<UInt128, byte>(MemoryMarshal.CreateSpan(ref id, 1)))[0];
        }

        public static void MakeFlatContentFilePath(out string path, ContentId contentId, string rootPath)
        {
            MakeContentName(out string contentName, contentId);
            path = $"{rootPath}/{contentName}";
        }

        public static int GetHierarchicalContentDirectoryDepth(ContentStorageImplBase.MakeContentPathFunction func)
        {
            // TODO: 4K and 32K variants? Do we need those?

            if (func == MakeFlatContentFilePath)
            {
                return 1;
            }
            else if (func == MakeSha256HierarchicalContentFilePathForFat16KCluster)
            {
                return 2;
            }
            else
            {
                DebugUtil.Abort();
                return 0;
            }
        }

        public static void MakeFlatPlaceHolderFilePath(out string path, PlaceHolderId placeHolderId, string rootPath)
        {
            MakePlaceHolderName(out string placeHolderName, placeHolderId);
            path = $"{rootPath}/{placeHolderName}";
        }

        public static void MakeSha256HierarchicalContentFilePathForFat16KCluster(out string path, ContentId contentId, string rootPath)
        {
            uint hashByte = Get8BitSha256HashPrefix(contentId);
            MakeContentName(out string contentName, contentId);
            path = $"{rootPath}/{hashByte:X8}/{contentName}";
        }

        public static void MakeSha256HierarchicalPlaceHolderFilePathForFat16KCluster(out string path, PlaceHolderId placeHolderId, string rootPath)
        {
            uint hashByte = Get8BitSha256HashPrefix(placeHolderId);
            MakePlaceHolderName(out string placeHolderName, placeHolderId);
            path = $"{rootPath}/{hashByte:X8}/{placeHolderName}";
        }

        public static int GetHierarchicalPlaceHolderDirectoryDepth(ContentStorageImplBase.MakePlaceHolderPathFunction func)
        {
            if (func == MakeFlatPlaceHolderFilePath)
            {
                return 1;
            }
            else if (func == MakeSha256HierarchicalPlaceHolderFilePathForFat16KCluster)
            {
                return 2;
            }
            else
            {
                DebugUtil.Abort();
                return 0;
            }
        }
    }
}