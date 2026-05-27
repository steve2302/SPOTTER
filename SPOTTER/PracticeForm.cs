using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SPOTTER.Controllers;
using SPOTTER.Models;

namespace SPOTTER
{
    /// <summary>
    /// Practice form for training users on gamepad controller usage.
    /// Voice is the primary cue — the audio label is spoken aloud when each
    /// target appears so the user must match button-to-sound from memory.
    /// Buttons that record the same data (e.g. DPad and Left Thumbstick
    /// directions) are treated as fully equivalent correct answers.
    /// </summary>
    public partial class PracticeForm : Form
    {
        private GameControllerHandler _controller;
        private ControllerSettings _settings;
        private Timer _gameTimer;
        private Timer _controllerTimer;

        // UI Controls
        private Panel _targetPanel;
        private Label _voiceHintLabel;
        private Label _targetLabel;
        private Label _instructionLabel;
        private Label _scoreLabel;
        private Label _streakLabel;
        private Label _accuracyLabel;
        private Label _responseTimeLabel;
        private Label _levelLabel;
        private ProgressBar _timeBar;
        private Button _startButton;
        private Button _pauseButton;
        private Button _stopButton;
        private ComboBox _difficultyCombo;
        private Panel _statsPanel;
        private Label _controllerStatusLabel;

        // Game state
        private bool _gameActive = false;
        private bool _gamePaused = false;
        private DateTime _targetStartTime;
        private string _currentTarget;
        private string _expectedResponse;
        private int _score = 0;
        private int _correctResponses = 0;
        private int _totalResponses = 0;
        private int _currentStreak = 0;
        private int _bestStreak = 0;
        private List<double> _responseTimes = new List<double>();
        private int _level = 1;
        private int _timeLimit = 5000;
        private Random _random = new Random();

        private Dictionary<string, int> _difficultyTimeLimits = new Dictionary<string, int>
        {
            { "Easy",   8000 },
            { "Medium", 5000 },
            { "Hard",   3000 },
            { "Expert", 2000 }
        };

        // key = audio label (spoken text), value = expected recorded response (",<data>")
        // One entry per unique recorded value — equivalent buttons (DPad / Left Stick) share one entry.
        private Dictionary<string, string> _targets = new Dictionary<string, string>();
        private GamepadAudioConfig _audioConfig;

        public PracticeForm(ControllerSettings settings)
        {
            _settings = settings;

            _audioConfig = new GamepadAudioConfig();
            if (_audioConfig.LoadFromFile())
                LoadTargetsFromConfig();
            else
                LoadDefaultTargets();

            InitializeComponent();
            InitializeController();
        }

        // ----------------------------------------------------------------
        // Target loading
        // ----------------------------------------------------------------

        private void LoadTargetsFromConfig()
        {
            _targets.Clear();
            var seenResponses = new HashSet<string>();
            var skipButtons = new HashSet<string> { "Start_On", "Start_Off" };

            foreach (var kvp in _audioConfig.GetAllMappings())
            {
                string button = kvp.Key;
                if (skipButtons.Contains(button)) continue;

                string audioText = kvp.Value;
                if (string.IsNullOrWhiteSpace(audioText) || audioText == "not configured") continue;

                string recordedData = _audioConfig.GetRecordedData(button);
                if (string.IsNullOrWhiteSpace(recordedData) || recordedData == "NA") continue;

                string expectedResponse = "," + recordedData;

                // seenResponses.Add returns false when already present — skip duplicates so that
                // equivalent buttons (e.g. DPadUp and LeftThumbUp) appear only once in the game.
                if (seenResponses.Add(expectedResponse))
                    _targets[audioText] = expectedResponse;
            }

            if (_targets.Count == 0)
                LoadDefaultTargets();
        }

