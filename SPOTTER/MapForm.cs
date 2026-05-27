using System;
using System.Drawing;
using System.Windows.Forms;
using GMap.NET;
using GMap.NET.MapProviders;
using GMap.NET.WindowsForms;
using GMap.NET.WindowsForms.Markers;
using SPOTTER.Models;

namespace SPOTTER
{
    public partial class MapForm : Form
    {
        // Map controls
        private GMapControl _mapControl;
        private GMapOverlay _markersOverlay;
        private GMapOverlay _trackOverlay;
        private GMapMarker _currentPositionMarker;
        private GMapRoute _trackRoute;
        private System.Collections.Generic.List<PointLatLng> _trackPoints;

        // Toolbar controls - THESE WERE MISSING!
        private ToolStrip _toolStrip;
        private ToolStripButton _btnClearTrack;
        private ToolStripComboBox _mapProviderCombo;
        private ToolStripButton _btnCenterOnPosition;

        // Settings
        private bool _autoCenterEnabled = true;

        public MapForm()
        {
            InitializeComponent();
            InitializeToolbar();  // Toolbar should be added first (appears at top)
            InitializeMap();      // Map fills remaining space
        }

        private void InitializeToolbar()
        {
            _toolStrip = new ToolStrip();
            _toolStrip.Dock = DockStyle.Top;

            // Map provider selector
            _mapProviderCombo = new ToolStripComboBox();
            _mapProviderCombo.Items.AddRange(new object[] {
                "Google Satellite",
                "Google Street",
                "OpenStreetMap",
                "Bing Satellite"
            });
            _mapProviderCombo.SelectedIndex = 0;
            _mapProviderCombo.SelectedIndexChanged += MapProviderCombo_SelectedIndexChanged;
            _toolStrip.Items.Add(new ToolStripLabel("Map Type:"));
            _toolStrip.Items.Add(_mapProviderCombo);

            _toolStrip.Items.Add(new ToolStripSeparator());

            // Clear track button
            _btnClearTrack = new ToolStripButton("Clear Track");
            _btnClearTrack.Click += (s, e) => ClearTrack();
            _toolStrip.Items.Add(_btnClearTrack);

            // Auto-center toggle
            _btnCenterOnPosition = new ToolStripButton("Auto-Center: ON");
            _btnCenterOnPosition.CheckOnClick = true;
            _btnCenterOnPosition.Checked = true;
            _btnCenterOnPosition.CheckedChanged += (s, e) =>
            {
                _autoCenterEnabled = _btnCenterOnPosition.Checked;
                _btnCenterOnPosition.Text = _autoCenterEnabled ? "Auto-Center: ON" : "Auto-Center: OFF";
            };
            _toolStrip.Items.Add(_btnCenterOnPosition);

            this.Controls.Add(_toolStrip);
        }

        private void MapProviderCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (_mapProviderCombo.SelectedIndex)
            {
                case 0:
                    _mapControl.MapProvider = GMapProviders.GoogleSatelliteMap;
                    break;
                case 1:
                    _mapControl.MapProvider = GMapProviders.GoogleMap;
                    break;
                case 2:
                    _mapControl.MapProvider = GMapProviders.OpenStreetMap;
                    break;
                case 3:
                    _mapControl.MapProvider = GMapProviders.BingSatelliteMap;
                    break;
            }
        }

        private void InitializeMap()
        {
            // CRITICAL: Configure GMap.NET BEFORE creating the control
            GMapProvider.WebProxy = null;
            GMaps.Instance.Mode = AccessMode.ServerOnly;  // No SQLite caching
            GMaps.Instance.UseMemoryCache = true;

            // Create map control
            _mapControl = new GMapControl
            {
                Dock = DockStyle.Fill,
                MapProvider = GMapProviders.GoogleSatelliteMap,
                Position = new PointLatLng(-33.8688, 151.2093), // Sydney default
                MinZoom = 5,
                MaxZoom = 18,
                Zoom = 14,
                ShowCenter = false,
                DragButton = MouseButtons.Left,
                CanDragMap = true,
                MouseWheelZoomEnabled = true
            };

            // Set GMap.NET configuration
            GMapProvider.WebProxy = null;
            GMaps.Instance.Mode = AccessMode.ServerAndCache;

            // Create overlays for markers and tracks
            _markersOverlay = new GMapOverlay("markers");
            _trackOverlay = new GMapOverlay("tracks");
            _trackPoints = new System.Collections.Generic.List<PointLatLng>();

            _mapControl.Overlays.Add(_trackOverlay);
            _mapControl.Overlays.Add(_markersOverlay);

            // Add map to form
            this.Controls.Add(_mapControl);
        }

