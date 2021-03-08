using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UnityNetworkingLibrary.Messages
{
    class MessageExample : Message
    {
        int data;
        
        public override bool IsReliable => false;
        public override MessageType Type => MessageType.None;
        public override UInt16 Length => _messageHeaderBytes + sizeof(int);

        public MessageExample(int data)
        {
            this.data = data;
            base.Priority = 2;
        }

        protected override void SerializeData(BinaryWriter writer)
        {
            writer.Write(data);
        }
    }
}
