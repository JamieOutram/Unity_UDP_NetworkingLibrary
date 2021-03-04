using System;
using System.Collections;
using UnityNetworkingLibrary.ExceptionExtensions;
namespace UnityNetworkingLibrary
{

    enum PacketType
    {
        None,
        ClientConnectionRequest,
        ServerChallengeRequest,
        ClientChallengeResponse,
        dataUnreliable,
        dataReliable, //If packet contains any reliable data send with this type to receive ack back immediately
    }

    //Note: acknowledgement is attached to every packet sent, this way packet could contain data and acknowledgement. 
    //Server will not bother to send ack back when packet is received immediatly unless flagged as reliable data and decoded without error.
    //Reliable acks could just be empty data packets.

    //TODO: Accessor for data section of packet
    //Future TODO: add some structure to data to allow minor transmission error corrections
    class Packet
    {
        //Define sizes of packet header data
        const int checksumBytes = 4;
        const int idBytes = 2;
        public const byte ackedBytesLength = 4;
        public const byte ackedBitsLength = 8 * ackedBytesLength;
        const int packetTypeBytes = 1;
        const int saltBytes = 8;
        public const int saltLengthBits = 8 * saltBytes;

        //Checksum = 4 bytes, Id = 2 bytes, AckedBytes, packetType = 1 byte, salt = 64bit, data = x bits 
        public const int headerSize = checksumBytes + idBytes + ackedBytesLength + packetTypeBytes + saltBytes;

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
                //Sync full packet byte array
                if (packetData != null)
                    Buffer.BlockCopy(_id, 0, packetData, checksumBytes, idBytes);
            }
        }

        byte[] _ackedBytes; //Encodes last x acknowledged bits. 

        public BitArray AckedBits
        {
            get
            {
                return new BitArray(_ackedBytes);
            }
            set
            {
                if (_ackedBytes == null)
                    _ackedBytes = new byte[ackedBytesLength];
                value.CopyTo(_ackedBytes, 0);
                //Sync full packet byte array
                if (packetData != null)
                    Buffer.BlockCopy(_ackedBytes, 0, packetData, checksumBytes + idBytes, ackedBytesLength);
            }
        } //BitArray Wrapper for _ackedBytes

        byte[] _packetType;
        public PacketType Type
        {
            get
            {
                return (PacketType)_packetType[0];
            }
            set
            {
                if (_packetType == null) _packetType = new byte[packetTypeBytes];
                _packetType[0] = (byte)value;
                //Update packet data
                if (packetData != null)
                    Buffer.BlockCopy(_packetType, 0, packetData, checksumBytes + idBytes + ackedBytesLength, packetTypeBytes);
            }
        }

        byte[] packetData; //Full serialized packet data
        public byte[] PacketData { get { return packetData; } }

        //Create packet
        public Packet(UInt16 id, BitArray ackedBits, PacketType packetType, byte[] salt, byte[] data = null, byte priority = 0)
        {

            this.Priority = priority;
            this.Id = id;
            this.AckedBits = ackedBits;
            var tmpPacketType = new byte[1];
            tmpPacketType[0] = (byte)packetType;
            int packetLength = headerSize;
            if (data != null) packetLength += data.Length;
            else data = new byte[0];
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
            Buffer.BlockCopy(checksum, 0, this.packetData, 0, checksumBytes);
        }

        /*Decodes and breaks up the provided packet
        * Returns: checksum, id, Acknowledged packet bits, packet type, salt, data
        */
        public static (UInt32, UInt16, BitArray, PacketType, byte[], byte[]) Decode(byte[] packetData)
        {
            //Split packet data
            byte[][] dataBytes = Utils.SplitArray(packetData, new int[] { checksumBytes, idBytes, ackedBytesLength, packetTypeBytes, saltBytes, packetData.Length - headerSize });

            UInt32 checksum = BitConverter.ToUInt32(dataBytes[0], 0);
            //Throw an error if Checksum does not match expected
            if (checksum != Crc32C.Crc32CAlgorithm.Compute(packetData, checksumBytes, packetData.Length - checksumBytes))
                throw new PacketChecksumException();

            UInt16 id = BitConverter.ToUInt16(dataBytes[1], 0);
            BitArray ackedBits = new BitArray(dataBytes[2]);
            PacketType type = (PacketType)dataBytes[3][0];
            byte[] salt = dataBytes[4];
            byte[] data = dataBytes[5];

            return (checksum, id, ackedBits, type, salt, data);
        }
    }
}
