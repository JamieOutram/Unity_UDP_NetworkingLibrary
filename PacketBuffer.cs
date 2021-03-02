using System;
using System.Collections.Generic;
using System.Text;

namespace UnityNetworkingLibrary
{
    class PacketBuffer
    {
        Packet[] buffer;
        //int _firstEmptyPtr;
        public int Length
        {
            get { return buffer.Length; }
        }

        public PacketBuffer(int size)
        {
            buffer = new Packet[size];
            //_firstEmptyPtr = 0;
        }

        public void AddPacket(Packet packet)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] == null)
                {
                    buffer[i] = packet;
                    break;
                }
            }
        }

        public Packet GetPacket(UInt16 packetId)
        {
            foreach (Packet p in buffer)
            {
                if (p.Id == packetId)
                {
                    return p;
                }
            }
            throw new ExceptionExtensions.PacketNotFoundException();
        }

        public Packet GetPacket(int index)
        {
            return buffer[index];
        }
    }
}
