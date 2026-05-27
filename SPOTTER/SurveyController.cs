using System;
using System.IO;
//using System.Device.Location;
using SPOTTER.Models;

namespace SPOTTER.Controllers
{
    /// <summary>
    /// Main controller that coordinates all application functionality
    /// </summary>
    public class SurveyController
    {
        private SessionInfo _sessionInfo;
        private GPSController _gpsController;
        private GPSMonitoringExtension _gpsMonitor;
        private GameControllerHandler _gameController;
        private FileController _fileController;
        private ControllerSettings _controllerSettings;
        //private NmeaParser _nmeaParser;

        //public int SatellitesInView => _nmeaParser?.SatellitesInView ?? 0;
        //public int SatellitesUsed => _nmeaParser?.SatellitesUsed ?? 0;
        public int SatellitesInView => _gpsController?.SatellitesInView ?? 0;
        public int SatellitesUsed => _gpsController?.SatellitesUsed ?? 0;

        public event EventHandler<SatelliteInfoEventArgs> SatelliteInfoUpdated;
        public event EventHandler<string> LogMessage;
        public event EventHandler<string> StatusMessage;
        public event EventHandler<GPSUpdateStatusEventArgs> GPSUpdateWarning;
        public event EventHandler<Exception> ErrorOccurred;
        public event EventHandler<bool> ControllerConnectionChanged;

        public SessionInfo CurrentSession => _sessionInfo;
        public LocationData CurrentLocation => _gpsController.CurrentLocation;
        public bool IsSessionActive { get; private set; }

        public SurveyController()
        {
            _sessionInfo = new SessionInfo();
            _controllerSettings = new ControllerSettings();
            _gpsController = new GPSController();
            _gameController = new GameControllerHandler(_controllerSettings);
            _fileController = new FileController();

            // Set up event handlers
            _gpsController.LocationUpdated += (s, e) => OnLocationUpdated(e);
            _gpsController.StatusChanged += (s, e) => OnGPSStatusChanged(e);
            _gameController.KeyPressed += (s, e) => OnKeyPressed(e);
            _gameController.ObservationRecorded += (s, e) => OnObservationRecorded(e);
            _gameController.ControllerConnectionChanged += (s, e) => OnControllerConnectionChanged(e);
            _gpsController.SatelliteInfoUpdated += (s, e) => OnSatelliteInfoUpdated(e);

            // Initialize GPS monitoring
            _gpsMonitor = new GPSMonitoringExtension(_gpsController);
            _gpsMonitor.GPSUpdateStatusChanged += (s, e) => OnGPSUpdateStatusChanged(e);

            // Initialise NMEA Parser
            //_nmeaParser = new NmeaParser();
            //_nmeaParser.SatelliteInfoUpdated += (s, e) => OnSatelliteInfoUpdated(e);
            //_nmeaParser.ErrorOccurred += (s, e) => StatusMessage?.Invoke(this, e);
        }

        /// <summary>
        /// Initializes the application controllers
        /// </summary>
        public void Initialize()
        {
            _gpsController.Start();
        }

        /// <summary>
        /// Shuts down the application controllers
        /// </summary>
        public void Shutdown()
        {
            _gpsController.Stop();
            _gpsMonitor.Stop();
            _gameController?.Dispose();
            //_nmeaParser?.Disconnect();
        }

        /// <summary>
        /// Updates controller state
        /// </summary>
        public void Update()
        {
            _gameController.Update();
        }

        /// <summary>
        /// Starts a new session
        /// </summary>
        /// <param name="sessionInfo">Session information</param>
        /// <returns>True if session started successfully</returns>
        public bool StartSession(SessionInfo sessionInfo)
        {
            try
            {
                if (IsSessionActive)
                    throw new InvalidOperationException("A session is already active. Stop it before starting a new one.");

                // Validate session info
                if (!sessionInfo.Validate(out string errorMessage))
                {
                    throw new InvalidOperationException(errorMessage);
                }

                _sessionInfo = sessionInfo;

                // Create filenames based on session info
                _sessionInfo.CreateFileNames();

                // Ensure required directories exist
                _fileController.EnsureDirectoriesExist(_sessionInfo);

                // Initialize session files with headers
                _fileController.InitializeSessionFiles(_sessionInfo);

                // Prevent system sleep during session
                PowerManagement.PreventSleep();

                IsSessionActive = true;
                StatusMessage?.Invoke(this, "Session started successfully");

                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                return false;
            }
        }

        /// <summary>
        /// Stops the current session
        /// </summary>
        /// <returns>True if session stopped successfully</returns>
        public bool StopSession()
        {
            try
            {
                IsSessionActive = false;

                // Allow system sleep again
                PowerManagement.AllowSleep();

                StatusMessage?.Invoke(this, "Session stopped");
                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                return false;
            }
        }

        /// <summary>
        /// Gets the current controller settings
        /// </summary>
        /// <returns>Current controller settings</returns>
        public ControllerSettings GetControllerSettings()
        {
            return _controllerSettings;
        }

