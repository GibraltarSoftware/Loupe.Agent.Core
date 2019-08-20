using System.IO;
using Loupe;
using Loupe.Core.Data;
using Loupe.Core.IO;
using Loupe.Core.Monitor;
using NUnit.Framework;

namespace Loupe.Core.Test.Data
{
    [TestFixture]
    public class SimplePackageTests
    {
        private string m_OutputFileNamePath;

        [OneTimeSetUp]
        public void Init()
        {
            if (string.IsNullOrEmpty(m_OutputFileNamePath) == false)
            {
                //we're re-initing:  delete any existing temp file.
                File.Delete(m_OutputFileNamePath);
            }

            m_OutputFileNamePath = Path.GetTempFileName();
            File.Delete(m_OutputFileNamePath); //get temp file name creates the file as part of allocating the name.
            m_OutputFileNamePath += "." + Log.PackageExtension;
        }

        [Test]
        public void CreateEmptyPackage()
        {
            using(var package = new SimplePackage())
            {
                using (ProgressMonitorStack stack = new ProgressMonitorStack("saving Package"))
                {
                    package.Save(stack, m_OutputFileNamePath);
                }
            }

            Assert.IsTrue(File.Exists(m_OutputFileNamePath), "Package was not created");

            File.Delete(m_OutputFileNamePath);
        }

        [Test]
        public void CreateLargePackage()
        {
            using (var package = new SimplePackage())
            {
                DirectoryInfo repository = new DirectoryInfo(LocalRepository.CalculateRepositoryPath(Log.SessionSummary.Product));

                FileInfo[] allExistingFileFragments = repository.GetFiles("*." + Log.LogExtension, SearchOption.TopDirectoryOnly);

                foreach (var fileFragment in allExistingFileFragments)
                {
                    var sourceFile = FileHelper.OpenFileStream(fileFragment.FullName, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                    if (sourceFile != null)
                    {
                        package.AddSession(sourceFile);
                    }
                }

                using (ProgressMonitorStack stack = new ProgressMonitorStack("saving Package"))
                {
                    package.Save(stack, m_OutputFileNamePath);
                }
            }

            Assert.IsTrue(File.Exists(m_OutputFileNamePath), "Packge was not created");
            Assert.Greater(new FileInfo(m_OutputFileNamePath).Length, 100, "The package was likely empty but should have contained multiple sessions.");

            File.Delete(m_OutputFileNamePath);            
        }
    }
}
