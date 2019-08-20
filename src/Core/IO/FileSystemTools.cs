using System;
using System.Globalization;
using System.IO;

namespace Loupe.Core.IO
{
    /// <summary>
    /// Common routines for manipulating files and directories that extend the .NET framework
    /// </summary>
    public static class FileSystemTools
    {
        private const int DefaultFileBufferSize = 32768;

        /// <summary>
        /// Sanitize the provided directory name by substituting underscore for illegal values.
        /// </summary>
        /// <param name="directoryName">The name of the directory to sanitize.</param>
        /// <returns>The sanitized directory name.</returns>
        public static string SanitizeDirectoryName(string directoryName)
        {
            return SanitizeDirectoryName(directoryName, '_');
        }

        /// <summary>
        /// Sanitize the provided directory name by substituting a specified character for illegal values.
        /// </summary>
        /// <param name="directoryName">The name of the directory to sanitize.</param>
        /// <param name="replaceChar">The character to substitute for illegal values, must be legal.</param>
        /// <returns>The sanitized directory name.</returns>
        public static string SanitizeDirectoryName(string directoryName, char replaceChar)
        {
            if (string.IsNullOrEmpty(directoryName))
                throw new ArgumentNullException(directoryName);

            string name = directoryName;
            foreach (char c in Path.GetInvalidPathChars())
                name = name.Replace(c, replaceChar);

            return name;
        }

        /// <summary>
        /// Sanitize the provided file name (without path) by substituting an underscore for illegal values.
        /// </summary>
        /// <param name="fileName">The file name to sanitize</param>
        /// <returns>The sanitized file name.</returns>
        public static string SanitizeFileName(string fileName)
        {
            return SanitizeFileName(fileName, '_');
        }

        /// <summary>
        /// Sanitize the provided file name (without path) by substituting the specified character for illegal values.
        /// </summary>
        /// <param name="fileName">The file name to sanitize</param>
        /// <param name="replaceChar">The character to substitute for illegal values, must be legal.</param>
        /// <returns>The sanitized file name.</returns>
        public static string SanitizeFileName(string fileName, char replaceChar)
        {
            string name = fileName;
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, replaceChar);

            return name;
        }

        /// <summary>
        /// Ensures that the provided full file name and path is unique, and makes it unique if necessary.
        /// </summary>
        /// <param name="path">The candidate path to verify</param>
        /// <returns>A unique path based on the provided path.</returns>
        public static string MakeFileNamePathUnique(string path)
        {

            const string format = "{0}-{1}{2}";
            FileInfo fileInfo = new FileInfo(path);
            if (!fileInfo.Exists)
                return path;

            // break up the path into its constituent parts
            string folder = fileInfo.DirectoryName;
            string file = Path.GetFileNameWithoutExtension(path);
            string extension = fileInfo.Extension;

            // get the list of existing files in the current folder based on this file name
            string searchPattern = string.Format(CultureInfo.InvariantCulture, format, file, "*", extension);
            string[] files = Directory.GetFiles(folder, searchPattern, SearchOption.TopDirectoryOnly);

            // iterate over the file names looking for the largest number
            // in the position where we put our counter
            int startIndex = file.Length + 1;
            int maskLength = startIndex + extension.Length + 1;
            int counter = 0;
            foreach (string fileName in files)
            {
                int charCount = fileName.Length - maskLength;
                string suffix = fileName.Substring(startIndex, charCount);
                int.TryParse(suffix, out var thisCounter);
                if (thisCounter > counter)
                    counter = thisCounter;
            }

            // generate a unique file path
            // we increment counter here because it currently lists the
            // largest counter value we've seen
            counter++;

            string uniqueFile = string.Format(CultureInfo.InvariantCulture, format, file, counter, extension);
            return Path.Combine(folder, uniqueFile); //make sure we return a fully qualified path..
        }

        /// <summary>
        /// Ensure that the path to the provided fully qualified file name exists, creating it if necessary.
        /// </summary>
        /// <param name="fileNamePath">A fully qualified file name and path.</param>
        public static void EnsurePathExists(string fileNamePath)
        {
            string filePath = Path.GetDirectoryName(fileNamePath);

            if (Directory.Exists(filePath) == false)
                Directory.CreateDirectory(filePath);
        }

