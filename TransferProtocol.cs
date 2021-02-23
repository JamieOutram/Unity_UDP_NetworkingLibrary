using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
namespace UnityNetworkingLibrary
{
    //Contain formats for messages sent when connecting, and security of generic data transfer
    //Also defines reliable and unreliable communication modes
    static class TransferProtocol
    {
        enum PacketType
        {
            ClientConnect,
            ServerChallenge,
            ClientChallenge,
            data, //TODO: Later subdivided? or could encapsulate command in data and deal with elsewhere
        }

        private static int RollSalt()
        {
            throw new NotImplementedException();
        }
        

        //At a basic level Every packet needs a checksum (CRC32), command, salt (server or client or xor), data 
        struct SendPacket
        {
            UDPSocket socket;
            byte[] checksum;
            byte packetType;
            byte[] salt;
            byte[] data;
            byte[] packetData;

            public SendPacket(PacketType packetType, UInt64 salt, byte[] data)
            {
                this.packetType = (byte)packetType;
                this.salt = BitConverter.GetBytes(salt);
                this.data = data;
                byte[] crclessData = Combine(this.packetType, this.salt, this.data);
                
                this.checksum = BitConverter.GetBytes(Crc32C.Crc32CAlgorithm.Compute(crclessData));
                this.packetData = new byte[4 + crclessData.Length];
                Buffer.BlockCopy(checksum, 0, this.packetData, 0, checksum.Length);
                Buffer.BlockCopy(crclessData, 0, this.packetData, 4, crclessData.Length);
            }
        }

        struct ConnectionRequestPacket
        {
            
        }

        private static byte[] Combine(byte type, params byte[][] arrays)
        {
            
            byte[] rv = new byte[1+arrays.Sum(a => a.Length)]; //type + arrays length
            rv[0] = type;
            int offset = 1;
            foreach (byte[] array in arrays)
            {
                Buffer.BlockCopy(array, 0, rv, offset, array.Length);
                offset += array.Length;
            }
            return rv;
        }
    }
}
