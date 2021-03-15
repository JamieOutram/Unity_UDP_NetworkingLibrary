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
            PacketManager.NewIdState state = PacketManager.GetNewOverflowingIdState(packet.Id, highestId, (ushort)(packet.Id - buffer.Length), (ushort)(packet.Id + buffer.Length));
            if (state == PacketManager.NewIdState.Invalid)
                return; //Dont change buffer if adding an invalid packet is attempted

            //For new entries need to erase old entries
            if(state == PacketManager.NewIdState.New)
            {
                //Erase all entries from previous highest+1 to new id
                ushort i = highestId;
                i += 1;
                while (i != packet.Id) //due to overflow could be above or below
                {
                    idBuffer[GetIndex(i)] = uint.MaxValue;
                    i += 1;
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
