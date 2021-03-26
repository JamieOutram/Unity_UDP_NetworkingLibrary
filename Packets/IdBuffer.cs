using System;
using System.Collections.Generic;
using System.Text;

namespace UnityNetworkingLibrary.Packets
{
    using ExceptionExtensions;

    abstract class IdBuffer
    {
        public enum InputIdState
        {
            Old,
            New,
            Invalid,
        }

        //Returns wether the new id to be set is old, new or out of acceptable bounds
        public static InputIdState GetIdBufferEntryState(ushort newId, ushort latestId, ushort LB, ushort UB)
        {
            if (LB < UB)
            {
                //Case 1: no overflow
                if (latestId < newId && newId <= UB)
                {
                    //new packet
                    return InputIdState.New;
                }
                else if (LB <= newId && newId <= latestId)
                {
                    //out of order packet or duplicate latest
                    return InputIdState.Old;
                }
                else
                {
                    //out of bounds packet
                    return InputIdState.Invalid;
                }
            }
            else if (latestId > UB)
            {
                //Case 2: new packet ids could overflow 
                if (latestId < newId || newId <= UB)
                {
                    //new packet
                    return InputIdState.New;
                }
                else if (LB <= newId && newId <= latestId)
                {
                    //out of order packet or duplicate latest
                    return InputIdState.Old;
                }
                else
                {
                    //out of bounds packet
                    return InputIdState.Invalid;
                }
            }
            else if (LB > latestId)
            {
                //Case 3: old packet ids could underflow
                if (latestId < newId && newId <= UB)
                {
                    //new packet
                    return InputIdState.New;
                }
                else if (LB <= newId || newId <= latestId)
                {
                    //out of order packet or duplicate latest
                    return InputIdState.Old;
                }
                else
                {
                    //out of bounds packet
                    return InputIdState.Invalid;
                }
            }
            else
            {
                //Logic/State Error
                throw new OverflowingIdStateException();
            }
        }
    }

    class IdBuffer<T> : IdBuffer
    {
        uint[] idBuffer;
        T[] buffer;
        ushort highestId;
        //int _firstEmptyPtr;
        public int Length
        {
            get { return buffer.Length; }
        }

        public IdBuffer(uint size)
        {
            buffer = new T[size];
            idBuffer = new uint[size];
            for(int i = 0; i<size; i++)
            {
                idBuffer[i] = uint.MaxValue;
            }
            highestId = ushort.MaxValue;
            //_firstEmptyPtr = 0;
        }

        public void Clear()
        {
            //Only clear id's, this way packets will overrite naturally
            for (int i = 0; i < idBuffer.Length; i++)
            {
                idBuffer[i] = uint.MaxValue;
            }
            highestId = ushort.MaxValue; 
        }

        public void Add(ushort id, T item)
        {
            InputIdState state = GetIdBufferEntryState(id, highestId, (ushort)(id - buffer.Length), (ushort)(id + buffer.Length));
            if (state == InputIdState.Invalid)
                throw new PacketIdTooOldOrNewException(); //Dont change buffer if adding an invalid packet is attempted

            //For new entries need to erase old entries
            if(state == InputIdState.New)
            {
                //Erase all entries from previous highest+1 to new id
                ushort i = highestId;
                i += 1;
                while (i != id) //due to overflow could be above or below
                {
                    idBuffer[GetIndex(i)] = uint.MaxValue;
                    i += 1;
                }
                highestId = id;
            }

            //Finally assign new packet to buffer
            int index = GetIndex(id);
            buffer[index] = item;
            idBuffer[index] = id;
        }

        public T Get(ushort id)
        {
            int index = GetIndex(id);
            if (idBuffer[index] == id)
            {
                return buffer[index];
            }
            else
            {
                throw new PacketNotFoundException();
            }
        }

        int GetIndex(ushort id)
        {
            return id % buffer.Length;
        }
        
    }
}
