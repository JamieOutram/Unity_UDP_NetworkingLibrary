﻿using System;
using System.Collections;
using System.IO;
using System.Collections.ObjectModel;
using UnityNetworkingLibrary.ExceptionExtensions;
namespace UnityNetworkingLibrary
{
    using Utils;
    public enum PacketType
    {
        None,
        Ack,
        ClientConnectionRequest,
        ServerChallengeRequest,
        ClientChallengeResponse,
        dataUnreliable,
        dataReliable, //If packet contains any reliable data send with this type to receive ack back immediately
    }

    //Note: acknowledgement is attached to every packet sent, this way packet could contain data and acknowledgement. 
    //Server will not bother to send ack back when packet is received immediatly unless flagged as reliable data and decoded without error.
    //Reliable acks could just be empty data packets.

    //Future TODO: add some structure to data to allow minor transmission error corrections
    public class Packet
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
        public const int headerSize = checksumBytes + idBytes*2 + ackedBytesLength + packetTypeBytes + saltBytes;

        public int Length { get { return _messageData.Length + headerSize; } }

        public byte Priority { get; set; } //priority needs to be fully editable by packet manager.

        //Store values in writeable formats
        UInt16 _id; //Id and bitack need to be editable by packet manager.
        UInt16 _ackId; //Id of last received packet
        byte[] _ackedBytes; //Encodes last x acknowledged bits. 
        PacketType _packetType;
        UInt64 _salt;
        byte[] _messageData;

        //Accessors (kept around as they may be useful for validation)
        public UInt16 Id 
        {
            get
            {
                return _id;
            }
            set
            {
                _id = value;
            }
        }
        public UInt16 AckId
        {
            get
            {
                return _ackId;
            }
            set
            {
                _ackId = value;
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
            }
        }

        //Returns a clone of the packet data.
        //mainly for testing purpouses, avoid cloning if possible
        public byte[] GetMessageData() 
        {
            return (byte[])_messageData.Clone();
        }

        public byte GetMessageData(int i)
        {
            return _messageData[i];
        }
        public void SetMessageData(byte[] value)
        {
            //Based on type may need to pad packet data
            if (Type == PacketType.ClientConnectionRequest || Type == PacketType.ClientChallengeResponse)
            {
                //Connection request should contain no messages
                if (value != null)
                    throw new InvalidConnectionRequestPacket();
                //Connection request should be padded to max single packet size
                _messageData = new byte[PacketManager._maxPacketDataBytes];
                return;
            }

            if (value == null)
            {
                _messageData = new byte[1];
            }
            else
            {
                if (value.Length + headerSize > PacketManager._maxPacketSizeBytes)
                    throw new PacketSizeException();

                _messageData = value;
            }
        }

        //Create packet
        public Packet(UInt16 id, UInt16 ackId, BitArray ackedBits, PacketType packetType, UInt64 salt, byte[] data = null, byte priority = 0)
        {
            this.Priority = priority;
            this.Id = id;
            this.AckId = ackId;
            this.AckedBits = ackedBits;
            this.Type = packetType;
            this.Salt = salt;
            this.SetMessageData(data);
        }

        public byte[] Serialize()
        {

            //Calculate packet length
            int packetLength = headerSize + _messageData.Length;


            //Define write stream
            MemoryStream stream = new MemoryStream(packetLength);
            BinaryWriter writer = new BinaryWriter(stream);

            //Set writing position to after checksum
            writer.Seek(checksumBytes, SeekOrigin.Begin);

            //Construct byte array for checksum
            writer.Write(_id);
            writer.Write(_ackId);
            writer.Write(_ackedBytes);
            writer.Write((byte)_packetType);
            writer.Write(_salt);
            writer.Write(_messageData);
            byte[] output = stream.ToArray();

            //Calculate checksum
            UInt32 checksum = Crc32C.Crc32CAlgorithm.Compute(output, checksumBytes, output.Length - checksumBytes);

            //Note: might be faster to buffer.blockcopy checksum into packet data array
            //Change writer position
            writer.Seek(0, SeekOrigin.Begin);
            //Add checksum to front of packet
            writer.Write(checksum);
            output = stream.ToArray();

            //Dispose of write stream
            stream.Dispose();
            writer.Dispose();

            return output;
        }

        //Data for header of a decoded packet
        public struct Header
        {
            public UInt16 id;
            public UInt16 ackId;
            public BitArray ackedBits;
            public PacketType packetType;
            public UInt64 salt;
            public Header(UInt16 id, UInt16 ackId, BitArray ackedBits, PacketType packetType, UInt64 salt)
            {
                this.id = id;
                this.ackId = ackId;
                this.ackedBits = ackedBits;
                this.packetType = packetType;
                this.salt = salt;
            }
        }

        /*Deserializes the header from the provided packet
        * Returns: Tuple (Header, data)
        */
        public static (Header, byte[]) Decode(byte[] packetData)
        {
            if (packetData.Length > PacketManager._maxPacketSizeBytes)
                throw new PacketSizeException();

            MemoryStream stream = new MemoryStream(packetData);
            CustomBinaryReader reader = new CustomBinaryReader(stream);

            //Read checksum off front
            UInt32 checksum = reader.ReadUInt32();
            //Throw an error if Checksum does not match expected
            if (checksum != Crc32C.Crc32CAlgorithm.Compute(packetData, checksumBytes, packetData.Length - checksumBytes))
                throw new PacketChecksumException();

            //Read remaining data in order
            UInt16 id = reader.ReadUInt16();
            UInt16 ackId = reader.ReadUInt16();
            BitArray ackedBits = new BitArray(reader.ReadBytes(ackedBytesLength));
            PacketType type = (PacketType)reader.ReadByte();
            UInt64 salt = reader.ReadUInt64();
            byte[] data = reader.ReadBytes(packetData.Length - headerSize);

            stream.Dispose();
            reader.Dispose();

            return (new Header(id, ackId, ackedBits, type, salt), data);
        }
    }
}
