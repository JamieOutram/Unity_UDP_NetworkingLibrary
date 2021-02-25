using System;
using System.Security.Cryptography;

namespace UnityNetworkingLibrary
{
    //Mangages ordering and priority of packets to send
    class PacketManager
    {
        internal const int _maxPacketSizeBytes = 1024;
        internal const int _maxPacketSizeBits = 8 * _maxPacketSizeBytes;
        internal const byte _ackedBytesLength = 4;
        internal const byte _ackedBitsLength = 8 * _ackedBytesLength;
        UInt16 currentPacketID = 0;
        //TODO: Some sort of queue and priority system for sending and receiving packets. Note probably handled at a higher level than this though


        //Sends data packet and awaits sending confirmation if not received, sends again, repeats until standard timeout.
        private static void SendReliable(PacketType type, byte[] data)
        {

        }



        static RNGCryptoServiceProvider random = new RNGCryptoServiceProvider(); //Secure random function

        static int saltLengthLimit = 32;
        static byte[] GetSalt()
        {
            return GetSalt(saltLengthLimit);
        }
        static byte[] GetSalt(int maximumSaltLength)
        {
            var salt = new byte[maximumSaltLength];
            random.GetNonZeroBytes(salt);
            return salt;
        }

        //At a basic level Every packet needs a checksum (CRC32), dataformat, salt (server or client or xor), data 
        
    }
}
