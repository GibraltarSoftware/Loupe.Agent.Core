using Loupe.Core.Monitor.Serialization;
using NUnit.Framework;

namespace Loupe.Core.Test.Core
{
    [TestFixture]
    public class HelperMethodTests
    {
        [Test]
        public void TextParseTrailingNull()
        {
            //one trailing period
            var stringFragments = TextParse.CategoryName("unmanagedDvm.storageSubsystem.<04:21:59 PM> 11/10/2011: pipeline driver 0 started successfully.");
            VerifyStringFragments(stringFragments, 3);
        }

        [Test]
        public void TextParseCategoryTrailingEmpty()
        {
            var stringFragments = TextParse.CategoryName("unmanagedDvm.storageSubsystem.<04:21:59 PM> 11/10/2011: pipeline driver 0 started successfully. ");
            VerifyStringFragments(stringFragments, 3);
        }

        [Test]
        public void TextParseCategoryInternalNull()
        {
            var stringFragments = TextParse.CategoryName("unmanagedDvm.storageSubsystem..<04:21:59 PM> 11/10/2011: pipeline driver 0 started successfully.");
            VerifyStringFragments(stringFragments, 3);
        }

        [Test]
        public void TextParseCategoryInternalEmpty()
        {
            var stringFragments = TextParse.CategoryName("unmanagedDvm.storageSubsystem. .<04:21:59 PM> 11/10/2011: pipeline driver 0 started successfully.");
            VerifyStringFragments(stringFragments, 3);
        }

        [Test]
        public void TextParseCategoryFirstNull()
        {
            var stringFragments = TextParse.CategoryName(".unmanagedDvm.storageSubsystem.<04:21:59 PM> 11/10/2011: pipeline driver 0 started successfully.");
            VerifyStringFragments(stringFragments, 3);
        }

        [Test]
        public void TextParseCategoryFirstEmpty()
        {
            //interior white space segment
            var stringFragments = TextParse.CategoryName(" .unmanagedDvm.storageSubsystem.<04:21:59 PM> 11/10/2011: pipeline driver 0 started successfully.");
            VerifyStringFragments(stringFragments, 3);
        }

        [Test]
        public void TextParseCategoryAllEmpty()
        {
            //interior white space segment
            var stringFragments = TextParse.CategoryName(" .  .  . ");
            VerifyStringFragments(stringFragments, 0);
        }

        [Test]
        public void TextParseCategoryAllNull()
        {
            //interior white space segment
            var stringFragments = TextParse.CategoryName("....");
            VerifyStringFragments(stringFragments, 0);
        }

        [Test]
        public void TextParseCategoryEmpty()
        {
            //interior white space segment
            var stringFragments = TextParse.CategoryName("");
            VerifyStringFragments(stringFragments, 0);
        }

        [Test]
        public void TextParseCategoryNull()
        {
            //interior white space segment
            var stringFragments = TextParse.CategoryName(null);
            VerifyStringFragments(stringFragments, 0);
        }

        private void VerifyStringFragments(string[] fragments, int expectedCount)
        {
            Assert.IsNotNull(fragments, "No fragments array was provided");

            Assert.AreEqual(expectedCount, fragments.Length);

            for (int curFragmentIndex = 0; curFragmentIndex < expectedCount; curFragmentIndex++ )
            {
                string fragment = fragments[curFragmentIndex];
                Assert.IsNotNull(fragment, "The string fragment in position {0} was null", curFragmentIndex);
                Assert.IsNotEmpty(fragment, "The string fragment in position {0} was null", curFragmentIndex);
            }
        }
    }
}
