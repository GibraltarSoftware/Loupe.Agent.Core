using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace Loupe.Core.Test
{
    internal static class Extensions
    {
        public static string ToDisplayString(this byte[] array)
        {
            return BitConverter.ToString(array);
        }


        public static void CompareArray<T>(this T[] source, T[] dest)
        {
            Assert.AreEqual(source.Length, dest.Length);
            for (var i = 0; i < source.Length; i++)
                Assert.AreEqual(source[i], dest[i]);
        }
    }
}
