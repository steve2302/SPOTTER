using SPOTTER.Abstractions;
using SPOTTER.Models;

namespace SPOTTER.Mobile;

/// <summary>
/// Step 4 landing page: starts the iOS GPS provider and shows live fixes.
/// Will be replaced by the real session-setup / logger pages once gamepad
/// input and session flow land.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly IGpsProvider _gps;
    private int _fixCount;

    public MainPage(IGpsProvider gps)
    {
        InitializeComponent();
        _gps = gps;
        _gps.StatusChanged += OnStatusChanged;
        _gps.LocationUpdated += OnLocationUpdated;
    }

    private void OnStartClicked(object? sender, EventArgs e)
    {
        StartButton.IsEnabled = false;
        StopButton.IsEnabled = true;
        _fixCount = 0;
        FixCountLabel.Text = "Fixes received: 0";
        _gps.Start();
    }

    private void OnStopClicked(object? sender, EventArgs e)
    {
        _gps.Stop();
        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
    }

    private void OnStatusChanged(object? sender, string message)
    {
        StatusLabel.Text = message;
    }

    private void OnLocationUpdated(object? sender, LocationData location)
    {
        _fixCount++;
        FixCountLabel.Text = $"Fixes received: {_fixCount}";

        LatLabel.Text = $"Latitude: {location.Latitude:F6}";
        LonLabel.Text = $"Longitude: {location.Longitude:F6}";
        AltLabel.Text = $"Altitude: {location.Altitude:F1} m";
        BearingLabel.Text = $"Bearing: {location.Bearing:F1}°";
        SpeedLabel.Text = $"Speed: {location.Speed:F2} m/s";
        AccLabel.Text = $"Horizontal accuracy: ±{location.HorizontalAccuracy:F1} m";
        FixTimeLabel.Text = location.GpsTime.HasValue
            ? $"Fix time: {location.GpsTime.Value:HH:mm:ss} UTC"
            : "Fix time: —";
    }
}
