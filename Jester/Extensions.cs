using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace x0.Jester
{
    internal static class Extensions
    {
#if UNITY_2020_1_OR_NEWER
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryAdd<TKey, TValue>(this IDictionary<TKey, TValue> dict, TKey key, TValue value)
        {
            if (!dict.ContainsKey(key)) {
                dict.Add(key, value);
                return true;
            }
            return false;
        }
#endif
    }
}
