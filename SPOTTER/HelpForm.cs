using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SPOTTER
{
    /// <summary>
    /// Two-panel help viewer: topic tree on the left, scrollable content on the right.
    /// </summary>
    public class HelpForm : Form
    {
        private TreeView topicTree;
        private RichTextBox contentBox;
        private SplitContainer split;
        private Panel titleBar;
        private Label titleLabel;

        // Topic content keyed by TreeNode name
        private readonly Dictionary<string, Action<RichTextBox>> _topics =
            new Dictionary<string, Action<RichTextBox>>();

        public HelpForm()
        {
            BuildTopics();
            InitializeLayout();
            // Select the first node
            if (topicTree.Nodes.Count > 0)
                topicTree.SelectedNode = topicTree.Nodes[0];
        }

        // ----------------------------------------------------------------
        // Layout
        // ----------------------------------------------------------------

        private void InitializeLayout()
        {
            Text = "SPOTTER Help";
            ClientSize = new Size(1000, 700);
            MinimumSize = new Size(700, 500);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            Font = Theme.Body();
            BackColor = Theme.BackgroundPrimary;

            // Title bar
            titleBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Theme.AccentPrimary,
                Padding = new Padding(16, 0, 0, 0)
            };
            titleLabel = new Label
            {
                Text = "SPOTTER Help",
                Font = new Font("Segoe UI", 13F, FontStyle.Regular),
                ForeColor = Color.White,
                AutoSize = true,
                BackColor = Color.Transparent
            };
            titleBar.Controls.Add(titleLabel);
            titleLabel.Location = new Point(16, (48 - titleLabel.PreferredHeight) / 2);
            titleBar.Paint += (s, e) => { }; // ensure redraws

            // Split container
            split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterWidth = 4,
                BackColor = Theme.BackgroundTertiary
            };
            // SplitterDistance requires a valid width — set it once the form is shown.
            Load += (s, e) =>
            {
                split.Panel1MinSize = 160;
                split.Panel2MinSize = 300;
                split.SplitterDistance = 220;
            };

            // Left: topic tree
            topicTree = new TreeView
            {
                Dock = DockStyle.Fill,
                Font = Theme.Body(),
                BackColor = Theme.BackgroundTertiary,
                ForeColor = Theme.TextPrimary,
                BorderStyle = BorderStyle.None,
                ShowLines = true,
                ShowPlusMinus = true,
                FullRowSelect = true,
                ItemHeight = 24,
                Indent = 16,
                Padding = new Padding(4)
            };
            topicTree.AfterSelect += OnTopicSelected;

            // Right: content
            contentBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Theme.BackgroundPrimary,
                ForeColor = Theme.TextPrimary,
                Font = new Font("Segoe UI", 10F),
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = true,
                Padding = new Padding(16)
            };

            split.Panel1.Controls.Add(topicTree);
            split.Panel2.Controls.Add(contentBox);

            Controls.Add(split);
            Controls.Add(titleBar);

            PopulateTree();
        }

        private void PopulateTree()
        {
            topicTree.BeginUpdate();
            topicTree.Nodes.Clear();

            var sections = new[]
            {
                ("overview",        "Overview",             new[]
                {
                    ("what_is_spotter",     "What is SPOTTER?"),
                    ("screen_layout",       "Screen layout"),
                    ("getting_started",     "Getting started"),
                }),
                ("session",         "Starting a session",   new[]
                {
                    ("session_setup",       "Session setup"),
                    ("am_pm",              "AM / PM selection"),
                    ("observer",            "Observer"),
                    ("position",            "Observer position"),
                    ("weather",             "Weather conditions"),
                    ("start_stop",          "Starting and stopping"),
                }),
                ("controller",      "Game controller",      new[]
                {
                    ("controller_connect",  "Connecting the controller"),
                    ("controller_buttons",  "Button mapping"),
                    ("controller_config",   "Customising the configuration"),
                    ("audio_feedback",      "Audio feedback"),
                }),
                ("recording",       "Recording observations", new[]
                {
                    ("how_recorded",        "How observations are recorded"),
                    ("data_format",         "Data file format"),
                    ("output_files",        "Output file locations"),
                    ("session_log",         "On-screen session log"),
                }),
                ("gps",             "GPS",                  new[]
                {
                    ("gps_setup",           "GPS setup"),
                    ("gps_status",          "GPS status indicators"),
                    ("gps_accuracy",        "GPS accuracy"),
                }),
                ("menus",           "Menus",                new[]
                {
                    ("menu_file",           "File menu"),
                    ("menu_edit",           "Edit menu"),
                    ("menu_view",           "View menu"),
                    ("menu_help",           "Help menu"),
                }),
                ("troubleshooting", "Troubleshooting",      new[]
                {
                    ("ts_no_controller",    "Controller not detected"),
                    ("ts_no_gps",           "No GPS fix"),
                    ("ts_no_data",          "No data being recorded"),
                    ("ts_audio",            "Audio not working"),
                }),
            };

            foreach (var (sectionKey, sectionTitle, children) in sections)
            {
                var parent = new TreeNode(sectionTitle) { Name = sectionKey };
                foreach (var (key, label) in children)
                {
                    parent.Nodes.Add(new TreeNode(label) { Name = key });
                }
                topicTree.Nodes.Add(parent);
            }

            topicTree.ExpandAll();
            topicTree.EndUpdate();
        }

        // ----------------------------------------------------------------
        // Topic selection
        // ----------------------------------------------------------------

        private void OnTopicSelected(object sender, TreeViewEventArgs e)
        {
            string key = e.Node.Name;
            if (_topics.TryGetValue(key, out var writer))
            {
                contentBox.Clear();
                writer(contentBox);
                contentBox.SelectionStart = 0;
                contentBox.ScrollToCaret();
                titleLabel.Text = "SPOTTER Help  —  " + e.Node.Text;
            }
            else
            {
                // Parent node: show section index
                contentBox.Clear();
                Heading(contentBox, e.Node.Text);
                Para(contentBox, "Select a topic from the list on the left.");
                foreach (TreeNode child in e.Node.Nodes)
                    Para(contentBox, "•  " + child.Text);
            }
        }

        // ----------------------------------------------------------------
        // RTF helpers
        // ----------------------------------------------------------------

        private static void Heading(RichTextBox rtb, string text)
        {
            rtb.SelectionFont = new Font("Segoe UI", 15F, FontStyle.Regular);
            rtb.SelectionColor = Theme.AccentPrimary;
            rtb.AppendText(text + "\n");
            rtb.SelectionFont = new Font("Segoe UI", 10F);
            rtb.SelectionColor = Theme.BorderSecondary;
            rtb.AppendText(new string('―', 60) + "\n\n");
        }

        private static void Subheading(RichTextBox rtb, string text)
        {
            rtb.SelectionFont = new Font("Segoe UI", 11F, FontStyle.Bold);
            rtb.SelectionColor = Theme.TextPrimary;
            rtb.AppendText(text + "\n");
            rtb.SelectionFont = new Font("Segoe UI", 10F);
            rtb.SelectionColor = Theme.TextPrimary;
        }

        private static void Para(RichTextBox rtb, string text)
        {
            rtb.SelectionFont = new Font("Segoe UI", 10F, FontStyle.Regular);
            rtb.SelectionColor = Theme.TextPrimary;
            rtb.AppendText(text + "\n\n");
        }

        private static void BulletList(RichTextBox rtb, params string[] items)
        {
            rtb.SelectionFont = new Font("Segoe UI", 10F, FontStyle.Regular);
            rtb.SelectionColor = Theme.TextPrimary;
            foreach (var item in items)
                rtb.AppendText("  •  " + item + "\n");
            rtb.AppendText("\n");
        }

        private static void KeyValue(RichTextBox rtb, string key, string value)
        {
            rtb.SelectionFont = new Font("Segoe UI", 10F, FontStyle.Bold);
            rtb.SelectionColor = Theme.TextPrimary;
            rtb.AppendText(key + ":  ");
            rtb.SelectionFont = new Font("Segoe UI", 10F, FontStyle.Regular);
            rtb.SelectionColor = Theme.TextSecondary;
            rtb.AppendText(value + "\n");
        }

        private static void Note(RichTextBox rtb, string text)
        {
            rtb.SelectionFont = new Font("Segoe UI", 9.5F, FontStyle.Italic);
            rtb.SelectionColor = Theme.AccentDeep;
            rtb.AppendText("ⓘ  " + text + "\n\n");
        }

        private static void Warning(RichTextBox rtb, string text)
        {
            rtb.SelectionFont = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            rtb.SelectionColor = Theme.WarnPrimary;
            rtb.AppendText("⚠  " + text + "\n\n");
        }

        // ----------------------------------------------------------------
        // Topic content
        // ----------------------------------------------------------------

        private void BuildTopics()
        {
            // ---- Overview ----

            _topics["what_is_spotter"] = rtb =>
            {
                Heading(rtb, "What is SPOTTER?");
                Para(rtb,
                    "SPOTTER (Survey Program for Observing, Tracking, Tagging and Event " +
                    "Recording) is a Windows application built for NSW Department of Primary " +
                    "Industries and Regional Development (DPIRD) aerial wildlife survey teams.");
                Para(rtb,
                    "During an aerial survey, an observer sits in an aircraft and presses " +
                    "buttons on an Xbox-compatible game controller whenever they sight an animal. " +
                    "SPOTTER records each sighting with a precise GPS timestamp and location, " +
                    "building a timestamped CSV data file that can be analysed after the flight.");
                Subheading(rtb, "Key capabilities");
                BulletList(rtb,
                    "Records species, count, and distance band per observation with a single button press",
                    "Stamps every observation with UTC time, latitude, longitude, bearing, altitude, and HDOP",
                    "Provides spoken audio confirmation of each button press so the observer does not need to look at the screen",
                    "Writes a separate GPS track log every second during the session",
                    "Supports multiple observer positions and configurable button mappings",
                    "Runs entirely offline — no internet connection required");
            };

            _topics["screen_layout"] = rtb =>
            {
                Heading(rtb, "Screen layout");
                Para(rtb, "The main window is divided into three vertical panels:");

                Subheading(rtb, "Left rail — Session setup");
                Para(rtb,
                    "Contains all the controls you fill in before starting a session: " +
                    "time of day (AM/PM), observer name, observer position in the aircraft, " +
                    "cloud cover, temperature, wind speed, and the Start/Stop button.");

                Subheading(rtb, "Centre panel — Last observation");
                Para(rtb,
                    "Shows a live summary of the most recent observation: the species seen, " +
                    "the count, and the distance band. A freshness indicator fades when no " +
                    "new observation has been received for a while. Below the summary, the " +
                    "session log displays every recorded entry in chronological order.");

                Subheading(rtb, "Right rail — Status");
                Para(rtb,
                    "Shows real-time GPS status (fix quality, latitude, longitude, accuracy, " +
                    "satellite count), controller connection status, and file recording status.");

                Subheading(rtb, "Header bar");
                Para(rtb, "Displays the SPOTTER logo and the current time of day (updates every second).");

                Subheading(rtb, "Status strip (bottom)");
                Para(rtb,
                    "Shows GPS fix quality, coordinates, horizontal accuracy, satellite count, " +
                    "and the current time at a glance.");
            };

            _topics["getting_started"] = rtb =>
            {
                Heading(rtb, "Getting started");
                Subheading(rtb, "Before the flight");
                BulletList(rtb,
                    "Connect your Xbox-compatible controller via USB or Bluetooth",
                    "Connect the GPS receiver (the app auto-detects serial COM ports)",
                    "Launch SPOTTER — the controller and GPS status will appear in the right rail",
                    "Confirm the controller shows \"Connected\" in the CONTROLLER card",
                    "Wait for the GPS card to show a valid fix (green dot)");

                Subheading(rtb, "On the transect");
                BulletList(rtb,
                    "Fill in all session fields in the left panel (AM/PM, observer, position, weather)",
                    "Press Start — data recording begins immediately",
                    "When you sight an animal, press the species trigger on the controller",
                    "Follow up with count (A/B/X/Y) and distance (D-pad or left thumbstick) buttons",
                    "Press Start again (the controller Start button) to mark a transect boundary",
                    "Press the app Stop button when the session is finished");

                Subheading(rtb, "After the flight");
                Para(rtb,
                    "Data files are written to Desktop\\AerialSurveyLogger\\Data\\ as .dat files. " +
                    "GPS track logs are in Desktop\\AerialSurveyLogger\\TrackLogs\\ as .log files. " +
                    "Both are plain-text CSV format and can be opened in Excel or any text editor.");
                Note(rtb, "A new file is created each time you press Start, so multiple sessions in one day produce separate files.");
            };

            // ---- Session ----

            _topics["session_setup"] = rtb =>
            {
                Heading(rtb, "Session setup");
                Para(rtb,
                    "All fields in the SESSION card must be completed before you can press Start. " +
                    "The Start button will validate the inputs and show an error message if anything is missing.");
                KeyValue(rtb, "AM / PM", "Indicates the morning or afternoon survey block.");
                KeyValue(rtb, "Observer", "Your name selected from the observer list (or typed directly).");
                KeyValue(rtb, "Position", "Your seat in the aircraft: Left Front, Left Rear, Right Front, or Right Rear.");
                KeyValue(rtb, "Cloud cover", "Oktas (0–8) selected from the dropdown.");
                KeyValue(rtb, "Temperature", "Air temperature in degrees Celsius.");
                KeyValue(rtb, "Wind", "Wind speed in knots.");
                Para(rtb, "Weather values are stamped on every observation row in the data file.");
            };

            _topics["am_pm"] = rtb =>
            {
                Heading(rtb, "AM / PM selection");
                Para(rtb,
                    "Two buttons labelled AM and PM appear in the SESSION card. Only one can be " +
                    "active at a time. Click a button to select it — it will appear pressed/recessed. " +
                    "Click again to deselect if you need to change your choice.");
                Note(rtb,
                    "You must select either AM or PM before the session can start. " +
                    "If neither is selected, the validation check will prompt you.");
            };

            _topics["observer"] = rtb =>
            {
                Heading(rtb, "Observer");
                Para(rtb,
                    "The Observer field is a type-in combo box. You can either select an existing " +
                    "name from the dropdown list or type a new name directly.");

                Subheading(rtb, "Editing the observer list");
                Para(rtb,
                    "Go to Edit > Edit Observer List to open an in-app editor. Add or remove names, " +
                    "then click Save. The list is stored in observers.txt next to the application and " +
                    "is loaded automatically on startup.");
                Note(rtb,
                    "Names are saved when the application closes, so any name you type during a " +
                    "session will be remembered next time.");
            };

            _topics["position"] = rtb =>
            {
                Heading(rtb, "Observer position");
                Para(rtb,
                    "Four radio buttons identify where you are sitting in the aircraft. The position " +
                    "is recorded in every observation row so that, when multiple observers are flying " +
                    "simultaneously on different channels, their data can be merged and matched " +
                    "to the correct side of the transect.");
                BulletList(rtb,
                    "Left Front (LF) — left seat, forward of the wing",
                    "Left Rear (LR)  — left seat, behind the wing",
                    "Right Front (RF) — right seat, forward of the wing",
                    "Right Rear (RR)  — right seat, behind the wing");
            };

            _topics["weather"] = rtb =>
            {
                Heading(rtb, "Weather conditions");
                Para(rtb,
                    "Cloud cover, temperature, and wind speed are recorded with every observation. " +
                    "They do not need to be updated during a session — set them once before pressing Start.");
                KeyValue(rtb, "Cloud cover", "Oktas (0 = clear sky, 8 = completely overcast). Select from the dropdown.");
                KeyValue(rtb, "Temperature", "Use the up/down arrows or type a value. Range: −50 to +60 °C.");
                KeyValue(rtb, "Wind speed",  "Use the up/down arrows or type a value in knots.");
            };

            _topics["start_stop"] = rtb =>
            {
                Heading(rtb, "Starting and stopping a session");
                Subheading(rtb, "Starting");
                Para(rtb,
                    "Click the Start button at the bottom of the left panel. SPOTTER will validate " +
                    "all session fields, check that a controller is connected, and then create a new " +
                    "data file on disk. The button changes to Stop and the session controls are locked " +
                    "to prevent accidental changes mid-flight.");
                Warning(rtb, "A controller must be connected before you can start a session.");

                Subheading(rtb, "Stopping");
                Para(rtb,
                    "Click the Stop button (same button, relabelled). A confirmation dialog will ask " +
                    "you to confirm. Once confirmed, recording stops, the session controls are " +
                    "unlocked, and all data is safely on disk.");

                Subheading(rtb, "Restarting in the same session");
                Para(rtb,
                    "You can press Start again immediately after stopping. A completely new data file " +
                    "is created with the current timestamp — previous data is never overwritten.");

                Subheading(rtb, "Transect markers (controller Start button)");
                Para(rtb,
                    "During a session, pressing the Start button on the controller (not the app button) " +
                    "writes a start_transect or end_transect marker into the data file. Use this to " +
                    "flag transect boundaries without stopping the session.");
            };

            // ---- Controller ----

            _topics["controller_connect"] = rtb =>
            {
                Heading(rtb, "Connecting the controller");
                Para(rtb,
                    "SPOTTER supports any XInput-compatible controller, including all Xbox One and " +
                    "Xbox Series controllers connected via USB or the Xbox Wireless Adapter.");

                Subheading(rtb, "USB connection (recommended for surveys)");
                BulletList(rtb,
                    "Plug the controller into any USB port before launching SPOTTER",
                    "The CONTROLLER card in the right rail will show a green dot and \"Connected\"",
                    "If you plug in after launch, the status updates automatically within a second");

                Subheading(rtb, "Wireless connection");
                BulletList(rtb,
                    "Pair the controller to the Xbox Wireless Adapter",
                    "Ensure the adapter is plugged in before launching SPOTTER",
                    "Battery level is not shown — use USB or fresh batteries for surveys");

                Warning(rtb, "If the controller disconnects mid-session, observations cannot be recorded. " +
                    "Reconnect the controller and verify the green dot appears before continuing.");
            };

            _topics["controller_buttons"] = rtb =>
            {
                Heading(rtb, "Button mapping (default)");
                Para(rtb,
                    "The default mapping is defined in GamepadAudioConfig.csv. All mappings can be " +
                    "customised — see Customising the configuration.");

                Subheading(rtb, "Species / observation triggers");
                KeyValue(rtb, "Left Trigger",   "Grey kangaroo — records a full observation row");
                KeyValue(rtb, "Right Trigger",  "Red kangaroo — records a full observation row");
                KeyValue(rtb, "Left Shoulder",  "Goat — records a full observation row");

                Subheading(rtb, "Count");
                KeyValue(rtb, "A button", "Count: 1");
                KeyValue(rtb, "B button", "Count: 2");
                KeyValue(rtb, "Y button", "Count: 3");
                KeyValue(rtb, "X button", "Count: 4");

                Subheading(rtb, "Distance band");
                KeyValue(rtb, "D-pad Down / Left Thumb Down",   "Yellow: 0–50 m");
                KeyValue(rtb, "D-pad Left / Left Thumb Left",   "Green: 50–100 m");
                KeyValue(rtb, "D-pad Up   / Left Thumb Up",     "Blue: 100–200 m");
                KeyValue(rtb, "D-pad Right / Left Thumb Right", "Black: 200–300 m");

                Subheading(rtb, "Session control");
                KeyValue(rtb, "Start button",        "Toggle transect start / end marker");
                KeyValue(rtb, "Right Shoulder",      "Glare (special event)");
                KeyValue(rtb, "Back button",         "Delete last record");
                KeyValue(rtb, "Right Thumbstick Down", "Count: 10");

                Subheading(rtb, "Recording order");
                Para(rtb,
                    "Press the species trigger first — this stamps the GPS location and time. " +
                    "Then press count and distance buttons. These are appended to the same row in the data file. " +
                    "A typical sequence is: trigger (species) → A (count 1) → D-pad Down (0–50 m).");
            };

            _topics["controller_config"] = rtb =>
            {
                Heading(rtb, "Customising the controller configuration");
                Para(rtb,
                    "Go to Edit > Edit Controller configuration. This opens the file " +
                    "GamepadAudioConfig.csv in your default CSV editor (e.g. Notepad or Excel).");
                Warning(rtb, "Changes to GamepadAudioConfig.csv take effect only after restarting SPOTTER.");

                Subheading(rtb, "CSV columns");
                KeyValue(rtb, "Button",       "The controller input identifier (e.g. A, LeftTrigger, DPadUp).");
                KeyValue(rtb, "AudioText",    "The phrase spoken aloud when the button is pressed.");
                KeyValue(rtb, "RecordedData", "The text written into the data file for this button press.");

                Subheading(rtb, "Valid button identifiers");
                BulletList(rtb,
                    "A, B, X, Y",
                    "LeftShoulder, RightShoulder",
                    "LeftTrigger, RightTrigger",
                    "DPadUp, DPadDown, DPadLeft, DPadRight",
                    "LeftThumbUp, LeftThumbDown, LeftThumbLeft, LeftThumbRight",
                    "RightThumbUp, RightThumbDown, RightThumbLeft, RightThumbRight",
                    "Start_On, Start_Off, Back");

                Note(rtb, "RecordedData values for distance bands should use the compact format: " +
                    "(yellow) 0-50, (green) 50-100, (blue) 100-200, (black) 200-300. " +
                    "These are recognised by the LAST OBSERVATION display.");
            };

            _topics["audio_feedback"] = rtb =>
            {
                Heading(rtb, "Audio feedback");
                Para(rtb,
                    "Every button press triggers spoken audio so you do not need to look at the " +
                    "screen while scanning for animals. Audio is provided by the Windows " +
                    "Text-to-Speech engine (no internet required).");
                Para(rtb,
                    "The spoken phrase comes from the AudioText column of GamepadAudioConfig.csv. " +
                    "You can change any phrase by editing that column — for example, changing " +
                    "\"grey kangaroo\" to \"grey roo\" for brevity.");
                Para(rtb,
                    "If a button has no AudioText entry, SPOTTER falls back to speaking the " +
                    "button identifier name (e.g. \"LeftTrigger\").");
                Note(rtb,
                    "Audio from different button presses can overlap. If you press two buttons " +
                    "rapidly, the second sound interrupts the first. The speed of the speech " +
                    "synthesiser is set to Rate 3 (faster than normal) for survey conditions.");
            };

            // ---- Recording ----

            _topics["how_recorded"] = rtb =>
            {
                Heading(rtb, "How observations are recorded");
                Para(rtb,
                    "SPOTTER distinguishes two types of button press:");

                Subheading(rtb, "Observation buttons (triggers and Left Shoulder)");
                Para(rtb,
                    "Pressing a species trigger (Left Trigger, Right Trigger, Left Shoulder) " +
                    "writes a new complete row to the data file. The row includes the current " +
                    "time, GPS location, bearing, altitude, accuracy, weather, and the species name.");

                Subheading(rtb, "Modifier buttons (count, distance, etc.)");
                Para(rtb,
                    "All other buttons (A/B/X/Y, D-pad, thumbstick, Right Shoulder, Back) append " +
                    "a comma-separated field to the current row. They do not start a new line.");

                Subheading(rtb, "Recommended workflow");
                BulletList(rtb,
                    "1. Spot animal",
                    "2. Press species trigger immediately (stamps GPS location)",
                    "3. Press count button (A = 1, B = 2, Y = 3, X = 4)",
                    "4. Press distance button (D-pad or left thumbstick)");

                Warning(rtb,
                    "Data is only written to disk when a session is active (after pressing Start). " +
                    "Button presses before Start are ignored.");
            };

            _topics["data_format"] = rtb =>
            {
                Heading(rtb, "Data file format");
                Para(rtb, "Each data file is a plain-text CSV with the following structure:");

                Subheading(rtb, "Header line (first line)");
                Para(rtb, "  Wednesday 21 05 2026 10_30_00 AM_AM_JD_LeftFront");

                Subheading(rtb, "Observation rows");
                Para(rtb, "  10:30:15 AM,AM,JD,LeftFront,638123456789000,−33.8688,151.2093,045.0,300,1.2,1.8,3,22,8,grey_kangaroo,1,(yellow) 0-50");

                Subheading(rtb, "Column definitions");
                KeyValue(rtb, "1",  "Time (local, long format)");
                KeyValue(rtb, "2",  "Session (AM or PM)");
                KeyValue(rtb, "3",  "Observer initials");
                KeyValue(rtb, "4",  "Observer position");
                KeyValue(rtb, "5",  "UTC ticks (DateTime.UtcNow.Ticks)");
                KeyValue(rtb, "6",  "Latitude (decimal degrees)");
                KeyValue(rtb, "7",  "Longitude (decimal degrees)");
                KeyValue(rtb, "8",  "Bearing / course (degrees)");
                KeyValue(rtb, "9",  "Altitude (metres)");
                KeyValue(rtb, "10", "HDOP (horizontal dilution of precision)");
                KeyValue(rtb, "11", "VDOP (vertical dilution of precision)");
                KeyValue(rtb, "12", "Cloud cover (oktas)");
                KeyValue(rtb, "13", "Temperature (°C)");
                KeyValue(rtb, "14", "Wind speed (knots)");
                KeyValue(rtb, "15+", "Observation fields: species, count, distance — one field per button press");

                Subheading(rtb, "Track log rows");
                Para(rtb, "  10:30:15 AM,638123456789000,−33.8688,151.2093,045.0");
                Para(rtb, "Track logs contain: local time, UTC ticks, latitude, longitude, bearing.");
            };

            _topics["output_files"] = rtb =>
            {
                Heading(rtb, "Output file locations");
                Para(rtb, "All files are written to your Windows Desktop under the folder AerialSurveyLogger:");
                BulletList(rtb,
                    "Desktop\\AerialSurveyLogger\\Data\\        — observation data (.dat files)",
                    "Desktop\\AerialSurveyLogger\\TrackLogs\\   — GPS track logs (.log files)");

                Subheading(rtb, "File naming");
                Para(rtb,
                    "Files are named with the session timestamp, AM/PM, observer initials, and position. " +
                    "Example:  2026-05-21_10-30-00-412_AM_JD_LeftFront.dat");
                Para(rtb,
                    "Because the timestamp includes milliseconds, each session always produces a unique file name, " +
                    "even if you start and stop multiple times on the same day.");

                Subheading(rtb, "Data safety");
                Para(rtb,
                    "Every button press is written to disk immediately. If the application crashes " +
                    "between presses, at most one button press can be lost. The files remain readable.");
            };

            _topics["session_log"] = rtb =>
            {
                Heading(rtb, "On-screen session log");
                Para(rtb,
                    "The large text area in the centre panel mirrors everything written to the data file " +
                    "during the current session. It scrolls automatically so the most recent entry is " +
                    "always visible.");
                Para(rtb,
                    "The log is cleared each time you press Start, so it always reflects the current " +
                    "session only. The full history is always in the data file on disk.");
                Note(rtb,
                    "The log uses a fixed-width (monospace) font so that columns align for easy reading " +
                    "in the field.");
            };

            // ---- GPS ----

            _topics["gps_setup"] = rtb =>
            {
                Heading(rtb, "GPS setup");
                Para(rtb,
                    "SPOTTER automatically scans all available serial COM ports for a NMEA GPS receiver " +
                    "on startup. No manual port selection is required.");
                Subheading(rtb, "Compatible receivers");
                Para(rtb,
                    "Any NMEA 0183 GPS receiver that provides GGA and VTG sentences should work. " +
                    "Common examples: u-blox modules, Garmin GPS 18x, GlobalSat BU-353.");
                Subheading(rtb, "Baud rate");
                Para(rtb, "The receiver should be set to 4800, 9600, or 38400 baud. SPOTTER tries common rates automatically.");
                Note(rtb, "If GPS is not detected, use View > Test GPS to open the NMEA debug monitor, which shows raw sentences received on each port.");
            };

            _topics["gps_status"] = rtb =>
            {
                Heading(rtb, "GPS status indicators");
                Para(rtb, "GPS status appears in three places simultaneously:");

                Subheading(rtb, "GPS card (right rail)");
                BulletList(rtb,
                    "Green dot + \"Good fix\" — valid position, use with confidence",
                    "Red dot + \"No fix\" — receiver connected but no satellite lock yet",
                    "Grey dot + \"Not connected\" — no GPS receiver detected");
                Para(rtb,
                    "The card also shows latitude, longitude, HDOP accuracy, and a satellite bar graph " +
                    "with one bar per tracked satellite.");

                Subheading(rtb, "Status strip (bottom of window)");
                Para(rtb,
                    "Shows GPS fix quality, coordinates, horizontal accuracy (± metres), and satellite " +
                    "count at a glance without needing to look at the right rail.");

                Subheading(rtb, "Freshness warning");
                Para(rtb,
                    "If GPS position has not updated for more than 30 seconds during a session, " +
                    "SPOTTER shows a warning in the status bar. Observations are still recorded " +
                    "but the GPS fields in the data file will contain stale coordinates.");
            };

            _topics["gps_accuracy"] = rtb =>
            {
                Heading(rtb, "GPS accuracy");
                Para(rtb,
                    "The GPS card shows HDOP (Horizontal Dilution of Precision). HDOP is a unitless " +
                    "multiplier — a lower value means better accuracy.");
                KeyValue(rtb, "HDOP < 1.0",  "Ideal — sub-metre accuracy likely");
                KeyValue(rtb, "HDOP 1–2",    "Excellent — suitable for all survey work");
                KeyValue(rtb, "HDOP 2–5",    "Good — positional error typically < 10 m");
                KeyValue(rtb, "HDOP > 5",    "Poor — consider waiting for more satellites");
                Para(rtb,
                    "VDOP (vertical) is also recorded in the data file. Altitude accuracy is generally " +
                    "worse than horizontal accuracy and is less critical for ground-based survey analysis.");
                Note(rtb,
                    "In a fast-moving aircraft, a one-second delay between pressing a button and the GPS " +
                    "fix being stamped equates to roughly 30–70 m of positional uncertainty at typical " +
                    "survey airspeeds. Press the species button the instant you sight the animal.");
            };

            // ---- Menus ----

            _topics["menu_file"] = rtb =>
            {
                Heading(rtb, "File menu");
                KeyValue(rtb, "Quit", "Closes the application. If a session is active, stop it first using the Stop button.");
            };

            _topics["menu_edit"] = rtb =>
            {
                Heading(rtb, "Edit menu");
                KeyValue(rtb, "Edit Observer List",
                    "Opens an in-app editor where you can add or remove observer names from the dropdown list. " +
                    "Changes are saved immediately.");
                KeyValue(rtb, "Edit Controller configuration",
                    "Opens GamepadAudioConfig.csv in your default application (e.g. Notepad or Excel). " +
                    "After saving your changes, restart SPOTTER for them to take effect.");
                Warning(rtb, "Controller configuration changes require an application restart.");
            };

            _topics["menu_view"] = rtb =>
            {
                Heading(rtb, "View menu");
                KeyValue(rtb, "Show Map",
                    "Opens a floating map window that plots your GPS position in real time. " +
                    "Requires an active GPS fix.");
                KeyValue(rtb, "Test GPS",
                    "Opens the GPS Debug Monitor, which shows raw NMEA sentences and parsed " +
                    "values. Useful for diagnosing GPS connection issues.");
            };

            _topics["menu_help"] = rtb =>
            {
                Heading(rtb, "Help menu");
                KeyValue(rtb, "Practice",
                    "Opens a practice window where you can test controller button presses without " +
                    "recording data. Use this to familiarise yourself with the button layout " +
                    "before a survey.");
                KeyValue(rtb, "Help", "Opens this help window.");
                KeyValue(rtb, "About", "Shows version, copyright, and acknowledgement information.");
            };

            // ---- Troubleshooting ----

            _topics["ts_no_controller"] = rtb =>
            {
                Heading(rtb, "Troubleshooting: controller not detected");
                Para(rtb,
                    "The CONTROLLER card shows a red dot and \"Disconnected\", or a warning dialog " +
                    "appeared on startup.");
                Subheading(rtb, "Checks");
                BulletList(rtb,
                    "Ensure the controller is plugged into a USB port (or paired to the wireless adapter)",
                    "Try pressing the Xbox button on the controller to wake it up",
                    "Disconnect and reconnect the USB cable",
                    "Check Device Manager to confirm the controller is recognised by Windows",
                    "Try a different USB port",
                    "Confirm the controller works in another application (e.g. Windows Game Controllers in Control Panel)");
                Note(rtb,
                    "SPOTTER polls for controller connection every 50 milliseconds. Once the controller " +
                    "is recognised by Windows, the status will update automatically within one second.");
            };

            _topics["ts_no_gps"] = rtb =>
            {
                Heading(rtb, "Troubleshooting: no GPS fix");
                Para(rtb, "The GPS card shows a red or grey dot.");
                Subheading(rtb, "Grey dot — not connected");
                BulletList(rtb,
                    "Check that the GPS receiver is plugged in and powered on",
                    "Open View > Test GPS to see which ports are being scanned",
                    "Try unplugging and replugging the receiver",
                    "Confirm the receiver appears in Device Manager as a COM port");
                Subheading(rtb, "Red dot — connected but no fix");
                BulletList(rtb,
                    "Move to an area with clear sky view (outdoors, away from buildings)",
                    "Allow 1–3 minutes for cold-start satellite acquisition",
                    "Check the satellite bar graph — at least 4 satellites are needed for a 3D fix",
                    "Verify the receiver antenna is unobstructed");
                Note(rtb,
                    "Inside a building or hangar you will not get a fix. Walk the receiver outside to " +
                    "acquire satellites, then bring it back to the aircraft.");
            };

            _topics["ts_no_data"] = rtb =>
            {
                Heading(rtb, "Troubleshooting: no data being recorded");
                BulletList(rtb,
                    "Confirm the session is active — the Start button should read \"Stop\"",
                    "Check that the controller is connected (green dot in right rail)",
                    "Look for the data file in Desktop\\AerialSurveyLogger\\Data\\",
                    "Open the file in Notepad and confirm the header line is present",
                    "Press a button and check that the on-screen session log updates",
                    "Confirm there is sufficient disk space on the desktop drive");
                Note(rtb,
                    "Button presses before pressing Start are silently discarded. If you pressed " +
                    "buttons and the log is empty, the session was not active.");
            };

            _topics["ts_audio"] = rtb =>
            {
                Heading(rtb, "Troubleshooting: audio not working");
                BulletList(rtb,
                    "Check Windows volume is not muted",
                    "Confirm the correct audio output device is selected in Windows Sound settings",
                    "Verify the headset or speaker is connected and powered on",
                    "Check that the AudioText column in GamepadAudioConfig.csv is not empty for the buttons you are pressing",
                    "Restart SPOTTER after making changes to GamepadAudioConfig.csv",
                    "Open Control Panel > Speech Recognition > Text-to-Speech and test the voice");
                Note(rtb,
                    "SPOTTER uses the Windows built-in Text-to-Speech engine (SAPI). It does not " +
                    "require any additional audio software.");
            };
        }
    }
}
