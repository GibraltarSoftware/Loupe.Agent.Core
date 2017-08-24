using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Gibraltar;
using Gibraltar.Data;
using Gibraltar.Data.Internal;
using Gibraltar.Monitor;
using Loupe.Extensibility.Data;
using NUnit.Framework;

namespace Loupe.Core.Test.Data
{
    [TestFixture]
    public class PackagerTests
    {
        private const string SDSCustomerName = "esymmetrix";

        private string m_OutputFileNamePath;
        private PackageSendEventArgs m_EndSendResult;
        private bool m_EndSendReceived;

        [OneTimeSetUp]
        public void Init()
        {
            if (string.IsNullOrEmpty(m_OutputFileNamePath) == false)
            {
                //we're re-initing:  delete any existing temp file.
                File.Delete(m_OutputFileNamePath);
            }

            string tempFileName = Path.GetTempFileName();
            File.Delete(tempFileName); //just trying to be clean

            m_OutputFileNamePath = tempFileName + "." + Log.PackageExtension;
            File.Delete(m_OutputFileNamePath); //get temp file name creates the file as part of allocating the name.
        }

        [Test]
        public void CreateNonePackage()
        {
            using (Packager newPackager = new Packager())
            {
                newPackager.SendToFile(SessionCriteria.None, false, m_OutputFileNamePath, false);
            }

            //because this is the none package it won't have any content, but won't thrown an error.
            Assert.IsFalse(File.Exists(m_OutputFileNamePath));
            File.Delete(m_OutputFileNamePath);
        }

        [Test]
        public void CreateNewSessionsPackage()
        {
            var newSessions = GetNewSessions();
            if (newSessions.Count == 0)
            {
                Trace.TraceInformation("Unable to run test CreateNewSessionsPackage because there were no new sessions at the start of the test.");
                return;
            }

            using (Packager newPackager = new Packager())
            {
                newPackager.SendToFile(SessionCriteria.NewSessions, false, m_OutputFileNamePath, false);
            }

            Assert.IsTrue(File.Exists(m_OutputFileNamePath));
            File.Delete(m_OutputFileNamePath);
        }

        [Test]
        public void CreateAllSessionsPackage()
        {
            using (Packager newPackager = new Packager())
            {
                newPackager.SendToFile(SessionCriteria.AllSessions, false, m_OutputFileNamePath, false);
            }

            Assert.IsTrue(File.Exists(m_OutputFileNamePath));
            File.Delete(m_OutputFileNamePath);
        }

        [Test]
        public void CreateActiveSessionPackage()
        {
            using (Packager newPackager = new Packager())
            {
                newPackager.SendToFile(SessionCriteria.ActiveSession, false, m_OutputFileNamePath, false);
            }

            Assert.IsTrue(File.Exists(m_OutputFileNamePath));
            File.Delete(m_OutputFileNamePath);
        }

        [Test]
        public void MarkSessionsAsRead()
        {
            //find all of the "new" sessions
            var newSessions = GetNewSessions();

            if (newSessions.Count == 0)
            {
                Trace.TraceInformation("Unable to run test MarkSessionsAsRead because there were no new sessions at the start of the test.");
                return;
            }

            using (Packager newPackager = new Packager(Log.SessionSummary.Product))
            {
                //create the package
                newPackager.SendToFile(SessionCriteria.NewSessions, true, m_OutputFileNamePath, false);
            }
            Assert.IsTrue(File.Exists(m_OutputFileNamePath));

            //Now find out what new sessions there are to compare.
            var newSessionsPost = GetNewSessions();

            //and compare the two.
            if (newSessionsPost.Count == 0)
            {
                //we HAVE to be good - there are no new.
            }
            else
            {
                //we MIGHT still be good - if none of our new sessions are still there.
                foreach (var newSession in newSessions)
                {
                    //this session Id better not be in the new list....
                    Assert.AreEqual(null, newSessionsPost.Find(new Predicate<ISessionSummary>(x => x.Id == newSession.Id)), "The session {0} is still in the index as new and should have been marked sent.", newSession.Id); //null is not found
                }
            }

            File.Delete(m_OutputFileNamePath);
        }
        
        [Test]
        public void SendPackageViaWeb()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                using (Packager newPackager = new Packager())
                {
                    //we are saying don't override config,then basically sending default values for all of the config overrides (there is no overload that skips them)
                    newPackager.SendToServer(SessionCriteria.AllSessions, false, false, false, false, null, null, 0, false, null, null, false);
                }
            });
        }

        [Test]
        public void SendPackageViaWebOverrideCustomer()
        {
            using (Packager newPackager = new Packager())
            {
                newPackager.SendToServer(SessionCriteria.AllSessions, false, false, true, true, SDSCustomerName, null, 0, false, null, null, false);
            }
        }

        [Ignore("We don't have a local web server for testing")]
        [Test]
        public void SendPackageToLocalWebServer()
        {
            using (Packager newPackager = new Packager())
            {
                newPackager.SendToServer(SessionCriteria.AllSessions, false, false, true, false, null, "localhost", 58330, false, null, null, false);
            }
        }

        [Test]
        public void SendPackageViaWebFail()
        {
            Assert.Throws<GibraltarException>(() =>
            {
                using (Packager newPackager = new Packager())
                {
                    newPackager.SendToServer(SessionCriteria.AllSessions, false, false, true, true, "BogusNonexistantCustomer", null, 0, false, null, null, false);
                }
            });
        }

        [Test]
        public void SendPackageViaWebAsyncFail()
        {
            using (Packager newPackager = new Packager())
            {
                m_EndSendReceived = false;
                m_EndSendResult = null;
                newPackager.EndSend += Packager_EndSend;
                newPackager.SendToServer(SessionCriteria.AllSessions, false, false, true, true, "BogusNonexistantCustomer", null, 0, false, null, null, true);

                while (m_EndSendReceived == false)
                {
                    Thread.Sleep(16);
                }
                newPackager.EndSend -= Packager_EndSend;

                //we better have gotten a fail from the end sent result.
                Assert.IsNotNull(m_EndSendResult);
                Assert.IsTrue(m_EndSendResult.Result == AsyncTaskResult.Error);
            }
            m_EndSendReceived = false;
            m_EndSendResult = null;
        }

        private void Packager_EndSend(object sender, PackageSendEventArgs args)
        {
            m_EndSendReceived = true;
            m_EndSendResult = args;
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

        #region Private Properties and Methods

        private static ISessionSummaryCollection GetNewSessions()
        {
            /*
            DataSet allSessions = RepositoryManager.Collection.GetSessions();

            string productName, applicationName, applicationDescription;
            Version applicationVersion;
            ContextSummary.GetApplicationNameSafe(out productName, out applicationName, out applicationVersion, out applicationDescription);

            string whereClause = string.Format(CultureInfo.InvariantCulture, "[Product_Name] = '{0}' AND [Application_Name] = '{1}' AND [Status_Name] <> 'running' AND [Is_New] = 1",
                                               productName, applicationName);

            //filter the set of sessions to just new sessions
            DataView newSessions = new DataView(allSessions.Tables[0], whereClause, null, DataViewRowState.CurrentRows);
            return newSessions;
             * */

            var localRepository = new LocalRepository(Log.SessionSummary.Product);
            return localRepository.Find(new SessionCriteriaPredicate(Log.SessionSummary.Product, null, SessionCriteria.NewSessions).Predicate);
        }

        #endregion
    }
}