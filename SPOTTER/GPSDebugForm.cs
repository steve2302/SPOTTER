using SPOTTER.Helpers;
using SPOTTER.Models;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace SPOTTER.Controllers
{
    /// <summary>
    /// Debug window showing real-time GPS data and NMEA sentences
    /// </summary>
    public class GPSDebugForm : Form
    {
        private TextBox _nmeaTextBox;
        private TextBox _parsedDataTextBox;
        private Label _statusLabel;
        private Label _portLabel;
        private Button _clearButton;
        private CheckBox _autoScrollCheckBox;
        private int _sentenceCount = 0;
        private int _maxLines = 500;

        private GPSController _gpsController;
        private GPSUIBridge _gpsBridge;

        private int _satellitesInView = 0;
        private int _satellitesUsed = 0;

        public GPSDebugForm(GPSController gpsController)
        {
            if (gpsController == null)
                throw new ArgumentNullException(nameof(gpsController));
            _gpsController = gpsController;

            InitializeComponent();

            // Create bridge to existing controller (don't start it - it's already running!)
            //_gpsController = new GPSController();            
            _gpsBridge = new GPSUIBridge(_gpsController, this);

            // Subscribe to events
            _gpsBridge.RawNMEAReceivedOnUI += OnRawNMEAReceived;
            _gpsBridge.LocationUpdatedOnUI += OnLocationUpdated;
            _gpsBridge.StatusChangedOnUI += OnStatusChanged;
            _gpsBridge.SatelliteInfoUpdatedOnUI += OnSatelliteInfoUpdated;  // ADDED

            //_gpsController.Start();

            // Update initial status
            if (_gpsController.IsConnected)
            {
                UpdateStatus("Connected", true);
                UpdatePort(_gpsController.ConnectedPortName, 4800);
            }
        }

        private void OnSatelliteInfoUpdated(object sender, SatelliteInfoEventArgs args)
        {
            _satellitesInView = args.SatellitesInView;
            _satellitesUsed = args.SatellitesUsed;

            // Trigger a refresh of the parsed data display
            if (_gpsController.CurrentLocation != null && _gpsController.CurrentLocation.IsValid)
            {
                UpdateParsedData(_gpsController.CurrentLocation);
            }
        }

        private void OnLocationUpdated(object sender, LocationData location)
        {
            UpdateParsedData(location);
        }

        private void OnStatusChanged(object sender, string status)
        {
            UpdateStatus(status, _gpsController.IsConnected);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form properties
            this.Text = "GPS Debug Monitor";
            this.Size = new Size(1000, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;

            // Status panel
            Panel statusPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.LightGray,
                Padding = new Padding(10)
            };

            _statusLabel = new Label
            {
                Text = "Status: Waiting for GPS...",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.DarkBlue,
                AutoSize = false,
                Size = new Size(900, 25),
                Location = new Point(10, 10)
            };

            _portLabel = new Label
            {
                Text = "Port: Not connected",
                Font = new Font("Segoe UI", 9),
                AutoSize = false,
                Size = new Size(400, 20),
                Location = new Point(10, 35)
            };

            Label sentenceLabel = new Label
            {
                Text = "Sentences: 0",
                Font = new Font("Segoe UI", 9),
                AutoSize = false,
                Size = new Size(200, 20),
                Location = new Point(10, 55),
                Name = "sentenceCountLabel"
            };

            _clearButton = new Button
            {
                Text = "Clear",
                Size = new Size(80, 30),
                Location = new Point(420, 35),
                BackColor = Color.White
            };
            _clearButton.Click += (s, e) => ClearAll();

            _autoScrollCheckBox = new CheckBox
            {
                Text = "Auto-scroll",
                Checked = true,
                Location = new Point(510, 40),
                AutoSize = true
            };

            statusPanel.Controls.Add(_statusLabel);
            statusPanel.Controls.Add(_portLabel);
            statusPanel.Controls.Add(sentenceLabel);
            statusPanel.Controls.Add(_clearButton);
            statusPanel.Controls.Add(_autoScrollCheckBox);

            // Split container for two text boxes
            SplitContainer splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 400,
                BorderStyle = BorderStyle.Fixed3D
            };

            // NMEA sentences panel
            Panel nmeaPanel = new Panel { Dock = DockStyle.Fill };
            Label nmeaLabel = new Label
            {
                Text = "Raw NMEA Sentences:",
                Dock = DockStyle.Top,
                Height = 25,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.LightBlue,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 0, 0, 0)
            };

            _nmeaTextBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9),
                BackColor = Color.Black,
                ForeColor = Color.LimeGreen,
                ReadOnly = true,
                WordWrap = false
            };

            nmeaPanel.Controls.Add(_nmeaTextBox);
            nmeaPanel.Controls.Add(nmeaLabel);
            splitContainer.Panel1.Controls.Add(nmeaPanel);

            // Parsed data panel
            Panel parsedPanel = new Panel { Dock = DockStyle.Fill };
            Label parsedLabel = new Label
            {
                Text = "Parsed GPS Data:",
                Dock = DockStyle.Top,
                Height = 25,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.LightGreen,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(5, 0, 0, 0)
            };

            _parsedDataTextBox = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10),
                BackColor = Color.White,
                ForeColor = Color.Black,
                ReadOnly = true
            };

            parsedPanel.Controls.Add(_parsedDataTextBox);
            parsedPanel.Controls.Add(parsedLabel);
            splitContainer.Panel2.Controls.Add(parsedPanel);

            // Add controls to form
            this.Controls.Add(splitContainer);
            this.Controls.Add(statusPanel);

            this.ResumeLayout(false);
        }

        /// <summary>
        /// Updates the connection status
        /// </summary>
        public void UpdateStatus(string status, bool isConnected)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string, bool>(UpdateStatus), status, isConnected);
                return;
            }

            _statusLabel.Text = $"Status: {status}";
            _statusLabel.ForeColor = isConnected ? Color.DarkGreen : Color.DarkRed;
        }

        /// <summary>
        /// Updates the connected port information
        /// </summary>
        public void UpdatePort(string portName, int baudRate)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string, int>(UpdatePort), portName, baudRate);
                return;
            }

            _portLabel.Text = $"Port: {portName} @ {baudRate} baud";
        }

        /// <summary>
        /// Adds a raw NMEA sentence to the display
        /// </summary>
        public void AddNMEASentence(string sentence)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(AddNMEASentence), sentence);
                return;
            }

            _sentenceCount++;
            
            // Update sentence count
            var countLabel = this.Controls.Find("sentenceCountLabel", true);
            if (countLabel.Length > 0)
            {
                countLabel[0].Text = $"Sentences: {_sentenceCount}";
            }

            // Add timestamp and sentence
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string line = $"[{timestamp}] {sentence}";

            // Limit lines to prevent memory issues
            string[] lines = _nmeaTextBox.Lines;
            if (lines.Length > _maxLines)
            {
                // Keep only the last maxLines
                string[] newLines = new string[_maxLines];
                Array.Copy(lines, lines.Length - _maxLines + 1, newLines, 0, _maxLines - 1);
                newLines[_maxLines - 1] = line;
                _nmeaTextBox.Lines = newLines;
            }
            else
            {
                _nmeaTextBox.AppendText(line + Environment.NewLine);
            }

            // Auto-scroll if enabled
            if (_autoScrollCheckBox.Checked)
            {
                _nmeaTextBox.SelectionStart = _nmeaTextBox.Text.Length;
                _nmeaTextBox.ScrollToCaret();
            }
        }

        /// <summary>
        /// Updates the parsed GPS data display
        /// </summary>
        public void UpdateParsedData(LocationData location)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<LocationData>(UpdateParsedData), location);
                return;
            }

            string data = $"===================================================\n" +
                         $"GPS DATA - Updated: {DateTime.Now:HH:mm:ss}\n" +
                         $"===================================================\n\n" +
                         $"POSITION:\n" +
                         $"  Latitude:     {location.Latitude:F7} deg\n" +
                         $"  Longitude:    {location.Longitude:F7} deg\n" +
                         $"  Altitude:     {location.Altitude:F1} m\n\n" +
                         $"MOTION:\n" +
                         $"  Speed:        {location.Speed:F1} km/h\n" +
                         $"  Bearing:      {location.Bearing:F1} deg\n\n" +
                         $"QUALITY:\n" +
                         $"  Satellites Used:     {location.SatelliteCount}\n" +
                         $"  Satellites In View:  {_satellitesInView}\n" +
                         $"  HDOP:         {location.HorizontalAccuracy:F2}\n" +
                         $"  VDOP:         {location.VerticalAccuracy:F2}\n" +
                         $"  Valid Fix:    {(location.HasValidFix() ? "YES" : "NO")}\n" +
                         $"  Accurate:     {(location.IsAccurate() ? "YES" : "NO")}\n\n" +
                         $"TIMING:\n" +
                         $"  GPS Time:     {location.Timestamp:yyyy-MM-dd HH:mm:ss} UTC\n" +
                         $"  Local Time:   {location.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss}\n" +
                         $"  UTC Ticks:    {location.UTCTicks}\n\n" +
                         $"STATUS:\n" +
                         $"  Is Valid:     {location.IsValid}\n";
            _parsedDataTextBox.Text = data;
        }

        /// <summary>
        /// Adds a debug message
        /// </summary>
        public void AddDebugMessage(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(AddDebugMessage), message);
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string line = $"[{timestamp}] *** {message} ***";
            
            _nmeaTextBox.AppendText(line + Environment.NewLine);
            
            if (_autoScrollCheckBox.Checked)
            {
                _nmeaTextBox.SelectionStart = _nmeaTextBox.Text.Length;
                _nmeaTextBox.ScrollToCaret();
            }
        }

        /// <summary>
        /// Clears all displays
        /// </summary>
        private void ClearAll()
        {
            _nmeaTextBox.Clear();
            _parsedDataTextBox.Clear();
            _sentenceCount = 0;
            
            var countLabel = this.Controls.Find("sentenceCountLabel", true);
            if (countLabel.Length > 0)
            {
                countLabel[0].Text = "Sentences: 0";
            }
        }

        private void OnRawNMEAReceived(object sender, string nmea)
        {
            // This is already on UI thread thanks to GPSUIBridge, so just update directly:
            if (!_nmeaTextBox.IsDisposed && !this.IsDisposed)
            {
               _nmeaTextBox.AppendText(nmea + "\r\n");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            _gpsBridge?.Dispose();
            //_gpsController?.Stop();
        }
    }
}
