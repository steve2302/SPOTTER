using System;
using System.IO.Ports;
using System.Text.RegularExpressions;

namespace SPOTTER.Controllers
{
    /// <summary>
    /// Parses NMEA sentences from GPS devices and extracts satellite information
    /// </summary>
    public class NmeaParser
    {
        private SerialPort _serialPort;
        private int _satellitesInView = 0;
        private int _satellitesUsed = 0;

        public event EventHandler<SatelliteInfoEventArgs> SatelliteInfoUpdated;
        public event EventHandler<string> ErrorOccurred;

        public int SatellitesInView => _satellitesInView;
        public int SatellitesUsed => _satellitesUsed;
        public bool IsConnected => _serialPort != null && _serialPort.IsOpen;

        /// <summary>
        /// Opens a connection to the GPS device on the specified COM port
        /// </summary>
        /// <param name="portName">COM port name (e.g., "COM3")</param>
        /// <param name="baudRate">Baud rate (typically 4800 or 9600 for GPS)</param>
        public bool Connect(string portName, int baudRate = 4800)
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _serialPort.Close();
                    _serialPort.Dispose();
                }

                _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One);
                _serialPort.DataReceived += SerialPort_DataReceived;
                _serialPort.Open();

                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Failed to connect to GPS on {portName}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Disconnects from the GPS device
        /// </summary>
        public void Disconnect()
        {
            try
            {
                if (_serialPort != null && _serialPort.IsOpen)
                {
                    _serialPort.DataReceived -= SerialPort_DataReceived;
                    _serialPort.Close();
                    _serialPort.Dispose();
                    _serialPort = null;
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Error disconnecting from GPS: {ex.Message}");
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string data = _serialPort.ReadLine();
                ParseNmeaSentence(data);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading NMEA data: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses an NMEA sentence and extracts satellite information
        /// </summary>
        /// <param name="sentence">NMEA sentence string</param>
        private void ParseNmeaSentence(string sentence)
        {
            if (string.IsNullOrWhiteSpace(sentence))
                return;

            // Validate checksum
            if (!ValidateChecksum(sentence))
                return;

            // Parse GGA sentence for satellites used
            if (sentence.StartsWith("$GPGGA") || sentence.StartsWith("$GNGGA"))
            {
                ParseGGA(sentence);
            }
            // Parse GSV sentence for satellites in view
            else if (sentence.StartsWith("$GPGSV") || sentence.StartsWith("$GLGSV") ||
                     sentence.StartsWith("$GAGSV") || sentence.StartsWith("$GNGSV"))
            {
                ParseGSV(sentence);
            }
        }

        /// <summary>
        /// Validates the NMEA sentence checksum
        /// </summary>
        private bool ValidateChecksum(string sentence)
        {
            try
            {
                // NMEA format: $GPGGA,data,data,...*CS
                int checksumIndex = sentence.IndexOf('*');
                if (checksumIndex < 0)
                    return false;

                string dataToCheck = sentence.Substring(1, checksumIndex - 1); // Exclude $ and *
                string checksumStr = sentence.Substring(checksumIndex + 1, 2);

                int checksum = 0;
                foreach (char c in dataToCheck)
                {
                    checksum ^= c;
                }

                int receivedChecksum = Convert.ToInt32(checksumStr, 16);
                return checksum == receivedChecksum;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Parses GGA sentence to get number of satellites used
        /// Format: $GPGGA,time,lat,N,lon,E,quality,numSV,HDOP,alt,M,geoid,M,,*CS
        /// </summary>
        private void ParseGGA(string sentence)
        {
            try
            {
                string[] parts = sentence.Split(',');

                if (parts.Length >= 8)
                {
                    if (int.TryParse(parts[7], out int satUsed))
                    {
                        _satellitesUsed = satUsed;

                        // Notify listeners of update
                        SatelliteInfoUpdated?.Invoke(this, new SatelliteInfoEventArgs
                        {
                            SatellitesInView = _satellitesInView,
                            SatellitesUsed = _satellitesUsed
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing GGA: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses GSV sentence to get number of satellites in view
        /// Format: $GPGSV,numMsg,msgNum,numSV,sat1,elev1,azim1,snr1,...*CS
        /// </summary>
        private void ParseGSV(string sentence)
        {
            try
            {
                string[] parts = sentence.Split(',');

                if (parts.Length >= 4)
                {
                    // The third field is the total number of satellites in view
                    if (int.TryParse(parts[3], out int satInView))
                    {
                        _satellitesInView = satInView;

                        // Notify listeners of update
                        SatelliteInfoUpdated?.Invoke(this, new SatelliteInfoEventArgs
                        {
                            SatellitesInView = _satellitesInView,
                            SatellitesUsed = _satellitesUsed
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing GSV: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets a list of available COM ports
        /// </summary>
        public static string[] GetAvailablePorts()
        {
            return SerialPort.GetPortNames();
        }
    }

    /// <summary>
    /// Event arguments for satellite information updates
    /// </summary>
    public class SatelliteInfoEventArgs : EventArgs
    {
        public int SatellitesInView { get; set; }
        public int SatellitesUsed { get; set; }
    }
}