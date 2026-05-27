using SPOTTER.Controllers;
using SPOTTER.Controls;
using SPOTTER.Helpers;
using SPOTTER.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SPOTTER
{
    public partial class ImprovedLogger : Form
    {
        private SurveyController _surveyController;
        private Timer _controllerTimer;
        private Timer _trackLogTimer;
        private Timer _statusUpdateTimer;
        private string _lastTimeDisplay = "";
        private DateTime _lastGpsRecoveryTime = DateTime.MinValue;

        // Add these fields to track previous values
        private string _lastTimeText = "";
        private string _lastLatText = "";
        private string _lastLongText = "";
        private string _lastAccuracyText = "";
        private string _lastGPSText = "";

        // For GPS status display
        private bool _isGpsWarningDisplayed = false;
        private DateTime _lastGpsWarningTime = DateTime.MinValue;
        private bool _gpsWasEverGood = false;

        // GPS-lost vibration pulsing
        private Timer _gpsVibrationTimer;
        private int _gpsVibrationCount = 0;
        private const int GpsVibrationMaxPulses = 5;

        // For Map display
        private MapForm _mapForm;
        private bool _mapVisible = false;

        private Controllers.GPSDebugForm _gpsDebugForm;

        private GPSUIBridge _gpsBridge;

        public ImprovedLogger()
        {
            // SPOTTER: install the flat menu/strip renderer before InitializeComponent
            // so the menu strip picks it up on first paint.
            ToolStripManager.Renderer = new SpotterMenuRenderer();

            InitializeComponent();
            // Enable double buffering to prevent flashing
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer |
                          ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint, true);
            this.UpdateStyles();
            InitializeControllers();
            // You should have something like this already:
            _gpsBridge = new GPSUIBridge(_surveyController.GetGPSController(), this);
            // Subscribe to satellite info updates
            _gpsBridge.SatelliteInfoUpdatedOnUI += OnSatelliteInfoUpdated;
            SetupEventHandlers();
        }

        #region Initialization

        private void InitializeControllers()
        {
            // Initialize the survey controller
            _surveyController = new SurveyController();

            // Initialize timers
            _controllerTimer = new Timer { Interval = 50, Enabled = true };
            //_controllerTimer = new Timer { Interval = 5, Enabled = true };
            _trackLogTimer = new Timer { Interval = 1000, Enabled = true };
            _statusUpdateTimer = new Timer { Interval = 1000, Enabled = true };

            // Set timer event handlers
            _controllerTimer.Tick += ControllerTimer_Tick;
            _trackLogTimer.Tick += TrackLogTimer_Tick;
            _statusUpdateTimer.Tick += StatusUpdateTimer_Tick;

            // In your InitializeControllers or SetupEventHandlers method:
            _surveyController.GetGPSController().LocationUpdated += OnGPSLocationUpdated;
        }

        private void SetupEventHandlers()
        {
            // Set up event handlers for the survey controller
            _surveyController.LogMessage += (s, e) => UpdateDataDisplay(e);
            _surveyController.StatusMessage += (s, e) => UpdateStatusMessage(e);
            _surveyController.ErrorOccurred += (s, e) => HandleError(e);
            _surveyController.GPSUpdateWarning += (s, e) => HandleGpsUpdateWarning(e);
            _surveyController.SatelliteInfoUpdated += (s, e) => UpdateSatelliteInfo(e);

            // Add controller connection monitoring
            _surveyController.ControllerConnectionChanged += OnControllerConnectionChanged;

            // Reflect the controller's current connection state immediately on startup
            bool alreadyConnected = _surveyController.IsControllerConnected();
            OnControllerConnectionChanged(this, alreadyConnected);
        }

        private void LoadObservers()
        {
            try
            {
                string observersText = null;
                string startupPath = Application.StartupPath;

                if (string.IsNullOrEmpty(startupPath))
                {
                    System.Diagnostics.Debug.WriteLine("Application.StartupPath is null or empty");
                    return;
                }

                // Priority 1: user-edited file saved alongside the exe
                string filePath = Path.Combine(Application.StartupPath, "observers.txt");

                // Priority 2: file copied from Resources folder at build time
                string resourceFilePath = Path.Combine(Application.StartupPath, "Resources", "observers.txt");

                if (File.Exists(filePath))
                {
                    observersText = File.ReadAllText(filePath);
                }
                else if (File.Exists(resourceFilePath))
                {
                    observersText = File.ReadAllText(resourceFilePath);
                }
                else
                {
                    // Fallback to embedded resource string
                    try
                    {
                        observersText = Properties.Resources.observers;
                    }
                    catch
                    {
                        // Resource not available
                    }
                }

                if (!string.IsNullOrWhiteSpace(observersText))
                {
                    // Clear existing items
                    cmbObserver.Items.Clear();

                    // Split by comma or newline (support both file formats)
                    string[] observers = observersText.Split(new char[] { ',', '\n', '\r' },
                        StringSplitOptions.RemoveEmptyEntries);

                    foreach (string observer in observers)
                    {
                        string trimmedObserver = observer.Trim();
                        if (!string.IsNullOrWhiteSpace(trimmedObserver))
                        {
                            cmbObserver.Items.Add(trimmedObserver);
                        }
                    }
                }
                else
                {
                    MessageBox.Show(
                        "Could not load observers list from file or resources.\n\nUsing default list.",
                        "Load Warning",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading observers list: {ex.Message}\n\nUsing default list.",
                    "Load Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }

        #endregion

        #region Event Handlers

        private void ImprovedLogger_Load(object sender, EventArgs e)
        {

            //===========================================================
            // Add this as the FIRST line in ImprovedLogger_Load
            System.Diagnostics.Debug.WriteLine("\n=== COM PORT DIAGNOSTIC ===");
            string[] allPorts = System.IO.Ports.SerialPort.GetPortNames();
            System.Diagnostics.Debug.WriteLine($"Windows sees {allPorts.Length} COM port(s):");
            if (allPorts.Length == 0)
            {
                System.Diagnostics.Debug.WriteLine("  ERROR: NO COM PORTS DETECTED!");
            }
            else
            {
                foreach (string port in allPorts)
                {
                    System.Diagnostics.Debug.WriteLine($"  - {port}");
                }
            }
            System.Diagnostics.Debug.WriteLine("=== END DIAGNOSTIC ===\n");
            //=============================================================

            // Load observers list
            LoadObservers();

            // Start GPS tracking
            _surveyController.Initialize();

            // Check if controller is connected
            if (!_surveyController.IsControllerConnected())
            {
                ShowControllerWarning();
            }

            // Start controller timer
            _controllerTimer.Start();

            // Update UI
            UpdateTimeDisplay();

            // Update staellite info
            //string[] ports = NmeaParser.GetAvailablePorts();
            //if (ports.Length > 0)
            //{
            //    _surveyController.ConnectToGpsPort(ports[0]); // Or let user select
            //}
        }

        private void ImprovedLogger_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_surveyController.IsSessionActive)
            {
                // Ask for confirmation before closing
                var result = MessageBox.Show(
                    "A session is currently active. Are you sure you want to quit?",
                    "Confirm Exit",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.No)
                {
                    e.Cancel = true;
                    return;
                }
            }

            // Persist the observer list so user additions survive app restarts
            SaveObservers();

            // Shutdown controllers
            _surveyController.Shutdown();
        }

        private void SaveObservers()
        {
            try
            {
                var items = new List<string>();
                foreach (var item in cmbObserver.Items)
                    items.Add(item.ToString());

                // Include any text currently typed in the box that isn't in the list
                string current = cmbObserver.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(current) && !items.Contains(current))
                    items.Add(current);

                if (items.Count > 0)
                {
                    string filePath = Path.Combine(Application.StartupPath, "observers.txt");
                    File.WriteAllText(filePath, string.Join(",", items));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving observers: {ex.Message}");
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (!_surveyController.IsSessionActive)
            {
                StartSession();
            }
            else
            {
                StopSession();
            }
        }

        private void HelpToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            using (var help = new HelpForm())
                help.ShowDialog(this);
        }

        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (AboutBox1 box = new AboutBox1())
            {
                box.ShowDialog(this);
            }
        }

        private void QuitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void ShowControllerWarning()
        {
            var result = MessageBox.Show(
                "Warning: No game controller detected!\n\n" +
                "Please connect an Xbox-compatible game controller to use this application.\n\n" +
                "The application will continue to monitor for a controller connection.\n\n" +
                "Click OK to continue, or Cancel to exit the application.",
                "Controller Not Detected",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Cancel)
            {
                Application.Exit();
            }
        }

        private void OnControllerConnectionChanged(object sender, bool isConnected)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<object, bool>(OnControllerConnectionChanged), sender, isConnected);
                return;
            }

            if (isConnected)
            {
                UpdateStatusMessage("Controller connected and ready");
                lblControllerDot.ForeColor = Theme.GoodDot;
                lblControllerStatusText.Text = "Connected";
                lblControllerStatusText.ForeColor = Theme.GoodPrimary;
            }
            else
            {
                UpdateStatusMessage("Controller disconnected - please reconnect");
                lblControllerDot.ForeColor = Theme.AlertPrimary;
                lblControllerStatusText.Text = "Disconnected";
                lblControllerStatusText.ForeColor = Theme.AlertPrimary;
            }
        }

        private void OnGPSLocationUpdated(object sender, LocationData location)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<object, LocationData>(OnGPSLocationUpdated), sender, location);
                return;
            }

            // ---- Existing status-strip update ----
            // Update satellite count in toolbar
            toolStripStatusSatellites.Text = $"Sats: {location.SatelliteCount}";

            // Update GPS status
            if (location.HasValidFix())
            {
                toolStripStatusGPS.Text = "GPS: Good";
                toolStripStatusGPS.ForeColor = Color.Green;
            }
            else
            {
                toolStripStatusGPS.Text = "GPS: No Fix";
                toolStripStatusGPS.ForeColor = Color.Red;
            }

            // ---- SPOTTER: drive the right-rail GPS card ----
            if (location != null && location.HasValidFix())
            {
                _gpsWasEverGood = true;
                lblGpsLatValue.Text = location.Latitude.ToString("F5");
                lblGpsLonValue.Text = location.Longitude.ToString("F5");
                lblGpsAccValue.Text = $"{location.HorizontalAccuracy:F1} m";
                lblGpsStatusDot.ForeColor = Theme.GoodDot;
                lblGpsStatusText.Text = "3D fix";
                lblGpsStatusText.ForeColor = Theme.GoodPrimary;
            }
            else
            {
                lblGpsStatusDot.ForeColor = Theme.SatelliteUnused;
                lblGpsStatusText.Text = "No fix";
                lblGpsStatusText.ForeColor = Theme.TextSecondary;
            }
        }

        #endregion

        #region Timer Handlers

        private void ControllerTimer_Tick(object sender, EventArgs e)
        {
            _surveyController.Update();
            //UpdateTimeDisplay();
        }

        private void TrackLogTimer_Tick(object sender, EventArgs e)
        {
            // Track log is written by SurveyController.OnLocationUpdated on every GPS event.
            // A second write path here caused a file-sharing conflict (both paths opened the
            // same file concurrently from different threads). Left empty intentionally.
        }

        private void StatusUpdateTimer_Tick(object sender, EventArgs e)
        {
            // Update the clock label in the session card every second
            UpdateTimeDisplay();

            // SPOTTER: refresh the hero "freshness" caption every tick
            TickFreshnessLabel();

            // Get location data once
            var location = _surveyController.CurrentLocation;

            // DEBUG: Log GPS status info
            System.Diagnostics.Debug.WriteLine($"GPS Status Check: " +
                $"Lat={location.Latitude:F5}, " +
                $"Accuracy={location.HorizontalAccuracy:F1}m, " +
                $"HasValidFix={location.HasValidFix()}, " +
                $"WarningDisplayed={_isGpsWarningDisplayed}");

            // Build new text values
            string timeText = $"Time: {DateTime.Now:HH:mm:ss}";
            string latText = $"Lat: {location.Latitude:F5}";
            string longText = $"Long: {location.Longitude:F5}";
            string accuracyText = $"Accuracy: {location.HorizontalAccuracy:F1}m";

            // Check if GPS data is stale
            bool isGPSStale = false;
            try
            {
                TimeSpan timeSinceGPSUpdate = DateTime.UtcNow - location.Timestamp;
                if (timeSinceGPSUpdate.TotalSeconds > 15)
                {
                    isGPSStale = true;
                }
            }
            catch { }

            // Suspend layout to prevent flickering during multiple updates
            statusStrip.SuspendLayout();
            try
            {
                // Only update if changed
                if (timeText != _lastTimeText)
                {
                    toolStripStatusTime.Text = timeText;
                    _lastTimeText = timeText;
                }

                if (latText != _lastLatText)
                {
                    toolStripStatusLatitude.Text = latText;
                    _lastLatText = latText;
                }

                if (longText != _lastLongText)
                {
                    toolStripStatusLongitude.Text = longText;
                    _lastLongText = longText;
                }

                if (accuracyText != _lastAccuracyText)
                {
                    toolStripStatusAccuracy.Text = accuracyText;
                    _lastAccuracyText = accuracyText;
                }

                // Determine GPS status based on current conditions
                string newGPSText;
                System.Drawing.Image newGPSImage;

                // Check if we should clear the warning display
                if (_isGpsWarningDisplayed)
                {
                    TimeSpan warningDisplayTime = DateTime.Now - _lastGpsWarningTime;
                    if (warningDisplayTime.TotalSeconds > 30)
                    {
                        _isGpsWarningDisplayed = false;
                    }
                }

                // Determine current GPS status (prioritize warnings from GPS monitor)
                if (_isGpsWarningDisplayed)
                {
                    // Keep the warning display active - don't change it
                    // The warning was set by HandleGpsUpdateWarning
                }
                else if (isGPSStale)
                {
                    // Only apply stale check if we haven't recently received a recovery message
                    TimeSpan timeSinceRecovery = DateTime.Now - _lastGpsRecoveryTime;

                    if (timeSinceRecovery.TotalSeconds > 3)
                    {
                        // GPS monitor hasn't said it's updating recently, so trust the stale check
                        newGPSText = "GPS Connection Lost";
                        newGPSImage = Properties.Resources.satellite_xxl_red;

                        if (newGPSText != _lastGPSText || toolStripStatusGPS.Image != newGPSImage)
                        {
                            toolStripStatusGPS.Text = newGPSText;
                            toolStripStatusGPS.Image = newGPSImage;
                            _lastGPSText = newGPSText;
                        }
                    }
                    // else: Recently recovered, don't override with stale check
                }
                else if (location.HasValidFix())
                {
                    newGPSText = "GPS Accuracy OK";
                    newGPSImage = Properties.Resources.satellite_xxl_green;

                    if (newGPSText != _lastGPSText || toolStripStatusGPS.Image != newGPSImage)
                    {
                        toolStripStatusGPS.Text = newGPSText;
                        toolStripStatusGPS.Image = newGPSImage;
                        _lastGPSText = newGPSText;
                    }
                }
                else
                {
                    newGPSText = "GPS Accuracy Low";
                    newGPSImage = Properties.Resources.satellite_xxl_yellow;

                    if (newGPSText != _lastGPSText || toolStripStatusGPS.Image != newGPSImage)
                    {
                        toolStripStatusGPS.Text = newGPSText;
                        toolStripStatusGPS.Image = newGPSImage;
                        _lastGPSText = newGPSText;
                    }
                }
            }
            finally
            {
                statusStrip.ResumeLayout(false);
                statusStrip.PerformLayout();
            }

            // Update map if visible
            if (_mapVisible && _mapForm != null && !_mapForm.IsDisposed)
            {
                _mapForm.UpdatePosition(location);
            }
        }

        #endregion

        #region Helper Methods

        private void StartSession()
        {
            // Check if controller is connected before starting session
            if (!_surveyController.IsControllerConnected())
            {
                MessageBox.Show(
                    "Cannot start session: No game controller detected!\n\n" +
                    "Please connect a game controller before starting a session.",
                    "Controller Required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            // Validate inputs
            if (!ValidateSessionInputs(out string errorMessage))
            {
                MessageBox.Show(errorMessage, "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Set up directory paths - ADDED THIS SECTION
            string baseDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "AerialSurveyLogger");

            string dataDirectory = Path.Combine(baseDirectory, "Data");
            string logDirectory = Path.Combine(baseDirectory, "TrackLogs");

            // Create session info with all required properties
            SessionInfo sessionInfo = new SessionInfo
            {
                Session = rbAM.Checked ? SessionInfo.TimeOfDay.AM : SessionInfo.TimeOfDay.PM,
                ObserverName = GetObserverInitials(),
                Position = GetObserverPosition(),
                CloudCover = cmbCloud.Text != "" ? cmbCloud.Text : "NA",
                Temperature = nudTemperature.Value,
                Wind = nudWind.Value,
                DataDirectory = dataDirectory,      // ADDED - Required!
                LogDirectory = logDirectory          // ADDED - Required!
            };

            // Start session
            if (_surveyController.StartSession(sessionInfo))
            {
                // Update UI
                btnStart.Text = "Stop";
                SetSessionControlsEnabled(false);
                _trackLogTimer.Start();
                txtDataStream.Clear();
            }
        }

        private void StopSession()
        {
            var result = MessageBox.Show(
                "Are you sure you want to stop the current session?",
                "Confirm Stop",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                if (_surveyController.StopSession())
                {
                    // Update UI
                    btnStart.Text = "Start";
                    SetSessionControlsEnabled(true);
                    _trackLogTimer.Stop();
                }
            }
        }

        private bool ValidateSessionInputs(out string errorMessage)
        {
            errorMessage = string.Empty;

            // Check time of day
            if (!rbAM.Checked && !rbPM.Checked)
            {
                errorMessage = "You must select a time (AM/PM)";
                return false;
            }

            // Check observer
            if (string.IsNullOrWhiteSpace(cmbObserver.Text))
            {
                errorMessage = "You must select an observer";
                return false;
            }

            // Check position
            if (!rbLeftFront.Checked && !rbLeftRear.Checked &&
                !rbRightFront.Checked && !rbRightRear.Checked)
            {
                errorMessage = "You must select a position";
                return false;
            }

            return true;
        }

        private string GetObserverInitials()
        {
            // Extract initials from observer name
            string name = cmbObserver.Text;
            string initials = "";

            foreach (string part in name.Split(' '))
            {
                if (!string.IsNullOrWhiteSpace(part) && part.Length > 0)
                {
                    initials += part[0];
                }
            }

            return initials;
        }

        private SessionInfo.ObserverPosition GetObserverPosition()
        {
            if (rbLeftFront.Checked) return SessionInfo.ObserverPosition.LeftFront;
            if (rbLeftRear.Checked) return SessionInfo.ObserverPosition.LeftRear;
            if (rbRightFront.Checked) return SessionInfo.ObserverPosition.RightFront;
            return SessionInfo.ObserverPosition.RightRear;
        }

        private void SetSessionControlsEnabled(bool enabled)
        {
            // Enable/disable session controls
            rbAM.Enabled = enabled;
            rbPM.Enabled = enabled;
            cmbObserver.Enabled = enabled;
            rbLeftFront.Enabled = enabled;
            rbLeftRear.Enabled = enabled;
            rbRightFront.Enabled = enabled;
            rbRightRear.Enabled = enabled;
            cmbCloud.Enabled = enabled;
            nudTemperature.Enabled = enabled;
            nudWind.Enabled = enabled;
        }

        private void UpdateDataDisplay(string message)
        {
            if (txtDataStream.InvokeRequired)
            {
                txtDataStream.Invoke(new Action<string>(UpdateDataDisplay), message);
                return;
            }

            txtDataStream.AppendText(message);
            txtDataStream.ScrollToCaret();

            if (message.Contains(",") && !message.Contains("delete_last_record"))
            {
                UpdateLastObservationPanel(message);
            }
        }

        private void UpdateLastObservationPanel(string message)
        {
            bool isNewRecord = message.StartsWith(Environment.NewLine) || message.StartsWith("\n");
            if (isNewRecord)
            {
                _heroCountSum = 0;
                heroCountTile.Value = "0";
            }

            string[] parts = message.Split(',');
            if (parts.Length < 2) return;
            string token = parts[parts.Length - 1].Trim();
            if (string.IsNullOrEmpty(token)) return;

            // 1) Numeric token -> accumulate into running sum for this record
            if (int.TryParse(token, out int count))
            {
                _heroCountSum += count;
                heroCountTile.Value = _heroCountSum.ToString();
                MarkObservationFresh();
                return;
            }

            // 2) Distance token -> coloured pill (works for both old word form and new compact form)
            {
                var bin = DistanceBin.Classify(token);
                if (bin != DistanceBin.Bin.Unknown)
                {
                    heroDistancePill.Text = PrettifyDistance(token);
                    MarkObservationFresh();
                    return;
                }
            }

            // 3) Word-form count token -> accumulate into running sum for this record
            int wordCount = WordToCount(token);
            if (wordCount > 0)
            {
                _heroCountSum += wordCount;
                heroCountTile.Value = _heroCountSum.ToString();
                MarkObservationFresh();
                return;
            }

            // 4) Otherwise, treat as species
            lblHeroSpecies.Text = char.ToUpper(token[0]) + token.Substring(1);
            MarkObservationFresh();
        }

        // -----------------------------------------------------------------
        // SPOTTER hero-panel helpers
        // -----------------------------------------------------------------

        private static readonly Dictionary<string, int> _numberWords =
            new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "zero", 0 }, { "twenty", 20 }, { "forty", 40 }, { "fifty", 50 },
            { "seventy", 70 }, { "one hundred", 100 }, { "one fifty", 150 },
            { "two hundred", 200 }, { "three hundred", 300 }
        };

        private DateTime _lastHeroUpdateUtc = DateTime.MinValue;
        private int _heroCountSum = 0;

        private string PrettifyDistance(string token)
        {
            string t = token.ToLowerInvariant();
            foreach (string c in new[] { "blue ", "green ", "yellow ", "black " })
                if (t.StartsWith(c)) { t = t.Substring(c.Length); break; }

            var m = Regex.Match(t, @"(.+?) to (.+?) meter");
            if (m.Success)
            {
                string lo = m.Groups[1].Value.Trim();
                string hi = m.Groups[2].Value.Trim();
                if (_numberWords.TryGetValue(lo, out int loN) &&
                    _numberWords.TryGetValue(hi, out int hiN))
                {
                    return $"{loN}–{hiN} m";
                }
            }
            return token;
        }

        private int WordToCount(string token)
        {
            switch (token.ToLowerInvariant())
            {
                case "one":   return 1;
                case "two":   return 2;
                case "three": return 3;
                case "four":  return 4;
                case "ten":   return 10;
                default:      return 0;
            }
        }

        private void MarkObservationFresh()
        {
            _lastHeroUpdateUtc = DateTime.UtcNow;
            lblHeroFreshness.Text = "Just logged";
            lblHeroFreshnessDot.ForeColor = Theme.GoodDot;
        }

        private void TickFreshnessLabel()
        {
            if (lblHeroFreshness == null) return; // before InitializeComponent finishes

            if (_lastHeroUpdateUtc == DateTime.MinValue)
            {
                lblHeroFreshness.Text = "Awaiting first input";
                lblHeroFreshnessDot.ForeColor = Theme.SatelliteUnused;
                return;
            }
            var age = DateTime.UtcNow - _lastHeroUpdateUtc;
            if (age.TotalSeconds < 5)
            {
                lblHeroFreshness.Text = "Just logged";
                lblHeroFreshnessDot.ForeColor = Theme.GoodDot;
            }
            else if (age.TotalMinutes < 1)
            {
                lblHeroFreshness.Text = $"Logged {(int)age.TotalSeconds} sec ago";
                lblHeroFreshnessDot.ForeColor = Theme.GoodDot;
            }
            else if (age.TotalMinutes < 5)
            {
                lblHeroFreshness.Text = $"Logged {(int)age.TotalMinutes} min ago";
                lblHeroFreshnessDot.ForeColor = Theme.WarnPrimary;
            }
            else
            {
                lblHeroFreshness.Text = $"Logged {(int)age.TotalMinutes} min ago";
                lblHeroFreshnessDot.ForeColor = Theme.AlertPrimary;
            }
        }

        private void StartGpsLostVibration()
        {
            _gpsVibrationCount = 0;
            _surveyController.TriggerGpsLostVibration();
            _gpsVibrationCount++;

            if (_gpsVibrationTimer == null)
            {
                _gpsVibrationTimer = new Timer { Interval = 5000 };
                _gpsVibrationTimer.Tick += GpsVibrationTimer_Tick;
            }
            _gpsVibrationTimer.Start();
        }

        private void StopGpsLostVibration()
        {
            _gpsVibrationTimer?.Stop();
            _gpsVibrationCount = 0;
        }

        private void GpsVibrationTimer_Tick(object sender, EventArgs e)
        {
            if (_gpsVibrationCount >= GpsVibrationMaxPulses)
            {
                _gpsVibrationTimer.Stop();
                return;
            }
            _surveyController.TriggerGpsLostVibration();
            _gpsVibrationCount++;
        }

        private bool IsNumeric(string value)
        {
            return int.TryParse(value, out _);
        }

        private void UpdateStatusMessage(string message)
        {
            if (lblStatus.InvokeRequired)
            {
                lblStatus.Invoke(new Action<string>(UpdateStatusMessage), message);
                return;
            }

            lblStatus.Text = message;
        }

        private void HandleError(Exception ex)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<Exception>(HandleError), ex);
                return;
            }

            // Log error
            System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");

            // Show error to user
            MessageBox.Show(
                $"An error occurred: {ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private void HandleGpsUpdateWarning(GPSUpdateStatusEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<GPSUpdateStatusEventArgs>(HandleGpsUpdateWarning), e);
                return;
            }

            if (!e.IsUpdating)
            {
                // GPS is NOT updating - show warning
                toolStripStatusGPS.Image = Properties.Resources.satellite_xxl_red;
                toolStripStatusGPS.Text = $"GPS Not Updating ({e.TimeSinceLastUpdate.TotalSeconds:0}s)";

                if (!_isGpsWarningDisplayed && _gpsWasEverGood)
                    StartGpsLostVibration();

                _isGpsWarningDisplayed = true;
                _lastGpsWarningTime = DateTime.Now;

                UpdateStatusMessage(e.Message);

                if (e.TimeSinceLastUpdate.TotalSeconds > 30 && _surveyController.IsSessionActive)
                {
                    TimeSpan timeSinceLastWarning = DateTime.Now - _lastGpsWarningTime;
                    if (timeSinceLastWarning.TotalMinutes >= 1)
                    {
                        MessageBox.Show(
                            $"Warning: GPS position has not updated for {e.TimeSinceLastUpdate.TotalSeconds:0} seconds.\n\n" +
                            "Please check your GPS connection and ensure you have a clear view of the sky.",
                            "GPS Update Warning",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);

                        _lastGpsWarningTime = DateTime.Now;
                    }
                }
            }
            else
            {
                // GPS IS updating - clear warning and record recovery time
                StopGpsLostVibration();
                _isGpsWarningDisplayed = false;
                _lastGpsRecoveryTime = DateTime.Now;

                var location = _surveyController.CurrentLocation;
                if (location.HasValidFix())
                {
                    toolStripStatusGPS.Image = Properties.Resources.satellite_xxl_green;
                    toolStripStatusGPS.Text = "GPS Fix OK";
                }
                else
                {
                    toolStripStatusGPS.Image = Properties.Resources.satellite_xxl_yellow;
                    toolStripStatusGPS.Text = "GPS Accuracy Low";
                }

                UpdateStatusMessage("GPS connection restored");
            }
        }

        private void UpdateTimeDisplay()
        {
            string newTime = DateTime.Now.ToString("HH:mm:ss");

            // Only update if the time has actually changed
            if (newTime != _lastTimeDisplay)
            {
                lblCurrentTime.Text = newTime;
                _lastTimeDisplay = newTime;
            }
        }

        private void UpdateSatelliteInfo(SatelliteInfoEventArgs e)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<SatelliteInfoEventArgs>(UpdateSatelliteInfo), e);
                return;
            }

            toolStripStatusSatellites.Text = $"Sats: {e.SatellitesUsed}/{e.SatellitesInView}";
            lblGpsSatValue.Text = $"{e.SatellitesUsed} / {e.SatellitesInView}";
            satBars.SetCounts(e.SatellitesUsed, e.SatellitesInView);

            lblSatBarsCaption.Text = satBars.HasRealSnr
                ? "Signal strength per satellite"
                : "Used vs in-view (awaiting SNR feed)";
        }

        //private void ShowMapToolStripMenuItem_Click(object sender, EventArgs e)
        //{
        //    if (_mapForm == null || _mapForm.IsDisposed)
        //    {
        //        _mapForm = new MapForm();
        //        _mapForm.FormClosing += (s, args) => { _mapVisible = false; };
        //        _mapForm.Show();
        //        _mapVisible = true;

        //        // Update map with current position
        //        if (_surveyController.CurrentLocation.HasValidFix())
        //        {
        //            _mapForm.UpdatePosition(_surveyController.CurrentLocation);
        //        }
        //    }
        //    else
        //    {
        //        _mapForm.BringToFront();
        //    }
        //}

        #endregion

        private void EditObserverListToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Show an in-app editor so changes take effect immediately
            using (var dlg = new Form())
            {
                dlg.Text = "Edit Observer List";
                dlg.Size = new Size(420, 320);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;

                var lbl = new Label
                {
                    Text = "One observer per line (or comma-separated):",
                    Dock = DockStyle.Top,
                    Height = 28,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Padding = new Padding(4, 0, 0, 0)
                };
                var txt = new TextBox
                {
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    Dock = DockStyle.Fill,
                    Font = Theme.Body()
                };
                // Populate from current combobox items
                txt.Text = string.Join(Environment.NewLine,
                    cmbObserver.Items.Cast<string>());

                var btnSave = new Button
                {
                    Text = "Save",
                    DialogResult = DialogResult.OK,
                    Dock = DockStyle.Bottom,
                    Height = 32
                };
                btnSave.Click += (s2, e2) =>
                {
                    cmbObserver.Items.Clear();
                    foreach (var line in txt.Text.Split(
                        new char[] { ',', '\n', '\r' },
                        StringSplitOptions.RemoveEmptyEntries))
                    {
                        string name = line.Trim();
                        if (!string.IsNullOrWhiteSpace(name))
                            cmbObserver.Items.Add(name);
                    }
                    SaveObservers();
                };

                dlg.Controls.Add(txt);
                dlg.Controls.Add(btnSave);
                dlg.Controls.Add(lbl);
                dlg.ShowDialog(this);
            }
        }

        private void EditControllerConfigToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string filePath = Path.Combine(Application.StartupPath, "GamepadAudioConfig.csv");
            if (!File.Exists(filePath))
            {
                MessageBox.Show(
                    $"File not found:\n{filePath}",
                    "Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MessageBox.Show(
                "The controller configuration file will open for editing.\n\n" +
                "IMPORTANT: Any changes you make will not take effect until the application is restarted.",
                "Restart Required",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(filePath) { UseShellExecute = true });
        }

        private void showMapToolStripMenuItem_Click_1(object sender, EventArgs e)
        {
            if (_mapForm == null || _mapForm.IsDisposed)
            {
                _mapForm = new MapForm();
                _mapForm.FormClosing += (s, args) =>
                {
                    _mapVisible = false;
                };

                _mapForm.Show();
                _mapVisible = true;

                // Update map with current position
                if (_surveyController.CurrentLocation.HasValidFix())
                {
                    _mapForm.UpdatePosition(_surveyController.CurrentLocation);
                }
            }
            else
            {
                _mapForm.BringToFront();
                _mapForm.Focus();
            }
        }

        private void testGPSToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (_gpsDebugForm == null || _gpsDebugForm.IsDisposed)
            {
                // Fixed version:
                _gpsDebugForm = new Controllers.GPSDebugForm(_surveyController.GetGPSController());
                _gpsDebugForm.Show();                
                //_surveyController.SubscribeToGPSDebug(_gpsDebugForm);
            }
            else
            {
                _gpsDebugForm.BringToFront();
            }          
        }

        private void ViewControllerMapToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var viewer = new GamepadConfigViewer())
                viewer.ShowDialog(this);
        }

        private void practiceToolStripMenuItem2_Click(object sender, EventArgs e)
        {
            // Create and show practice form with current controller settings
            PracticeForm practiceForm = new PracticeForm(_surveyController.GetControllerSettings());
            practiceForm.ShowDialog(this);
        }

        /// <summary>
        /// Handles satellite info updates from GPS (already on UI thread via GPSUIBridge)
        /// </summary>
        private void OnSatelliteInfoUpdated(object sender, SatelliteInfoEventArgs e)
        {
            // Update the toolbar satellite count
            toolStripStatusSatellites.Text = $"Sats: {e.SatellitesUsed}/{e.SatellitesInView}";

            // Optional: Change color based on satellite quality
            if (e.SatellitesUsed >= 4)
            {
                toolStripStatusSatellites.ForeColor = Color.Green;  // Good GPS fix
            }
            else if (e.SatellitesUsed > 0)
            {
                toolStripStatusSatellites.ForeColor = Color.Orange; // Weak GPS fix
            }
            else
            {
                toolStripStatusSatellites.ForeColor = Color.Red;    // No GPS fix
            }
        }

        //private void practiceToolStripMenuItem2_Click(object sender, EventArgs e)
        //{

        //}
    }
}