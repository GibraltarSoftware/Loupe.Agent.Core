using System;
using System.Collections.Generic;
using System.Text;
using Gibraltar;
using NUnit.Framework;

namespace Loupe.Core.Test.Core
{
    [TestFixture]
    public class FilePathTests
    {
        [Test]
        public void File_Is_Lower_Case()
        {
            var originalValue = "THIS IS UPPER CASE";
            var testValue = FileSystemTools.SanitizeFileName(originalValue, false, true);
            Assert.That(originalValue.ToLowerInvariant(), Is.EqualTo(testValue));
        }

        [Test]
        public void Path_Is_Lower_Case()
        {
            var originalValue = "THIS IS UPPER CASE";
            var testValue = FileSystemTools.SanitizeDirectoryName(originalValue, false, true);
            Assert.That(originalValue.ToLowerInvariant(), Is.EqualTo(testValue));
        }

        [Test]
        public void File_Spaces_Are_Removed()
        {
            var originalValue = "THIS IS UPPER CASE";
            var testValue = FileSystemTools.SanitizeFileName(originalValue, true, true);
            Assert.That(testValue.Contains(" ") == false);
        }

        [Test]
        public void Path_Spaces_Are_Removed()
        {
            var originalValue = "THIS IS UPPER CASE";
            var testValue = FileSystemTools.SanitizeDirectoryName(originalValue, true, true);
            Assert.That(testValue.Contains(" ") == false);
        }

        [Test]
        public void File_Spaces_Are_Not_Removed()
        {
            var originalValue = "THIS IS UPPER CASE";
            var testValue = FileSystemTools.SanitizeFileName(originalValue, false, true);
            Assert.That(testValue.Contains(" "));
        }

        [Test]
        public void Path_Spaces_Are_Not_Removed()
        {
            var originalValue = "THIS IS UPPER CASE";
            var testValue = FileSystemTools.SanitizeDirectoryName(originalValue, false, true);
            Assert.That(testValue.Contains(" "));
        }

        [Test]
        public void File_Tabs_Are_Removed()
        {
            {
                var originalValue = "THIS\tIS\tUPPER\tCASE";
                var testValue = FileSystemTools.SanitizeFileName(originalValue, false, true);
                Assert.That(testValue.Contains("\t") == false);
            }
        }

        [Test]
        public void Path_Tabs_Are_Removed()
        {
            {
                var originalValue = "THIS\tIS\tUPPER\tCASE";
                var testValue = FileSystemTools.SanitizeDirectoryName(originalValue, false, true);
                Assert.That(testValue.Contains("\t") == false);
            }
        }
    }
}
