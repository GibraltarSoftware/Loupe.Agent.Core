using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Gibraltar.Data;
using Gibraltar.Monitor;
using Gibraltar.Server.Client;
using Loupe.Configuration;
using Loupe.Extensibility.Data;
using NUnit.Framework;

namespace Loupe.Core.Test.Server
{
    [TestFixture]
    public class When_Uploading_Sessions
    {
        private const string LogCategory = nameof(When_Uploading_Sessions);

        private ServerConfiguration m_ServerConfiguration;
        private LocalRepository m_Repository;

        public When_Uploading_Sessions()
        {
            m_ServerConfiguration = new ServerConfiguration
            {
                UseGibraltarService = true,
                CustomerName = "configurationtest"
            };
        }


        [Test]
        public async Task Can_Recover_From_Missing_Data()
        {
            var connection = GetConnection();
            var repository = GetRepository();

            SessionHeader header;
            var fileLength = 0L;
            using (var sessionStream =
                File.OpenRead(Path.Combine(TestContext.CurrentContext.TestDirectory, "Content", "SampleSession.glf")))
            {
                sessionStream.Position = 0;
                var reader = new GLFReader(sessionStream);
                header = reader.SessionHeader;

                fileLength = sessionStream.Length;

                repository.AddSession(sessionStream);
            }

            using (var request = new SessionUploadRequest(repository.Id, repository, header.Id, header.FileId, false))
            {
                var progressFile = request.GenerateTemporarySessionFileNamePath();

                //mess with the progress file...
                using (var sessionTrackingFileStream = new FileStream(progressFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    using (var writer = new BinaryWriter(sessionTrackingFileStream, Encoding.UTF8))
                    {
                        var partialFileLength = Convert.ToInt64(fileLength * 0.7);
                        writer.Write(partialFileLength);
                        writer.Flush();
                        sessionTrackingFileStream.SetLength(sessionTrackingFileStream.Position);
                    }
                }

                //because upload request uses a multiprocess lock we put it in a using to ensure it gets disposed.
                //explicitly prepare the session - this returns true if we got the lock meaning no one else is actively transferring this session right now.
                Assert.IsTrue(request.PrepareSession());

                //this should internally recover from starting mid-stream.
                await connection.ExecuteRequest(request, 0).ConfigureAwait(false);
            }
        }

        private HubConnection GetConnection()
        {
            var connection = new HubConnection(m_ServerConfiguration);
            return connection;
        }

        private LocalRepository GetRepository()
        {
            if (m_Repository == null)
            {
                var repoDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory,
                    nameof(When_Uploading_Sessions), "Repository");
                m_Repository = new LocalRepository(Log.SessionSummary.Product, repoDirectory);
            }

            return m_Repository;
        }
    }
}