        private void OnLocationUpdated(LocationData locationData)
        {
            // If session is active, record track log data
            if (IsSessionActive)
            {
                try
                {
                    _fileController.WriteTrackLogData(_sessionInfo, locationData);
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, ex);
                }
            }
        }

        private void OnGPSStatusChanged(string status)
        {
            StatusMessage?.Invoke(this, $"GPS status: {status}");
        }

        //private void OnGPSUpdateStatusChanged(GPSUpdateStatusEventArgs e)
        //{
        //    // Forward both warnings and recovery notifications
        //    GPSUpdateWarning?.Invoke(this, e);

        //    if (!e.IsUpdating)
        //    {
        //        StatusMessage?.Invoke(this, e.Message);
        //    }
        //}
        private void OnGPSUpdateStatusChanged(GPSUpdateStatusEventArgs e)
        {
            // Always forward GPS update status changes to UI
            GPSUpdateWarning?.Invoke(this, e);

            // Log status messages
            if (!e.IsUpdating)
            {
                StatusMessage?.Invoke(this, e.Message);
            }
            else
            {
                StatusMessage?.Invoke(this, "GPS position updating normally");
            }
        }

        private void OnKeyPressed(string key)
        {
            LogMessage?.Invoke(this, key);

            if (IsSessionActive)
            {
                try
                {
                    _fileController.WriteObservationData(_sessionInfo, key);
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, ex);
                }
            }
        }

        private void OnObservationRecorded(string observation)
        {
            if (IsSessionActive)
            {
                try
                {
                    ObservationRecord record = CreateObservationRecord(observation);
                    string formattedRecord = Environment.NewLine + record.ToString();

                    LogMessage?.Invoke(this, formattedRecord);
                    _fileController.WriteObservationData(_sessionInfo, formattedRecord);
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, ex);
                }
            }
        }

        private ObservationRecord CreateObservationRecord(string observation)
        {
            string sessionString = _sessionInfo.Session == SessionInfo.TimeOfDay.AM ? "AM" : "PM";
            DateTime bestTime = _gpsController.GetBestTime();

            return new ObservationRecord
            {
                Timestamp = bestTime,
                UTCTicks = bestTime.Ticks,
                Session = sessionString,
                Observer = _sessionInfo.ObserverName,
                Position = _sessionInfo.Position.ToString(),
                Latitude = CurrentLocation.Latitude,
                Longitude = CurrentLocation.Longitude,
                Bearing = CurrentLocation.Bearing,
                Altitude = CurrentLocation.Altitude,
                HDOP = CurrentLocation.HorizontalAccuracy,
                VDOP = CurrentLocation.VerticalAccuracy,
                CloudCover = _sessionInfo.CloudCover,
                Temperature = _sessionInfo.Temperature,
                Wind = _sessionInfo.Wind,
                Observation = observation
            };
        }

        private void OnControllerConnectionChanged(bool isConnected)
        {
            // Forward the event to UI
            ControllerConnectionChanged?.Invoke(this, isConnected);

            // Also update status message
            StatusMessage?.Invoke(this, isConnected ?
                "Controller connected" :
                "Controller disconnected");
        }

        /// <summary>
        /// Checks if a game controller is connected
        /// </summary>
        /// <returns>True if controller is connected</returns>
        public bool IsControllerConnected()
        {
            return _gameController != null && _gameController.IsConnected;
        }

        public void TriggerGpsLostVibration()
        {
            if (_gameController != null && _gameController.IsConnected)
                _gameController.SetVibration(0.8f, 0.8f, 1000);
        }

        /// <summary>
        /// Manually writes current location to track log
        /// </summary>
        public void WriteTrackLog()
        {
            // Only write if session is active and properly initialized
            if (IsSessionActive && _sessionInfo != null &&
                !string.IsNullOrEmpty(_sessionInfo.LogDirectory) &&
                !string.IsNullOrEmpty(_sessionInfo.TrackLogFileName))
            {
                try
                {
                    _fileController.WriteTrackLogData(_sessionInfo, _gpsController.CurrentLocation);
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, ex);
                }
            }
        }

        //public bool ConnectToGpsPort(string portName)
        //{
        //    return _nmeaParser.Connect(portName);
        //}

        private void OnSatelliteInfoUpdated(SatelliteInfoEventArgs e)
        {
            SatelliteInfoUpdated?.Invoke(this, e);
        }

        //public void SubscribeToGPSDebug(Controllers.GPSDebugForm debugForm)
        //{
        //    _gpsController.RawNMEAReceived += (s, sentence) =>
        //        debugForm.AddNMEASentence(sentence);

        //    _gpsController.StatusChanged += (s, status) =>
        //        debugForm.UpdateStatus(status, _gpsController.IsConnected);

        //    _gpsController.LocationUpdated += (s, location) =>
        //        debugForm.UpdateParsedData(location);

        //    debugForm.UpdateStatus("Monitoring GPS...", _gpsController.IsConnected);
        //}

        public GPSController GetGPSController()
        {
            return _gpsController;
        }
    }
}
