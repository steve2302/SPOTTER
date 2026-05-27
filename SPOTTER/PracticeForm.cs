using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SPOTTER.Controllers;
using SPOTTER.Models;
using SharpDX.XInput;

namespace SPOTTER
{
    /// <summary>
    /// Practice form for training users on gamepad controller usage
    /// Simulates aerial survey observations without requiring GPS
    /// </summary>
    public partial class PracticeForm : Form
    {
        private GameControllerHandler _controller;
        private ControllerSettings _settings;
        private Timer _gameTimer;
        private Timer _controllerTimer;

        // UI Controls
        private Panel _targetPanel;
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
        private int _timeLimit = 5000; // milliseconds
        private Random _random = new Random();

        // Difficulty settings
        private Dictionary<string, int> _difficultyTimeLimits = new Dictionary<string, int>
        {
            { "Easy", 8000 },
            { "Medium", 5000 },
            { "Hard", 3000 },
            { "Expert", 2000 }
        };

        // Target definitions — populated from GamepadAudioConfig.csv at startup
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

        private void LoadTargetsFromConfig()
        {
            _targets.Clear();
            var skipButtons = new HashSet<string> { "Start_On", "Start_Off" };
            foreach (var kvp in _audioConfig.GetAllMappings())
            {
                string button = kvp.Key;
                if (skipButtons.Contains(button)) continue;
                string audioText = kvp.Value;
                string recordedData = _audioConfig.GetRecordedData(button);
                if (string.IsNullOrWhiteSpace(recordedData) || recordedData == "NA") continue;
                if (audioText == "not configured") continue;
                _targets[button + " (" + audioText + ")"] = "," + recordedData;
            }
            if (_targets.Count == 0)
                LoadDefaultTargets();
        }

