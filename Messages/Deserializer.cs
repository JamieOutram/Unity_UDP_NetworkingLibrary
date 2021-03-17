using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
namespace UnityNetworkingLibrary.Messages
{
    using Utils;

    //A class Containing all message type deserialization methods which can step through and return messages in the given data 
    public class Deserializer : IDisposable
    {
        MemoryStream messageDataStream;
        CustomBinaryReader reader;

        bool _shouldDispose;

        public Deserializer(byte[] messageData)
        {
            this.messageDataStream = new MemoryStream(messageData);
            this.reader = new CustomBinaryReader(this.messageDataStream);
            _shouldDispose = true;
        }

        public Deserializer(CustomBinaryReader reader)
        {
            this.messageDataStream = null; //only stored for disposal anyway
            this.reader = reader;
            _shouldDispose = false;
        }

        public Message GetNextMessage()
        {
            //Deserialize message header 
            MessageType type = (MessageType)reader.ReadByte();
            //Based on header choose message type and reconstruct
            Message m = ClassUtils.InstantiateChildFromEnum<Message, MessageType>(type);
            //Deserialise message data
            m.Deserialize(reader);
            return m;
        }

        public void Dispose()
        {
            if (_shouldDispose)
            {
                messageDataStream.Dispose();
                reader.Dispose();
            }
        }
    }
}
