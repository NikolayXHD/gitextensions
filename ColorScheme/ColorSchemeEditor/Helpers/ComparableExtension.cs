using System;

namespace ColorScheme
{
    public static class ComparableExtension
    {
        public static float Modulo(this float v, int d)
        {
            v = v % d;
            if (v < 0)
                v += d;

            return v;
        }

        public static TVal WithinRange<TVal>(this TVal value, TVal min, TVal max) where TVal : IComparable<TVal> =>
            value.AtLeast(min).AtMost(max);

        public static TVal AtLeast<TVal>(this TVal value, TVal min) where TVal : IComparable<TVal> =>
            value.CompareTo(min) < 0 ? min : value;

        public static TVal AtMost<TVal>(this TVal value, TVal max) where TVal : IComparable<TVal> =>
            value.CompareTo(max) > 0 ? max : value;
    }
}