        /// <summary>
        /// Checks the attributes on the file and clears read-only attributes.
        /// </summary>
        /// <param name="fileNamePath"></param>
        public static void MakeFileWriteable(string fileNamePath)
        {
            //make sure the file isn't read only because delete will fail.
            FileAttributes currentAttribs = File.GetAttributes(fileNamePath);
            if ((currentAttribs & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
            {
                File.SetAttributes(fileNamePath, currentAttribs ^ FileAttributes.ReadOnly);
            }            
        }

        /// <summary>
        /// Find out the size of the file specified
        /// </summary>
        /// <param name="fileNamePath"></param>
        /// <returns>The file size in bytes or 0 if the file is not found.</returns>
        public static long GetFileSize(string fileNamePath)
        {
            long fileSize = 0;
            if (File.Exists(fileNamePath))
            {
                FileInfo fileInfo = new FileInfo(fileNamePath);
                fileSize = fileInfo.Length;
            }

            return fileSize;
        }

        /// <summary>
        /// Open a temporary file for read and write and return the open FileStream.
        /// </summary>
        /// <param name="fileName">The full file name path created.</param>
        /// <param name="deleteOnClose">True to set the file delete on close, false to leave the file after close
        /// (caller must delete, rename, etc).</param>
        /// <returns>An open read-write FileStream.</returns>
        public static FileStream GetTempFileStream(out string fileName, bool deleteOnClose)
        {
            fileName = Path.GetTempFileName();
            FileOptions options = deleteOnClose ? FileOptions.DeleteOnClose : FileOptions.None;
            FileStream stream = new FileStream(fileName, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, DefaultFileBufferSize, options);
            return stream;
        }

        /// <summary>
        /// Open a temporary file for read and write and return the open FileStream which will delete-on-close.
        /// </summary>
        /// <returns>An open read-write FileStream which is set to delete on close.</returns>
        public static FileStream GetTempFileStream()
        {
            return GetTempFileStream(out var fileName, true);
        }

        /// <summary>
        /// Open a temporary file for read and write and return the open FileStream which will NOT delete-on-close.
        /// </summary>
        /// <param name="fileName">The full file name path created.</param>
        /// <returns>An open read-write FileStream which will NOT delete on close.</returns>
        public static FileStream GetTempFileStream(out string fileName)
        {
            return GetTempFileStream(out fileName, false);
        }

        /// <summary>
        /// Copy the content of a Stream into a temporary file opened for read and write and return the open FileStream.
        /// </summary>
        /// <param name="contentStream">An open Stream to copy from its current Position to its end.</param>
        /// <param name="fileName">The full file name path created.</param>
        /// <param name="deleteOnClose">True to set the file delete on close, false to leave the file after close
        /// (caller must delete, rename, etc).</param>
        /// <returns>An open read-write FileStream with a copy of the contentStream.</returns>
        public static FileStream GetTempFileStreamCopy(Stream contentStream, out string fileName, bool deleteOnClose)
        {
            FileStream outStream = GetTempFileStream(out fileName, deleteOnClose);

            StreamContentPump(contentStream, outStream);

            outStream.Position = 0; // Reset the position to the start of the stream.
            return outStream;
        }

        /// <summary>
        /// Copy the content of a Stream into a temporary file opened for read and write and return the open FileStream which will delete-on-close.
        /// </summary>
        /// <param name="contentStream">An open Stream to copy from its current Position to its end.</param>
        /// <returns>An open read-write FileStream which is set to delete on close with a copy of the contentStream.</returns>
        public static FileStream GetTempFileStreamCopy(Stream contentStream)
        {
            return GetTempFileStreamCopy(contentStream, out var fileName, true);
        }

        /// <summary>
        /// Copy the content of a Stream into a temporary file opened for read and write and return the open FileStream which will NOT delete-on-close.
        /// </summary>
        /// <param name="contentStream">An open Stream to copy from its current Position to its end.</param>
        /// <param name="fileName">The full file name path created.</param>
        /// <returns>An open read-write FileStream which will NOT delete on close with a copy of the contentStream.</returns>
        public static FileStream GetTempFileStreamCopy(Stream contentStream, out string fileName)
        {
            return GetTempFileStreamCopy(contentStream, out fileName, false);
        }
        
        /// <summary>
        /// Pump the remaining contents of one stream at its current Position into another stream at its current Position.
        /// </summary>
        /// <param name="sourceStream">The Stream to read from, starting at its current Position.</param>
        /// <param name="destinationStream">The Stream to write into, starting at its current Position.</param>
        /// <returns>The total number of bytes copied.</returns>
        public static long StreamContentPump(Stream sourceStream, Stream destinationStream)
        {
            byte[] buffer = new byte[DefaultFileBufferSize];
            int bytesRead;
            long totalBytesCopied = 0;
            while ((bytesRead = sourceStream.Read(buffer, 0, DefaultFileBufferSize)) > 0)
            {
                destinationStream.Write(buffer, 0, bytesRead);
                totalBytesCopied += bytesRead;
            }
            destinationStream.Flush();
            return totalBytesCopied;
        }

        /// <summary>
        /// Pump the contents of one stream from its current Position into another stream at its current Position up to a
        /// maximum byte count.
        /// </summary>
        /// <param name="sourceStream">The Stream to read from, starting at its current Position.</param>
        /// <param name="destinationStream">The Stream to write into, starting at its current Position.</param>
        /// <param name="maxCount">The maximum count of bytes to copy.  Non-positive count will copy nothing.</param>
        /// <returns>The total number of bytes copied.</returns>
        public static long StreamContentPump(Stream sourceStream, Stream destinationStream, long maxCount)
        {
            if (maxCount <= 0)
                return 0;

            byte[] buffer = new byte[DefaultFileBufferSize];
            long fastLimit = maxCount - DefaultFileBufferSize; // How close to the end can we go in full buffer chunks?
            int bytesRead;
            long totalBytesCopied = 0;
            while (totalBytesCopied <= fastLimit && (bytesRead = sourceStream.Read(buffer, 0, DefaultFileBufferSize)) > 0)
            {
                destinationStream.Write(buffer, 0, bytesRead);
                totalBytesCopied += bytesRead;
            }
            // Okay, now we may need to copy a remaining fraction, so we have to calculate the byte count to read.
            while (totalBytesCopied < maxCount && (bytesRead = sourceStream.Read(buffer, 0, (int)(maxCount - totalBytesCopied))) > 0)
            {
                destinationStream.Write(buffer, 0, bytesRead);
                totalBytesCopied += bytesRead;
            }
            destinationStream.Flush();
#if DEBUG
            // We shouldn't need the #if around this, but just to be paranoid...
            System.Diagnostics.Debug.Assert(totalBytesCopied <= maxCount, "StreamContentPump exceeded the specified maxCount!");
#endif
            return totalBytesCopied;
        }

        /// <summary>
        /// Copy the entire contents of one stream into another, preserving the source Position.
        /// </summary>
        /// <param name="sourceStream">The Stream to read from Position 0, restoring its original Position when completed.</param>
        /// <param name="destinationStream">The Stream to write into, which will be advanced by the number of bytes written</param>
        /// <returns>The total number of bytes copied.</returns>
        public static long StreamContentCopy(Stream sourceStream, Stream destinationStream)
        {
            return StreamContentCopy(sourceStream, destinationStream, false);
        }

        /// <summary>
        /// Copy the entire contents of one stream into another, preserving Position.
        /// </summary>
        /// <param name="sourceStream">The Stream to read from Position 0, restoring its original Position when completed.</param>
        /// <param name="destinationStream">The Stream to write into which may optionally be restored to its original position</param>
        /// <param name="resetDestinationToOriginalPosition">True to reset the destination stream back to its starting position</param>
        /// <returns>The total number of bytes copied.</returns>
        public static long StreamContentCopy(Stream sourceStream, Stream destinationStream, bool resetDestinationToOriginalPosition)
        {
            long originalSourcePosition = sourceStream.Position;
            long originalDestinationPosition = destinationStream.Position;
            if (sourceStream.CanSeek)
                sourceStream.Position = 0;

            long totalBytesCopied = StreamContentPump(sourceStream, destinationStream);

            if (sourceStream.CanSeek)
                sourceStream.Position = originalSourcePosition;

            if (resetDestinationToOriginalPosition)
                destinationStream.Position = originalDestinationPosition;

            return totalBytesCopied;
        }

        /// <summary>
        /// Copy a file to a target location, replacing an existing file if the source is newer.
        /// </summary>
        public static void CopyIfNewer(string sourceFileNamePath, string targetFileNamePath)
        {
            if (File.Exists(targetFileNamePath))
            {
                DateTime dateSource = File.GetLastWriteTimeUtc(sourceFileNamePath);
                DateTime dateTarget = File.GetLastWriteTimeUtc(targetFileNamePath);

                if (dateTarget < dateSource)
                {
                    File.Copy(sourceFileNamePath, targetFileNamePath, true);
                }
            }
            else
            {
                File.Copy(sourceFileNamePath, targetFileNamePath);
            }
        }
    }
}
