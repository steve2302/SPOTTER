using System;

namespace SPOTTER.Abstractions
{
    /// <summary>
    /// Platform-neutral contract for spoken and recorded audio cues
    /// (confirming a button press, calling a species name, warning on GPS loss).
    /// The iOS implementation will use AVFoundation and AVSpeechSynthesizer;
    /// Windows currently keeps NAudio + System.Speech inside
    /// <see cref="SPOTTER.Controllers.GameControllerHandler"/> and does not
    /// yet implement this interface. Forward-looking contract.
    /// </summary>
    public interface IAudioCue : IDisposable
    {
        /// <summary>Speak a line of text through the platform's TTS engine.</summary>
        void SpeakText(string text);

        /// <summary>Play a sound file from a local path. Short cues only.</summary>
        void PlayFile(string path);

        /// <summary>Interrupt any speech or playback in progress.</summary>
        void Stop();
    }
}
