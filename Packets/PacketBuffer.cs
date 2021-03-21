using System;
using System.Collections.Generic;
using System.Text;

namespace UnityNetworkingLibrary
{
    using ExceptionExtensions;

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
            IdBufferEntryState state = GetIdBufferEntryState(packet.Id, highestId, (ushort)(packet.Id - buffer.Length), (ushort)(packet.Id + buffer.Length));
            if (state == IdBufferEntryState.Invalid)
                throw new PacketIdTooOldOrNewException(); //Dont change buffer if adding an invalid packet is attempted

            //For new entries need to erase old entries
            if(state == IdBufferEntryState.New)
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
                throw new PacketNotFoundException();
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


        public enum IdBufferEntryState
        {
            Old,
            New,
            Invalid,
        }
        //Returns wether the new id to be set is old, new or out of acceptable bounds
        public static IdBufferEntryState GetIdBufferEntryState(ushort newId, ushort latestId, ushort LB, ushort UB)
        {
            if (LB < UB)
            {
                //Case 1: no overflow
                if (latestId < newId && newId <= UB)
                {
                    //new packet
                    return IdBufferEntryState.New;
                }
                else if (LB <= newId && newId <= latestId)
                {
                    //out of order packet or duplicate latest
                    return IdBufferEntryState.Old;
                }
                else
                {
                    //out of bounds packet
                    return IdBufferEntryState.Invalid;
                }
            }
            else if (latestId > UB)
            {
                //Case 2: new packet ids could overflow 
                if (latestId < newId || newId <= UB)
                {
                    //new packet
                    return IdBufferEntryState.New;
                }
                else if (LB <= newId && newId <= latestId)
                {
                    //out of order packet or duplicate latest
                    return IdBufferEntryState.Old;
                }
                else
                {
                    //out of bounds packet
                    return IdBufferEntryState.Invalid;
                }
            }
            else if (LB > latestId)
            {
                //Case 3: old packet ids could underflow
                if (latestId < newId && newId <= UB)
                {
                    //new packet
                    return IdBufferEntryState.New;
                }
                else if (LB <= newId || newId <= latestId)
                {
                    //out of order packet or duplicate latest
                    return IdBufferEntryState.Old;
                }
                else
                {
                    //out of bounds packet
                    return IdBufferEntryState.Invalid;
                }
            }
            else
            {
                //Logic/State Error
                throw new OverflowingIdStateException();
            }
        }
    }
}
