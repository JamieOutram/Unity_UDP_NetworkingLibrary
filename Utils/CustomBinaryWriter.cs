using System;
using System.Collections.Generic;
using System.Text;

namespace UnityNetworkingLibrary.Utils
{
    //Wrapper class for network stream reader (for easy switching or custom writing methods if changed later)
    public class CustomBinaryWriter : IDisposable
    {
        System.IO.BinaryWriter writer;

        public CustomBinaryWriter(System.IO.MemoryStream stream)
        {
            writer = new System.IO.BinaryWriter(stream);
        }

        public void Dispose()
        {
            writer.Dispose();
        }

        public void Write(byte value)
        {
            writer.Write(value);
        }

        public void Write(Int16 value)
        {
            writer.Write(value);
        }

        public void Write(Int32 value)
        {
            writer.Write(value);
        }
        public void Write(Int64 value)
        {
            writer.Write(value);
        }
        public void Write(UInt16 value)
        {
            writer.Write(value);
        }
        public void Write(UInt32 value)
        {
            writer.Write(value);
        }
        public void Write(UInt64 value)
        {
            writer.Write(value);
        }
        public void Write(string value)
        {
            writer.Write(value);
        }
        public void Write(byte[] value)
        {
            writer.Write(value);
        }
        public void Write(double value)
        {
            writer.Write(value);
        }
        public void Write(bool value)
        {
            writer.Write(value);
        }
        public void Write(char value)
        {
            writer.Write(value);
        }
    }


}
