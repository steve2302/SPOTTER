using System;

namespace SPOTTER.Abstractions
{
    /// <summary>
    /// Platform-neutral contract for a game controller used to record wildlife
    /// observations hands-free. The Windows implementation
    /// (<see cref="SPOTTER.Controllers.GameControllerHandler"/>) polls XInput
    /// once per UI tick. The iOS implementation will subscribe to the
    /// GameController framework's input events on a Bluetooth-paired
    /// Xbox or PlayStation controller.
    ///
    /// Button-to-observation mapping is shared across platforms via
    /// <see cref="SPOTTER.Controllers.GamepadAudioConfig"/>, so implementations
    /// only produce the raw button-id and observation strings — the lookup
    /// table itself lives in Core.
    /// </summary>
    public interface IGamepadProvider : IDisposable
    {
        /// <summary>True when a controller is currently paired and reporting.</summary>
        bool IsConnected { get; }

        /// <summary>True while the observer has toggled a break — observations suppressed.</summary>
        bool IsInBreak { get; }

        /// <summary>Raw button-id string for every press (e.g. "A", "DPadUp", "LeftTrigger").</summary>
        event EventHandler<string> KeyPressed;

        /// <summary>
        /// Mapped observation text to write to the session log,
        /// resolved via <see cref="SPOTTER.Controllers.GamepadAudioConfig.GetRecordedData"/>.
        /// </summary>
        event EventHandler<string> ObservationRecorded;

        /// <summary>Fires when the controller connects or disconnects.</summary>
        event EventHandler<bool> ControllerConnectionChanged;

        /// <summary>
        /// Trigger rumble. Motor intensities are 0.0 to 1.0;
        /// duration is milliseconds. On platforms with a single haptic
        /// channel the implementation may average the two values.
        /// </summary>
        void SetVibration(float leftMotor, float rightMotor, int durationMs = 500);

        /// <summary>Stop any rumble in progress.</summary>
        void StopVibration();
    }
}
