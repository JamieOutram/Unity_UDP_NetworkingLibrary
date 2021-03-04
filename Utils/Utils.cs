using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
namespace UnityNetworkingLibrary
{
    static class Utils
    {
        //Combines multiple arrays into one new array 
        public static T[] CombineArrays<T>(params T[][] arrays)
        {
            T[] rv = new T[arrays.Sum(a => a.Length)];
            int offset = 0;
            foreach (T[] array in arrays)
            {
                Buffer.BlockCopy(array, 0, rv, offset, array.Length);
                offset += array.Length;
            }
            return rv;
        }
        public static void CombineArrays<T>(ref T[] output, int offset = 0, params T[][] arrays)
        {
            try
            {
                foreach (T[] array in arrays)
                {
                    Buffer.BlockCopy(array, 0, output, offset, array.Length);
                    offset += array.Length;
                }
            }
            catch (IndexOutOfRangeException)
            {
                throw new IndexOutOfRangeException("Combination failed, Is the output array large enough?");
            }
        }

        public static T[][] SplitArray<T>(T[] array,int[] sizes)
        {
            if ((sizes.Sum() != array.Length))
                throw new IndexOutOfRangeException("Provided sizes do not sum to array length");
            T[][] outputArrays = new T[sizes.Length][];
            int startIndex = 0;
            int size = 0;
            for (int i = 0; i < sizes.Length; i++)
            {
                startIndex += size;
                size = sizes[i];
                outputArrays[i] = new T[size];
                Buffer.BlockCopy(array, startIndex, outputArrays[i], 0, size);
            }
        }

    }
}
