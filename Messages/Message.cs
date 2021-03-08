using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
namespace UnityNetworkingLibrary
{
    public enum MessageType //List of different message codes
    {
        None,
        ConnectionRequest,
        FatalError,
        ChallengeRequest,
        ChallengeResponse,
    }

    abstract class Message
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

        protected virtual void SerializeHeader(BinaryWriter writer)
        {
            writer.Write(Length);
            writer.Write((byte)Type);
        }

        protected abstract void SerializeData(BinaryWriter writer);

        public byte[] Serialize()
        {
            MemoryStream stream = new MemoryStream(Length);
            BinaryWriter writer = new BinaryWriter(stream);
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

        public void Serialize(BinaryWriter writer)
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
    }
}
