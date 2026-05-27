using System;

namespace SPOTTER.Models
{
    /// <summary>
    /// Represents settings for the game controller
    /// </summary>
    public class ControllerSettings
    {
        /// <summary>
        /// Threshold value for detecting thumbstick movement (0.0 to 1.0)
        /// Lower values make thumbsticks more sensitive
        /// </summary>
        public float ThumbstickTolerance { get; set; } = 0.5f;

        /// <summary>
        /// Threshold value for detecting trigger presses (0.0 to 1.0)
        /// Lower values make triggers more sensitive
        /// </summary>
        public float TriggerTolerance { get; set; } = 0.5f;
    }
}
