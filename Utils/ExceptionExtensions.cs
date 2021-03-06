using System;
using System.Collections.Generic;
using System.Text;

namespace UnityNetworkingLibrary
{
    namespace ExceptionExtensions
    {
        //---------------SOCKETS----------------
        [Serializable]
        //Socket failed to connect to given port an ip
        class ConnectionFailedException : Exception
        {
            public ConnectionFailedException() { }

            public ConnectionFailedException(string role)
                : base(String.Format("{0} failed to connect", role))
            {

            }
        }

        //---------------PACKETS----------------
        [Serializable]
        //Packet checksum does not match for data (Likely a transmission error has occured)
        class PacketChecksumException : Exception
        {
            public PacketChecksumException() { }

        }

        [Serializable]
        //Packet Id could not be found
        class PacketNotFoundException : Exception
        {
            public PacketNotFoundException() { }
        }

        [Serializable]
        //Attempted to add too much data to a packet
        class PacketSizeException : Exception
        {
            public PacketSizeException() { }
        }

        //---------------QUEUE----------------
        [Serializable]
        //IndexableQueue Is full and cannot accept any more entries
        class QueueFullException : Exception
        {
            public QueueFullException() { }
        }

        [Serializable]
        //IndexableQueue is empty (no more data to pop)
        class QueueEmptyException : Exception
        {
            public QueueEmptyException() { }
        }

    }
}
