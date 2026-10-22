using SPOTTER.Models;
using System;
using System.Collections.Generic;
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

        // Failover state: ordered list of preferred ports and the index of the
        // one we most recently connected to (or tried). Advances by one every
        // time the watchdog closes a port, so the next scan starts with the
        // following entry (eg Bluetooth GPS after u-blox dropped).
        private readonly object _preferredPortsLock = new object();
        private List<string> _preferredPorts = new List<string>();
        private int _preferredPortIndex = 0;
        private DateTime _connectedAt = DateTime.MinValue;

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

        // Minimum number of valid NMEA sentences a port must emit within the
        // test window to be accepted. Was 2 — relaxed to 1 because Bluetooth
        // SPP channels can take 1–3 s to negotiate after Open() and may only
        // deliver one full sentence inside the 3 s test window. The read-loop
        // watchdog (see FailoverSeconds) will catch a false-positive port
        // that emitted a single $-prefixed burst then went silent, so lowering
        // the acceptance bar is safe.
        private const int MIN_VALID_NMEA_FOR_ACCEPT = 1;

        /// <summary>
        /// Legacy single-port accessor. Setting this REPLACES the preferred-ports list
        /// with a single entry; getting it returns the first entry of the list, or null
        /// when the list is empty. For multi-source configurations (eg u-blox + Bluetooth
        /// GPS failover on the FZ-G1) use <see cref="PreferredPorts"/> instead.
        /// </summary>
        public string PreferredPort
        {
            get
            {
                lock (_preferredPortsLock)
                    return _preferredPorts.Count > 0 ? _preferredPorts[0] : null;
            }
            set
            {
                lock (_preferredPortsLock)
                {
                    _preferredPorts.Clear();
                    if (!string.IsNullOrWhiteSpace(value))
                        _preferredPorts.Add(value.Trim());
                    _preferredPortIndex = 0;
                }
            }
        }

        /// <summary>
        /// Ordered list of ports to try before the full auto-scan. The first entry is
        /// the primary source; subsequent entries are failover targets, each tried in
        /// turn when the watchdog decides the current port has gone quiet. Case-insensitive
        /// duplicates and blank entries are stripped. Example FZ-G1 configuration:
        /// ["COM5", "COM10"] = u-blox first, then Bluetooth GPS pairing.
        /// </summary>
        public IReadOnlyList<string> PreferredPorts
        {
            get
            {
                lock (_preferredPortsLock)
                    return _preferredPorts.ToArray();
            }
            set
            {
                lock (_preferredPortsLock)
                {
                    _preferredPorts.Clear();
                    if (value != null)
                    {
                        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var p in value)
                        {
                            if (string.IsNullOrWhiteSpace(p)) continue;
                            string trimmed = p.Trim();
                            if (seen.Add(trimmed))
                                _preferredPorts.Add(trimmed);
                        }
                    }
                    _preferredPortIndex = 0;
                }
            }
        }

        /// <summary>
        /// Seconds of silence (no valid NMEA fix) before the controller closes the
        /// current port and tries the next <see cref="PreferredPorts"/> entry.
        /// Set to 0 or less to disable the watchdog (SPOTTER stays on the current
        /// port indefinitely). Default: 10 seconds.
        /// Note: actual trigger latency is this value plus the port read timeout
        /// (~2 s), because the watchdog is checked between reads.
        /// </summary>
        public int FailoverSeconds { get; set; } = 10;

        public GPSController()
        {
            CurrentLocation = new LocationData();

            // Diagnostic: Show available COM ports
            GpsLog.WriteLine("");
            GpsLog.WriteLine("=== COM PORT DIAGNOSTIC ===");
            string[] ports = SerialPort.GetPortNames();
            GpsLog.WriteLine($"Windows sees {ports.Length} COM port(s):");
            foreach (string port in ports)
            {
                GpsLog.WriteLine($"  - {port}");
            }
            GpsLog.WriteLine("=== END DIAGNOSTIC ===");
            GpsLog.WriteLine("");
        }

        /// <summary>
        /// Starts GPS controller - scans for GPS and begins reading
        /// </summary>
        public void Start()
        {
            if (_isRunning)
            {
                GpsLog.WriteLine("GPS Controller already running");
                return;
            }

            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();

            OnStatusChanged("Scanning for GPS...");

            GpsLog.WriteLine("=== GPS Controller Starting ===");

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
                GpsLog.WriteLine($"Error stopping GPS task: {ex.Message}");
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
                        GpsLog.WriteLine($"GPS scan cycle complete - no GPS found. Retrying in {SCAN_RETRY_DELAY_MS / 1000} seconds...");
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
                    GpsLog.WriteLine($"Error reading GPS data: {ex.Message}");
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
            GpsLog.WriteLine("=== Starting GPS Port Scan ===");

            // Common GPS baud rates used for testing
            int[] baudRates = { 4800, 9600, 19200, 38400, 115200 };

            // Snapshot the preferred-ports list and rotate it so that the entry we
            // most recently failed over FROM is tried last. On the first scan after
            // startup _preferredPortIndex is 0, so the list is tried in the user's
            // configured order. After each watchdog event the index advances by one,
            // which pushes the failed port to the back of the queue for the next scan.
            List<string> preferredSnapshot;
            int rotation;
            lock (_preferredPortsLock)
            {
                preferredSnapshot = new List<string>(_preferredPorts);
                rotation = preferredSnapshot.Count > 0
                    ? ((_preferredPortIndex % preferredSnapshot.Count) + preferredSnapshot.Count) % preferredSnapshot.Count
                    : 0;
            }

            var rotatedPreferred = new List<string>(preferredSnapshot.Count);
            for (int i = 0; i < preferredSnapshot.Count; i++)
                rotatedPreferred.Add(preferredSnapshot[(rotation + i) % preferredSnapshot.Count]);

            // Try each preferred port in rotated order, at each baud rate
            foreach (string preferred in rotatedPreferred)
            {
                GpsLog.WriteLine($"==> Trying preferred port: {preferred}");
                foreach (int baudRate in baudRates)
                {
                    if (cancellationToken.IsCancellationRequested) return false;
                    if (await TestPort(preferred, baudRate, cancellationToken))
                    {
                        // Record which configured entry we actually ended up on, so a
                        // later failover advances past THIS one (not past whichever
                        // entry happened to be at _preferredPortIndex at scan start).
                        lock (_preferredPortsLock)
                        {
                            int idx = _preferredPorts.FindIndex(
                                p => string.Equals(p, preferred, StringComparison.OrdinalIgnoreCase));
                            if (idx >= 0) _preferredPortIndex = idx;
                        }
                        OnStatusChanged($"GPS found on {preferred} at {baudRate} baud");
                        return true;
                    }
                }
                GpsLog.WriteLine($"    Preferred port {preferred} not available");
            }

            if (preferredSnapshot.Count > 0)
                GpsLog.WriteLine("    No preferred port responded — falling back to full scan");

            // Get available ports and clean them. Exclude everything already tried
            // above as a preferred entry (not just the single legacy PreferredPort).
            var preferredSet = new HashSet<string>(preferredSnapshot, StringComparer.OrdinalIgnoreCase);
            string[] availablePorts = SerialPort.GetPortNames()
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Where(p => !preferredSet.Contains(p))
                .OrderBy(p => p)
                .ToArray();

            GpsLog.WriteLine($"Windows reports {availablePorts.Length} COM port(s) available");

            if (availablePorts.Length == 0)
            {
                GpsLog.WriteLine("ERROR: No COM ports found!");
                return false;
            }

            GpsLog.WriteLine("Available COM ports:");
            foreach (string port in availablePorts)
            {
                GpsLog.WriteLine($"  - {port}");
            }

            // Try common GPS baud rates (already declared above)

            // Test each available port directly
            foreach (string portName in availablePorts)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    GpsLog.WriteLine("GPS scan cancelled by user");
                    return false;
                }

                GpsLog.WriteLine($"\n==> Testing port: {portName}");
                OnStatusChanged($"Testing {portName}...");

                // Try each baud rate for this port
                foreach (int baudRate in baudRates)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return false;

                    GpsLog.WriteLine($"    Trying {portName} at {baudRate} baud");

                    if (await TestPort(portName, baudRate, cancellationToken))
                    {
                        OnStatusChanged($"GPS found on {portName} at {baudRate} baud");
                        return true;
                    }
                }

                GpsLog.WriteLine($"    {portName} - No GPS found at any baud rate");
                OnStatusChanged($"No GPS on {portName}, trying next port...");
            }

            GpsLog.WriteLine("\nNo GPS found on any available COM port");
            GpsLog.WriteLine("All available ports were tested but none responded with valid NMEA data");
            return false;
        }

        /// <summary>
        /// Reads a single NMEA line from the port with a bounded timeout.
        ///
        /// IMPORTANT: On .NET Framework, SerialPort.BaseStream.ReadAsync does NOT
        /// honour either the port's ReadTimeout OR the CancellationToken — a
        /// well-known defect. A port that opens cleanly but never emits data
        /// (eg a Bluetooth INCOMING port with nothing connecting IN to the
        /// tablet) will wedge BaseStream.ReadAsync forever, no matter what
        /// timeout you set.
        ///
        /// The fix is to use the synchronous ReadLine() method, which DOES
        /// honour port.ReadTimeout and throws TimeoutException reliably.
        /// We run it on a background thread so TestPort stays async-friendly.
        /// Note: the CancellationToken will not interrupt a blocked ReadLine —
        /// the background thread stays blocked until ReadTimeout fires — but
        /// since ReadTimeout is bounded (typically 3 s), the scan can only be
        /// delayed by one port-timeout, not wedged indefinitely.
        /// </summary>
        private Task<string> ReadLineAsync(SerialPort port, CancellationToken cancellationToken, int timeoutMs)
        {
            // Make sure the SYNCHRONOUS ReadLine's own timeout matches what the
            // caller asked for, overriding whatever was set at port construction.
            port.ReadTimeout = timeoutMs;

            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                // ReadLine will throw TimeoutException when ReadTimeout elapses
                // with no complete line — exactly the signal TestPort needs to
                // give up on this port and move to the next.
                return port.ReadLine();
            }, cancellationToken);
        }

        /// <summary>
        /// Tests if a specific COM port has valid NMEA GPS data
        /// </summary>
        private async Task<bool> TestPort(string portName, int baudRate, CancellationToken cancellationToken)
        {
            SerialPort testPort = null;

            try
            {
                GpsLog.WriteLine($"        Attempting to open {portName}...");

                testPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = PORT_TEST_TIMEOUT_MS,
                    WriteTimeout = PORT_TEST_TIMEOUT_MS,
                    // Many Bluetooth GPS SPP channels only deliver data after
                    // DTR is asserted. Harmless for USB and the u-blox virtual
                    // COM, necessary for most Bluetooth modules.
                    DtrEnable = true,
                    RtsEnable = true
                };

                testPort.Open();
                GpsLog.WriteLine($"        âœ“ Port {portName} opened successfully");

                // Try to read several lines and look for valid NMEA
                DateTime startTime = DateTime.Now;
                int validNMEACount = 0;
                int readAttempts = 0;
                const int MAX_ATTEMPTS = 10;

                while ((DateTime.Now - startTime).TotalMilliseconds < PORT_TEST_TIMEOUT_MS &&
                       validNMEACount < MIN_VALID_NMEA_FOR_ACCEPT &&
                       readAttempts < MAX_ATTEMPTS)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        GpsLog.WriteLine($"        Cancelled while testing {portName}");
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
                            GpsLog.WriteLine($"        Read [{readAttempts}]: {preview}");
                        }

                        if (IsValidNMEASentence(line))
                        {
                            validNMEACount++;
                            GpsLog.WriteLine($"        âœ“ Valid NMEA sentence! Count: {validNMEACount}");
                        }
                    }
                    catch (TimeoutException)
                    {
                        GpsLog.WriteLine($"        Timeout reading from {portName}");
                        break;
                    }
                    catch (Exception ex)
                    {
                        GpsLog.WriteLine($"        Error reading: {ex.Message}");
                        break;
                    }
                }

                if (validNMEACount >= MIN_VALID_NMEA_FOR_ACCEPT)
                {
                    GpsLog.WriteLine($"        SUCCESS: valid GPS on {portName} at {baudRate} baud ({validNMEACount} NMEA sentence(s) seen)");

                    // Close test port
                    testPort.Close();
                    testPort.Dispose();
                    testPort = null;

                    // Create fresh connection for actual use. Keep DTR/RTS
                    // asserted here too — some Bluetooth stacks drop the SPP
                    // data channel if DTR is released even briefly.
                    _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
                    {
                        ReadTimeout = NMEA_READ_TIMEOUT_MS,
                        WriteTimeout = NMEA_READ_TIMEOUT_MS,
                        DtrEnable = true,
                        RtsEnable = true
                    };

                    _serialPort.Open();
                    _connectedPort = portName;

                    // Reset watchdog state so the failover timer starts from the
                    // moment this connection opened, not from an older session.
                    _connectedAt = DateTime.UtcNow;
                    _lastValidFix = DateTime.MinValue;

                    GpsLog.WriteLine($"        Fresh connection established to {portName}");
                    return true;
                }
                else
                {
                    GpsLog.WriteLine($"        Not enough valid NMEA sentences (got {validNMEACount}, need {MIN_VALID_NMEA_FOR_ACCEPT})");
                }
            }
            catch (UnauthorizedAccessException)
            {
                GpsLog.WriteLine($"        âœ— ACCESS DENIED to {portName} (port in use by another application)");
            }
            catch (IOException ex)
            {
                GpsLog.WriteLine($"        âœ— IO Error on {portName}: {ex.Message}");
            }
            catch (Exception ex)
            {
                GpsLog.WriteLine($"        âœ— Error testing {portName}: {ex.Message}");
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
        /// Reads and processes NMEA data from the connected serial port.
        /// Also runs the failover watchdog: if <see cref="FailoverSeconds"/> seconds
        /// pass with no valid fix (either because none has ever been received on this
        /// connection, or because the stream went quiet mid-session), the method
        /// advances the preferred-port index, closes the port, and returns. The
        /// outer <see cref="ScanAndConnectGPS"/> loop then reconnects, starting with
        /// the NEXT preferred port — eg Bluetooth GPS after the u-blox dropped.
        /// </summary>
        private async Task ReadNMEAData(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _serialPort != null && _serialPort.IsOpen)
            {
                // Watchdog check: if more than FailoverSeconds have elapsed without
                // a valid fix on this connection, give up on this port and let the
                // outer scan loop try the next preferred source.
                if (FailoverSeconds > 0 && ShouldFailover())
                {
                    string closingPort = _connectedPort;
                    GpsLog.WriteLine($"GPS watchdog: {FailoverSeconds}s with no valid fix on {closingPort} — failing over");
                    AdvancePreferredPortIndex();
                    OnStatusChanged($"GPS silent on {closingPort} — switching source");
                    CloseSerialPort();
                    return;
                }

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
                    // Normal timeout, continue reading (watchdog on next iteration)
                    continue;
                }
                catch (InvalidOperationException ex)
                {
                    GpsLog.WriteLine($"Serial port closed unexpectedly: {ex.Message}");
                    break;
                }
                catch (IOException ex)
                {
                    GpsLog.WriteLine($"IO error while reading from serial port: {ex.Message}");
                    break;
                }
                catch (Exception ex)
                {
                    GpsLog.WriteLine($"Unexpected error while reading NMEA data: {ex.Message}");
                    break;
                }
            }
        }

        /// <summary>
        /// True when the watchdog should trigger a failover on the current connection.
        /// Uses <see cref="_lastValidFix"/> when a fix has been seen on this connection;
        /// otherwise uses <see cref="_connectedAt"/>, so a port that opens cleanly but
        /// never emits a valid sentence is also abandoned after the grace period.
        /// </summary>
        private bool ShouldFailover()
        {
            if (_connectedAt == DateTime.MinValue) return false;
            DateTime referenceTime = (_lastValidFix > _connectedAt) ? _lastValidFix : _connectedAt;
            return (DateTime.UtcNow - referenceTime).TotalSeconds > FailoverSeconds;
        }

        /// <summary>
        /// Advances <see cref="_preferredPortIndex"/> so that the next port scan
        /// starts with the entry AFTER the one we just abandoned. No-op when there
        /// is less than two preferred ports configured (in a single-port setup, the
        /// index stays at 0 and the scan simply retries the same port).
        /// </summary>
        private void AdvancePreferredPortIndex()
        {
            lock (_preferredPortsLock)
            {
                if (_preferredPorts.Count > 1)
                    _preferredPortIndex = (_preferredPortIndex + 1) % _preferredPorts.Count;
            }
        }

        /// <summary>
        /// Validates whether a string looks like a GNSS NMEA 0183 sentence.
        /// Uses a STRUCTURAL check rather than a hardcoded prefix list so new
        /// talker IDs (future GNSS constellations) and legitimate variations
        /// are accepted without code changes. The downstream parser only acts
        /// on the three-letter sentence TYPE (GGA/RMC/GSA/GSV) so any valid
        /// GNSS talker produces usable data.
        ///
        /// Accepted format:  $&lt;2 upper-case alpha talker&gt;&lt;3 upper-case alpha type&gt;,...
        /// Accepted talkers: GP (GPS), GN (combined GNSS), GL (GLONASS),
        ///                   GA (Galileo), BD/GB (BeiDou), QZ (QZSS),
        ///                   GI (NavIC/IRNSS), plus any future AA–ZZ talker.
        /// Accepted types:   GGA, RMC, GSA, GSV (needed for position + sats),
        ///                   VTG, GLL, ZDA (common supporting sentences —
        ///                   accepting them lets a port qualify quickly even
        ///                   if the first full sentence is a VTG not a GGA).
        /// Checksum is NOT verified here: some real-world GPSs ship bad
        /// checksums under weak signal, and the downstream parsers tolerate
        /// malformed sub-fields anyway. Keeping validation permissive at this
        /// gate costs little — the read-loop watchdog removes a port that
        /// turns out to be junk within FailoverSeconds.
        /// </summary>
        private bool IsValidNMEASentence(string sentence)
        {
            if (string.IsNullOrWhiteSpace(sentence)) return false;

            sentence = sentence.Trim();

            // Minimum shape: "$XXYYY," = 7 characters, '$' then 2+3 letters, then comma.
            if (sentence.Length < 7 || sentence[0] != '$' || sentence[6] != ',')
                return false;

            // Talker: two upper-case letters
            if (!IsUpperAlpha(sentence[1]) || !IsUpperAlpha(sentence[2]))
                return false;

            // Sentence type: three upper-case letters
            if (!IsUpperAlpha(sentence[3]) || !IsUpperAlpha(sentence[4]) || !IsUpperAlpha(sentence[5]))
                return false;

            string type = sentence.Substring(3, 3);
            switch (type)
            {
                case "GGA":
                case "RMC":
                case "GSA":
                case "GSV":
                case "VTG":
                case "GLL":
                case "ZDA":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsUpperAlpha(char c)
        {
            return c >= 'A' && c <= 'Z';
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
                GpsLog.WriteLine($"Error parsing NMEA sentence: {ex.Message}");
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
                GpsLog.WriteLine($"Error parsing GGA: {ex.Message}");
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
                GpsLog.WriteLine($"Error parsing RMC: {ex.Message}");
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
                GpsLog.WriteLine($"Error parsing GSA: {ex.Message}");
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
                GpsLog.WriteLine($"Error parsing GSV: {ex.Message}");
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

            // Latitude: DDMM.mmmm  → decimal at index 4 → 2 degree digits
            // Longitude: DDDMM.mmmm → decimal at index 5 → 3 degree digits
            // Use decimal position, not string length — GpsGate and some receivers
            // emit extra decimal places which breaks a length-based heuristic.
            int degreeDigits = (decimalIndex == 4) ? 2 : 3;

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
                    GpsLog.WriteLine($"Error closing serial port: {ex.Message}");
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
            GpsLog.WriteLine($"GPS Status: {status}");
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