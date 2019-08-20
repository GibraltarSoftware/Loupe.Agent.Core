using System;
using Loupe;
using NUnit.Framework;

namespace Loupe.Core.Test
{
    [TestFixture]
    public class StringReferenceTests
    {
        private static string GetTestString(int number)
        {
            return "test " + number;
        }

        private static void GarbageCollection()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        [Test]
#if !DEBUG
        [Ignore("debugging test only")] // Until we can fix the Release optimizations breaking the GC expectations.
#endif
        public void WeakStringCollectionTest()
        {
            string primeZero = GetTestString(0);
            string primeOne = GetTestString(1);
            string primeTwo = GetTestString(2);
            string primeThree = GetTestString(3);

            StringReference.WeakStringCollection testCollection = new StringReference.WeakStringCollection(primeZero);
            Assert.AreEqual(1, testCollection.Count);
            primeZero = null;
            GC.Collect(); // And make sure GC kills it.
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.AreEqual(0, testCollection.Pack()); // Confirm that it's gone.
            primeZero = GetTestString(0);
            Assert.AreEqual(1, testCollection.PackAndOrAdd(ref primeZero)); // Add it back.

            string testZero = GetTestString(0);
            Assert.IsFalse(ReferenceEquals(testZero, primeZero));
            Assert.AreEqual(1, testCollection.PackAndOrAdd(ref testZero));
            Assert.IsTrue(ReferenceEquals(testZero, primeZero));

            primeZero = null; // Remove the original reference.
            GC.Collect(); // And make sure GC kills it (but won't kill the new copy).
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.AreEqual(1, testCollection.Pack());

            testZero = null; // Remove the new reference.
            GC.Collect(); // And make sure GC kills it.
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.AreEqual(1, testCollection.PackAndOrAdd(ref primeOne));

            primeZero = GetTestString(0);
            Assert.AreEqual(2, testCollection.PackAndOrAdd(ref primeZero));
            testZero = GetTestString(0);
            Assert.IsFalse(ReferenceEquals(testZero, primeZero));
            Assert.AreEqual(2, testCollection.PackAndOrAdd(ref testZero));
            Assert.IsTrue(ReferenceEquals(testZero, primeZero));

            primeOne = null; // Remove the original reference.
            GC.Collect(); // And make sure GC kills it.
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.AreEqual(2, testCollection.PackAndOrAdd(ref primeTwo));
            primeOne = GetTestString(1);
            Assert.AreEqual(3, testCollection.PackAndOrAdd(ref primeOne));
            Assert.AreEqual(4, testCollection.PackAndOrAdd(ref primeThree));

            testZero = GetTestString(0);
            Assert.IsFalse(ReferenceEquals(testZero, primeZero));
            Assert.AreEqual(4, testCollection.PackAndOrAdd(ref testZero));
            Assert.IsTrue(ReferenceEquals(testZero, primeZero));

            primeZero = null; // Remove the original reference (but not the testZero reference).
            primeOne = null; // Remove the only reference.
            primeTwo = null; // Remove the only reference.
            GC.Collect(); // And make sure GC kills them.
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.AreEqual(2, testCollection.Pack());

            primeZero = GetTestString(0);
            Assert.IsFalse(ReferenceEquals(testZero, primeZero));
            Assert.AreEqual(2, testCollection.PackAndOrAdd(ref primeZero));
            Assert.IsTrue(ReferenceEquals(testZero, primeZero));
        }
    }
}