        private void LoadDefaultTargets()
        {
            _targets["A Button (1)"]                      = ",1";
            _targets["B Button (2)"]                      = ",2";
            _targets["Y Button (3)"]                      = ",3";
            _targets["X Button (4)"]                      = ",4";
            _targets["Left Shoulder (Goat)"]              = ",goat";
            _targets["Right Shoulder (Glare)"]            = ",glare";
            _targets["D-Pad Up (Blue 100-200m)"]          = ",(blue) 100-200";
            _targets["D-Pad Left (Green 50-100m)"]        = ",(green) 50-100";
            _targets["D-Pad Down (Yellow 0-50m)"]         = ",(yellow) 0-50";
            _targets["D-Pad Right (Black 200-300m)"]      = ",(black) 200-300";
            _targets["Left Trigger (Grey Kangaroo)"]      = ",grey_kangaroo";
            _targets["Right Trigger (Red Kangaroo)"]      = ",red_kangaroo";
            _targets["Back Button (Delete last record)"]  = ",delete_last_record";
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // Form properties
            this.Text = "Practice Mode - Aerial Survey Logger";
            this.Size = new Size(1000, 750);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(240, 240, 240);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            // Title
            Label titleLabel = new Label
            {
                Text = "Practice Mode",
                Font = new Font("Segoe UI", 24, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 120, 215),
                AutoSize = true,
                Location = new Point(20, 20)
            };
            this.Controls.Add(titleLabel);

            // Instructions
            _instructionLabel = new Label
            {
                Text = "Select difficulty and mode, then press Start to begin training!",
                Font = new Font("Segoe UI", 12),
                ForeColor = Color.FromArgb(80, 80, 80),
                AutoSize = false,
                Size = new Size(960, 60),
                Location = new Point(20, 70),
                TextAlign = ContentAlignment.TopLeft
            };
            this.Controls.Add(_instructionLabel);

            // Settings Panel
            Panel settingsPanel = new Panel
            {
                Location = new Point(20, 140),
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
            _difficultyCombo.SelectedIndex = 1; // Medium
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

            // Target Panel (main game area)
            _targetPanel = new Panel
            {
                Location = new Point(20, 220),
                Size = new Size(960, 300),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };

            _targetLabel = new Label
            {
                Text = "Ready",
                Font = new Font("Segoe UI", 36, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 120, 215),
                AutoSize = false,
                Size = new Size(940, 200),
                Location = new Point(10, 50),
                TextAlign = ContentAlignment.MiddleCenter
            };
            _targetPanel.Controls.Add(_targetLabel);

            _timeBar = new ProgressBar
            {
                Location = new Point(10, 260),
                Size = new Size(940, 30),
                Style = ProgressBarStyle.Continuous,
                ForeColor = Color.FromArgb(0, 200, 0)
            };
            _targetPanel.Controls.Add(_timeBar);

            this.Controls.Add(_targetPanel);

            // Stats Panel
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
            Label titleLabel = new Label
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

        private void InitializeController()
        {
            try
            {
                _controller = new GameControllerHandler(_settings);
                _controller.KeyPressed += Controller_KeyPressed;
                _controller.ObservationRecorded += Controller_ObservationRecorded;
                _controller.ControllerConnectionChanged += Controller_ConnectionChanged;

                // Update initial connection status
                UpdateControllerStatus(_controller.IsConnected);

                // Controller polling timer
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
            _controllerStatusLabel.ForeColor = isConnected ?
                Color.FromArgb(0, 150, 0) : Color.FromArgb(220, 53, 69);
        }

        private void Controller_KeyPressed(object sender, string key)
        {
            if (_gameActive && !_gamePaused)
            {
                CheckResponse(key);
            }
        }

        private void Controller_ObservationRecorded(object sender, string observation)
        {
            if (_gameActive && !_gamePaused)
            {
                CheckResponse(observation);
            }
        }

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

            // Get time limit based on difficulty
            _timeLimit = _difficultyTimeLimits[_difficultyCombo.SelectedItem.ToString()];

            _startButton.Enabled = false;
            _pauseButton.Enabled = true;
            _stopButton.Enabled = true;
            _difficultyCombo.Enabled = false;

            _controllerTimer?.Start();

            UpdateStats();
            ShowNextTarget();

            // Start game timer
            _gameTimer = new Timer { Interval = 100 };
            _gameTimer.Tick += GameTimer_Tick;
            _gameTimer.Start();
        }

        private void PauseButton_Click(object sender, EventArgs e)
        {
            if (_gamePaused)
                ResumeGame();
            else
                PauseGame();
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
            _targetStartTime = DateTime.Now; // Reset timer
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

            // Get random target
            var targetList = _targets.ToList();
            var targetIndex = _random.Next(targetList.Count);
            var target = targetList[targetIndex];

            _currentTarget = target.Key;
            _expectedResponse = target.Value;

            // Update display
            _targetLabel.Text = _currentTarget;
            _targetLabel.ForeColor = Color.FromArgb(0, 120, 215);
            _targetStartTime = DateTime.Now;
            _timeBar.Value = 100;

            // Update instruction
            _instructionLabel.Text = "Press the correct button quickly! Level " + _level + " - Time Limit: " + (_timeLimit / 1000.0).ToString("F1") + "s";
        }

        private void GameTimer_Tick(object sender, EventArgs e)
        {
            if (!_gameActive || _gamePaused) return;

            double elapsed = (DateTime.Now - _targetStartTime).TotalMilliseconds;
            double remaining = _timeLimit - elapsed;

            if (remaining <= 0)
            {
                // Time's up - wrong answer
                HandleIncorrectResponse();
            }
            else
            {
                // Update progress bar
                _timeBar.Value = (int)((remaining / _timeLimit) * 100);

                // Change colour based on time remaining
                if (remaining < _timeLimit * 0.3)
                    _targetLabel.ForeColor = Color.FromArgb(220, 53, 69); // Red
                else if (remaining < _timeLimit * 0.6)
                    _targetLabel.ForeColor = Color.FromArgb(255, 165, 0); // Orange
            }
        }

        private void CheckResponse(string response)
        {
            if (!_gameActive || _gamePaused || string.IsNullOrEmpty(_expectedResponse)) return;

            double responseTime = (DateTime.Now - _targetStartTime).TotalMilliseconds;
            _totalResponses++;

            if (response == _expectedResponse)
            {
                _correctResponses++;
                _currentStreak++;
                if (_currentStreak > _bestStreak)
                    _bestStreak = _currentStreak;

                _responseTimes.Add(responseTime);

                // Calculate score (faster = more points)
                int basePoints = 100;
                int timeBonus = (int)((1.0 - (responseTime / _timeLimit)) * 100);
                int streakBonus = Math.Min(_currentStreak * 10, 100);
                int points = basePoints + timeBonus + streakBonus;
                _score += points;

                _targetLabel.Text = "CORRECT! +" + points;
                _targetLabel.ForeColor = Color.FromArgb(0, 200, 0);

                // Level up every 10 correct responses
                if (_correctResponses % 10 == 0)
                {
                    _level++;
                    _timeLimit = Math.Max(1000, _timeLimit - 200); // Decrease time, minimum 1s
                }
            }
            else
            {
                HandleIncorrectResponse();
            }

            UpdateStats();

            // Show next target after brief delay
            Timer delayTimer = new Timer { Interval = 800 };
            delayTimer.Tick += (s, e) =>
            {
                delayTimer.Stop();
                delayTimer.Dispose();
                ShowNextTarget();
            };
            delayTimer.Start();
        }

        private void HandleIncorrectResponse()
        {
            _currentStreak = 0;
            _totalResponses++;

            _targetLabel.Text = "WRONG!";
            _targetLabel.ForeColor = Color.FromArgb(220, 53, 69);

            // Penalty: -50 points (but don't go below 0)
            _score = Math.Max(0, _score - 50);

            UpdateStats();
        }

        private void UpdateStats()
        {
            _scoreLabel.Text = _score.ToString();
            _levelLabel.Text = _level.ToString();
            _streakLabel.Text = _currentStreak + " (Best: " + _bestStreak + ")";

            double accuracy = _totalResponses > 0 ?
                (_correctResponses * 100.0 / _totalResponses) : 0;
            _accuracyLabel.Text = accuracy.ToString("F1") + "%";

            double avgTime = _responseTimes.Count > 0 ?
                _responseTimes.Average() / 1000.0 : 0;
            _responseTimeLabel.Text = avgTime.ToString("F2") + "s";
        }

        private void ShowGameSummary()
        {
            double accuracy = _totalResponses > 0 ?
                (_correctResponses * 100.0 / _totalResponses) : 0;
            double avgTime = _responseTimes.Count > 0 ?
                _responseTimes.Average() / 1000.0 : 0;

            string summary = "Game Complete!\n\n" +
                "Final Score: " + _score + "\n" +
                "Level Reached: " + _level + "\n" +
                "Accuracy: " + accuracy.ToString("F1") + "% (" + _correctResponses + "/" + _totalResponses + ")\n" +
                "Average Response Time: " + avgTime.ToString("F2") + "s\n" +
                "Best Streak: " + _bestStreak + "\n\n" +
                "Difficulty: " + _difficultyCombo.SelectedItem;

            _targetLabel.Text = "Game Over!";
            _targetLabel.ForeColor = Color.FromArgb(0, 120, 215);
            _instructionLabel.Text = "Click Start to play again!";

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
