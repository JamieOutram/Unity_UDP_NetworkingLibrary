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
        public class ConnectionFailedException : Exception
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
        public class PacketChecksumException : Exception
        {
            public PacketChecksumException() { }

        }

        [Serializable]
        //Packet Id could not be found
        public class PacketNotFoundException : Exception
        {
            public PacketNotFoundException() { }
        }

        [Serializable]
        //Attempted to add too much data to a packet
        public class PacketSizeException : Exception
        {
            public PacketSizeException() { }
        }

        [Serializable]
        public class InvalidConnectionRequestPacket : Exception
        {
            public InvalidConnectionRequestPacket() { }
        }

        //------------PACKET_MANAGMENT---------
        [Serializable]
        public class PacketIdTooOldOrNew : Exception
        {
            public PacketIdTooOldOrNew() { }
        }

        //---------------QUEUE----------------
        [Serializable]
        //IndexableQueue Is full and cannot accept any more entries
        public class QueueFullException : Exception
        {
            public QueueFullException() { }
        }

        [Serializable]
        //IndexableQueue is empty (no more data to pop)
        public class QueueEmptyException : Exception
        {
            public QueueEmptyException() { }
        }

        

    }
}
