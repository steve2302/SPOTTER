using SPOTTER.Abstractions;
using SPOTTER.Models;

namespace SPOTTER.Mobile;

/// <summary>
/// Step 5 landing page: GPS fixes + Xbox-controller button events + rumble test.
/// Will be replaced by the real session-setup / logger pages once session
/// management lands.
/// </summary>
public partial class MainPage : ContentPage
{
    private readonly IGpsProvider _gps;
    private readonly IGamepadProvider _gamepad;
    private int _fixCount;
    private int _buttonCount;

    public MainPage(IGpsProvider gps, IGamepadProvider gamepad)
    {
        InitializeComponent();

        _gps = gps;
        _gamepad = gamepad;

        _gps.StatusChanged += OnGpsStatusChanged;
        _gps.LocationUpdated += OnLocationUpdated;

        _gamepad.ControllerConnectionChanged += OnControllerConnectionChanged;
        _gamepad.KeyPressed += OnGamepadKeyPressed;

        // Reflect any controller already paired at startup.
        if (_gamepad.IsConnected)
            GamepadStatusLabel.Text = "Controller connected";
    }

    // ---- GPS -------------------------------------------------------------

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

    private void OnGpsStatusChanged(object? sender, string message)
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

    // ---- Gamepad ---------------------------------------------------------

    private void OnControllerConnectionChanged(object? sender, bool connected)
    {
        GamepadStatusLabel.Text = connected
            ? "Controller connected"
            : "No controller connected";
    }

    private void OnGamepadKeyPressed(object? sender, string button)
    {
        _buttonCount++;
        GamepadLastButtonLabel.Text = $"Last button: {button}";
        GamepadCountLabel.Text = $"Button presses: {_buttonCount}";
    }

    private void OnRumbleClicked(object? sender, EventArgs e)
    {
        // 1 second of medium rumble on both motors — matches the Windows
        // app's GPS-lost pattern (SetVibration(0.8, 0.8, 1000)).
        _gamepad.SetVibration(0.8f, 0.8f, 1000);
    }
}