        /// <summary>
        /// Updates the current GPS position on the map
        /// </summary>
        public void UpdatePosition(LocationData location)
        {
            if (!location.HasValidFix())
                return;

            // Update position on UI thread
            if (InvokeRequired)
            {
                Invoke(new Action<LocationData>(UpdatePosition), location);
                return;
            }

            PointLatLng point = new PointLatLng(location.Latitude, location.Longitude);

            // Update or create current position marker
            if (_currentPositionMarker != null)
            {
                _markersOverlay.Markers.Remove(_currentPositionMarker);
            }

            _currentPositionMarker = new GMarkerArrow(point, (float)location.Bearing, Color.Blue);

            // Add tooltip with GPS info
            var tooltip = new GMapToolTip(_currentPositionMarker);
            tooltip.Fill = new SolidBrush(Color.FromArgb(220, 255, 255, 255));
            tooltip.Foreground = new SolidBrush(Color.Black);
            tooltip.Stroke = new Pen(Color.Black, 2);
            tooltip.TextPadding = new Size(10, 10);

            _currentPositionMarker.ToolTipText =
                $"POSITION\n" +
                $"━━━━━━━━━━━━━━━\n" +
                $"Lat: {location.Latitude:F6}\n" +
                $"Lon: {location.Longitude:F6}\n" +
                $"Alt: {location.Altitude:F0}m\n" +
                $"Speed: {location.Speed:F1} km/h\n" +
                $"Heading: {location.Bearing:F0}°\n" +
                $"Satellites: {location.SatelliteCount}\n" +
                $"HDOP: {location.HorizontalAccuracy:F1}\n" +
                $"Time: {location.Timestamp:HH:mm:ss}";

            _currentPositionMarker.ToolTip = tooltip;
            _currentPositionMarker.ToolTipMode = MarkerTooltipMode.Always;

            _markersOverlay.Markers.Add(_currentPositionMarker);

            // Add point to track
            _trackPoints.Add(point);

            // Update track route
            if (_trackRoute != null)
            {
                _trackOverlay.Routes.Remove(_trackRoute);
            }

            if (_trackPoints.Count > 1)
            {
                _trackRoute = new GMapRoute(_trackPoints, "track");

                _trackRoute.Stroke = new Pen(Color.FromArgb(180, Color.Blue), 3);
                _trackOverlay.Routes.Add(_trackRoute);
            }

            // Center map on current position if auto-center is enabled
            if (_autoCenterEnabled)
            {
                _mapControl.Position = point;
            }

            // Refresh display
            _mapControl.Refresh();
        }

        /// <summary>
        /// Adds an observation marker to the map
        /// </summary>
        public void AddObservationMarker(LocationData location, string observationType)
        {
            if (!location.HasValidFix())
                return;

            if (InvokeRequired)
            {
                Invoke(new Action<LocationData, string>(AddObservationMarker), location, observationType);
                return;
            }

            PointLatLng point = new PointLatLng(location.Latitude, location.Longitude);

            // Choose marker color based on observation type
            GMarkerGoogleType markerType = GMarkerGoogleType.green;
            if (observationType.Contains("kangaroo"))
                markerType = GMarkerGoogleType.red;
            else if (observationType.Contains("goat"))
                markerType = GMarkerGoogleType.yellow;
            else if (observationType.Contains("emu"))
                markerType = GMarkerGoogleType.blue;

            var marker = new GMarkerGoogle(point, markerType);
            marker.ToolTipText = $"{observationType}\n{location.Timestamp:HH:mm:ss}";

            _markersOverlay.Markers.Add(marker);
            _mapControl.Refresh();
        }

        /// <summary>
        /// Clears the track history
        /// </summary>
        public void ClearTrack()
        {
            _trackPoints.Clear();
            if (_trackRoute != null)
            {
                _trackOverlay.Routes.Remove(_trackRoute);
                _trackRoute = null;
            }
            _mapControl.Refresh();
        }

        /// <summary>
        /// Changes the map provider (satellite, street, etc.)
        /// </summary>
        public void SetMapProvider(string provider)
        {
            switch (provider.ToLower())
            {
                case "satellite":
                    _mapControl.MapProvider = GMapProviders.GoogleSatelliteMap;
                    break;
                case "street":
                    _mapControl.MapProvider = GMapProviders.GoogleMap;
                    break;
                case "osm":
                    _mapControl.MapProvider = GMapProviders.OpenStreetMap;
                    break;
                case "bing":
                    _mapControl.MapProvider = GMapProviders.BingSatelliteMap;
                    break;
            }
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // MapForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1024, 768);
            this.Name = "MapForm";
            this.Text = "GPS Track Map - Aerial Survey Logger";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ResumeLayout(false);
        }
    }
}