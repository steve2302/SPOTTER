using SPOTTER.Models;
using SharpDX;
using SharpDX.XInput;
using System;
using System.IO;
using System.Speech.Synthesis;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SPOTTER.Controllers
{
    /// <summary>
    /// Handles game controller input and mapping for observation recording
    /// </summary>
    public class GameControllerHandler
    {
        private Controller _controller;
        private State _currentState;
        private State _previousState;
        private UserIndex _userIndex = UserIndex.One;
        private SpeechSynthesizer _voice;
        private ControllerSettings _settings;
        private GamepadAudioConfig _audioConfig;

        // NAudio components for mixing (allows overlapping sounds)
        private IWavePlayer _outputDevice;
        private MixingSampleProvider _mixer;

        public event EventHandler<string> KeyPressed;
        public event EventHandler<string> ObservationRecorded;
        public event EventHandler<bool> ControllerConnectionChanged;

        public bool IsConnected => _controller.IsConnected;
        public bool IsInBreak { get; private set; }

        // Controller state tracking
        private bool _leftThumbstickReleased = true;
        private bool _rightThumbstickReleased = true;
        private bool _leftTriggerReleased = true;
        private bool _rightTriggerReleased = true;

        private bool _previouslyConnected;
        private bool _currentlyConnected;

        public GameControllerHandler(ControllerSettings settings)
        {
            _settings = settings;
            _voice = new SpeechSynthesizer();

            _audioConfig = new GamepadAudioConfig();

            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GamepadAudioConfig.csv");
                _audioConfig.LoadFromFile(configPath);
                System.Diagnostics.Debug.WriteLine("Audio configuration loaded successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading audio config: {ex.Message}");
            }

            InitializeAudio();

            try
            {
                _controller = new Controller(_userIndex);
                _currentlyConnected = _controller.IsConnected;
                _previouslyConnected = _currentlyConnected;

                if (_controller.IsConnected)
                {
                    _currentState = _controller.GetState();
                    _previousState = _currentState;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Controller initialization error: {ex.Message}");
                _currentlyConnected = false;
                _previouslyConnected = false;
            }
        }

        private void InitializeAudio()
        {
            try
            {
                _outputDevice = new WaveOutEvent();
                _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
                _mixer.ReadFully = true;
                _outputDevice.Init(_mixer);
                _outputDevice.Play();

                System.Diagnostics.Debug.WriteLine("NAudio initialized successfully - overlapping audio enabled");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing audio: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the controller state and processes inputs
        /// </summary>
        public void Update()
        {
            if (_controller == null)
                return;

            _previousState = _currentState;

            try
            {
                _currentState = _controller.GetState();
            }
            catch (Exception)
            {
                _currentlyConnected = false;
                if (_previouslyConnected != _currentlyConnected)
                {
                    _previouslyConnected = _currentlyConnected;
                    ControllerConnectionChanged?.Invoke(this, _currentlyConnected);
                }
                return;
            }

            _previouslyConnected = _currentlyConnected;
            _currentlyConnected = _controller.IsConnected;

            if (_previouslyConnected != _currentlyConnected)
                ControllerConnectionChanged?.Invoke(this, _currentlyConnected);

            if (!_currentlyConnected)
                return;

            ProcessControls();
        }

        // Builds the log string for a button from the config, falling back to the button ID.
        private string GetRecorded(string buttonId)
        {
            string data = _audioConfig.GetRecordedData(buttonId);
            return "," + (string.IsNullOrWhiteSpace(data) ? buttonId : data);
        }

        private void ProcessControls()
        {
            if (_currentState.Gamepad.Buttons != _previousState.Gamepad.Buttons)
            {
                CheckButtonPress(
                    (_currentState.Gamepad.Buttons  & GamepadButtonFlags.A) == GamepadButtonFlags.A,
                    (_previousState.Gamepad.Buttons & GamepadButtonFlags.A) == GamepadButtonFlags.A,
                    GetRecorded("A"), "A");
                CheckButtonPress(
                    (_currentState.Gamepad.Buttons  & GamepadButtonFlags.B) == GamepadButtonFlags.B,
                    (_previousState.Gamepad.Buttons & GamepadButtonFlags.B) == GamepadButtonFlags.B,
                    GetRecorded("B"), "B");
                CheckButtonPress(
                    (_currentState.Gamepad.Buttons  & GamepadButtonFlags.Y) == GamepadButtonFlags.Y,
                    (_previousState.Gamepad.Buttons & GamepadButtonFlags.Y) == GamepadButtonFlags.Y,
                    GetRecorded("Y"), "Y");
                CheckButtonPress(
                    (_currentState.Gamepad.Buttons  & GamepadButtonFlags.X) == GamepadButtonFlags.X,
                    (_previousState.Gamepad.Buttons & GamepadButtonFlags.X) == GamepadButtonFlags.X,
                    GetRecorded("X"), "X");

                if ((_currentState.Gamepad.Buttons  & GamepadButtonFlags.LeftShoulder) == GamepadButtonFlags.LeftShoulder &&
                    (_previousState.Gamepad.Buttons & GamepadButtonFlags.LeftShoulder) != GamepadButtonFlags.LeftShoulder)
                    RecordButton(GetRecorded("LeftShoulder"), "LeftShoulder");

                if ((_currentState.Gamepad.Buttons  & GamepadButtonFlags.RightShoulder) == GamepadButtonFlags.RightShoulder &&
                    (_previousState.Gamepad.Buttons & GamepadButtonFlags.RightShoulder) != GamepadButtonFlags.RightShoulder)
                    RecordButton(GetRecorded("RightShoulder"), "RightShoulder");

                if ((_currentState.Gamepad.Buttons  & GamepadButtonFlags.Start) == GamepadButtonFlags.Start &&
                    (_previousState.Gamepad.Buttons & GamepadButtonFlags.Start) != GamepadButtonFlags.Start)
                    ToggleBreak();

                if ((_currentState.Gamepad.Buttons  & GamepadButtonFlags.Back) == GamepadButtonFlags.Back &&
                    (_previousState.Gamepad.Buttons & GamepadButtonFlags.Back) != GamepadButtonFlags.Back)
                    RecordButton(GetRecorded("Back"), "Back");

                CheckButtonPress(
                    (_currentState.Gamepad.Buttons  & GamepadButtonFlags.LeftThumb) == GamepadButtonFlags.LeftThumb,
                    (_previousState.Gamepad.Buttons & GamepadButtonFlags.LeftThumb) == GamepadButtonFlags.LeftThumb,
                    GetRecorded("LeftThumbClick"), "LeftThumbClick");
                CheckButtonPress(
                    (_currentState.Gamepad.Buttons  & GamepadButtonFlags.RightThumb) == GamepadButtonFlags.RightThumb,
                    (_previousState.Gamepad.Buttons & GamepadButtonFlags.RightThumb) == GamepadButtonFlags.RightThumb,
                    GetRecorded("RightThumbClick"), "RightThumbClick");
            }

            // D-Pad
            GamepadButtonFlags dpadMask = GamepadButtonFlags.DPadUp | GamepadButtonFlags.DPadDown |
                                          GamepadButtonFlags.DPadLeft | GamepadButtonFlags.DPadRight;

            if ((_currentState.Gamepad.Buttons & dpadMask) != (_previousState.Gamepad.Buttons & dpadMask))
            {
                CheckDPadPress(
                    (_currentState.Gamepad.Buttons  & GamepadButtonFlags.DPadUp)    == GamepadButtonFlags.DPadUp,
                    (_previousState.Gamepad.Buttons & GamepadButtonFlags.DPadUp)    == GamepadButtonFlags.DPadUp,
                    GetRecorded("DPadUp"), "DPadUp");
                CheckDPadPress(
                    (_currentState.Gamepad.Buttons  & GamepadButtonFlags.DPadLeft)  == GamepadButtonFlags.DPadLeft,
                    (_previousState.Gamepad.Buttons & GamepadButtonFlags.DPadLeft)  == GamepadButtonFlags.DPadLeft,
                    GetRecorded("DPadLeft"), "DPadLeft");
                CheckDPadPress(
                    (_currentState.Gamepad.Buttons  & GamepadButtonFlags.DPadDown)  == GamepadButtonFlags.DPadDown,
                    (_previousState.Gamepad.Buttons & GamepadButtonFlags.DPadDown)  == GamepadButtonFlags.DPadDown,
                    GetRecorded("DPadDown"), "DPadDown");
                CheckDPadPress(
                    (_currentState.Gamepad.Buttons  & GamepadButtonFlags.DPadRight) == GamepadButtonFlags.DPadRight,
                    (_previousState.Gamepad.Buttons & GamepadButtonFlags.DPadRight) == GamepadButtonFlags.DPadRight,
                    GetRecorded("DPadRight"), "DPadRight");
            }

            ProcessTriggers();
            ProcessThumbsticks();
        }

        private void ProcessTriggers()
        {
            float scaledTolerance = _settings.TriggerTolerance * 255f;

            if (_currentState.Gamepad.LeftTrigger  < scaledTolerance) _leftTriggerReleased  = true;
            if (_currentState.Gamepad.RightTrigger < scaledTolerance) _rightTriggerReleased = true;

            if (_currentState.Gamepad.LeftTrigger > scaledTolerance && _leftTriggerReleased)
            {
                _leftTriggerReleased = false;
                RecordButton(GetRecorded("LeftTrigger"), "LeftTrigger");
            }

            if (_currentState.Gamepad.RightTrigger > scaledTolerance && _rightTriggerReleased)
            {
                _rightTriggerReleased = false;
                RecordButton(GetRecorded("RightTrigger"), "RightTrigger");
            }
        }

        private void ProcessThumbsticks()
        {
            CheckThumbstickReleases();

            // Suppress direction events while the click button is physically held — pressing the
            // stick down causes slight XY deflection that would otherwise fire an unintended
            // direction event in the same update cycle as the click.
            bool leftClickHeld  = (_currentState.Gamepad.Buttons & GamepadButtonFlags.LeftThumb)  == GamepadButtonFlags.LeftThumb;
            bool rightClickHeld = (_currentState.Gamepad.Buttons & GamepadButtonFlags.RightThumb) == GamepadButtonFlags.RightThumb;

            // Left thumbstick
            GamepadButtonFlags leftDir = GetThumbstickDirection(true);
            if (_leftThumbstickReleased && leftDir != 0 && !leftClickHeld)
            {
                _leftThumbstickReleased = false;
                switch (leftDir)
                {
                    case GamepadButtonFlags.DPadDown:  RecordButton(GetRecorded("LeftThumbDown"),  "LeftThumbDown");  break;
                    case GamepadButtonFlags.DPadLeft:  RecordButton(GetRecorded("LeftThumbLeft"),  "LeftThumbLeft");  break;
                    case GamepadButtonFlags.DPadUp:    RecordButton(GetRecorded("LeftThumbUp"),    "LeftThumbUp");    break;
                    case GamepadButtonFlags.DPadRight: RecordButton(GetRecorded("LeftThumbRight"), "LeftThumbRight"); break;
                }
            }

            // Right thumbstick
            GamepadButtonFlags rightDir = GetThumbstickDirection(false);
            if (_rightThumbstickReleased && rightDir != 0 && !rightClickHeld)
            {
                _rightThumbstickReleased = false;
                switch (rightDir)
                {
                    case GamepadButtonFlags.DPadDown:  RecordButton(GetRecorded("RightThumbDown"),  "RightThumbDown");  break;
                    case GamepadButtonFlags.DPadLeft:  RecordButton(GetRecorded("RightThumbLeft"),  "RightThumbLeft");  break;
                    case GamepadButtonFlags.DPadUp:    RecordButton(GetRecorded("RightThumbUp"),    "RightThumbUp");    break;
                    case GamepadButtonFlags.DPadRight: RecordButton(GetRecorded("RightThumbRight"), "RightThumbRight"); break;
                }
            }
        }

        private void CheckThumbstickReleases()
        {
            float leftX  = _currentState.Gamepad.LeftThumbX  / 32767f;
            float leftY  = _currentState.Gamepad.LeftThumbY  / 32767f;
            float rightX = _currentState.Gamepad.RightThumbX / 32767f;
            float rightY = _currentState.Gamepad.RightThumbY / 32767f;

            if (Math.Abs(leftX)  < _settings.ThumbstickTolerance &&
                Math.Abs(leftY)  < _settings.ThumbstickTolerance)
                _leftThumbstickReleased = true;

            if (Math.Abs(rightX) < _settings.ThumbstickTolerance &&
                Math.Abs(rightY) < _settings.ThumbstickTolerance)
                _rightThumbstickReleased = true;
        }

        private void RecordButton(string keyValue, string buttonId)
        {
            if (_audioConfig.GetNewRecord(buttonId))
                RecordObservation(keyValue, buttonId);
            else
                RecordKeyPress(keyValue, buttonId);
        }

        private void CheckButtonPress(bool isCurrentlyPressed, bool wasPreviouslyPressed, string keyValue, string buttonId)
        {
            if (isCurrentlyPressed && !wasPreviouslyPressed)
                RecordButton(keyValue, buttonId);
        }

        private void CheckDPadPress(bool isCurrentlyPressed, bool wasPreviouslyPressed, string keyValue, string buttonId)
        {
            if (isCurrentlyPressed && !wasPreviouslyPressed)
                RecordButton(keyValue, buttonId);
        }

        private void RecordKeyPress(string keyValue, string buttonIdentifier)
        {
            PlaySoundFromConfig(buttonIdentifier);
            KeyPressed?.Invoke(this, keyValue);
        }

        private void RecordObservation(string observation, string buttonIdentifier)
        {
            PlaySoundFromConfig(buttonIdentifier);
            ObservationRecorded?.Invoke(this, observation);
        }

        private void PlaySoundFromConfig(string buttonIdentifier)
        {
            StopCurrentAudio();

            try
            {
                string audioText = _audioConfig.GetAudioText(buttonIdentifier);

                if (!string.IsNullOrWhiteSpace(audioText))
                {
                    _voice.Rate = 3;
                    _voice.SpeakAsync(audioText);
                    System.Diagnostics.Debug.WriteLine($"Playing TTS for {buttonIdentifier}: {audioText}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"No audio text configured for: {buttonIdentifier}");
                    _voice.Rate = 3;
                    _voice.SpeakAsync(buttonIdentifier.Replace("_", " "));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error playing audio for '{buttonIdentifier}': {ex.Message}");
            }
        }

        private void ToggleBreak()
        {
            IsInBreak = !IsInBreak;
            string buttonIdentifier = IsInBreak ? "Start_On" : "Start_Off";
            RecordButton(GetRecorded(buttonIdentifier), buttonIdentifier);
        }

        private void StopCurrentAudio()
        {
            try
            {
                if (_voice != null && _voice.State == System.Speech.Synthesis.SynthesizerState.Speaking)
                    _voice.SpeakAsyncCancelAll();

                _mixer?.RemoveAllMixerInputs();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping audio: {ex.Message}");
            }
        }

        private void AddMixerInput(ISampleProvider input)
        {
            _mixer?.AddMixerInput(ConvertToRightChannelCount(input));
        }

        private ISampleProvider ConvertToRightChannelCount(ISampleProvider input)
        {
            if (input.WaveFormat.Channels == _mixer.WaveFormat.Channels)
                return input;
            if (input.WaveFormat.Channels == 1 && _mixer.WaveFormat.Channels == 2)
                return new MonoToStereoSampleProvider(input);
            throw new NotImplementedException("Channel count conversion not supported");
        }

        private GamepadButtonFlags GetThumbstickDirection(bool leftStick)
        {
            short x = leftStick ? _currentState.Gamepad.LeftThumbX : _currentState.Gamepad.RightThumbX;
            short y = leftStick ? _currentState.Gamepad.LeftThumbY : _currentState.Gamepad.RightThumbY;

            float xFloat = x;
            float yFloat = y;
            float scaledTolerance = _settings.ThumbstickTolerance * 32767f;

            float absX = Math.Abs(xFloat);
            float absY = Math.Abs(yFloat);

            if (absX > absY && absX > scaledTolerance)
                return (x > 0) ? GamepadButtonFlags.DPadRight : GamepadButtonFlags.DPadLeft;
            else if (absX < absY && absY > scaledTolerance)
                return (y > 0) ? GamepadButtonFlags.DPadUp : GamepadButtonFlags.DPadDown;

            return (GamepadButtonFlags)0;
        }

        public void SetVibration(float leftMotor, float rightMotor, int duration = 500)
        {
            try
            {
                var vibration = new Vibration
                {
                    LeftMotorSpeed = (ushort)(leftMotor * 65535f),
                    RightMotorSpeed = (ushort)(rightMotor * 65535f)
                };

                _controller.SetVibration(vibration);

                System.Threading.Tasks.Task.Delay(duration).ContinueWith(t =>
                {
                    var stopVibration = new Vibration { LeftMotorSpeed = 0, RightMotorSpeed = 0 };
                    _controller.SetVibration(stopVibration);
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting vibration: {ex.Message}");
            }
        }

        public void StopVibration()
        {
            try
            {
                var stopVibration = new Vibration { LeftMotorSpeed = 0, RightMotorSpeed = 0 };
                _controller.SetVibration(stopVibration);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping vibration: {ex.Message}");
            }
        }

        public void Dispose()
        {
            try
            {
                _outputDevice?.Stop();
                _outputDevice?.Dispose();
                _mixer?.RemoveAllMixerInputs();
                _voice?.Dispose();
                _controller = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing GameControllerHandler: {ex.Message}");
            }
        }
    }
}
