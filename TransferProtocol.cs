using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Security.Cryptography;

namespace UnityNetworkingLibrary
{
    //Contain formats for messages sent when connecting, and security of generic data transfer
    //Also defines reliable and unreliable communication modes
    static class TransferProtocol
    {

        //Attempts to send connection request to target server using the given socket
        //Returns 0 if successfully connected, returns negativve error code otherwise
        //This is a blocking function and should only be called in its own thread as to not block the game from updating.
        internal static void ClientConnect(UDPSocket socket, string targetIP, string targetPort, int packetBurst = 3)
        {
            
            //Send connection request packet x times 
            //Begins checking received data for challenge request 
            //If Timeout send try X more times
            //If still timeout throw ConnectionFailedException

            //If challenge request recieved 
                //Send challenge response reliably?
                //calculate and assign xor salt for subsequent packets
                //Start polling server to maintain connection
        }

        //TODO: Some sort of queue and priority system for sending and receiving packets. Note probably handled at a higher level than this though


        //Sends data packet and awaits sending confirmation if not received, sends again, repeats until standard timeout.
        private static void SendReliable(PacketType type, byte[] data)
        {

        }

        #region PRIVATE MEMBERS

        static RNGCryptoServiceProvider random = new RNGCryptoServiceProvider(); //Secure random function
        enum PacketType
        {
            ClientConnectionRequest,
            ServerChallengeRequest,
            ClientChallengeResponse,
            data, //TODO: Later subdivided? or could encapsulate command in data and deal with elsewhere
        }

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
        struct SendPacket
        {
            byte[] checksum;
            byte packetType;
            byte[] salt;
            byte[] data;
            byte[] packetData;

            public SendPacket(PacketType packetType, byte[] salt, byte[] data)
            {
                this.packetType = (byte)packetType;
                this.salt = salt;
                this.data = data;
                //Construct byte array for checksum
                byte[] crclessData = new byte[1 + this.data.Length + this.salt.Length];
                crclessData[0] = this.packetType;
                Buffer.BlockCopy(Utils.Combine(this.salt, this.data), 0, crclessData, 1, this.salt.Length + this.data.Length);
                //Calculate checksum
                this.checksum = BitConverter.GetBytes(Crc32C.Crc32CAlgorithm.Compute(crclessData));

                //Construct full packet byte array
                this.packetData = new byte[4 + crclessData.Length];
                Buffer.BlockCopy(checksum, 0, this.packetData, 0, checksum.Length);
                Buffer.BlockCopy(crclessData, 0, this.packetData, 4, crclessData.Length);
            }
        }
        #endregion

    }
}
