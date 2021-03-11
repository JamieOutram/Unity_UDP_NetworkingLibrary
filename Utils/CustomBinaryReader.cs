using System;
using System.Collections.Generic;
using System.Text;

namespace UnityNetworkingLibrary.Utils
{
    //Wrapper class for network stream reader (for easy switching or custom writing methods if changed later)
    public class CustomBinaryReader : IDisposable
    {
        System.IO.BinaryReader reader;

        public CustomBinaryReader(System.IO.MemoryStream stream)
        {
            reader = new System.IO.BinaryReader(stream);
        }

        public void Dispose()
        {
            reader.Dispose();
        }

        public byte ReadByte()
        {
            return reader.ReadByte();
        }

        public Int16 ReadInt16()
        {
            return reader.ReadInt16();
        }

        public Int32 ReadInt32()
        {
            return reader.ReadInt32();
        }

        public Int64 ReadInt64()
        {
            return reader.ReadInt64();
        }
        public UInt16 ReadUInt16()
        {
            return reader.ReadUInt16();
        }

        public UInt32 ReadUInt32()
        {
            return reader.ReadUInt32();
        }

        public UInt64 ReadUInt64()
        {
            return reader.ReadUInt64();
        }

        public string ReadString()
        {
            //Currently expects a prefix integer 
            //The integer of the value parameter is written out seven bits at a time,
            //starting with the seven least-significant bits.
            //The high bit of a byte indicates whether there are more bytes to be written after this one.
            return reader.ReadString();
        }

        public byte[] ReadBytes(int count)
        {
            return reader.ReadBytes(count);
        }

        public double ReadDouble()
        {
            return reader.ReadDouble();
        }

        public bool ReadBoolean()
        {
            return reader.ReadBoolean();
        }

        public char ReadChar()
        {
            return reader.ReadChar();
        }
    }
}
