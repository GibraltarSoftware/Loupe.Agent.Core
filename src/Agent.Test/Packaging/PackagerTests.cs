using System;
using System.IO;
using System.Runtime.InteropServices;
using Gibraltar.Agent;
using NUnit.Framework;

namespace Loupe.Agent.Test.Packaging
{
    [TestFixture]
    public class PackagerTests
    {
        private const string ServerOverrideCustomerName = "ConfigurationTest";
        private const string ServerOverrideServerName = "us.onloupe.com";
        private const string ServerOverrideBaseDirectory = "";

        private const string ServerOverrideRepository = "ConfigurationTest";

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

            //we have to smack on a GLP or the extension will get replaced.
            m_OutputFileNamePath += "." + Log.PackageExtension;
            File.Delete(m_OutputFileNamePath); //get temp file name creates the file as part of allocating the name.
        }


        [Test]
        public void CreateEmptyPackage()
        {
            using (Packager newPackager = new Packager())
            {
                File.Delete(m_OutputFileNamePath);
                newPackager.SendToFile(0, false, m_OutputFileNamePath);
            }

            //because this is the none package it won't have any content, but won't thrown an error.
            Assert.IsFalse(File.Exists(m_OutputFileNamePath));
            File.Delete(m_OutputFileNamePath);
        }

        [Test]
        public void CreateNonePackage()
        {
            using (Packager newPackager = new Packager())
            {
                File.Delete(m_OutputFileNamePath);
                newPackager.SendToFile(SessionCriteria.None, false, m_OutputFileNamePath);
            }

            //because this is the none package it won't have any content, but won't thrown an error.
            Assert.IsFalse(File.Exists(m_OutputFileNamePath));
            File.Delete(m_OutputFileNamePath);
        }

        [Test]
        public void CreateNewSessionsPackage()
        {
            using (Packager newPackager = new Packager())
            {
                File.Delete(m_OutputFileNamePath);
                newPackager.SendToFile(SessionCriteria.NewSessions, false, m_OutputFileNamePath);
            }

            //we don't do the assert on this one because there may be no new sessions package.
            File.Delete(m_OutputFileNamePath);
        }

        [Test]
        public void CreateAllSessionsPackage()
        {
            using (Packager newPackager = new Packager())
            {
                File.Delete(m_OutputFileNamePath);
                newPackager.SendToFile(SessionCriteria.AllSessions, false, m_OutputFileNamePath);
            }

            Assert.IsTrue(File.Exists(m_OutputFileNamePath));
            File.Delete(m_OutputFileNamePath);
        }

        [Test]
        public void CreateActiveSessionPackage()
        {
            //we need to guarantee that there is something being logged for this test to work.
            Log.StartSession("Just making sure we're up and logging for this test.");

            using (Packager newPackager = new Packager())
            {
                File.Delete(m_OutputFileNamePath);
                newPackager.SendToFile(SessionCriteria.ActiveSession, false, m_OutputFileNamePath);
            }

            Assert.IsTrue(File.Exists(m_OutputFileNamePath));
            File.Delete(m_OutputFileNamePath);
        }

        [Test]
        public void CreateCompletedSessionPackage()
        {
            using (Packager newPackager = new Packager())
            {
                File.Delete(m_OutputFileNamePath);
                newPackager.SendToFile(SessionCriteria.CompletedSessions, false, m_OutputFileNamePath);
            }

            //we don't do the assert on this one because there may be no completed sessions package.
            File.Delete(m_OutputFileNamePath);
        }

        [Test]
        public void CreateCriticalSessionPackage()
        {
            using (Packager newPackager = new Packager())
            {
                File.Delete(m_OutputFileNamePath);
                newPackager.SendToFile(SessionCriteria.CriticalSessions, false, m_OutputFileNamePath);
            }

            //we don't really know we have one of these, so no assertion.
            File.Delete(m_OutputFileNamePath);
        }

        [Test]
        public void CreateErrorSessionPackage()
        {
            using (Packager newPackager = new Packager())
            {
                File.Delete(m_OutputFileNamePath);
                newPackager.SendToFile(SessionCriteria.ErrorSessions, false, m_OutputFileNamePath);
            }

            //we don't do the assert on this one because there may be no error sessions package.
            File.Delete(m_OutputFileNamePath);
        }

        [Test]
        public void CreateWarningSessionPackage()
        {
            using (Packager newPackager = new Packager())
            {
                File.Delete(m_OutputFileNamePath);
                newPackager.SendToFile(SessionCriteria.WarningSessions, false, m_OutputFileNamePath);
            }

            //we don't do the assert on this one because there may be no warning sessions package.
            File.Delete(m_OutputFileNamePath);
        }

        [Test]
        public void CreateCombinationSessionPackage()
        {
            using (Packager newPackager = new Packager())
            {
                File.Delete(m_OutputFileNamePath);
                newPackager.SendToFile(SessionCriteria.WarningSessions | SessionCriteria.NewSessions | SessionCriteria.None, false, m_OutputFileNamePath);
            }

            //we don't do the assert on this one because there may be no sessions package.
            File.Delete(m_OutputFileNamePath);
        }

