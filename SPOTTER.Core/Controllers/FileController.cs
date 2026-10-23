using System;
using System.IO;
using SPOTTER.Models;

namespace SPOTTER.Controllers
{
    /// <summary>
    /// Handles file operations for the application
    /// </summary>
    public class FileController
    {
        /// <summary>
        /// Ensures required directories exist
        /// </summary>
        /// <param name="sessionInfo">Current session information</param>
        public void EnsureDirectoriesExist(SessionInfo sessionInfo)
        {
            if (sessionInfo == null)
                throw new ArgumentNullException(nameof(sessionInfo));

            try
            {
                if (!string.IsNullOrEmpty(sessionInfo.DataDirectory) &&
                    !Directory.Exists(sessionInfo.DataDirectory))
                {
                    Directory.CreateDirectory(sessionInfo.DataDirectory);
                }

                if (!string.IsNullOrEmpty(sessionInfo.LogDirectory) &&
                    !Directory.Exists(sessionInfo.LogDirectory))
                {
                    Directory.CreateDirectory(sessionInfo.LogDirectory);
                }
            }
            catch (IOException ex)
            {
                throw new IOException($"Could not create required directories: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Initializes session files with header information
        /// </summary>
        /// <param name="sessionInfo">Current session information</param>
        public void InitializeSessionFiles(SessionInfo sessionInfo)
        {
            if (sessionInfo == null)
                throw new ArgumentNullException(nameof(sessionInfo));

            if (string.IsNullOrEmpty(sessionInfo.DataDirectory) ||
                string.IsNullOrEmpty(sessionInfo.LogDirectory) ||
                string.IsNullOrEmpty(sessionInfo.DataFileName) ||
                string.IsNullOrEmpty(sessionInfo.TrackLogFileName))
            {
                throw new InvalidOperationException("Session info paths are not initialized");
            }

            string headerLine = $"{sessionInfo.SessionDate:dddd dd MM yyyy hh_mm_ss tt}_" +
                           $"{(sessionInfo.Session == SessionInfo.TimeOfDay.AM ? "AM" : "PM")}_" +
                           $"{sessionInfo.ObserverName}_{sessionInfo.Position}";

            try
            {
                // Initialize data file — always create fresh (append:false overwrites if same name exists)
                using (StreamWriter dataWriter = new StreamWriter(
                    Path.Combine(sessionInfo.DataDirectory, sessionInfo.DataFileName), false))
                {
                    dataWriter.WriteLine(headerLine);
                }

                // Initialize track log file — always create fresh
                using (StreamWriter logWriter = new StreamWriter(
                    Path.Combine(sessionInfo.LogDirectory, sessionInfo.TrackLogFileName), false))
                {
                    logWriter.WriteLine(headerLine);
                }
            }
            catch (IOException ex)
            {
                throw new IOException($"Could not initialize session files: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Writes data to the observation file
        /// </summary>
        /// <param name="sessionInfo">Current session information</param>
        /// <param name="data">Data to write</param>
        /// <param name="newLine">Whether to start a new line</param>
        public void WriteObservationData(SessionInfo sessionInfo, string data, bool newLine = false)
        {
            if (sessionInfo == null)
            {
                System.Diagnostics.Debug.WriteLine("Cannot write observation data: sessionInfo is null");
                return;
            }

            if (string.IsNullOrEmpty(sessionInfo.DataDirectory) ||
                string.IsNullOrEmpty(sessionInfo.DataFileName))
            {
                System.Diagnostics.Debug.WriteLine("Cannot write observation data: paths not initialized");
                return;
            }

            try
            {
                string filePath = Path.Combine(sessionInfo.DataDirectory, sessionInfo.DataFileName);

                using (StreamWriter writer = new StreamWriter(filePath, true))
                {
                    if (newLine)
                    {
                        writer.WriteLine(data);
                    }
                    else
                    {
                        writer.Write(data);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error writing observation data: {ex.Message}");
                throw new IOException($"Could not write observation data: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Writes data to the track log file
        /// </summary>
        /// <param name="sessionInfo">Current session information</param>
        /// <param name="locationData">Location data to write</param>
        public void WriteTrackLogData(SessionInfo sessionInfo, LocationData locationData)
        {
            if (sessionInfo == null)
            {
                System.Diagnostics.Debug.WriteLine("Cannot write track log: sessionInfo is null");
                return;
            }

            if (string.IsNullOrEmpty(sessionInfo.LogDirectory) ||
                string.IsNullOrEmpty(sessionInfo.TrackLogFileName))
            {
                System.Diagnostics.Debug.WriteLine("Cannot write track log: paths not initialized");
                return;
            }

            if (locationData == null)
            {
                System.Diagnostics.Debug.WriteLine("Cannot write track log: locationData is null");
                return;
            }

            try
            {
                string filePath = Path.Combine(sessionInfo.LogDirectory, sessionInfo.TrackLogFileName);
                DateTime trackTime = locationData.GpsTime ?? DateTime.UtcNow;
                string trackData = $"{trackTime:HH:mm:ss},{trackTime.Ticks}," +
                              $"{locationData.Latitude},{locationData.Longitude},{locationData.Bearing}";

                using (StreamWriter writer = new StreamWriter(filePath, true))
                {
                    writer.WriteLine(trackData);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error writing track log: {ex.Message}");
                throw new IOException($"Could not write track log data: {ex.Message}", ex);
            }
        }
    }
}