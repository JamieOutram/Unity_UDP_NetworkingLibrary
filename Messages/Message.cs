using System;
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
        public int Priority { get; set; } 
        public UInt16 Bytes { get; protected set; } //full serialized message size in bytes
        public MessageType Type { get; private set; }

        public Message() 
        {
            this.Priority = 0;
            this.Type = MessageType.None;
            this.Bytes = 0;
        }

        public Message(int priority, MessageType type)
        {
            this.Priority = priority;
            this.Type = type;
            this.Bytes = _messageHeaderBytes; 
        }
        
        public abstract byte[] Serialize();
    }
}
