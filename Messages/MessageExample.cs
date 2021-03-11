using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UnityNetworkingLibrary.Messages
{
    using Utils;

    class MessageExample : Message
    {
        public int IntData { get; private set; }
        public string StringData { get; private set; }

        public override bool IsReliable => false;
        public override MessageType Type => MessageType.None;
        public override UInt16 Length => _messageHeaderBytes + sizeof(int);

        public MessageExample(int data, string variableData)
        {
            if (variableData.Length > byte.MaxValue || variableData.Length < 0) //Limit string length
                throw new ArgumentOutOfRangeException();
            this.StringData = variableData;
            this.IntData = data;
            base.Priority = 2;
        }

        protected override void SerializeData(CustomBinaryWriter writer)
        {
            writer.Write(IntData);
            writer.Write(StringData);
        }

        public override void Deserialize(CustomBinaryReader reader)
        {
            IntData = reader.ReadInt32();
            StringData = reader.ReadString();
        }
    }
}