        [Test]
        public void CreatePredicateLambdaPackage()
        {
            using (Packager newPackager = new Packager())
            {
                File.Delete(m_OutputFileNamePath);
                newPackager.SendToFile((s) => s.HostName == Log.SessionSummary.HostName, false, m_OutputFileNamePath);
            }

            Assert.IsTrue(File.Exists(m_OutputFileNamePath));
            File.Delete(m_OutputFileNamePath);            
        }

        [Test]
        public void CreatePredicateNamedMethodPackage()
        {
            using (Packager newPackager = new Packager())
            {
                m_PredicateMatches = 0;
                File.Delete(m_OutputFileNamePath);
                newPackager.SendToFile(OnPackagerNamedMethodPredicate, false, m_OutputFileNamePath);
            }

            Assert.IsTrue(File.Exists(m_OutputFileNamePath));
            Assert.IsTrue(m_PredicateMatches > 0);
            File.Delete(m_OutputFileNamePath);
        }

        private int m_PredicateMatches;

        private bool OnPackagerNamedMethodPredicate(SessionSummary session)
        {
            Assert.IsNotNull(session);

            var match = (session.Application == Log.SessionSummary.Application) && (session.Product == Log.SessionSummary.Product);

            if (match)
                m_PredicateMatches++;

            return match;
        }

        [Test]
        public void CreatePackageFromAlternateDirectory()
        {
            //find our normal log directory..
            var loggingPath = Path.Combine(Environment.GetEnvironmentVariable(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ProgramData" : "Home"),
                "Gibraltar\\Local Logs\\Loupe");

            var tempDir = Path.Combine(Path.GetTempPath(), "PackagerTests");
            Directory.CreateDirectory(tempDir);
            try
            {
                var logFiles = Directory.GetFiles(loggingPath, "*.glf");
                for(int curFileIndex = 0; curFileIndex < Math.Min(10, logFiles.Length); curFileIndex++)
                {
                    var logFileNamePath = logFiles[curFileIndex];
                    File.Copy(logFileNamePath, Path.Combine(tempDir, Path.GetFileName(logFileNamePath)));
                }

                using (Packager newPackager = new Packager("Loupe", null, loggingPath))
                {
                    File.Delete(m_OutputFileNamePath);
                    newPackager.SendToFile(SessionCriteria.AllSessions, false, m_OutputFileNamePath);
                }

                Assert.IsTrue(File.Exists(m_OutputFileNamePath));
            }
            finally
            {
                File.Delete(m_OutputFileNamePath);
                Directory.Delete(tempDir, true);
            }
        }


        [Test]
        public void SendPackageViaServer()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                using (Packager newPackager = new Packager())
                {
                    newPackager.SendToServer(SessionCriteria.AllSessions, false);
                }
            });
        }

        [Test]
        public void SendPackageViaServerOverrideCustomer()
        {
            using (Packager newPackager = new Packager())
            {
                newPackager.SendToServer(SessionCriteria.AllSessions, false, ServerOverrideCustomerName);
            }
        }

        [Test]
        public void SendPackageViaServerOverrideServer()
        {
            using (Packager newPackager = new Packager())
            {
                newPackager.SendToServer(SessionCriteria.AllSessions, false, ServerOverrideServerName, 0, false, ServerOverrideBaseDirectory, null);
            }
        }

        [Test]
        public void SendPackageViaServerOverrideServerAndRepository()
        {
            using (Packager newPackager = new Packager())
            {
                newPackager.SendToServer(SessionCriteria.AllSessions, false, "hub.gibraltarsoftware.com", 0, true, null, ServerOverrideRepository);
            }
        }

        [Test]
        public void SendPackageViaServerMissingArgsCustomerNull()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                using (Packager newPackager = new Packager())
                {
                    newPackager.SendToServer(SessionCriteria.AllSessions, false, null);
                }
            });
        }

        [Test]
        public void SendPackageViaServerMissingArgsCustomerEmpty()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                using (Packager newPackager = new Packager())
                {
                    newPackager.SendToServer(SessionCriteria.AllSessions, false, "");
                }
            });
        }

        [Test]
        public void SendPackageViaServerMissingArgsServerNull()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                using (Packager newPackager = new Packager())
                {
                    newPackager.SendToServer(SessionCriteria.AllSessions, false, null, 0, false, null, null);
                }
            });
        }

        [Test]
        public void SendPackageViaServerMissingArgsServerEmpty()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                using (Packager newPackager = new Packager())
                {
                    newPackager.SendToServer(SessionCriteria.AllSessions, false, "", 0, false, null, null);
                }
            });
        }

        [Test]
        public void SendPackageViaServerBadArgsPortNegative()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                using (Packager newPackager = new Packager())
                {
                    newPackager.SendToServer(SessionCriteria.AllSessions, false, ServerOverrideServerName, -20, false, ServerOverrideBaseDirectory, null);
                }
            });
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            if ((string.IsNullOrEmpty(m_OutputFileNamePath) == false)
                && (File.Exists(m_OutputFileNamePath)))
            {
                File.Delete(m_OutputFileNamePath);
            }
        }
    }
}