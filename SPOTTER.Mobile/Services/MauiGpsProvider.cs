using System.Diagnostics;
using Microsoft.Maui.Devices.Sensors;
using SPOTTER.Abstractions;
using SPOTTER.Controllers;
using SPOTTER.Models;
using MauiLocation = Microsoft.Maui.Devices.Sensors.Location;

namespace SPOTTER.Mobile.Services;

/// <summary>
/// iOS implementation of <see cref="IGpsProvider"/> backed by MAUI's
/// <see cref="IGeolocation"/> (which wraps CoreLocation on iPadOS).
/// On a Wi-Fi + Cellular iPad this reads the built-in GNSS directly;
/// no NMEA parsing is involved, so the SatelliteInfoUpdated and
/// RawNMEAReceived events are declared but never fire. SatellitesInView
/// and SatellitesUsed stay zero.
/// </summary>
public sealed class MauiGpsProvider : IGpsProvider
{
    private readonly IGeolocation _geolocation;
    private readonly LocationData _currentLocation = new();
    private bool _isConnected;
    private bool _isListening;
    private DateTime _lastFixSystemTime = DateTime.MinValue;

    public MauiGpsProvider(IGeolocation geolocation)
    {
        _geolocation = geolocation;
    }

    public LocationData CurrentLocation => _currentLocation;

    public bool IsConnected => _isConnected;

    public string ConnectedDeviceName => _isConnected ? "iPad built-in GPS" : string.Empty;

    // CoreLocation does not expose satellite counts.
    public int SatellitesInView => 0;
    public int SatellitesUsed => 0;

    public event EventHandler<LocationData>? LocationUpdated;
    public event EventHandler<string>? StatusChanged;

    // CoreLocation does not expose satellite counts or raw NMEA sentences,
    // so these interface events are declared with no-op accessors. The
    // explicit add/remove keeps the compiler quiet about an "unused event"
    // and documents the intentional omission.
    public event EventHandler<SatelliteInfoEventArgs>? SatelliteInfoUpdated { add { } remove { } }
    public event EventHandler<string>? RawNMEAReceived { add { } remove { } }

    public DateTime GetBestTime()
    {
        // Prefer the last GPS-stamped fix extended by the system clock,
        // falling back to the system clock when no fix has arrived yet.
        if (_currentLocation.GpsTime.HasValue && _lastFixSystemTime != DateTime.MinValue)
        {
            var elapsed = DateTime.UtcNow - _lastFixSystemTime;
            return _currentLocation.GpsTime.Value + elapsed;
        }
        return DateTime.UtcNow;
    }

    public async void Start()
    {
        if (_isListening) return;

        RaiseStatus("Requesting location permission...");

        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            }

            if (status != PermissionStatus.Granted)
            {
                RaiseStatus("Location permission denied");
                return;
            }

            _geolocation.LocationChanged -= OnLocationChanged;
            _geolocation.ListeningFailed -= OnListeningFailed;
            _geolocation.LocationChanged += OnLocationChanged;
            _geolocation.ListeningFailed += OnListeningFailed;

            var request = new GeolocationListeningRequest(
                GeolocationAccuracy.Best,
                TimeSpan.FromSeconds(1));

            var started = await _geolocation.StartListeningForegroundAsync(request);
            if (started)
            {
                _isListening = true;
                _isConnected = true;
                RaiseStatus("Listening for GPS fixes");
            }
            else
            {
                RaiseStatus("Failed to start location updates");
            }
        }
        catch (FeatureNotSupportedException)
        {
            RaiseStatus("Location not supported on this device");
        }
        catch (FeatureNotEnabledException)
        {
            RaiseStatus("Location services are turned off");
        }
        catch (PermissionException)
        {
            RaiseStatus("Location permission denied");
        }
        catch (Exception ex)
        {
            RaiseStatus($"GPS error: {ex.Message}");
            Debug.WriteLine($"[MauiGpsProvider.Start] {ex}");
        }
    }

    public void Stop()
    {
        if (!_isListening) return;

        try
        {
            _geolocation.LocationChanged -= OnLocationChanged;
            _geolocation.ListeningFailed -= OnListeningFailed;
            _geolocation.StopListeningForeground();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MauiGpsProvider.Stop] {ex}");
        }
        finally
        {
            _isListening = false;
            _isConnected = false;
            RaiseStatus("Stopped");
        }
    }

    public void Dispose() => Stop();

    private void OnLocationChanged(object? sender, GeolocationLocationChangedEventArgs e)
    {
        if (e.Location is null) return;
        ApplyFix(e.Location);
    }

    private void OnListeningFailed(object? sender, GeolocationListeningFailedEventArgs e)
    {
        _isConnected = false;
        RaiseStatus($"GPS listener failed: {e.Error}");
    }

    private void ApplyFix(MauiLocation fix)
    {
        _currentLocation.Latitude = fix.Latitude;
        _currentLocation.Longitude = fix.Longitude;
        _currentLocation.Altitude = fix.Altitude ?? 0;
        _currentLocation.Speed = fix.Speed ?? 0;
        _currentLocation.Bearing = fix.Course ?? 0;
        _currentLocation.HorizontalAccuracy = fix.Accuracy ?? 0;
        _currentLocation.VerticalAccuracy = fix.VerticalAccuracy ?? 0;
        _currentLocation.SatelliteCount = 0;
        _currentLocation.Timestamp = DateTime.UtcNow;
        _currentLocation.UTCTicks = _currentLocation.Timestamp.Ticks;
        _currentLocation.GpsTime = fix.Timestamp.UtcDateTime;
        _currentLocation.IsValid = true;

        _lastFixSystemTime = DateTime.UtcNow;
        _isConnected = true;

        var snapshot = _currentLocation;
        MainThread.BeginInvokeOnMainThread(() =>
            LocationUpdated?.Invoke(this, snapshot));
    }

    private void RaiseStatus(string message)
    {
        MainThread.BeginInvokeOnMainThread(() =>
            StatusChanged?.Invoke(this, message));
    }
}
