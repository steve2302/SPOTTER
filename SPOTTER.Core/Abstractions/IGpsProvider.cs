using System;
using SPOTTER.Models;

namespace SPOTTER.Abstractions
{
    /// <summary>
    /// Platform-neutral contract for a GPS source. The Windows implementation
    /// (<see cref="SPOTTER.Controllers.GPSController"/>) reads NMEA 0183 from a
    /// serial COM port with multi-port failover. The iOS implementation will
    /// read from CoreLocation (iPad built-in GNSS) and, optionally, parse
    /// NMEA 0183 from a paired Bluetooth receiver.
    ///
    /// Consumers subscribe to <see cref="LocationUpdated"/> for position fixes
    /// and <see cref="StatusChanged"/> for human-readable connection status.
    /// </summary>
    public interface IGpsProvider : IDisposable
    {
        /// <summary>Most recent parsed location fix, or an invalid default.</summary>
        LocationData CurrentLocation { get; }

        /// <summary>True while the provider is receiving data from a device.</summary>
        bool IsConnected { get; }

        /// <summary>
        /// A short, platform-specific name for the connected device
        /// (e.g. "COM5" on Windows, "iPad GPS" or "Bad Elf GPS Pro" on iOS).
        /// Empty or null when nothing is connected.
        /// </summary>
        string ConnectedDeviceName { get; }

        /// <summary>Satellites currently visible in the sky.</summary>
        int SatellitesInView { get; }

        /// <summary>Satellites used in the current fix.</summary>
        int SatellitesUsed { get; }

        /// <summary>Fires on every new position fix.</summary>
        event EventHandler<LocationData> LocationUpdated;

        /// <summary>Human-readable status string ("Scanning...", "Connected to COM5", etc.).</summary>
        event EventHandler<string> StatusChanged;

        /// <summary>Fires when satellite counts change.</summary>
        event EventHandler<SPOTTER.Controllers.SatelliteInfoEventArgs> SatelliteInfoUpdated;

        /// <summary>
        /// Fires for each raw NMEA sentence received, for diagnostics.
        /// On platforms that bypass NMEA (iOS CoreLocation) this event may
        /// never fire — subscribers must not depend on it.
        /// </summary>
        event EventHandler<string> RawNMEAReceived;

        /// <summary>
        /// Best available UTC time: the last GPS-derived UTC when fresh,
        /// otherwise the system clock. Preferred for observation timestamps.
        /// </summary>
        DateTime GetBestTime();

        /// <summary>Begin acquiring fixes. Idempotent — safe to call again.</summary>
        void Start();

        /// <summary>Stop acquiring fixes and release the device.</summary>
        void Stop();
    }
}
