using System;
using System.Collections;
using System.IO;
using System.Collections.ObjectModel;
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
        const int checksumBytes = sizeof(UInt32);
        const int idBytes = sizeof(UInt16);
        public const byte ackedBytesLength = 4;
        public const byte ackedBitsLength = 8 * ackedBytesLength;
        const int packetTypeBytes = sizeof(byte);
        const int saltBytes = sizeof(UInt64);
        public const int saltLengthBits = 8 * saltBytes;

        //Checksum = 4 bytes, Id = 2 bytes, AckedBytes, packetType = 1 byte, salt = 64bit, data = x bits 
        public const int headerSize = checksumBytes + idBytes + ackedBytesLength + packetTypeBytes + saltBytes;

        public int Length { get { return _packetData.Length; } }

        public byte Priority { get; set; } //priority needs to be fully editable by packet manager.

        //TODO: These should strictly just be an accessors for part of the full data packet, currently using twice the memory required

        //Store values in writeable formats
        UInt16 _id; //Id and bitack need to be editable by packet manager.
        byte[] _ackedBytes; //Encodes last x acknowledged bits. 
        PacketType _packetType;
        UInt64 _salt;
        byte[] _messageData;
        byte[] _packetData; //Full serialized packet data

        bool _isDirty = false;

        //Accessors
        public UInt16 Id //UInt16 wrapper for _id
        {
            get
            {
                return _id;
            }
            set
            {
                _id = value;
                //Sync full packet byte array
                _isDirty = true;
            }
        }
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
                //flag dirty
                _isDirty = true;
            }
        } //BitArray Wrapper for _ackedBytes
        public PacketType Type
        {
            get
            {
                return _packetType;
            }
            set
            {
                _packetType = value;
                //Flag dirty header data
                _isDirty = true;
            }
        }
        public UInt64 Salt
        {
            get
            {
                return _salt;
            }
            set
            {
                _salt = value;
                //Flag dirty header data
                _isDirty = true;
            }
        }
        public byte GetMessageData(int i)
        {
            return _messageData[i];
        }
        public void SetMessageData(byte[] value)
        {
            if (value.Length + headerSize > PacketManager._maxPacketSizeBytes)
                throw new PacketSizeException();

            _messageData = value;
            _isDirty = true;
        }
        public byte[] GetPacketData() //Call frugally as it returns a clone of the internal array
        {
            if (_isDirty)
                UpdatePacketData();

            return (byte[])_packetData.Clone();
        }

        //Create packet
        public Packet(UInt16 id, BitArray ackedBits, PacketType packetType, UInt64 salt, byte[] data = null, byte priority = 0)
        {
            this.Priority = priority;
            this.Id = id;
            this.AckedBits = ackedBits;
            this.Type = packetType;
            this.Salt = salt;
            this.SetMessageData(data);
            UpdatePacketData();
        }



        void UpdatePacketData()
        {
            //Calculate packet length
            int packetLength = headerSize;
            if (_messageData != null) packetLength += _messageData.Length;
            else
            {
                //If no message data provided, add one byte of Empty message data
                _messageData = new byte[1];
                _messageData[0] = 0;
            }

            switch (Type) //TODO
            {
                case PacketType.ClientConnectionRequest:
                    //Pad packet data to max size
                    //Do not include salt
                    break;
                case PacketType.ClientChallengeResponse:
                    //Pad packet data to max size
                    break;
            }
            MemoryStream stream = new MemoryStream(packetLength);
            BinaryWriter writer = new BinaryWriter(stream);

            //Set writing position to after checksum
            writer.Seek(checksumBytes, SeekOrigin.Begin);

            //Construct byte array for checksum
            writer.Write(_id);
            writer.Write(_ackedBytes);
            writer.Write((byte)_packetType);
            writer.Write(_salt);
            writer.Write(_messageData);
            this._packetData = stream.ToArray();

            //Calculate checksum
            UInt32 checksum = Crc32C.Crc32CAlgorithm.Compute(_packetData, checksumBytes, _packetData.Length - checksumBytes);

            //Note: might be faster to buffer.blockcopy checksum into packet data array
            //Change writer position
            writer.Seek(0, SeekOrigin.Begin);
            //Add checksum to front of packet
            writer.Write(checksum);
            _packetData = stream.ToArray();

            _isDirty = false;

            stream.Dispose();
            writer.Dispose();
        }


        public struct Header
        {
            public UInt16 id;
            public BitArray ackedBits;
            public PacketType packetType;
            public UInt64 salt;
            public Header(UInt16 id, BitArray ackedBits, PacketType packetType, UInt64 salt)
            {
                this.id = id;
                this.ackedBits = ackedBits;
                this.packetType = packetType;
                this.salt = salt;
            }
        }

        /*Decodes and breaks up the provided packet
        * Returns: checksum, id, Acknowledged packet bits, packet type, salt, data
        */
        public static (Header, byte[]) Decode(byte[] packetData)
        {
            if (packetData.Length > PacketManager._maxPacketSizeBytes)
                throw new PacketSizeException();

            MemoryStream stream = new MemoryStream(packetData);
            BinaryReader reader = new BinaryReader(stream);

            //Read checksum off front
            UInt32 checksum = reader.ReadUInt32();
            //Throw an error if Checksum does not match expected
            if (checksum != Crc32C.Crc32CAlgorithm.Compute(packetData, checksumBytes, packetData.Length - checksumBytes))
                throw new PacketChecksumException();

            //Read remaining data in order
            UInt16 id = reader.ReadUInt16();
            BitArray ackedBits = new BitArray(reader.ReadBytes(ackedBytesLength));
            PacketType type = (PacketType)reader.ReadByte();
            UInt64 salt = reader.ReadUInt64();
            byte[] data = reader.ReadBytes(packetData.Length - headerSize);

            stream.Dispose();
            reader.Dispose();

            return (new Header(id, ackedBits, type, salt), data);
        }
    }
}
