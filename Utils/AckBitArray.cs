using System;

namespace UnityNetworkingLibrary.Utils
{
    using Packets;
    public class AckBitArray
    {
        const int chunkSize = sizeof(byte) * 8;
        byte[] buffer;

        public int Length
        {
            get { return buffer.Length*8; }
        }

        public AckBitArray(int sizeBytes, ulong initialValue = 0L)
        {
            buffer = new byte[sizeBytes];
            int count = Math.Min(sizeof(ulong), sizeBytes);
            Buffer.BlockCopy(BitConverter.GetBytes(initialValue), 0, buffer, 0, count);
        }

        public AckBitArray(byte[] byteArray)
        {
            buffer = (byte[])byteArray.Clone();
        }
        public AckBitArray(AckBitArray ackBitArray)
        {
            buffer = (byte[])ackBitArray.buffer.Clone();
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as AckBitArray);
        }

        public void Clear()
        {
            Array.Clear(buffer, 0, buffer.Length);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                const int p = 16777619;
                int hash = (int)2166136261;

                for (int i = 0; i < buffer.Length; i++)
                    hash = (hash ^ buffer[i]) * p;

                hash += hash << 13;
                hash ^= hash >> 7;
                hash += hash << 3;
                hash ^= hash >> 17;
                hash += hash << 5;
                return hash;
            }
        }

        public byte[] ToBytes()
        {
            return (byte[])buffer.Clone();
        }

        public bool this[int i]
        {
            get {
                int index = i / 8; //should floor
                int bitNumber = i % 8;
                return GetBit(buffer[index], bitNumber); 
            }
            set {
                int index = i / 8; //should floor
                int bitIndex = i % 8;
                buffer[index] = SetBit(buffer[index], value, bitIndex);
            }
        }

        public static bool[] operator >> (AckBitArray a, int b)
        {
            return ShiftRight(a, b);
        }
        public static bool[] operator << (AckBitArray a, int b)
        {
            return ShiftLeft(a, b);
        }
        public static bool operator == (AckBitArray a, AckBitArray b)
        {
            if (a.Length != b.Length)
                return false;
            for(int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            return true;
        }
        public static bool operator ==(AckBitArray a, long b)
        {
            if (a.Length > sizeof(long)*8)
                return false;
            return a.ToLong() == b;
        }
        public static bool operator != (AckBitArray a, AckBitArray b)
        {
            return !(a == b);
        }
        public static bool operator !=(AckBitArray a, long b)
        {
            return !(a == b);
        }

        public long ToLong()
        {
            byte[] convert = new byte[8];
            Buffer.BlockCopy(buffer, 0, convert, 0, Math.Min(buffer.Length, 8));
            return BitConverter.ToInt64(convert, 0);
        }

        bool GetBit(byte b, int bitNumber)
        {
            return (b & (1 << bitNumber)) != 0;
        }
        byte SetBit(byte b, bool setTo, int bitIndex)
        {
            byte mask = (byte)(1 << bitIndex);
            return (byte)(setTo ? b | mask : b & ~mask);
        }

        /// <summary>
        /// Shifts the bits in an array of bytes to the left.
        /// </summary>
        /// <param name="bytes">The byte array to shift.</param>
        public static bool[] ShiftLeft(AckBitArray a, int count)
        {
            bool[] overflow = new bool[count];
            for (int i = a.Length-1; i >= 0; i--)
            {
                if (i >= a.Length-count)
                    overflow[i-(a.Length-count)] = a[i]; //buffer overflowed bits
                if (i >= count)
                    a[i] = a[i - count];
                else
                    a[i] = false; //append 0's
            }
            return overflow;
        }

        /// <summary>
        /// Shifts the bits in an array of bytes to the right.
        /// </summary>
        /// <param name="bytes">The byte array to shift.</param>
        public static bool[] ShiftRight(AckBitArray a, int count)
        {
            bool[] overflow = new bool[count];
            for(int i = 0; i < a.Length; i++)
            {
                if (i < count)
                    overflow[i] = a[i]; //buffer overflowed bits
                if (i < a.Length - count)
                    a[i] = a[i + count];
                else
                    a[i] = false; //append 0's
            }
            return overflow;
        }

        //Performs bitwise Or from a into this.buffer starting at the given bit offset (allows index overflow to 0)
        public void AddEncodedAck(AckBitArray a, int offset)
        {
            //a encodes a.length previous acks
            //makes sense for processing this that msb is oldest and lsb is newest

            int offBytes = offset / chunkSize; 
            int offBits = offset % chunkSize; 

            //first mask byte has no overflowing bits
            buffer[offBytes] |= (byte)(a.buffer[0] << offBits); 

            int i = offBytes + 1;
            int j = 1;
            while (j < a.buffer.Length)
            {
                //generate mask byte to apply to this.buffer
                buffer[i % buffer.Length] |= (byte)((a.buffer[j] << offBits) | (a.buffer[j - 1] >> (chunkSize - offBits))); 
                i++;
                j++;
            }
            //Last mask is only overflowing bits
            buffer[i % buffer.Length] |= (byte)(a.buffer[j - 1] >> (chunkSize - offBits)); //reference could wrap back to 0 due to mod
        }

        public AckBitArray GetEncodedAck(int offset, int byteCount)
        {
            int offBytes = offset / chunkSize; 
            int offBits = offset % chunkSize; 

            AckBitArray output = new AckBitArray(byteCount);

            int j = offBytes;
            int i = 0;
            while(i < byteCount)
            {
                output.buffer[i] = (byte)((buffer[j] >> offBits) | (buffer[j + 1] << (chunkSize - offBits)));
                i++;
                j++;
            }
            return output;

        }

        public void ClearBit(int index)
        {
            buffer[index/8] = SetBit(buffer[index/8], false, index%8);
        }

    }
}
