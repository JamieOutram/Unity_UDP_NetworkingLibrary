using System;
using System.Collections;

namespace UnityNetworkingLibrary
{

    enum PacketType
    {
        None,
        ClientConnectionRequest,
        ServerChallengeRequest,
        ClientChallengeResponse,
        data,
    }

    class Packet
    {
        //Checksum = 4 bytes, Id = 2 bytes, AckedBytes, packetType = 1 byte, salt = 64bit, data = x bits 
        public const int headerSize = 4 + 2 + PacketManager._ackedBytesLength + 1 + 8;
        public byte Priority { get; set; } //priority needs to be fully editable by packet manager.

        //TODO: These should strictly just be an accessors for part of the full data packet, currently using twice the memory required
        byte[] _id; //Id and bitack need to be editable by packet manager.
        public UInt16 Id //UInt32 wrapper for _id
        {
            get
            {
                return BitConverter.ToUInt16(_id, 0);
            }
            set
            {
                _id = BitConverter.GetBytes(value);
            }
        }
        
        byte[] _ackedBytes; //Encodes last x acknowledged bits. 
                           //Note: acknowledgement is attached to every packet, this way packet could contain data and acknowledgement. 
                           //Most acks will just be empty data packets.
        public BitArray AckedBits
        {
            get 
            {
                return new BitArray(_ackedBytes);
            }
            set
            {
                if(_ackedBytes == null) 
                    _ackedBytes = new byte[PacketManager._ackedBytesLength];
                value.CopyTo(_ackedBytes,0);
            }
        } //BitArray Wrapper for _ackedBytes

        byte[] packetData; //Full serialized packet data
        public byte[] PacketData { get { return packetData; } }

        //Create and encode packet
        public Packet(UInt16 id, BitArray ackedBits, PacketType packetType, byte[] salt, byte[] data = null, byte priority = 0)
        {
            const int checksumBytes = 4;
            this.Priority = priority;
            this.Id = id;
            this.AckedBits = ackedBits;
            var tmpPacketType = new byte[1];
            tmpPacketType[0] = (byte)packetType;
            int packetLength = checksumBytes + _id.Length + _ackedBytes.Length + 1 + salt.Length; //crc and packet type length are implicit
            if(data!=null) packetLength += data.Length;

            switch (packetType)
            {
                case PacketType.ClientConnectionRequest:
                    //Pad packet data to max size
                    //Do not include salt
                    break;
                case PacketType.ClientChallengeResponse:
                    //Pad packet data to max size
                    break;
            }
            //Construct byte array for checksum
            this.packetData = new byte[packetLength];
            Utils.CombineArrays(ref packetData, checksumBytes, _id, _ackedBytes, tmpPacketType, salt, data);

            //Calculate checksum
            byte[] checksum = BitConverter.GetBytes(Crc32C.Crc32CAlgorithm.Compute(packetData, checksumBytes, packetData.Length - checksumBytes));

            //Add checksum to front of packet
            Buffer.BlockCopy(checksum, 0, this.packetData, 0, checksum.Length);
        }

        public static (UInt32, UInt16, BitArray, PacketType, byte[], byte[]) Decode(byte[] packetData)
        {
            byte[][] dataBytes = Utils.SplitArray(packetData, new int[] { 4, 2, PacketManager._ackedBytesLength, 1, 8, packetData.Length - headerSize});
            //Split packet data
            UInt32 checksum = BitConverter.ToUInt32(dataBytes[0],0);
            UInt16 id = BitConverter.ToUInt16(dataBytes[1],0);
            BitArray ackedBits = new BitArray(dataBytes[2]);
            PacketType type = (PacketType)dataBytes[3][0];
            byte[] salt = dataBytes[4];
            byte[] data = dataBytes[5];
            return (checksum, id, ackedBits, type, salt, data);
        }
    }
}
