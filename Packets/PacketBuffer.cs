using System;
using System.Collections.Generic;
using System.Text;

namespace UnityNetworkingLibrary
{
    class PacketBuffer
    {
        uint[] idBuffer;
        Packet[] buffer;
        ushort highestId;
        //int _firstEmptyPtr;
        public int Length
        {
            get { return buffer.Length; }
        }

        public PacketBuffer(uint size)
        {
            buffer = new Packet[size];
            idBuffer = new uint[size];
            for(int i = 0; i<size; i++)
            {
                idBuffer[i] = uint.MaxValue;
            }
            highestId = ushort.MaxValue;
            //_firstEmptyPtr = 0;
        }

        public void AddPacket(Packet packet)
        {
            //if id has overflowed back to zero (rarely called)
            if (packet.Id < buffer.Length && highestId > buffer.Length) 
            {
                //If the last id received was the max value, nothing to erase
                if (highestId != ushort.MaxValue)
                {
                    //cycle from current index to top and erase ids between
                    for (ushort i = (ushort)(highestId + 1); i <= ushort.MaxValue; i++)
                    {
                        idBuffer[GetIndex(i)] = uint.MaxValue;
                    }
                }
                highestId = 0;
            }
            
            //TODO: Detect if overflow then backfill (See packet manager)

            //if it is a new most recent packet (commonly called)
            if (packet.Id > highestId)
            {
                //erase all ids between previous highest and new highest
                for (ushort i = (ushort)(highestId + 1); i < packet.Id; i++)
                {
                    idBuffer[GetIndex(i)] = uint.MaxValue;
                }
                highestId = packet.Id;
            }

            //Finally assign new packet to buffer
            int index = GetIndex(packet.Id);
            buffer[index] = packet;
            idBuffer[index] = packet.Id;
        }

        public Packet GetPacket(UInt16 packetId)
        {
            int index = GetIndex(packetId);
            if (idBuffer[index] == packetId)
            {
                return buffer[index];
            }
            else
            {
                throw new ExceptionExtensions.PacketNotFoundException();
            }
        }

        public Packet GetPacket(int index)
        {
            return buffer[index];
        }

        int GetIndex(ushort packetId)
        {
            return packetId % buffer.Length;
        }

    }
}
