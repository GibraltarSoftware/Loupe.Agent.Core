using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Gibraltar.Data
{
    /// <summary>
    /// Determines the correct physical paths to use for various Gibraltar scenarios
    /// </summary>
    public static class PathManager
    {
        /// <summary>
        /// The subfolder of the selected path used for the repository
        /// </summary>
        public const string RepositoryFolder = "Repository";

        /// <summary>
        /// The subfolder of the selected path used for local session log collection
        /// </summary>
        public const string CollectionFolder = "Local Logs";

        /// <summary>
        /// The subfolder of the selected path used for discovery information
        /// </summary>
        public const string DiscoveryFolder = "Discovery";

        /// <summary>
        /// Determine the best path of the provided type for the current user
        /// </summary>
        /// <param name="pathType">The path type to retrieve a path for</param>
        /// <returns>The best accessible path of the requested type.</returns>
        /// <remarks>The common application data folder is used if usable
        /// then the local application data folder as a last resort.</remarks>
        public static string FindBestPath(PathType pathType)
        {
            return FindBestPath(pathType, null);
        }

        /// <summary>
        /// Determine the best path of the provided type for the current user
        /// </summary>
        /// <param name="pathType">The path type to retrieve a path for</param>
        /// <param name="preferredPath">The requested full path to use if available.</param>
        /// <returns>The best accessible path of the requested type.</returns>
        /// <remarks>If the preferred path is usable it is used, otherwise the common application data folder is used
        /// then the local application data folder as a last resort.</remarks>
        public static string FindBestPath(PathType pathType, string preferredPath)
        {
            string bestPath = null;

            //first, if they provided an override path we'll start with that.
            if (string.IsNullOrEmpty(preferredPath) == false)
            {
                bestPath = preferredPath;
                if (PathIsUsable(bestPath) == false)
                {
                    //the override path is no good, ignore it.
                    bestPath = null;
                }
            }

            if (string.IsNullOrEmpty(bestPath))
            {
                string pathFolder = PathTypeToFolderName(pathType);

                //First, we want to try to use the all users data directory if this is not the user-repository.
                if (pathType != PathType.Repository)
                {
                    bestPath = CreatePath(GetCommonApplicationDataPath(), pathFolder);
                }

                //Did we get a good path? If not go to the user's folder.
                if (string.IsNullOrEmpty(bestPath))
                {
                    //nope, we need to switch to the user's LOCAL app data path as our first backup. (not appdata - that may be part of a roaming profile)
                    bestPath = CreatePath(GetLocalApplicationDataPath(), pathFolder);
                }
            }

            return bestPath;
        }

        /// <summary>
        /// Find the full path for the provided subfolder name within a special folder, and make sure it's usable (return null if fails).
        /// </summary>
        /// <returns>The full path to the requested folder if it is usable, null otherwise.</returns>
        private static string CreatePath(string basePath, string folderName)
        {
            string bestPath = ComputePath(basePath, folderName);

            if (PathIsUsable(bestPath) == false)
                bestPath = null;

            return bestPath;
        }

        /// <summary>
        /// Compute the full path for the provided subfolder name within a special folder.
        /// </summary>
        /// <returns>The full path to the requested folder, which may or may not exist.</returns>
        public static string ComputePath(string basePath, string folderName)
        {
            string bestPath = basePath;
            bestPath = Path.Combine(bestPath, "Gibraltar");
            bestPath = Path.Combine(bestPath, folderName);

            return bestPath;
        }

        /// <summary>
        /// Determines if the provided full path is usable for the current user
        /// </summary>
        /// <param name="path"></param>
        /// <returns>True if the path is usable, false otherwise</returns>
        /// <remarks>The path is usable if the current user can access the path, create files and write to existing files.</remarks>
        public static bool PathIsUsable(string path)
        {
            //I suck.  I can't figure out a way to easily check if we can create a file and write to it other than to... create a file and try to write to it.
            bool pathIsWritable = true;

            //create a random file name that won't already exist.
            string fileNamePath = Path.Combine(path, Guid.NewGuid().ToString() + ".txt");

            try
            {
                //first, we have to make sure the directory exists.
                if (Directory.Exists(path) == false)
                {
                    //it doesn't - we'll need to create it AND sent the right permissions on it.
                    DirectoryInfo newDirectory = Directory.CreateDirectory(path);
                }

                using (StreamWriter testFile = File.CreateText(fileNamePath))
                {
                    //OK, we can CREATE a file, can we WRITE to it?
                    testFile.WriteLine("This is a test file created by Loupe to verify that the directory is writable.");
                    testFile.Flush();
                }

                //we've written it and closed it, now open it again.
                using (StreamReader testFile = File.OpenText(fileNamePath))
                {
                    testFile.ReadToEnd();
                }

                //no exception there, we're good to go.  we'll delete it in a minute outside of our pass/fail handler.
            }
            catch
            {
                //if we can't do it, it's not writable for some reason.
                pathIsWritable = false;
            }

            try
            {
                File.Delete(fileNamePath);
            }
            catch
            {
                Debug.WriteLine("While the path {0} is usable because we can create, write, and read files we can't delete files, so purging won't work.");
            }

            return pathIsWritable;
        }

        private static string PathTypeToFolderName(PathType pathType)
        {
            string pathFolder;
            switch (pathType)
            {
                case PathType.Collection:
                    pathFolder = CollectionFolder;
                    break;
                case PathType.Repository:
                    pathFolder = RepositoryFolder;
                    break;
                case PathType.Discovery:
                    pathFolder = DiscoveryFolder;
                    break;
                default:
                    throw new InvalidDataException("The current path type is unknown, indicating a programming error.");
            }

            return pathFolder;
        }

        private static string GetLocalApplicationDataPath()
        {
#if (NETCOREAPP1_1)
            return Environment.GetEnvironmentVariable(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "LocalAppData" : "HOME");
#else
            var path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(path))
                path = Environment.GetEnvironmentVariable("HOME");

            return path;
#endif
        }

        private static string GetCommonApplicationDataPath()
        {
#if (NETCOREAPP1_1)
            return Environment.GetEnvironmentVariable(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "ProgramData" : "HOME");
#else
            var path = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            if (string.IsNullOrWhiteSpace(path))
                path = Environment.GetEnvironmentVariable("HOME");

            return path;
#endif
        }
    }
}