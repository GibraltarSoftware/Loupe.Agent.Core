using System;
using System.ComponentModel;
using System.IO;
using Loupe.Core.Logging;
using Microsoft.Extensions.Logging;

namespace Loupe.Core.IO
{
    /// <summary>
    /// Tracks files to detect changes.
    /// </summary>
    public class FileReference
    {
        private readonly ILogger m_Logger;

        private FileInfo m_CurrentFileInfo;
        private string m_Caption;
        private string m_FileNamePath;
        private DateTime m_LastWriteTime;
        private byte[] m_Data;

        /// <summary>
        /// Raised whenever a property changes (including the file data)
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Create a new file reference object
        /// </summary>
        /// <param name="referencedFile">The file information object for the file being added</param>
        internal FileReference(FileInfo referencedFile)
        {
            if (referencedFile == null)
                throw new ArgumentNullException(nameof(referencedFile));

            m_Logger = ApplicationLogging.CreateLogger<FileReference>();

            Caption = referencedFile.Name;
            FileNamePath = referencedFile.FullName;
            LastWriteTime = referencedFile.LastWriteTime;

            //load up the file as a snapshot load
            Refresh();
        }

        #region Public Properties and Method

        /// <summary>
        /// A display caption for the file
        /// </summary>
        public string Caption
        {
            get { return m_Caption; } 
            internal set
            {
                if (m_Caption != value)
                {
                    m_Caption = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("Caption"));
                }
            }
        }


        /// <summary>
        /// The full file name &amp; path being monitored
        /// </summary>
        public string FileNamePath
        {
            get { return m_FileNamePath; } 
            internal set
            {
                if (m_FileNamePath != value)
                {
                    m_FileNamePath = value;
                    OnPropertyChanged(new PropertyChangedEventArgs("FileNamePath"));
                }
            }
        }

        /// <summary>
        /// The full set of data in the file.
        /// </summary>
        public byte[] Data
        {
            get { return m_Data; } 
            private set
            {
                //instead of trying to detect change on this, assume it's a change.
                m_Data = value;
                OnPropertyChanged(new PropertyChangedEventArgs("Data"));
            }
        }

        /// <summary>
        /// The last time the file was written
        /// </summary>
        public DateTime LastWriteTime
        {
            get { return m_LastWriteTime; }
            private set
            {
                m_LastWriteTime = value;
                OnPropertyChanged(new PropertyChangedEventArgs("LastWriteTime"));
            }
        }

        #endregion

        #region Internal Properties and Methods

        /// <summary>
        /// Tell the file reference to check if it has been updated on disk.
        /// </summary>
        internal void Refresh()
        {
            //Confirm if the file has actually updated.
            FileInfo newFileInfo = new FileInfo(FileNamePath);
            if ((m_CurrentFileInfo == null) || (newFileInfo.LastWriteTime > m_CurrentFileInfo.LastWriteTime))
            {
                //there is no baseline or the update date has changed.
                m_CurrentFileInfo = newFileInfo;
                LastWriteTime = m_CurrentFileInfo.LastWriteTime;
                LoadData();
            }
        }

        /// <summary>
        /// Used while checking for updates to mark if the item was found during the update check or not.
        /// </summary>
        internal bool ItemFound { get; set; }

        #endregion

        #region Protected Properties and Methods

        /// <summary>
        /// Called to reload the data for the file reference from disk.
        /// </summary>
        /// <remarks>Raises the property changed event for the data property</remarks>
        protected void LoadData()
        {
            try
            {
                Data = File.ReadAllBytes(FileNamePath);
            }
            catch (Exception ex)
            {
                m_Logger.LogWarning("Unable to Load Data for File\r\nWhile attempting to load the data for '{0}' an exception was raised: {1}", FileNamePath, ex.Message, ex);
                Data = null;
            }
        }

        /// <summary>
        /// Called to raise the property changed event.
        /// </summary>
        /// <param name="args"></param>
        protected void OnPropertyChanged(PropertyChangedEventArgs args)
        {
            PropertyChangedEventHandler tempEvent = PropertyChanged;

            if (tempEvent != null)
            {
                tempEvent(this, args);
            }
        }

        #endregion
    }
}
