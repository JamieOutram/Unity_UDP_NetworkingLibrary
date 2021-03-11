using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
namespace UnityNetworkingLibrary
{
    using Utils;

    public enum MessageType //List of different message codes
    {
        None,
        ConnectionRequest,
        FatalError,
        ChallengeRequest,
        ChallengeResponse,
    }

    public abstract class Message
    {
        const int _messageLengthBytes = sizeof(UInt16);
        const int _messageTypeBytes = sizeof(byte);
        public const int _messageHeaderBytes = _messageLengthBytes + _messageTypeBytes;

        //Every message has a priority for local ordering but the priority is not part of the message
        public byte Priority { get; set; }
        public abstract UInt16 Length { get; } //full serialized message size in bytes
        public abstract MessageType Type { get; }
        public abstract bool IsReliable { get; } //If set to true the message's packet will be flaged as reliable

        public Message()
        {
            this.Priority = 0;
        }

        protected void SerializeHeader(CustomBinaryWriter writer)
        {
            writer.Write((byte)Type); //type should implicitly define length
        }

        protected abstract void SerializeData(CustomBinaryWriter writer);
        
        /* Stream does not need be instantiated for every message, can just have a writer passed;
        public byte[] Serialize()
        {
            MemoryStream stream = new MemoryStream(Length);
            CustomBinaryWriter writer = new CustomBinaryWriter(stream);
            try
            {
                SerializeHeader(writer);
                SerializeData(writer);
                return stream.ToArray();
            }
            catch (EndOfStreamException)
            {
                throw new EndOfStreamException();
            }
            finally
            {
                stream.Dispose();
                writer.Dispose();
            }
        }
        */

        public void Serialize(CustomBinaryWriter writer)
        {
            try
            {
                SerializeHeader(writer);
                SerializeData(writer);
            }
            catch (EndOfStreamException)
            {
                throw new EndOfStreamException();
            }
        }

        //Reads the serialized message data and sets properties
        //Assumes the header has already been read to identify the packet type
        public abstract void Deserialize(CustomBinaryReader reader);
    }
}
