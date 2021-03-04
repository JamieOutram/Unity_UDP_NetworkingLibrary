using System;
using System.Collections.Generic;
using System.Text;

namespace UnityNetworkingLibrary
{
    namespace ExceptionExtensions
    {
        [Serializable]
        class ConnectionFailedException : Exception
        {
            public ConnectionFailedException() { }

            public ConnectionFailedException(string role)
                : base(String.Format("{0} failed to connect", role))
            {

            }
        }

        class PacketChecksumException : Exception
        {
            public PacketChecksumException() { }

        }

        class QueueFullException : Exception
        {
            public QueueFullException() { }
        }

        class QueueEmptyException : Exception
        {
            public QueueEmptyException() { }
        }

        class PacketNotFoundException : Exception
        {
            public PacketNotFoundException() { }
        }

    }
}
