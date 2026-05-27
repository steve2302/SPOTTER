using System;
using System.Drawing;
//using System.Device.Location;
using SPOTTER.Models;
using System.Windows.Forms;

namespace SPOTTER.Controllers
{
    /// <summary>
    /// Extends GPSController with GPS update monitoring capabilities
    /// </summary>
    public class GPSMonitoringExtension
    {
        private readonly GPSController _gpsController;
        private DateTime _lastPositionUpdate;
        private LocationData _lastLocationData;
        private readonly Timer _gpsMonitorTimer;
        private const int GPS_STALE_WARNING_SECONDS = 10; // Time threshold before warning (seconds)
        private bool _hasWarned = false; // Track if we've already sent a warning

        public event EventHandler<GPSUpdateStatusEventArgs> GPSUpdateStatusChanged;

        public GPSMonitoringExtension(GPSController gpsController)
        {
            _gpsController = gpsController;
            _lastPositionUpdate = DateTime.Now;
            _lastLocationData = new LocationData();

            // Create timer to check for GPS updates
            _gpsMonitorTimer = new Timer();
            _gpsMonitorTimer.Interval = 5000; // Check every 5 seconds
            _gpsMonitorTimer.Tick += GpsMonitorTimer_Tick;

            // Subscribe to GPS controller events
            _gpsController.LocationUpdated += GpsController_LocationUpdated;

            // Start monitoring
            _gpsMonitorTimer.Start();
        }

        private void GpsController_LocationUpdated(object sender, LocationData e)
        {
            // Check if position has actually changed
            if (HasPositionChanged(e))
            {
                _lastPositionUpdate = DateTime.Now;
                _lastLocationData = e;

                // Only notify if we previously warned (state changed from not updating to updating)
                if (_hasWarned)
                {
                    _hasWarned = false;

                    // Notify listeners position is updating normally
                    GPSUpdateStatusChanged?.Invoke(this, new GPSUpdateStatusEventArgs
                    {
                        IsUpdating = true,
                        TimeSinceLastUpdate = TimeSpan.Zero,
                        Message = "GPS updating normally"
                    });
                }
            }
        }

        private void GpsMonitorTimer_Tick(object sender, EventArgs e)
        {
            TimeSpan timeSinceUpdate = DateTime.Now - _lastPositionUpdate;

            // Only notify once when GPS first becomes stale (state change from updating to not updating)
            if (timeSinceUpdate.TotalSeconds > GPS_STALE_WARNING_SECONDS && !_hasWarned)
            {
                _hasWarned = true;

                GPSUpdateStatusChanged?.Invoke(this, new GPSUpdateStatusEventArgs
                {
                    IsUpdating = false,
                    TimeSinceLastUpdate = timeSinceUpdate,
                    Message = $"Warning: GPS position has not updated for {timeSinceUpdate.TotalSeconds:0} seconds"
                });
            }
        }

        private bool HasPositionChanged(LocationData newData)
        {
            if (_lastLocationData == null) return true;

            // Check if latitude or longitude has changed meaningfully
            // Use a small threshold to account for minor GPS fluctuations
            const double threshold = 0.0000001;

            return Math.Abs(newData.Latitude - _lastLocationData.Latitude) > threshold ||
                   Math.Abs(newData.Longitude - _lastLocationData.Longitude) > threshold;
        }

        /// <summary>
        /// Stop monitoring GPS updates
        /// </summary>
        public void Stop()
        {
            _gpsMonitorTimer.Stop();
        }
    }

    /// <summary>
    /// Event arguments for GPS update status changes
    /// </summary>
    public class GPSUpdateStatusEventArgs : EventArgs
    {
        public bool IsUpdating { get; set; }
        public TimeSpan TimeSinceLastUpdate { get; set; }
        public string Message { get; set; }
    }
}