using System;

namespace Ryujinx.Horizon.Common
{
    public class InvalidResultException : Exception
    {
        public Result Result { get; }

        public InvalidResultException(Result result) : base($"Unexpected result code {result} returned.")
        {
            Result = result;
        }
    }
}
