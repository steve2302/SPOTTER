using System;
//using System.Device.Location;
using System.IO;

namespace SPOTTER.Models
{
    /// <summary>
    /// Represents session information for an aerial survey
    /// </summary>
    public class SessionInfo
    {
        public enum TimeOfDay { AM, PM }
        public enum ObserverPosition { LeftFront, LeftRear, RightFront, RightRear }

        public TimeOfDay Session { get; set; }
        public string ObserverName { get; set; }
        public ObserverPosition Position { get; set; }
        public string FilePath { get; set; }
        public string DataDirectory { get; set; }
        public string LogDirectory { get; set; }
        public string DataFileName { get; set; }
        public string TrackLogFileName { get; set; }
        public DateTime SessionDate { get; set; }

        // Weather information
        public string CloudCover { get; set; } = "NA";
        public decimal Temperature { get; set; } = -9999;
        public decimal Wind { get; set; } = -9999;

        public SessionInfo()
        {
            SessionDate = DateTime.Now;
        }

        /// <summary>
        /// Validates if all required session information is provided
        /// </summary>
        /// <param name="errorMessage">Error message if validation fails</param>
        /// <returns>True if valid, false otherwise</returns>
        public bool Validate(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(ObserverName))
            {
                errorMessage = "You must select an observer";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Creates file names for data and track log files
        /// </summary>
        public void CreateFileNames()
        {
            string sessionString = Session == TimeOfDay.AM ? "AM" : "PM";
            string timestamp = SessionDate.ToString("yyyy-MM-dd_HH-mm-ss-fff");
            string positionString = Position.ToString();

            // Sanitize observer name for use in filename
            string safeObserverName = SanitizeFileName(ObserverName);

            DataFileName = $"{timestamp}_{sessionString}_{safeObserverName}_{positionString}.dat";
            TrackLogFileName = $"{timestamp}_{sessionString}_{safeObserverName}_{positionString}.log";
        }

        /// <summary>
        /// Sanitizes a string for use as a filename by replacing invalid characters
        /// </summary>
        /// <param name="fileName">The filename to sanitize</param>
        /// <returns>A safe filename string</returns>
        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "Unknown";

            // Replace invalid characters with underscore
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c, '_');
            }

            // Also replace some additional characters that might cause issues
            fileName = fileName.Replace(' ', '_');
            fileName = fileName.Replace('.', '_');

            return fileName;
        }
    }
}
