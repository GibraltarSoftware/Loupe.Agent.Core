using System;
using System.IO;

namespace Loupe.Core.IO
{
    /// <summary>
    /// A class to provide common wrappers and direct access to low-level file calls.
    /// </summary>
    public static class FileHelper
    {
        /// <summary>
        /// Attempt to open a FileStream while avoiding exceptions.
        /// </summary>
        /// <param name="fileName">The full-path file name to create or open.</param>
        /// <param name="creationMode">An action to take on files that exist and do not exist</param>
        /// <param name="fileAccess">Desired access to the object, which can be read, write, or both</param>
        /// <param name="fileShare">The sharing mode of an object, which can be read, write, both, or none</param>
        /// <returns>An open FileStream, or null upon failure.</returns>
        public static FileStream OpenFileStream(string fileName, FileMode creationMode, FileAccess fileAccess, FileShare fileShare)
        {
            FileStream fileStream = null;

            try
            {
                fileStream = new FileStream(fileName, creationMode, fileAccess, fileShare); // What we're wrapping anyway.
            }
            catch (Exception ex)
            {
                GC.KeepAlive(ex);
                fileStream = null;
            }

            return fileStream;
        }

        /// <summary>
        /// Delete a file with no exception being thrown.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static bool SafeDeleteFile(string fileName)
        {
            bool fileDeleted = false;
            try
            {
                File.Delete(fileName);
                fileDeleted = true; //same difference...
            }
            catch (Exception)
            {
            }

            return fileDeleted;
        }


        /// <summary>
        /// Get a persistent lock on a file without opening it.
        /// </summary>
        /// <param name="fileName">The full-path file name to create or open.</param>
        /// <param name="creationMode">An action to take on files that exist and do not exist</param>
        /// <param name="fileAccess">Desired access to the object, which can be read, write, or both</param>
        /// <param name="fileShare">The sharing mode of an object, which can be read, write, both, or none</param>
        /// <returns></returns>
        public static FileLock GetFileLock(string fileName, FileMode creationMode, FileAccess fileAccess, FileShare fileShare)
        {
            FileLock fileLock = null;
            FileStream fileStream;
            try
            {
                fileStream = new FileStream(fileName, creationMode, fileAccess, fileShare);
            }
            catch
            {
                fileStream = null;
            }

            if (fileStream != null)
                fileLock = new FileLock(fileStream, fileName, creationMode, fileAccess, fileShare, false); //if they wanted delete, they had to use the explicit option...

            return fileLock;

        }
    }
}
