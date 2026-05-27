using System;

namespace SPOTTER.Models
{
    /// <summary>
    /// Represents an observation record for wildlife sightings
    /// </summary>
    public class ObservationRecord
    {
        /// <summary>
        /// Gets or sets the timestamp when the observation was recorded
        /// </summary>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Gets or sets the session identifier (AM/PM)
        /// </summary>
        public string Session { get; set; }

        /// <summary>
        /// Gets or sets the observer's name or initials
        /// </summary>
        public string Observer { get; set; }

        /// <summary>
        /// Gets or sets the observer's position in the aircraft
        /// </summary>
        public string Position { get; set; }

        /// <summary>
        /// Gets or sets the UTC time in ticks when the observation was made
        /// </summary>
        public long UTCTicks { get; set; }

        /// <summary>
        /// Gets or sets the latitude where the observation was made
        /// </summary>
        public double Latitude { get; set; }

        /// <summary>
        /// Gets or sets the longitude where the observation was made
        /// </summary>
        public double Longitude { get; set; }

        /// <summary>
        /// Gets or sets the bearing/course of the aircraft when the observation was made
        /// </summary>
        public double Bearing { get; set; }

        /// <summary>
        /// Gets or sets the altitude of the aircraft when the observation was made
        /// </summary>
        public double Altitude { get; set; }

        /// <summary>
        /// Gets or sets the horizontal accuracy (HDOP) of the GPS at time of observation
        /// </summary>
        public double HDOP { get; set; }

        /// <summary>
        /// Gets or sets the vertical accuracy (VDOP) of the GPS at time of observation
        /// </summary>
        public double VDOP { get; set; }

        /// <summary>
        /// Gets or sets the cloud cover at time of observation
        /// </summary>
        public string CloudCover { get; set; } = "NA";

        /// <summary>
        /// Gets or sets the temperature in Celsius at time of observation
        /// </summary>
        public decimal Temperature { get; set; } = -9999;

        /// <summary>
        /// Gets or sets the wind speed in knots at time of observation
        /// </summary>
        public decimal Wind { get; set; } = -9999;

        /// <summary>
        /// Gets or sets the actual observation details (species, count, distance class, etc.)
        /// </summary>
        public string Observation { get; set; }

        /// <summary>
        /// Initializes a new instance of the ObservationRecord class with the current timestamp
        /// </summary>
        public ObservationRecord()
        {
            Timestamp = DateTime.UtcNow;
        }

        /// <summary>
        /// Creates a formatted string representation of the observation record for logging
        /// </summary>
        /// <returns>Formatted string for the observation record</returns>
        public override string ToString()
        {
            return $"{Timestamp.ToLongTimeString()},{Session},{Observer},{Position},{UTCTicks}," +
                   $"{Latitude},{Longitude},{Bearing},{Altitude},{HDOP},{VDOP},{CloudCover},{Temperature},{Wind}{Observation}";
        }
    }
}