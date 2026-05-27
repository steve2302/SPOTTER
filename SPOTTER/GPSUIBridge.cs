using System;
using System.Windows.Forms;
using SPOTTER.Controllers;
using SPOTTER.Models;

namespace SPOTTER.Helpers
{
    /// <summary>
    /// Helper class that provides thread-safe GPS event handling for Windows Forms
    /// This ensures GPS events from background threads don't crash the UI
    /// </summary>
    public class GPSUIBridge : IDisposable
    {
        private readonly GPSController _gpsController;
        private readonly Control _uiControl; // Any UI control (form, textbox, etc.)

        // Events that fire on the UI thread
        public event EventHandler<LocationData> LocationUpdatedOnUI;
        public event EventHandler<string> StatusChangedOnUI;
        public event EventHandler<string> RawNMEAReceivedOnUI;
        public event EventHandler<SatelliteInfoEventArgs> SatelliteInfoUpdatedOnUI;

        /// <summary>
        /// Creates a thread-safe bridge between GPS controller and UI
        /// </summary>
        /// <param name="gpsController">The GPS controller to monitor</param>
        /// <param name="uiControl">Any UI control (typically the Form) used for thread marshalling</param>
        public GPSUIBridge(GPSController gpsController, Control uiControl)
        {
            _gpsController = gpsController ?? throw new ArgumentNullException(nameof(gpsController));
            _uiControl = uiControl ?? throw new ArgumentNullException(nameof(uiControl));

            // Subscribe to GPS events
            _gpsController.LocationUpdated += OnGPSLocationUpdated;
            _gpsController.StatusChanged += OnGPSStatusChanged;
            _gpsController.RawNMEAReceived += OnGPSRawNMEAReceived;
            _gpsController.SatelliteInfoUpdated += OnGPSSatelliteInfoUpdated;
        }

        /// <summary>
        /// Internal handler that marshals LocationUpdated to UI thread
        /// </summary>
        private void OnGPSLocationUpdated(object sender, LocationData location)
        {
            if (_uiControl.InvokeRequired)
            {
                _uiControl.BeginInvoke(new Action(() =>
                {
                    if (!_uiControl.IsDisposed)
                    {
                        LocationUpdatedOnUI?.Invoke(this, location);
                    }
                }));
            }
            else
            {
                if (!_uiControl.IsDisposed)
                {
                    LocationUpdatedOnUI?.Invoke(this, location);
                }
            }
        }

        /// <summary>
        /// Internal handler that marshals StatusChanged to UI thread
        /// </summary>
        private void OnGPSStatusChanged(object sender, string status)
        {
            if (_uiControl.InvokeRequired)
            {
                _uiControl.BeginInvoke(new Action(() =>
                {
                    if (!_uiControl.IsDisposed)
                    {
                        StatusChangedOnUI?.Invoke(this, status);
                    }
                }));
            }
            else
            {
                if (!_uiControl.IsDisposed)
                {
                    StatusChangedOnUI?.Invoke(this, status);
                }
            }
        }

        /// <summary>
        /// Internal handler that marshals RawNMEAReceived to UI thread
        /// </summary>
        private void OnGPSRawNMEAReceived(object sender, string nmea)
        {
            if (_uiControl.InvokeRequired)
            {
                _uiControl.BeginInvoke(new Action(() =>
                {
                    if (!_uiControl.IsDisposed)
                    {
                        RawNMEAReceivedOnUI?.Invoke(this, nmea);
                    }
                }));
            }
            else
            {
                if (!_uiControl.IsDisposed)
                {
                    RawNMEAReceivedOnUI?.Invoke(this, nmea);
                }
            }
        }

        /// <summary>
        /// Internal handler that marshals SatelliteInfoUpdated to UI thread
        /// </summary>
        private void OnGPSSatelliteInfoUpdated(object sender, SatelliteInfoEventArgs args)
        {
            if (_uiControl.InvokeRequired)
            {
                _uiControl.BeginInvoke(new Action(() =>
                {
                    if (!_uiControl.IsDisposed)
                    {
                        SatelliteInfoUpdatedOnUI?.Invoke(this, args);
                    }
                }));
            }
            else
            {
                if (!_uiControl.IsDisposed)
                {
                    SatelliteInfoUpdatedOnUI?.Invoke(this, args);
                }
            }
        }

        /// <summary>
        /// Disposes and unsubscribes from all events
        /// </summary>
        public void Dispose()
        {
            // Unsubscribe from GPS events
            if (_gpsController != null)
            {
                _gpsController.LocationUpdated -= OnGPSLocationUpdated;
                _gpsController.StatusChanged -= OnGPSStatusChanged;
                _gpsController.RawNMEAReceived -= OnGPSRawNMEAReceived;
                _gpsController.SatelliteInfoUpdated -= OnGPSSatelliteInfoUpdated;
            }
        }
    }
}

/* ============================================================================
 * USAGE EXAMPLE
 * ============================================================================
 * 
 * In your Form class:
 * 
 * public partial class MyGPSForm : Form
 * {
 *     private GPSController _gpsController;
 *     private GPSUIBridge _gpsBridge;
 *     
 *     public MyGPSForm()
 *     {
 *         InitializeComponent();
 *         
 *         // Create GPS controller
 *         _gpsController = new GPSController();
 *         
 *         // Create thread-safe bridge
 *         _gpsBridge = new GPSUIBridge(_gpsController, this);
 *         
 *         // Subscribe to SAFE UI events (these are already on UI thread!)
 *         _gpsBridge.LocationUpdatedOnUI += OnLocationUpdate;
 *         _gpsBridge.StatusChangedOnUI += OnStatusChange;
 *         _gpsBridge.RawNMEAReceivedOnUI += OnNMEAReceived;
 *         
 *         // Start GPS
 *         _gpsController.Start();
 *     }
 *     
 *     // These handlers are GUARANTEED to run on UI thread
 *     private void OnLocationUpdate(object sender, LocationData location)
 *     {
 *         // Safe to update UI directly - no Invoke needed!
 *         txtLatitude.Text = location.Latitude.ToString("F6");
 *         txtLongitude.Text = location.Longitude.ToString("F6");
 *     }
 *     
 *     private void OnStatusChange(object sender, string status)
 *     {
 *         // Safe to update UI directly
 *         lblStatus.Text = status;
 *     }
 *     
 *     private void OnNMEAReceived(object sender, string nmea)
 *     {
 *         // Safe to update UI directly
 *         txtDebug.AppendText(nmea + "\r\n");
 *     }
 *     
 *     protected override void OnFormClosing(FormClosingEventArgs e)
 *     {
 *         base.OnFormClosing(e);
 *         
 *         // Clean up
 *         _gpsBridge?.Dispose();
 *         _gpsController?.Stop();
 *         _gpsController?.Dispose();
 *     }
 * }
 * 
 * ============================================================================
 */