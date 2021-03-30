using System;

namespace UnityNetworkingLibrary.Utils
{
    using Packets;
    public class AckBitArray
    {
        
        protected byte[] buffer;

        public int LengthBits
        {
            get { return buffer.Length*8; }
        }

        public int LengthBytes
        {
            get { return buffer.Length; }
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

        public virtual bool this[int i]
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
            if (a.LengthBits != b.LengthBits)
                return false;
            for(int i = 0; i < a.LengthBits; i++)
            {
                if (a[i] != b[i])
                    return false;
            }
            return true;
        }
        public static bool operator ==(AckBitArray a, long b)
        {
            if (a.LengthBits > sizeof(long)*8)
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

        protected bool GetBit(byte b, int bitNumber)
        {
            return (b & (1 << bitNumber)) != 0;
        }
        protected byte SetBit(byte b, bool setTo, int bitIndex)
        {
            byte mask = (byte)(1 << bitIndex);
            return (byte)(setTo ? b | mask : b & ~mask);
        }

        public byte GetByte(int index)
        {
            return buffer[index];
        }


        /// <summary>
        /// Shifts the bits in an array of bytes to the left.
        /// </summary>
        /// <param name="bytes">The byte array to shift.</param>
        public static bool[] ShiftLeft(AckBitArray a, int count)
        {
            bool[] overflow = new bool[count];
            for (int i = a.LengthBits-1; i >= 0; i--)
            {
                if (i >= a.LengthBits-count)
                    overflow[i-(a.LengthBits-count)] = a[i]; //buffer overflowed bits
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
            for(int i = 0; i < a.LengthBits; i++)
            {
                if (i < count)
                    overflow[i] = a[i]; //buffer overflowed bits
                if (i < a.LengthBits - count)
                    a[i] = a[i + count];
                else
                    a[i] = false; //append 0's
            }
            return overflow;
        }

    }
}
