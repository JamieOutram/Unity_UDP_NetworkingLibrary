using System;
using System.Collections;
using System.IO;
using System.Collections.ObjectModel;
using UnityNetworkingLibrary.ExceptionExtensions;
namespace UnityNetworkingLibrary
{
    using Utils;
    using Messages;
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

        int _messagesBytes = 0; 
        public int Length { get { return _messagesBytes + headerSize; } }

        public byte Priority { get; set; } //priority needs to be fully editable by packet manager.

        //Store values in writeable formats
        UInt16 _id; //Id and bitack need to be editable by packet manager.
        UInt16 _ackId; //Id of last received packet
        byte[] _ackedBytes; //Encodes last x acknowledged bits. 
        PacketType _packetType;
        UInt64 _salt;
        Message[] _messages;

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
        public AckBitArray AckedBits
        {
            get
            {
                return new AckBitArray(_ackedBytes);
            }
            set
            {
                _ackedBytes = value.ToBytes();
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
        public Message[] GetMessages() 
        {
            return (Message[])_messages.Clone();
        }

        public Message GetMessage(int i)
        {
            return _messages[i];
        }

        public void SetMessages(Message[] value)
        {
            //Based on type may need to pad packet data
            if (Type == PacketType.ClientConnectionRequest || Type == PacketType.ClientChallengeResponse)
            {
                //Connection request should contain no messages
                if (value != null || value?.Length == 0)
                    throw new InvalidConnectionRequestException();
                //Connection request should be padded to max single packet size
                _messagesBytes = PacketManager._maxPacketDataBytes;
                return;
            }
            else if (value == null)
            {
                _messagesBytes = 0;
                _messages = new Message[0];
            }
            else
            {
                //Calculate length
                _messagesBytes = 0;
                foreach(Message m in value)
                {
                    _messagesBytes += m.Length;
                }
                if (this.Length > PacketManager._maxPacketSizeBytes)
                    throw new PacketSizeException();

                _messages = value;
            }
        }


        //Strips all unreliable messages from the packet
        public void StripUnreliableMessages()
        {
            //itterate message array and dispose + set null ref for unreliable messages
            for(int i = 0; i<_messages.Length; i++)
            {
                if (_messages[i] != null)
                {
                    if (!_messages[i].IsReliable)
                    {
                        _messagesBytes -= _messages[i].Length;
                        _messages[i].Dispose();
                        _messages[i] = null;
                    }
                }
            }
        }


        //Create packet
        public Packet(UInt16 id, UInt16 ackId, AckBitArray ackedBits, PacketType packetType, UInt64 salt, Message[] messages = null, byte priority = 0)
        {
            this.Priority = priority;
            this.Id = id;
            this.AckId = ackId;
            this.AckedBits = ackedBits;
            this.Type = packetType;
            this.Salt = salt;
            this.SetMessages(messages);
        }

        public byte[] Serialize()
        {

            //Define write stream
            MemoryStream stream = new MemoryStream(this.Length);
            CustomBinaryWriter writer = new CustomBinaryWriter(stream);

            //Set writing position to after checksum
            writer.Seek(checksumBytes, SeekOrigin.Begin);

            //Construct byte array for checksum
            writer.Write(_id);
            writer.Write(_ackId);
            writer.Write(_ackedBytes);
            writer.Write((byte)_packetType);
            writer.Write(_salt);
            foreach(Message m in _messages)
            {
                if (m != null)
                {
                    m.Serialize(writer);
                }
            }
            byte[] output = stream.ToArray();

            //Dispose of write stream
            stream.Dispose();
            writer.Dispose();

            //Calculate checksum and copy into array
            UInt32 checksum = Crc32C.Crc32CAlgorithm.Compute(output, checksumBytes, output.Length - checksumBytes);
            Buffer.BlockCopy(BitConverter.GetBytes(checksum), 0, output, 0, checksumBytes);

            return output;
        }

        //Data for header of a decoded packet
        public struct Header
        {
            public UInt16 id;
            public UInt16 ackId;
            public AckBitArray ackedBits;
            public PacketType packetType;
            public UInt64 salt;
            public Header(UInt16 id, UInt16 ackId, AckBitArray ackedBits, PacketType packetType, UInt64 salt)
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
        public static (Header, Message[]) Deserialize(byte[] packetData)
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
            AckBitArray ackedBits = new AckBitArray(reader.ReadBytes(ackedBytesLength));
            PacketType type = (PacketType)reader.ReadByte();
            UInt64 salt = reader.ReadUInt64();
            Message[] messages = Message.DeserializeStream(reader);

            stream.Dispose();
            reader.Dispose();

            return (new Header(id, ackId, ackedBits, type, salt), messages);
        }
    }
}