        private void LoadDefaultTargets()
        {
            _targets["1"]                         = ",1";
            _targets["2"]                         = ",2";
            _targets["3"]                         = ",3";
            _targets["4"]                         = ",4";
            _targets["Goat"]                      = ",goat";
            _targets["Glare"]                     = ",glare";
            _targets["100 to 200 metres"]         = ",(blue) 100-200";
            _targets["50 to 100 metres"]          = ",(green) 50-100";
            _targets["0 to 50 metres"]            = ",(yellow) 0-50";
            _targets["200 to 300 metres"]         = ",(black) 200-300";
            _targets["Grey Kangaroo"]             = ",grey_kangaroo";
            _targets["Red Kangaroo"]              = ",red_kangaroo";
            _targets["Delete last record"]        = ",delete_last_record";
        }

        // ----------------------------------------------------------------
        // UI construction
        // ----------------------------------------------------------------

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "Practice Mode — Aerial Survey Logger";
            this.Size = new Size(1000, 750);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(240, 240, 240);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            Label titleLabel = new Label
            {
                Text = "Practice Mode",
                Font = new Font("Segoe UI", 24, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 120, 215),
                AutoSize = true,
                Location = new Point(20, 20)
            };
            this.Controls.Add(titleLabel);

            _instructionLabel = new Label
            {
                Text = "Select difficulty, then press Start. Listen to the voice prompt and press the matching button.",
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.FromArgb(80, 80, 80),
                AutoSize = false,
                Size = new Size(960, 50),
                Location = new Point(20, 70),
                TextAlign = ContentAlignment.TopLeft
            };
            this.Controls.Add(_instructionLabel);

