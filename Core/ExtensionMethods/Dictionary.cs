using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageResizer.ExtensionMethods
{
    internal static class DictionaryExtensions
    {
 
        internal static T Get<T>(this IDictionary<string, object> dictionary, string key, T fallback = default(T))
        {
            object value;
            return dictionary.TryGetValue(key, out value) ? (T)value : fallback;
        }
    }
}