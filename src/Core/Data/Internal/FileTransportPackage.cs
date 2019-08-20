using System;
using System.Globalization;
using System.IO;



namespace Loupe.Data.Internal
{
    /// <summary>
    /// A transport package that is just being written out to a file.
    /// </summary>
    internal class FileTransportPackage : TransportPackageBase
    {
        public FileTransportPackage(string product, string application, SimplePackage package, string fileNamePath)
            : base(product, application, package)
        {
            if (string.IsNullOrEmpty(fileNamePath))
                throw new ArgumentNullException(nameof(fileNamePath));

            FileNamePath = fileNamePath;
        }

        #region Public Properties and Methods

        /// <summary>
        /// The full file name and path to write out to.
        /// </summary>
        public string FileNamePath { get; private set; }

        #endregion

        #region Private Properties and Methods

        protected override PackageSendEventArgs OnSend(ProgressMonitorStack progressMonitors)
        {
            int fileSizeBytes = 0;
            AsyncTaskResult result;
            string statusMessage;
            Exception taskException = null;
            try
            {
                //all we do is save the file out to our target path.
                Package.Save(progressMonitors, FileNamePath); // Uh-oh, we have to save it again!

                result = AsyncTaskResult.Success;
                fileSizeBytes = (int)FileSystemTools.GetFileSize(FileNamePath);
                statusMessage = string.Format(CultureInfo.CurrentCulture, "Package written to file {0}",
                                              Path.GetFileNameWithoutExtension(FileNamePath));
            }
            catch (Exception ex)
            {
                result = AsyncTaskResult.Error;
                statusMessage =
                    "Unable to save the package to disk.\r\n\r\nIt's possible that you don't have sufficient access to the directory to write the file or the media is read-only.";
                taskException = ex;
            }

            return new PackageSendEventArgs(fileSizeBytes, result, statusMessage, taskException);
        }

        #endregion
    }
}
