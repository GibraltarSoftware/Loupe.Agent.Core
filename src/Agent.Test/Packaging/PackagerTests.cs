using System;
using System.IO;
using System.Runtime.InteropServices;
using Gibraltar.Agent;
using Loupe.Configuration;
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

            Log.EndFile("Rolling over file to be sure we have something to package");
        }

        [SetUp]
        public void Setup()
        {
            File.Delete(m_OutputFileNamePath);
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
                newPackager.SendToFile(SessionCriteria.AllSessions, false, m_OutputFileNamePath);
            }

            Assert.IsTrue(File.Exists(m_OutputFileNamePath), "There was no output file from the package, most likely because there is no local data to package (like when unit tests are run the first time)");
        }

        [Test]
        public void CreateActiveSessionPackage()
        {
            //we need to guarantee that there is something being logged for this test to work.
            Log.StartSession("Just making sure we're up and logging for this test.");

            using (Packager newPackager = new Packager())
            {
                newPackager.SendToFile(SessionCriteria.ActiveSession, false, m_OutputFileNamePath);
            }

            Assert.IsTrue(File.Exists(m_OutputFileNamePath), "There was no output file from the package, most likely because there is no local data to package (like when unit tests are run the first time)");
        }

        [Test]
        public void CreateCompletedSessionPackage()
        {
            using (Packager newPackager = new Packager())
            {
                newPackager.SendToFile(SessionCriteria.CompletedSessions, false, m_OutputFileNamePath);
            }

            //we don't do the assert on this one because there may be no completed sessions package.
        }

        [Test]
        public void CreateCriticalSessionPackage()
        {
            using (Packager newPackager = new Packager())
            {
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
                newPackager.SendToFile(SessionCriteria.ErrorSessions, false, m_OutputFileNamePath);
            }

            //we don't do the assert on this one because there may be no error sessions package.
        }

        [Test]
        public void CreateWarningSessionPackage()
        {
            using (Packager newPackager = new Packager())
            {
                newPackager.SendToFile(SessionCriteria.WarningSessions, false, m_OutputFileNamePath);
            }

            //we don't do the assert on this one because there may be no warning sessions package.
        }

        [Test]
        public void CreateCombinationSessionPackage()
        {
            using (Packager newPackager = new Packager())
            {
                newPackager.SendToFile(SessionCriteria.WarningSessions | SessionCriteria.NewSessions | SessionCriteria.None, false, m_OutputFileNamePath);
            }

            //we don't do the assert on this one because there may be no sessions package.
        }

        [Test]
        public void CreatePredicateLambdaPackage()
        {
            using (Packager newPackager = new Packager())
            {
                newPackager.SendToFile((s) => s.HostName == Log.SessionSummary.HostName, false, m_OutputFileNamePath);
            }

            Assert.IsTrue(File.Exists(m_OutputFileNamePath), "There was no output file from the package, most likely because there is no local data to package (like when unit tests are run the first time)");
        }

        [Test]
        public void CreatePredicateNamedMethodPackage()
        {
            using (Packager newPackager = new Packager())
            {
                m_PredicateMatches = 0;
                newPackager.SendToFile(OnPackagerNamedMethodPredicate, false, m_OutputFileNamePath);
            }

            Assert.IsTrue(File.Exists(m_OutputFileNamePath), "There was no output file from the package, most likely because there is no local data to package (like when unit tests are run the first time)");
            Assert.IsTrue(m_PredicateMatches > 0);
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
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir); //so we start from a known, blank position.

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
                    newPackager.SendToFile(SessionCriteria.AllSessions, false, m_OutputFileNamePath);
                }

                Assert.IsTrue(File.Exists(m_OutputFileNamePath));
            }
            finally
            {
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
            var server = new ServerConfiguration
            {
                UseGibraltarService = true,
                CustomerName = ServerOverrideCustomerName
            };
            using (Packager newPackager = new Packager())
            {
                newPackager.SendToServer(SessionCriteria.AllSessions, false, server);
            }
        }

        [Test]
        public void SendPackageViaServerOverrideServer()
        {
            var server = new ServerConfiguration
            {
                Server = ServerOverrideServerName,
                Repository = ServerOverrideRepository,
                UseSsl = true
            };
            using (Packager newPackager = new Packager())
            {
                newPackager.SendToServer(SessionCriteria.AllSessions, false, server);
            }
        }

        [Test]
        public void SendPackageViaServerOverrideServerAndRepository()
        {
            var server = new ServerConfiguration
            {
                Server = "hub.gibraltarsoftware.com",
                Repository = ServerOverrideRepository,
                UseSsl = true
            };
            using (Packager newPackager = new Packager())
            {
                newPackager.SendToServer(SessionCriteria.AllSessions, false, server);
            }
        }

        [Test]
        public void SendPackageViaServerMissingArgsCustomerNull()
        {
            var server = new ServerConfiguration
            {
                UseGibraltarService = true,
                CustomerName = null
            };
            Assert.Throws<ArgumentException>(() =>
            {
                using (Packager newPackager = new Packager())
                {
                    newPackager.SendToServer(SessionCriteria.AllSessions, false, server);
                }
            });
        }

        [Test]
        public void SendPackageViaServerMissingArgsCustomerEmpty()
        {
            var server = new ServerConfiguration
            {
                UseGibraltarService = true,
                CustomerName = String.Empty
            };
            Assert.Throws<ArgumentException>(() =>
            {
                using (Packager newPackager = new Packager())
                {
                    newPackager.SendToServer(SessionCriteria.AllSessions, false, server);
                }
            });
        }

        [Test]
        public void SendPackageViaServerMissingArgsServerNull()
        {
            var server = new ServerConfiguration
            {
                UseGibraltarService = false,
                Server = null
            };
            Assert.Throws<ArgumentException>(() =>
            {
                using (Packager newPackager = new Packager())
                {
                    newPackager.SendToServer(SessionCriteria.AllSessions, false, server);
                }
            });
        }

        [Test]
        public void SendPackageViaServerMissingArgsServerEmpty()
        {
            var server = new ServerConfiguration
            {
                UseGibraltarService = false,
                Server = string.Empty
            };
            Assert.Throws<ArgumentException>(() =>
            {
                using (Packager newPackager = new Packager())
                {
                    newPackager.SendToServer(SessionCriteria.AllSessions, false, server);
                }
            });
        }

        [Test]
        public void SendPackageViaServerBadArgsPortNegative()
        {
            var server = new ServerConfiguration
            {
                UseGibraltarService = false,
                Server = ServerOverrideServerName,
                Port = -20,
                ApplicationBaseDirectory = ServerOverrideBaseDirectory
            };
            Assert.Throws<ArgumentException>(() =>
            {
                using (Packager newPackager = new Packager())
                {
                    newPackager.SendToServer(SessionCriteria.AllSessions, false, server);
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