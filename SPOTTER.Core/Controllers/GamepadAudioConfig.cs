using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SPOTTER.Controllers
{
    /// <summary>
    /// Manages gamepad audio configuration loaded from CSV file
    /// </summary>
    public class GamepadAudioConfig
    {
        private Dictionary<string, string> _audioMappings;
        private Dictionary<string, string> _recordedDataMappings;
        private Dictionary<string, bool> _newRecordMappings;

        public GamepadAudioConfig()
        {
            _audioMappings = new Dictionary<string, string>();
            _recordedDataMappings = new Dictionary<string, string>();
            _newRecordMappings = new Dictionary<string, bool>();
        }

        /// <summary>
        /// Loads configuration from CSV file (Button,AudioText), trying multiple possible locations
        /// </summary>
        // RFC 4180-compliant CSV field splitter — handles quoted fields containing commas.
        private static string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    { current.Append('"'); i++; }   // escaped quote ""
                    else
                    { inQuotes = !inQuotes; }
                }
                else if (c == ',' && !inQuotes)
                { fields.Add(current.ToString()); current.Clear(); }
                else
                { current.Append(c); }
            }
            fields.Add(current.ToString());
            return fields.ToArray();
        }

        private void ParseLine(string line)
        {
            string[] parts = ParseCsvLine(line);
            if (parts.Length < 2) return;
            string button = parts[0].Trim();
            if (string.IsNullOrEmpty(button)) return;
            _audioMappings[button] = parts[1].Trim();
            if (parts.Length >= 3)
                _recordedDataMappings[button] = parts[2].Trim();
            if (parts.Length >= 4)
                _newRecordMappings[button] = parts[3].Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase);
        }

        public bool LoadFromFile(string fileName = "GamepadAudioConfig.csv")
        {
            _audioMappings.Clear();
            _recordedDataMappings.Clear();
            _newRecordMappings.Clear();

            string[] possiblePaths = new string[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName),
                Path.Combine(Directory.GetCurrentDirectory(), fileName),
                Path.Combine(Environment.CurrentDirectory, fileName),
                fileName,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\" + fileName)
            };

            System.Diagnostics.Debug.WriteLine("=== SEARCHING FOR AUDIO CONFIG FILE ===");
            string foundPath = null;

            foreach (string path in possiblePaths)
            {
                try
                {
                    string fullPath = Path.GetFullPath(path);
                    System.Diagnostics.Debug.WriteLine($"Checking: {fullPath}");

                    if (File.Exists(fullPath))
                    {
                        foundPath = fullPath;
                        System.Diagnostics.Debug.WriteLine($"✓ FOUND: {fullPath}");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"  Error checking path: {ex.Message}");
                }
            }

            if (foundPath == null)
            {
                System.Diagnostics.Debug.WriteLine("✗ CONFIG FILE NOT FOUND IN ANY LOCATION");
                return false;
            }

            try
            {
                string[] lines = File.ReadAllLines(foundPath);
                System.Diagnostics.Debug.WriteLine($"Reading {lines.Length} lines from config file");

                // Skip header line
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (!string.IsNullOrWhiteSpace(line))
                        ParseLine(line);
                }

                System.Diagnostics.Debug.WriteLine($"✓ Successfully loaded {_audioMappings.Count} audio configurations");
                System.Diagnostics.Debug.WriteLine("=== END AUDIO CONFIG SEARCH ===");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error reading config file: {ex.Message}");
                System.Diagnostics.Debug.WriteLine("=== END AUDIO CONFIG SEARCH ===");
                return false;
            }
        }

        /// <summary>
        /// Loads configuration from embedded resource
        /// </summary>
        public bool LoadFromEmbeddedResource(string resourceName = "GamepadAudioConfig.csv")
        {
            _audioMappings.Clear();
            _recordedDataMappings.Clear();
            _newRecordMappings.Clear();

            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();

                string fullResourceName = assembly.GetManifestResourceNames()
                    .FirstOrDefault(r => r.EndsWith(resourceName));

                if (fullResourceName == null)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Embedded resource '{resourceName}' not found");
                    return false;
                }

                using (Stream stream = assembly.GetManifestResourceStream(fullResourceName))
                using (StreamReader reader = new StreamReader(stream))
                {
                    string line;
                    bool firstLine = true;

                    while ((line = reader.ReadLine()) != null)
                    {
                        if (firstLine) { firstLine = false; continue; }
                        string trimmedLine = line.Trim();
                        if (!string.IsNullOrWhiteSpace(trimmedLine))
                            ParseLine(trimmedLine);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"✓ Loaded {_audioMappings.Count} audio configurations from embedded resource");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading embedded resource: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the audio text for a specific button
        /// </summary>
        public string GetAudioText(string button)
        {
            if (_audioMappings.TryGetValue(button, out string audioText))
                return audioText;

            System.Diagnostics.Debug.WriteLine($"Warning: No audio text found for button '{button}'");
            return null;
        }

        /// <summary>
        /// Gets the RecordedData value for a specific button (the text written to the session log).
        /// Returns null when the button has no RecordedData entry.
        /// </summary>
        public string GetRecordedData(string button)
        {
            if (_recordedDataMappings.TryGetValue(button, out string data))
                return data;

            System.Diagnostics.Debug.WriteLine($"Warning: No RecordedData found for button '{button}'");
            return null;
        }

        /// <summary>
        /// Returns true when pressing this button should start a new record (new line).
        /// Returns false when the data should be appended to the current record.
        /// </summary>
        public bool GetNewRecord(string button)
        {
            if (_newRecordMappings.TryGetValue(button, out bool newRecord))
                return newRecord;
            return false;
        }

        /// <summary>
        /// Checks if a button has an audio configuration
        /// </summary>
        public bool HasAudioText(string button)
        {
            return _audioMappings.ContainsKey(button);
        }

        /// <summary>
        /// Gets all button mappings
        /// </summary>
        public Dictionary<string, string> GetAllMappings()
        {
            return new Dictionary<string, string>(_audioMappings);
        }

        /// <summary>
        /// Gets diagnostic information about the loaded configuration
        /// </summary>
        public string GetDiagnosticInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine($"Total Mappings: {_audioMappings.Count}");
            return info.ToString();
        }
    }
}
