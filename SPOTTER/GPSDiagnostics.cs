using System;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;

namespace SPOTTER.Controllers
{
    /// <summary>
    /// Diagnostic tool to help identify GPS connection issues
    /// </summary>
    public static class GPSDiagnostics
    {
        /// <summary>
        /// Tests a specific COM port and returns detailed diagnostic information
        /// </summary>
        public static async Task<string> DiagnosePort(string portName, int baudRate = 4800)
        {
            StringBuilder diagnosis = new StringBuilder();
            diagnosis.AppendLine($"=== GPS Diagnostics for {portName} at {baudRate} baud ===");
            diagnosis.AppendLine($"Test started: {DateTime.Now}");
            diagnosis.AppendLine();

            // Check if port exists
            string[] availablePorts = SerialPort.GetPortNames();
            diagnosis.AppendLine($"Available COM ports: {string.Join(", ", availablePorts)}");
            
            if (!Array.Exists(availablePorts, p => p == portName))
            {
                diagnosis.AppendLine($"ERROR: {portName} is not available!");
                return diagnosis.ToString();
            }

            diagnosis.AppendLine($"{portName} is available. Attempting to open...");
            diagnosis.AppendLine();

            SerialPort testPort = null;
            try
            {
                testPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 2000,
                    WriteTimeout = 2000,
                    NewLine = "\r\n",
                    DtrEnable = true,
                    RtsEnable = true,
                    Handshake = Handshake.None
                };

                testPort.Open();
                diagnosis.AppendLine($"âœ“ Port opened successfully");
                diagnosis.AppendLine($"  - IsOpen: {testPort.IsOpen}");
                diagnosis.AppendLine($"  - BaudRate: {testPort.BaudRate}");
                diagnosis.AppendLine($"  - DataBits: {testPort.DataBits}");
                diagnosis.AppendLine($"  - StopBits: {testPort.StopBits}");
                diagnosis.AppendLine($"  - Parity: {testPort.Parity}");
                diagnosis.AppendLine($"  - DTR: {testPort.DtrEnable}");
                diagnosis.AppendLine($"  - RTS: {testPort.RtsEnable}");
                diagnosis.AppendLine();

                // Try reading raw bytes first
                diagnosis.AppendLine("Attempting to read raw data (5 seconds)...");
                DateTime startTime = DateTime.Now;
                int bytesRead = 0;
                StringBuilder rawData = new StringBuilder();

                while ((DateTime.Now - startTime).TotalSeconds < 5)
                {
                    try
                    {
                        if (testPort.BytesToRead > 0)
                        {
                            byte[] buffer = new byte[testPort.BytesToRead];
                            int count = testPort.Read(buffer, 0, buffer.Length);
                            bytesRead += count;
                            rawData.Append(Encoding.ASCII.GetString(buffer, 0, count));
                        }
                        await Task.Delay(100);
                    }
                    catch (TimeoutException)
                    {
                        // Continue
                    }
                }

                diagnosis.AppendLine($"âœ“ Read {bytesRead} bytes in 5 seconds");
                diagnosis.AppendLine();

                if (bytesRead > 0)
                {
                    diagnosis.AppendLine("Raw data received (first 500 chars):");
                    diagnosis.AppendLine("----------------------------------------");
                    diagnosis.AppendLine(rawData.ToString().Substring(0, Math.Min(500, rawData.Length)));
                    diagnosis.AppendLine("----------------------------------------");
                    diagnosis.AppendLine();

                    // Analyze the data
                    string[] lines = rawData.ToString().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    diagnosis.AppendLine($"Parsed {lines.Length} lines from raw data");
                    
                    int nmeaCount = 0;
                    foreach (string line in lines)
                    {
                        if (line.StartsWith("$"))
                        {
                            nmeaCount++;
                            if (nmeaCount <= 5)
                            {
                                diagnosis.AppendLine($"  NMEA: {line}");
                            }
                        }
                    }
                    
                    diagnosis.AppendLine($"Total NMEA sentences found: {nmeaCount}");
                    
                    if (nmeaCount == 0)
                    {
                        diagnosis.AppendLine("âš  WARNING: No NMEA sentences detected!");
                        diagnosis.AppendLine("This may not be a GPS device, or it's not configured for NMEA output.");
                    }
                    else
                    {
                        diagnosis.AppendLine("âœ“ NMEA sentences detected - GPS is transmitting!");
                    }
                }
                else
                {
                    diagnosis.AppendLine("âš  WARNING: No data received from port!");
                    diagnosis.AppendLine("Possible causes:");
                    diagnosis.AppendLine("  - Wrong baud rate");
                    diagnosis.AppendLine("  - GPS not powered on");
                    diagnosis.AppendLine("  - GPS in wrong mode");
                    diagnosis.AppendLine("  - Hardware/cable issue");
                }

                testPort.Close();
            }
            catch (UnauthorizedAccessException)
            {
                diagnosis.AppendLine($"âœ— ERROR: Access denied to {portName}");
                diagnosis.AppendLine("The port may be in use by another application.");
            }
            catch (Exception ex)
            {
                diagnosis.AppendLine($"âœ— ERROR: {ex.GetType().Name}: {ex.Message}");
                diagnosis.AppendLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                if (testPort != null && testPort.IsOpen)
                {
                    testPort.Close();
                }
                testPort?.Dispose();
            }

            diagnosis.AppendLine();
            diagnosis.AppendLine($"Test completed: {DateTime.Now}");
            return diagnosis.ToString();
        }

        /// <summary>
        /// Scans all COM ports and reports which ones have data
        /// </summary>
        public static async Task<string> ScanAllPorts()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("=== Scanning All COM Ports ===");
            report.AppendLine($"Scan started: {DateTime.Now}");
            report.AppendLine();

            string[] ports = SerialPort.GetPortNames();
            report.AppendLine($"Found {ports.Length} COM ports: {string.Join(", ", ports)}");
            report.AppendLine();

            int[] baudRates = { 4800, 9600, 19200, 38400, 57600, 115200 };

            foreach (string port in ports)
            {
                report.AppendLine($"--- Testing {port} ---");
                
                foreach (int baud in baudRates)
                {
                    SerialPort testPort = null;
                    try
                    {
                        testPort = new SerialPort(port, baud)
                        {
                            ReadTimeout = 500,
                            NewLine = "\r\n",
                            DtrEnable = true,
                            RtsEnable = true
                        };

                        testPort.Open();
                        await Task.Delay(200);

                        if (testPort.BytesToRead > 0)
                        {
                            byte[] buffer = new byte[Math.Min(testPort.BytesToRead, 100)];
                            int count = testPort.Read(buffer, 0, buffer.Length);
                            string sample = Encoding.ASCII.GetString(buffer, 0, count);
                            
                            report.AppendLine($"  {baud} baud: DATA FOUND!");
                            report.AppendLine($"    Sample: {sample.Substring(0, Math.Min(50, sample.Length)).Replace("\r", "\\r").Replace("\n", "\\n")}");
                            
                            if (sample.Contains("$GP") || sample.Contains("$GN"))
                            {
                                report.AppendLine($"    âœ“âœ“âœ“ NMEA GPS DATA DETECTED!");
                            }
                        }
                        
                        testPort.Close();
                    }
                    catch
                    {
                        // Port not available at this baud rate
                    }
                    finally
                    {
                        testPort?.Dispose();
                    }
                }
                
                report.AppendLine();
            }

            report.AppendLine($"Scan completed: {DateTime.Now}");
            return report.ToString();
        }
    }
}
