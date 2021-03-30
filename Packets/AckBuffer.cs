using System;
using System.Collections.Generic;
using System.Text;

namespace UnityNetworkingLibrary.Packets
{
    using Utils;

    class AckBuffer : AckBitArray
    {
        const int chunkSize = sizeof(byte) * 8;

        public AckBuffer(int sizeBytes) : base(sizeBytes) { }

        public override bool this[int i]
        {
            get
            {
                int index = (i / 8) % buffer.Length; //should floor
                int bitNumber = i % 8;
                return GetBit(buffer[index], bitNumber);
            }
            set
            {
                int index = (i / 8) % buffer.Length; //should floor
                int bitIndex = i % 8;
                buffer[index] = SetBit(buffer[index], value, bitIndex);
            }
        }

        //Performs bitwise Or from a into this.buffer starting at the given bit offset (allows index overflow to 0)
        public void AddEncodedAck(AckBitArray a, ushort id)
        {
            //a encodes a.length previous acks
            //makes sense for processing this that msb is oldest and lsb is newest
            int offset = id % LengthBits;
            int offBytes = offset / chunkSize;
            int offBits = offset % chunkSize;

            //first mask byte has no overflowing bits
            buffer[offBytes] |= (byte)(a.GetByte(0) << offBits);

            int i = offBytes + 1;
            int j = 1;
            while (j < a.LengthBytes)
            {
                //generate mask byte to apply to this.buffer
                buffer[i % buffer.Length] |= (byte)((a.GetByte(j) << offBits) | (a.GetByte(j - 1) >> (chunkSize - offBits)));
                i++;
                j++;
            }
            //Last mask is only overflowing bits
            buffer[i % buffer.Length] |= (byte)(a.GetByte(j - 1) >> (chunkSize - offBits)); //reference could wrap back to 0 due to mod
        }

        public AckBitArray GetEncodedAck(ushort id, int byteCount)
        {
            int offset = id % LengthBits;
            int offBytes = offset / chunkSize;
            int offBits = offset % chunkSize;

            byte[] output = new byte[byteCount];

            int j = offBytes;
            int i = 0;
            while (i < byteCount)
            {
                output[i] = (byte)((buffer[j] >> offBits) | (buffer[j + 1] << (chunkSize - offBits)));
                i++;
                j++;
            }
            return new AckBitArray(output);
        }

        public void ClearBit(int index)
        {
            buffer[index / 8] = SetBit(buffer[index / 8], false, index % 8);
        }

    }
}
