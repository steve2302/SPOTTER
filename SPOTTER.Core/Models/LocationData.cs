using System;

namespace SPOTTER.Models
{
    /// <summary>
    /// Represents GPS location data
    /// </summary>
    public class LocationData
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public double Speed { get; set; }
        public double Bearing { get; set; }
        public double HorizontalAccuracy { get; set; }  // HDOP from GPS
        public double VerticalAccuracy { get; set; }    // VDOP from GPS
        public int SatelliteCount { get; set; }
        public DateTime Timestamp { get; set; }
        public long UTCTicks { get; set; }
        public bool IsValid { get; set; }
        /// <summary>GPS-derived UTC time parsed from the NMEA sentence. Null when no valid RMC sentence has been received.</summary>
        public DateTime? GpsTime { get; set; }

        /// <summary>
        /// Creates a new LocationData instance with default values
        /// </summary>
        public LocationData()
        {
            Latitude = 0;
            Longitude = 0;
            Altitude = 0;
            Speed = 0;
            Bearing = 0;
            HorizontalAccuracy = 0;
            VerticalAccuracy = 0;
            SatelliteCount = 0;
            Timestamp = DateTime.UtcNow;
            UTCTicks = Timestamp.Ticks;
            IsValid = false;
        }

        /// <summary>
        /// Checks if the location has a valid GPS fix
        /// </summary>
        public bool HasValidFix()
        {
            return IsValid &&
                   Latitude != 0 &&
                   Longitude != 0 &&
                   SatelliteCount > 0;
        }

        /// <summary>
        /// Checks if the location fix is accurate enough (HDOP < 5.0 is generally good)
        /// </summary>
        public bool IsAccurate()
        {
            return HasValidFix() && HorizontalAccuracy < 5.0;
        }

        /// <summary>
        /// Returns a formatted string of the location
        /// </summary>
        public override string ToString()
        {
            if (!IsValid)
                return "No GPS Fix";

            return $"Lat: {Latitude:F6}, Lon: {Longitude:F6}, Alt: {Altitude:F1}m, " +
                   $"Sats: {SatelliteCount}, HDOP: {HorizontalAccuracy:F1}";
        }
    }
}