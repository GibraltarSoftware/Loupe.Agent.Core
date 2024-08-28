using System;
using System.IO;

namespace Gibraltar.Data
{
    /// <summary>
    /// Creates a local, temporary file to wrap the provided stream which will be cleaned up on dispose.
    /// </summary>
    /// <remarks>This is primarily for converting non-seekable streams to seekable ones by copying them into a temporary file.</remarks>
    public class TempFileStream : FileStream
    {
        private bool m_Disposed = false;
        private readonly string m_TempFilePath;

        /// <summary>
        /// Create a new temporary file stream that wraps the provided stream.
        /// </summary>
        /// <param name="input"></param>
        public TempFileStream(Stream input) 
            : base(CreateTempFile(input, out string tempFilePath), FileMode.Open, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose)
        {
            m_TempFilePath = tempFilePath;
        }

        /// <summary>
        /// Create a new temporary file stream that wraps the provided stream.
        /// </summary>
        public TempFileStream()
            : base(CreateTempFile(out string tempFilePath), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose)
        {
            m_TempFilePath = tempFilePath;
        }

        private static string CreateTempFile(Stream input, out string tempFilePath)
        {
            tempFilePath = Path.GetTempFileName();
            try
            {
                using (var tempFile = File.Open(tempFilePath, FileMode.Create, FileAccess.Write))
                {
                    input.CopyTo(tempFile);
                }

                return tempFilePath;
            }
            catch
            {
                // Clean up any partial copy we made
                try
                {
                    if (File.Exists(tempFilePath))
                        File.Delete(tempFilePath);  
                }
                catch { }

                throw;
            }
        }

        private static string CreateTempFile(out string tempFilePath)
        {
            tempFilePath = Path.GetTempFileName();

            return tempFilePath;
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!m_Disposed)
            {
                if (disposing)
                {
                    // We opened the stream delete on close, but it's always possible that didn't work on some platforms.
                    try
                    {
                        File.Delete(m_TempFilePath);
                    }
                    catch { }
                }

                // No need to explicitly delete the file here since we're using FileOptions.DeleteOnClose
                m_Disposed = true;
            }
        }
    }
}
