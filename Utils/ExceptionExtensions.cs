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

        public class SocketNotReadyException: Exception
        {
            public SocketNotReadyException() { }
        }

        //---------------PACKETS----------------
        [Serializable]
        //Packet checksum does not match for data (Likely a transmission error has occured)
        public class PacketChecksumException : Exception
        {
            public PacketChecksumException() { }

        }

        [Serializable]
        public class PacketEncodedAckException : Exception
        {
            public PacketEncodedAckException() { }

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

        public class PacketDeserializationException : Exception
        {
            public PacketDeserializationException() { }
        }

        [Serializable]
        public class InvalidConnectionRequestException : Exception
        {
            public InvalidConnectionRequestException() { }
        }

        //------------PACKET_MANAGMENT---------
        [Serializable]
        public class PacketIdTooOldOrNewException : Exception
        {
            public PacketIdTooOldOrNewException() { }
        }

        public class PacketNotAcknowledgedException : Exception
        {
            public PacketNotAcknowledgedException() { }
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


        [Serializable]
        //When checking old or new id an unexpected state was detected
        public class OverflowingIdStateException : Exception
        {
            public OverflowingIdStateException() { }
        }

    }
}
