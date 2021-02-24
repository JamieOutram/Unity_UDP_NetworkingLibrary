using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
namespace UnityNetworkingLibrary
{
    static class Utils
    {
        //Combines multiple arrays into one new array 
        public static T[] Combine<T>(params T[][] arrays)
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
    }
}