            // Settings row
            Panel settingsPanel = new Panel
            {
                Location = new Point(20, 130),
                Size = new Size(960, 60),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            Label difficultyLabel = new Label
            {
                Text = "Difficulty:",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Location = new Point(20, 18),
                AutoSize = true
            };
            settingsPanel.Controls.Add(difficultyLabel);

            _difficultyCombo = new ComboBox
            {
                Font = new Font("Segoe UI", 11),
                Location = new Point(120, 15),
                Size = new Size(150, 30),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _difficultyCombo.Items.AddRange(new object[] { "Easy", "Medium", "Hard", "Expert" });
            _difficultyCombo.SelectedIndex = 1;
            settingsPanel.Controls.Add(_difficultyCombo);

            _startButton = new Button
            {
                Text = "Start",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Location = new Point(620, 10),
                Size = new Size(100, 40),
                BackColor = Color.FromArgb(0, 200, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            _startButton.FlatAppearance.BorderSize = 0;
            _startButton.Click += StartButton_Click;
            settingsPanel.Controls.Add(_startButton);

            _pauseButton = new Button
            {
                Text = "Pause",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Location = new Point(730, 10),
                Size = new Size(100, 40),
                BackColor = Color.FromArgb(255, 165, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            _pauseButton.FlatAppearance.BorderSize = 0;
            _pauseButton.Click += PauseButton_Click;
            settingsPanel.Controls.Add(_pauseButton);

            _stopButton = new Button
            {
                Text = "Stop",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                Location = new Point(840, 10),
                Size = new Size(100, 40),
                BackColor = Color.FromArgb(220, 53, 69),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Enabled = false
            };
            _stopButton.FlatAppearance.BorderSize = 0;
            _stopButton.Click += (s, e) => StopGame();
            settingsPanel.Controls.Add(_stopButton);

            this.Controls.Add(settingsPanel);

            // Target panel — voice hint sits above the main label
            _targetPanel = new Panel
            {
                Location = new Point(20, 210),
                Size = new Size(960, 310),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            _voiceHintLabel = new Label
            {
                Text = "TARGET",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.FromArgb(160, 160, 160),
                AutoSize = true,
                Location = new Point(10, 14)
            };
            _targetPanel.Controls.Add(_voiceHintLabel);

            _targetLabel = new Label
            {
                Text = "Ready",
                Font = new Font("Segoe UI", 42, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 120, 215),
                AutoSize = false,
                Size = new Size(940, 210),
                Location = new Point(10, 38),
                TextAlign = ContentAlignment.MiddleCenter
            };
            _targetPanel.Controls.Add(_targetLabel);

            _timeBar = new ProgressBar
            {
                Location = new Point(10, 265),
                Size = new Size(940, 30),
                Style = ProgressBarStyle.Continuous
            };
            _targetPanel.Controls.Add(_timeBar);

            this.Controls.Add(_targetPanel);

            // Stats panel
            _statsPanel = new Panel
            {
                Location = new Point(20, 540),
                Size = new Size(960, 150),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            int labelX = 30;
            int labelY = 20;
            int labelSpacing = 160;

            AddStatLabel("Score:", ref _scoreLabel, "0", new Point(labelX, labelY));
            AddStatLabel("Level:", ref _levelLabel, "1", new Point(labelX + labelSpacing, labelY));
            AddStatLabel("Streak:", ref _streakLabel, "0 (Best: 0)", new Point(labelX + labelSpacing * 2, labelY));

            labelY = 80;
            AddStatLabel("Accuracy:", ref _accuracyLabel, "0%", new Point(labelX, labelY));
            AddStatLabel("Avg Time:", ref _responseTimeLabel, "0.00s", new Point(labelX + labelSpacing, labelY));
            AddStatLabel("Controller:", ref _controllerStatusLabel, "Not Connected",
                new Point(labelX + labelSpacing * 2, labelY));

            this.Controls.Add(_statsPanel);

            this.ResumeLayout();
        }

        private void AddStatLabel(string title, ref Label valueLabel, string initialValue, Point location)
        {
            var titleLabel = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Location = location,
                AutoSize = true,
                ForeColor = Color.FromArgb(80, 80, 80)
            };
            _statsPanel.Controls.Add(titleLabel);

            valueLabel = new Label
            {
                Text = initialValue,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Location = new Point(location.X, location.Y + 25),
                AutoSize = true,
                ForeColor = Color.FromArgb(0, 120, 215)
            };
            _statsPanel.Controls.Add(valueLabel);
        }

        // ----------------------------------------------------------------
        // Controller
        // ----------------------------------------------------------------

        private void InitializeController()
        {
            try
            {
                _controller = new GameControllerHandler(_settings);
                _controller.KeyPressed += Controller_KeyPressed;
                _controller.ObservationRecorded += Controller_ObservationRecorded;
                _controller.ControllerConnectionChanged += Controller_ConnectionChanged;

                UpdateControllerStatus(_controller.IsConnected);

                _controllerTimer = new Timer { Interval = 50 };
                _controllerTimer.Tick += (s, e) => _controller?.Update();
                _controllerTimer.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error initializing controller: " + ex.Message,
                    "Controller Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void Controller_ConnectionChanged(object sender, bool isConnected)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<object, bool>(Controller_ConnectionChanged), sender, isConnected);
                return;
            }

            UpdateControllerStatus(isConnected);

            if (!isConnected && _gameActive)
            {
                PauseGame();
                MessageBox.Show("Controller disconnected! Please reconnect to continue.",
                    "Controller Disconnected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void UpdateControllerStatus(bool isConnected)
        {
            _controllerStatusLabel.Text = isConnected ? "Connected" : "Not Connected";
            _controllerStatusLabel.ForeColor = isConnected
                ? Color.FromArgb(0, 150, 0)
                : Color.FromArgb(220, 53, 69);
        }

        private void Controller_KeyPressed(object sender, string key)
        {
            if (_gameActive && !_gamePaused)
                CheckResponse(key);
        }

        private void Controller_ObservationRecorded(object sender, string observation)
        {
            if (_gameActive && !_gamePaused)
                CheckResponse(observation);
        }

        // ----------------------------------------------------------------
        // Game flow
        // ----------------------------------------------------------------

        private void StartButton_Click(object sender, EventArgs e)
        {
            if (!_controller.IsConnected)
            {
                MessageBox.Show("Please connect a game controller before starting practice mode.",
                    "No Controller", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            StartGame();
        }

        private void StartGame()
        {
            _gameActive = true;
            _gamePaused = false;
            _score = 0;
            _correctResponses = 0;
            _totalResponses = 0;
            _currentStreak = 0;
            _bestStreak = 0;
            _responseTimes.Clear();
            _level = 1;

            _timeLimit = _difficultyTimeLimits[_difficultyCombo.SelectedItem.ToString()];

            _startButton.Enabled = false;
            _pauseButton.Enabled = true;
            _stopButton.Enabled = true;
            _difficultyCombo.Enabled = false;

            _controllerTimer?.Start();

            UpdateStats();
            ShowNextTarget();

            _gameTimer = new Timer { Interval = 100 };
            _gameTimer.Tick += GameTimer_Tick;
            _gameTimer.Start();
        }

        private void PauseButton_Click(object sender, EventArgs e)
        {
            if (_gamePaused) ResumeGame();
            else             PauseGame();
        }

        private void PauseGame()
        {
            _gamePaused = true;
            _pauseButton.Text = "Resume";
            _gameTimer?.Stop();
            _controllerTimer?.Stop();
            _targetLabel.Text = "PAUSED";
            _targetLabel.ForeColor = Color.FromArgb(255, 165, 0);
        }

        private void ResumeGame()
        {
            _gamePaused = false;
            _pauseButton.Text = "Pause";
            _targetStartTime = DateTime.Now;
            _controllerTimer?.Start();
            _gameTimer?.Start();
            ShowNextTarget();
        }

        private void StopGame()
        {
            _gameActive = false;
            _gamePaused = false;
            _gameTimer?.Stop();
            _controllerTimer?.Stop();

            _startButton.Enabled = true;
            _pauseButton.Enabled = false;
            _stopButton.Enabled = false;
            _difficultyCombo.Enabled = true;

            ShowGameSummary();
        }

        private void ShowNextTarget()
        {
            if (!_gameActive || _gamePaused) return;

            var targetList = _targets.ToList();
            var target = targetList[_random.Next(targetList.Count)];

            _currentTarget = target.Key;       // audio label — what is spoken
            _expectedResponse = target.Value;  // ",<recordedData>" — what any matching button produces

            // Display the audio label (not the button name — that would give it away)
            _targetLabel.Text = _currentTarget;
            _targetLabel.ForeColor = Color.FromArgb(0, 120, 215);
            _voiceHintLabel.Text = "VOICE PROMPT";
            _voiceHintLabel.ForeColor = Color.FromArgb(160, 160, 160);
            _targetStartTime = DateTime.Now;
            _timeBar.Value = 100;

            _instructionLabel.Text =
                "Press the matching button — " +
                "Level " + _level + "  |  Time limit: " + (_timeLimit / 1000.0).ToString("F1") + "s";
        }

        private void GameTimer_Tick(object sender, EventArgs e)
        {
            if (!_gameActive || _gamePaused) return;

            double elapsed   = (DateTime.Now - _targetStartTime).TotalMilliseconds;
            double remaining = _timeLimit - elapsed;

            if (remaining <= 0)
            {
                HandleTimeout();
            }
            else
            {
                _timeBar.Value = (int)((remaining / _timeLimit) * 100);

                if (remaining < _timeLimit * 0.3)
                    _targetLabel.ForeColor = Color.FromArgb(220, 53, 69);
                else if (remaining < _timeLimit * 0.6)
                    _targetLabel.ForeColor = Color.FromArgb(255, 165, 0);
            }
        }

        private void CheckResponse(string response)
        {
            if (!_gameActive || _gamePaused || string.IsNullOrEmpty(_expectedResponse)) return;

            double responseTime = (DateTime.Now - _targetStartTime).TotalMilliseconds;
            _totalResponses++;

            // Any button that records the same data as the target is a correct answer —
            // this automatically handles equivalent inputs (DPad / Left Thumbstick, etc.)
            if (response == _expectedResponse)
            {
                _correctResponses++;
                _currentStreak++;
                if (_currentStreak > _bestStreak)
                    _bestStreak = _currentStreak;

                _responseTimes.Add(responseTime);

                int basePoints  = 100;
                int timeBonus   = (int)((1.0 - (responseTime / _timeLimit)) * 100);
                int streakBonus = Math.Min(_currentStreak * 10, 100);
                int points      = basePoints + timeBonus + streakBonus;
                _score += points;

                _targetLabel.Text      = "CORRECT!  +" + points;
                _targetLabel.ForeColor = Color.FromArgb(0, 180, 0);
                _voiceHintLabel.Text   = "";

                if (_correctResponses % 10 == 0)
                {
                    _level++;
                    _timeLimit = Math.Max(1000, _timeLimit - 200);
                }
            }
            else
            {
                _currentStreak         = 0;
                _targetLabel.Text      = "WRONG";
                _targetLabel.ForeColor = Color.FromArgb(220, 53, 69);
                _voiceHintLabel.Text   = "";
                _score = Math.Max(0, _score - 50);
            }

            UpdateStats();

            // Brief pause before showing the next target
            var delayTimer = new Timer { Interval = 800 };
            delayTimer.Tick += (s, e) =>
            {
                delayTimer.Stop();
                delayTimer.Dispose();
                ShowNextTarget();
            };
            delayTimer.Start();
        }

        private void HandleTimeout()
        {
            _currentStreak         = 0;
            _totalResponses++;
            _targetLabel.Text      = "TIME!";
            _targetLabel.ForeColor = Color.FromArgb(220, 53, 69);
            _voiceHintLabel.Text   = "";
            _score = Math.Max(0, _score - 50);

            UpdateStats();

            var delayTimer = new Timer { Interval = 800 };
            delayTimer.Tick += (s, e) =>
            {
                delayTimer.Stop();
                delayTimer.Dispose();
                ShowNextTarget();
            };
            delayTimer.Start();
        }

        private void UpdateStats()
        {
            _scoreLabel.Text  = _score.ToString();
            _levelLabel.Text  = _level.ToString();
            _streakLabel.Text = _currentStreak + " (Best: " + _bestStreak + ")";

            double accuracy = _totalResponses > 0
                ? (_correctResponses * 100.0 / _totalResponses) : 0;
            _accuracyLabel.Text = accuracy.ToString("F1") + "%";

            double avgTime = _responseTimes.Count > 0
                ? _responseTimes.Average() / 1000.0 : 0;
            _responseTimeLabel.Text = avgTime.ToString("F2") + "s";
        }

        private void ShowGameSummary()
        {
            double accuracy = _totalResponses > 0
                ? (_correctResponses * 100.0 / _totalResponses) : 0;
            double avgTime = _responseTimes.Count > 0
                ? _responseTimes.Average() / 1000.0 : 0;

            string summary =
                "Game Complete!\n\n" +
                "Final Score: "           + _score + "\n" +
                "Level Reached: "         + _level + "\n" +
                "Correct: "               + _correctResponses + " / " + _totalResponses + "\n" +
                "Accuracy: "              + accuracy.ToString("F1") + "%\n" +
                "Average Response Time: " + avgTime.ToString("F2") + "s\n" +
                "Best Streak: "           + _bestStreak + "\n\n" +
                "Difficulty: "            + _difficultyCombo.SelectedItem;

            _targetLabel.Text      = "Game Over";
            _targetLabel.ForeColor = Color.FromArgb(0, 120, 215);
            _instructionLabel.Text = "Click Start to play again.";

            MessageBox.Show(summary, "Practice Session Complete",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _gameTimer?.Stop();
            _gameTimer?.Dispose();
            _controllerTimer?.Stop();
            _controllerTimer?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
