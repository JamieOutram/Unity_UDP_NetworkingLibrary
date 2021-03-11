using System;
using System.Collections.Generic;
using System.Text;

namespace UnityNetworkingLibrary.Utils
{
    public static class ClassUtils
    {
        //Instantiates a child class selected by a given enum
        public static Tbase InstantiateChildFromEnum<Tbase, Tenum>(Tenum type)
        {
            var t = Type.GetType(typeof(Tbase).Namespace + "." + type.ToString(), throwOnError: false);

            if (t == null)
            {
                throw new InvalidOperationException(type.ToString() + " is not a known class");
            }

            if (!typeof(Tbase).IsAssignableFrom(t))
            {
                throw new InvalidOperationException(t.Name + " does not inherit from AbstractDto");
            }

            return (Tbase)Activator.CreateInstance(t);
        }
    }
}
