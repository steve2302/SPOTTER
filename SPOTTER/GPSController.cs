using SPOTTER.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SPOTTER.Controllers
{
    /// <summary>
    /// GPS Controller that reads NMEA data from serial COM ports
    /// Automatically scans and detects GPS on available COM ports
    /// </summary>
    public class GPSController : IDisposable
    {
        private SerialPort _serialPort;
        private CancellationTokenSource _cancellationTokenSource;
        private Task _readTask;
        private string _connectedPort;
        private bool _isRunning;
        private DateTime _lastValidFix = DateTime.MinValue;

        // NMEA parsing state
        private double _latitude;
        private double _longitude;
        private double _altitude;
        private double _speed;
        private double _bearing;
        private double _hdop;
        private double _vdop;
        private int _satelliteCount;
        private int _satellitesUsed;
        private int _satellitesInView;
        private DateTime _lastUpdateTime;
        private DateTime? _gpsTime;                          // last GPS UTC time from a valid RMC sentence
        private DateTime _gpsTimeReceivedAt = DateTime.MinValue; // system clock when _gpsTime was captured

        private const double GPS_TIME_STALENESS_SECONDS = 5.0;

        public LocationData CurrentLocation { get; private set; }
        public bool IsConnected => _serialPort != null && _serialPort.IsOpen;
        public string ConnectedPortName => _connectedPort;
        public int SatellitesInView => _satellitesInView;
        public int SatellitesUsed => _satellitesUsed;

        /// <summary>
        /// Returns the best available UTC timestamp. Uses GPS time from the most recent valid RMC
        /// sentence (interpolated forward with the system clock for sub-second precision) while it
        /// is less than <see cref="GPS_TIME_STALENESS_SECONDS"/> old. Falls back to DateTime.UtcNow
        /// when GPS time has never been received or the GPS has dropped out.
        /// </summary>
        public DateTime GetBestTime()
        {
            if (_gpsTime.HasValue)
            {
                TimeSpan elapsed = DateTime.UtcNow - _gpsTimeReceivedAt;
                if (elapsed.TotalSeconds <= GPS_TIME_STALENESS_SECONDS)
                    return _gpsTime.Value + elapsed;
            }
            return DateTime.UtcNow;
        }

        // Events
        public event EventHandler<LocationData> LocationUpdated;
        public event EventHandler<string> StatusChanged;
        public event EventHandler<string> RawNMEAReceived;
        public event EventHandler<SatelliteInfoEventArgs> SatelliteInfoUpdated;

        // Constants for COM port scanning
        private const int PORT_TEST_TIMEOUT_MS = 3000;
        private const int NMEA_READ_TIMEOUT_MS = 2000;
        private const int SCAN_RETRY_DELAY_MS = 5000;

        /// <summary>
        /// When set, this port is tried first before the full auto-scan.
        /// Use for devices like the Panasonic FZ-G1 where the u-blox GPS is on a known
        /// port (COM5) but may be skipped during auto-scan if the Windows sensor driver
        /// holds the port briefly on first access.
        /// </summary>
        public string PreferredPort { get; set; }

        public GPSController()
        {
            CurrentLocation = new LocationData();

            // Diagnostic: Show available COM ports
            Debug.WriteLine("");
            Debug.WriteLine("=== COM PORT DIAGNOSTIC ===");
            string[] ports = SerialPort.GetPortNames();
            Debug.WriteLine($"Windows sees {ports.Length} COM port(s):");
            foreach (string port in ports)
            {
                Debug.WriteLine($"  - {port}");
            }
            Debug.WriteLine("=== END DIAGNOSTIC ===");
            Debug.WriteLine("");
        }

        /// <summary>
        /// Starts GPS controller - scans for GPS and begins reading
        /// </summary>
        public void Start()
        {
            if (_isRunning)
            {
                Debug.WriteLine("GPS Controller already running");
                return;
            }

            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();

            OnStatusChanged("Scanning for GPS...");

            Debug.WriteLine("=== GPS Controller Starting ===");

            // Start GPS scanning and reading task
            _readTask = Task.Run(() => ScanAndConnectGPS(_cancellationTokenSource.Token), _cancellationTokenSource.Token);
        }

        /// <summary>
        /// Stops GPS controller and closes serial port
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;

            try
            {
                _cancellationTokenSource?.Cancel();
                _readTask?.Wait(1000); // Wait up to 1 second for task to complete
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error stopping GPS task: {ex.Message}");
            }

            CloseSerialPort();
            OnStatusChanged("GPS stopped");
        }

        /// <summary>
        /// Scans COM ports to find GPS and establishes connection
        /// </summary>
        private async Task ScanAndConnectGPS(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_serialPort == null || !_serialPort.IsOpen)
                {
                    // Try to find and connect to GPS
                    bool connected = await TryFindGPS(cancellationToken);

                    if (!connected)
                    {
                        Debug.WriteLine($"GPS scan cycle complete - no GPS found. Retrying in {SCAN_RETRY_DELAY_MS / 1000} seconds...");
                        OnStatusChanged("GPS not found - retrying...");
                        await Task.Delay(SCAN_RETRY_DELAY_MS, cancellationToken);
                        continue;
                    }
                }

                // Read NMEA data from connected port
                try
                {
                    await ReadNMEAData(cancellationToken);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error reading GPS data: {ex.Message}");
                    CloseSerialPort();
                    OnStatusChanged("GPS connection lost - reconnecting...");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Scans COM ports to find one with valid NMEA GPS data
        /// </summary>
        private async Task<bool> TryFindGPS(CancellationToken cancellationToken)
        {
            Debug.WriteLine("=== Starting GPS Port Scan ===");

            // If a preferred port is configured, try it first
            if (!string.IsNullOrWhiteSpace(PreferredPort))
            {
                Debug.WriteLine($"==> Trying preferred port: {PreferredPort}");
                int[] baudRates = { 4800, 9600, 19200, 38400, 115200 };
                foreach (int baudRate in baudRates)
                {
                    if (cancellationToken.IsCancellationRequested) return false;
                    if (await TestPort(PreferredPort, baudRate, cancellationToken))
                    {
                        OnStatusChanged($"GPS found on {PreferredPort} at {baudRate} baud");
                        return true;
                    }
                }
                Debug.WriteLine($"    Preferred port {PreferredPort} not available — falling back to full scan");
            }

            // Get available ports and clean them
            string[] availablePorts = SerialPort.GetPortNames()
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Where(p => !string.Equals(p, PreferredPort, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p)
                .ToArray();

            Debug.WriteLine($"Windows reports {availablePorts.Length} COM port(s) available");

            if (availablePorts.Length == 0)
            {
                Debug.WriteLine("ERROR: No COM ports found!");
                return false;
            }

            Debug.WriteLine("Available COM ports:");
            foreach (string port in availablePorts)
            {
                Debug.WriteLine($"  - {port}");
            }

            // Try common GPS baud rates
            int[] baudRates = { 4800, 9600, 19200, 38400, 115200 };

            // Test each available port directly
            foreach (string portName in availablePorts)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Debug.WriteLine("GPS scan cancelled by user");
                    return false;
                }

                Debug.WriteLine($"\n==> Testing port: {portName}");

                // Try each baud rate for this port
                foreach (int baudRate in baudRates)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return false;

                    Debug.WriteLine($"    Trying {portName} at {baudRate} baud");

                    if (await TestPort(portName, baudRate, cancellationToken))
                    {
                        OnStatusChanged($"GPS found on {portName} at {baudRate} baud");
                        return true;
                    }
                }

                Debug.WriteLine($"    {portName} - No GPS found at any baud rate");
            }

            Debug.WriteLine("\nNo GPS found on any available COM port");
            Debug.WriteLine("All available ports were tested but none responded with valid NMEA data");
            return false;
        }

        private async Task<string> ReadLineAsync(SerialPort port, CancellationToken cancellationToken, int timeoutMs)
        {
            var stream = port.BaseStream;
            var sb = new System.Text.StringBuilder();

            using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                linkedCts.CancelAfter(timeoutMs);
                var token = linkedCts.Token;
                var buffer = new byte[1];

                try
                {
                    while (true)
                    {
                        int n = await stream.ReadAsync(buffer, 0, 1, token).ConfigureAwait(false);
                        if (n == 0)
                            continue;

                        char ch = (char)buffer[0];
                        if (ch == '\n')
                            break;
                        if (ch != '\r')
                            sb.Append(ch);
                    }

                    return sb.ToString();
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException("ReadLine timed out or was cancelled.");
                }
            }
        }

        /// <summary>
        /// Tests if a specific COM port has valid NMEA GPS data
        /// </summary>
        private async Task<bool> TestPort(string portName, int baudRate, CancellationToken cancellationToken)
        {
            SerialPort testPort = null;

            try
            {
                Debug.WriteLine($"        Attempting to open {portName}...");

                testPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = PORT_TEST_TIMEOUT_MS,
                    WriteTimeout = PORT_TEST_TIMEOUT_MS
                };

                testPort.Open();
                Debug.WriteLine($"        âœ“ Port {portName} opened successfully");

                // Try to read several lines and look for valid NMEA
                DateTime startTime = DateTime.Now;
                int validNMEACount = 0;
                int readAttempts = 0;
                const int MAX_ATTEMPTS = 10;

                while ((DateTime.Now - startTime).TotalMilliseconds < PORT_TEST_TIMEOUT_MS &&
                       validNMEACount < 2 &&
                       readAttempts < MAX_ATTEMPTS)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Debug.WriteLine($"        Cancelled while testing {portName}");
                        break;
                    }

                    try
                    {
                        readAttempts++;
                        string line = await ReadLineAsync(testPort, cancellationToken, PORT_TEST_TIMEOUT_MS);

                        // Only log first few attempts to reduce spam
                        if (readAttempts <= 3)
                        {
                            string preview = line.Length > 60 ? line.Substring(0, 60) + "..." : line;
                            Debug.WriteLine($"        Read [{readAttempts}]: {preview}");
                        }

                        if (IsValidNMEASentence(line))
                        {
                            validNMEACount++;
                            Debug.WriteLine($"        âœ“ Valid NMEA sentence! Count: {validNMEACount}");
                        }
                    }
                    catch (TimeoutException)
                    {
                        Debug.WriteLine($"        Timeout reading from {portName}");
                        break;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"        Error reading: {ex.Message}");
                        break;
                    }
                }

                if (validNMEACount >= 2)
                {
                    Debug.WriteLine($"        âœ“âœ“âœ“ SUCCESS! Found valid GPS on {portName} at {baudRate} baud âœ“âœ“âœ“");

                    // Close test port
                    testPort.Close();
                    testPort.Dispose();
                    testPort = null;

                    // Create fresh connection for actual use
                    _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
                    {
                        ReadTimeout = NMEA_READ_TIMEOUT_MS,
                        WriteTimeout = NMEA_READ_TIMEOUT_MS
                    };

                    _serialPort.Open();
                    _connectedPort = portName;

                    Debug.WriteLine($"        Fresh connection established to {portName}");
                    return true;
                }
                else
                {
                    Debug.WriteLine($"        Not enough valid NMEA sentences (got {validNMEACount}, need 2)");
                }
            }
            catch (UnauthorizedAccessException)
            {
                Debug.WriteLine($"        âœ— ACCESS DENIED to {portName} (port in use by another application)");
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"        âœ— IO Error on {portName}: {ex.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"        âœ— Error testing {portName}: {ex.Message}");
            }
            finally
            {
                // Clean up test port
                if (testPort != null && testPort.IsOpen)
                {
                    try
                    {
                        testPort.Close();
                        testPort.Dispose();
                    }
                    catch { }
                }
            }

            return false;
        }



        /// <summary>
        /// Reads and processes NMEA data from the connected serial port
        /// </summary>
        private async Task ReadNMEAData(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _serialPort != null && _serialPort.IsOpen)
            {
                try
                {
                    string line = await Task.Run(() => _serialPort.ReadLine(), cancellationToken);
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        ProcessNMEASentence(line.Trim());
                    }
                }
                catch (TimeoutException)
                {
                    // Normal timeout, continue reading
                    continue;
                }
                catch (InvalidOperationException ex)
                {
                    Debug.WriteLine($"Serial port closed unexpectedly: {ex.Message}");
                    break;
                }
                catch (IOException ex)
                {
                    Debug.WriteLine($"IO error while reading from serial port: {ex.Message}");
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Unexpected error while reading NMEA data: {ex.Message}");
                    break;
                }
            }
        }

        /// <summary>
        /// Validates if a string is a proper NMEA sentence
        /// </summary>
        private bool IsValidNMEASentence(string sentence)
        {
            if (string.IsNullOrWhiteSpace(sentence))
                return false;

            sentence = sentence.Trim();

            // NMEA sentences start with $
            if (!sentence.StartsWith("$"))
                return false;

            // Check for common GPS sentence types (GP = GPS, GN = GNSS, GL = GLONASS)
            if (sentence.StartsWith("$GPGGA") || sentence.StartsWith("$GPRMC") ||
                sentence.StartsWith("$GPGSA") || sentence.StartsWith("$GPGSV") ||
                sentence.StartsWith("$GNGGA") || sentence.StartsWith("$GNRMC") ||
                sentence.StartsWith("$GNGSA") || sentence.StartsWith("$GNGSV") ||
                sentence.StartsWith("$GLGSA") || sentence.StartsWith("$GLGSV"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Processes a single NMEA sentence and updates location data
        /// </summary>
        private void ProcessNMEASentence(string sentence)
        {
            if (!IsValidNMEASentence(sentence))
                return;

            try
            {
                // Fire raw NMEA event for debugging/logging
                // WARNING: Event handlers must use Invoke() for UI updates!
                RawNMEAReceived?.Invoke(this, sentence);

                // Remove the $ at the start
                sentence = sentence.Substring(1);

                // Split by comma
                string[] parts = sentence.Split(',');

                if (parts.Length < 1)
                    return;

                string sentenceType = parts[0];

                // Process different NMEA sentence types
                if (sentenceType.EndsWith("GGA"))
                {
                    ParseGGA(parts);
                }
                else if (sentenceType.EndsWith("RMC"))
                {
                    ParseRMC(parts);
                }
                else if (sentenceType.EndsWith("GSA"))
                {
                    ParseGSA(parts);
                }
                else if (sentenceType.EndsWith("GSV"))
                {
                    ParseGSV(parts);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing NMEA sentence: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses GPGGA sentence (Global Positioning System Fix Data)
        /// </summary>
        private void ParseGGA(string[] parts)
        {
            // $GPGGA,hhmmss.ss,llll.ll,a,yyyyy.yy,a,x,xx,x.x,x.x,M,x.x,M,x.x,xxxx*hh

            if (parts.Length < 10)
                return;

            try
            {
                // Latitude
                if (!string.IsNullOrEmpty(parts[2]) && !string.IsNullOrEmpty(parts[3]))
                {
                    double lat = ParseCoordinate(parts[2]);
                    if (parts[3] == "S") lat = -lat;
                    _latitude = lat;
                }

                // Longitude
                if (!string.IsNullOrEmpty(parts[4]) && !string.IsNullOrEmpty(parts[5]))
                {
                    double lon = ParseCoordinate(parts[4]);
                    if (parts[5] == "W") lon = -lon;
                    _longitude = lon;
                }

                // Fix quality (0 = invalid, 1 = GPS fix, 2 = DGPS fix)
                int fixQuality = 0;
                if (!string.IsNullOrEmpty(parts[6]))
                    int.TryParse(parts[6], out fixQuality);

                // Number of satellites (satellites used for fix)
                if (!string.IsNullOrEmpty(parts[7]))
                {
                    int.TryParse(parts[7], out _satelliteCount);
                    _satellitesUsed = _satelliteCount; // GGA reports satellites used

                    // Fire satellite info event
                    OnSatelliteInfoUpdated();
                }

                // HDOP (Horizontal Dilution of Precision)
                if (!string.IsNullOrEmpty(parts[8]))
                    double.TryParse(parts[8], out _hdop);

                // Altitude
                if (!string.IsNullOrEmpty(parts[9]))
                    double.TryParse(parts[9], out _altitude);

                // Update location if we have a valid fix
                if (fixQuality > 0 && _satelliteCount > 0)
                {
                    _lastValidFix = DateTime.UtcNow;
                    UpdateLocation();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing GGA: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses GPRMC sentence (Recommended Minimum Specific GPS/Transit Data)
        /// </summary>
        private void ParseRMC(string[] parts)
        {
            // $GPRMC,hhmmss.ss,A,llll.ll,a,yyyyy.yy,a,x.x,x.x,ddmmyy,x.x,a*hh

            if (parts.Length < 10)
                return;

            try
            {
                // Status (A = active/valid, V = void/invalid)
                string status = parts[2];
                if (status != "A")
                    return; // Invalid fix

                // GPS UTC time — RMC is the only sentence that carries both time and date
                var parsed = ParseNmeaDateTime(parts[1], parts.Length > 9 ? parts[9] : null);
                if (parsed.HasValue)
                {
                    _gpsTime = parsed;
                    _gpsTimeReceivedAt = DateTime.UtcNow;
                }

                // Latitude
                if (!string.IsNullOrEmpty(parts[3]) && !string.IsNullOrEmpty(parts[4]))
                {
                    double lat = ParseCoordinate(parts[3]);
                    if (parts[4] == "S") lat = -lat;
                    _latitude = lat;
                }

                // Longitude
                if (!string.IsNullOrEmpty(parts[5]) && !string.IsNullOrEmpty(parts[6]))
                {
                    double lon = ParseCoordinate(parts[5]);
                    if (parts[6] == "W") lon = -lon;
                    _longitude = lon;
                }

                // Speed over ground (knots)
                if (!string.IsNullOrEmpty(parts[7]))
                {
                    double speedKnots = 0;
                    if (double.TryParse(parts[7], out speedKnots))
                    {
                        _speed = speedKnots * 1.852; // Convert knots to km/h
                    }
                }

                // Track angle (bearing)
                if (!string.IsNullOrEmpty(parts[8]))
                    double.TryParse(parts[8], out _bearing);

                _lastValidFix = DateTime.UtcNow;
                UpdateLocation();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing RMC: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses GPGSA sentence (GPS DOP and active satellites)
        /// </summary>
        private void ParseGSA(string[] parts)
        {
            // $GPGSA,A,3,04,05,,09,12,,,24,,,,,2.5,1.3,2.1*39

            if (parts.Length < 17)
                return;

            try
            {
                // HDOP (Horizontal) - parts[16]
                if (!string.IsNullOrEmpty(parts[16]))
                    double.TryParse(parts[16], out _hdop);

                // VDOP (Vertical) - parts[17]
                if (parts.Length > 17 && !string.IsNullOrEmpty(parts[17]))
                {
                    string vdopStr = parts[17].Split('*')[0]; // Remove checksum
                    double.TryParse(vdopStr, out _vdop);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing GSA: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses GPGSV sentence (GPS Satellites in View)
        /// Format: $GPGSV,numMsg,msgNum,numSV,sat1,elev1,azim1,snr1,...*CS
        /// </summary>
        private void ParseGSV(string[] parts)
        {
            // $GPGSV,3,1,12,01,,,43,02,,,44,03,,,45,04,,,46*7B

            if (parts.Length < 4)
                return;

            try
            {
                // The third field (parts[3]) is the total number of satellites in view
                if (!string.IsNullOrEmpty(parts[3]))
                {
                    // Remove checksum if present
                    string satCountStr = parts[3].Split('*')[0];

                    if (int.TryParse(satCountStr, out int satInView))
                    {
                        _satellitesInView = satInView;

                        // Fire satellite info event
                        OnSatelliteInfoUpdated();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error parsing GSV: {ex.Message}");
            }
        }

        /// <summary>
        /// Parses a UTC DateTime from RMC time (hhmmss.ss) and date (ddmmyy) fields.
        /// Returns null if either field is missing or malformed.
        /// </summary>
        private static DateTime? ParseNmeaDateTime(string timeField, string dateField)
        {
            if (string.IsNullOrEmpty(timeField) || timeField.Length < 6 ||
                string.IsNullOrEmpty(dateField) || dateField.Length < 6)
                return null;

            if (!int.TryParse(timeField.Substring(0, 2), out int hour))   return null;
            if (!int.TryParse(timeField.Substring(2, 2), out int minute)) return null;
            if (!int.TryParse(timeField.Substring(4, 2), out int second)) return null;
            if (!int.TryParse(dateField.Substring(0, 2), out int day))    return null;
            if (!int.TryParse(dateField.Substring(2, 2), out int month))  return null;
            if (!int.TryParse(dateField.Substring(4, 2), out int year))   return null;

            try
            {
                return new DateTime(2000 + year, month, day, hour, minute, second, DateTimeKind.Utc);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Converts NMEA coordinate format (ddmm.mmmm) to decimal degrees
        /// </summary>
        private double ParseCoordinate(string nmeaCoord)
        {
            if (string.IsNullOrEmpty(nmeaCoord))
                return 0;

            // NMEA format: ddmm.mmmm or dddmm.mmmm
            int decimalIndex = nmeaCoord.IndexOf('.');
            if (decimalIndex < 0)
                return 0;

            // Heuristic: latitude uses 2 degree digits, longitude uses 3
            int degreeDigits = (nmeaCoord.Length <= 9) ? 2 : 3;

            string degreesPart = nmeaCoord.Substring(0, degreeDigits);
            string minutesPart = nmeaCoord.Substring(degreeDigits);

            double degrees = double.Parse(degreesPart);
            double minutes = double.Parse(minutesPart);

            return degrees + (minutes / 60.0);
        }

        /// <summary>
        /// Updates the CurrentLocation property and fires LocationUpdated event
        /// </summary>
        private void UpdateLocation()
        {
            _lastUpdateTime = DateTime.UtcNow;

            CurrentLocation = new LocationData
            {
                Latitude = _latitude,
                Longitude = _longitude,
                Altitude = _altitude,
                Speed = _speed,
                Bearing = _bearing,
                HorizontalAccuracy = _hdop,
                VerticalAccuracy = _vdop,
                SatelliteCount = _satelliteCount,
                Timestamp = _lastUpdateTime,
                UTCTicks = _lastUpdateTime.Ticks,
                IsValid = true,
                GpsTime = _gpsTime
            };

            OnLocationUpdated(CurrentLocation);
        }

        /// <summary>
        /// Closes the serial port connection
        /// </summary>
        private void CloseSerialPort()
        {
            if (_serialPort != null)
            {
                try
                {
                    if (_serialPort.IsOpen)
                        _serialPort.Close();

                    _serialPort.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error closing serial port: {ex.Message}");
                }
                finally
                {
                    _serialPort = null;
                    _connectedPort = null;
                }
            }
        }

        /// <summary>
        /// Raises the LocationUpdated event
        /// </summary>
        private void OnLocationUpdated(LocationData location)
        {
            LocationUpdated?.Invoke(this, location);
        }

        /// <summary>
        /// Raises the StatusChanged event
        /// </summary>
        private void OnStatusChanged(string status)
        {
            Debug.WriteLine($"GPS Status: {status}");
            StatusChanged?.Invoke(this, status);
        }

        /// <summary>
        /// Raises the SatelliteInfoUpdated event
        /// </summary>
        private void OnSatelliteInfoUpdated()
        {
            SatelliteInfoUpdated?.Invoke(this, new SatelliteInfoEventArgs
            {
                SatellitesInView = _satellitesInView,
                SatellitesUsed = _satellitesUsed
            });
        }

        /// <summary>
        /// Disposes of resources
        /// </summary>
        public void Dispose()
        {
            Stop();
            CloseSerialPort();
            _cancellationTokenSource?.Dispose();
        }
    }
}