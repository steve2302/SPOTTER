using System.Diagnostics;
using CoreHaptics;
using Foundation;
using GameController;
using SPOTTER.Abstractions;

namespace SPOTTER.Mobile.Services;

/// <summary>
/// iOS implementation of <see cref="IGamepadProvider"/> backed by Apple's
/// GameController framework. Detects a paired Xbox One S / Series or
/// DualShock / DualSense controller, raises <see cref="KeyPressed"/> for
/// every button edge, and drives rumble through Core Haptics on the
/// left and right handle locality.
///
/// Pairing happens outside the app: iPad Settings → Bluetooth puts the
/// controller in the OS list. When the app launches, any already-paired
/// controller is picked up immediately. Connect / disconnect events are
/// handled live via GCController notifications.
///
/// ObservationRecorded is not raised yet — the GamepadAudioConfig.csv
/// mapping that resolves raw button names to observation strings will be
/// wired in alongside session management in a later step.
/// </summary>
public sealed class MauiGamepadProvider : IGamepadProvider
{
    private GCController? _controller;
    private CHHapticEngine? _leftEngine;
    private CHHapticEngine? _rightEngine;
    private NSObject? _didConnectObserver;
    private NSObject? _didDisconnectObserver;

    public MauiGamepadProvider()
    {
        // Watch for later pairings.
        _didConnectObserver = NSNotificationCenter.DefaultCenter.AddObserver(
            GCController.DidConnectNotification, HandleDidConnect);
        _didDisconnectObserver = NSNotificationCenter.DefaultCenter.AddObserver(
            GCController.DidDisconnectNotification, HandleDidDisconnect);

        // Pick up any controller that was already paired at launch.
        foreach (var c in GCController.Controllers)
        {
            Attach(c);
            break;
        }
    }

    public bool IsConnected => _controller is not null;

    public bool IsInBreak { get; private set; }

    public event EventHandler<string>? KeyPressed;

    // Not raised yet — see class docs. Declared with no-op accessors so
    // the compiler doesn't flag it as unused on iOS.
    public event EventHandler<string>? ObservationRecorded { add { } remove { } }

    public event EventHandler<bool>? ControllerConnectionChanged;

    public void SetVibration(float leftMotor, float rightMotor, int durationMs = 500)
    {
        PlayPulse(_leftEngine, leftMotor, durationMs);
        PlayPulse(_rightEngine, rightMotor, durationMs);
    }

    public void StopVibration()
    {
        // CHHapticEngine patterns are short-lived and end on their own.
        // A long-running continuous pattern would need an explicit player
        // reference to stop; we don't play those, so nothing to do.
    }

    public void Dispose()
    {
        if (_didConnectObserver is not null)
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver(_didConnectObserver);
            _didConnectObserver = null;
        }
        if (_didDisconnectObserver is not null)
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver(_didDisconnectObserver);
            _didDisconnectObserver = null;
        }
        Detach();
    }

    // ---- notification handlers -------------------------------------------

    private void HandleDidConnect(NSNotification note)
    {
        if (note.Object is GCController c)
            Attach(c);
    }

    private void HandleDidDisconnect(NSNotification note)
    {
        if (note.Object is GCController c && ReferenceEquals(c, _controller))
            Detach();
    }

    // ---- attach / detach --------------------------------------------------

    private void Attach(GCController controller)
    {
        if (_controller is not null) return;
        _controller = controller;

        StartHaptics(controller);
        WireButtons(controller);

        MainThread.BeginInvokeOnMainThread(() =>
            ControllerConnectionChanged?.Invoke(this, true));
    }

    private void Detach()
    {
        try { _leftEngine?.StopWithCompletionHandler(_ => { }); } catch { }
        try { _rightEngine?.StopWithCompletionHandler(_ => { }); } catch { }
        _leftEngine = null;
        _rightEngine = null;
        _controller = null;

        MainThread.BeginInvokeOnMainThread(() =>
            ControllerConnectionChanged?.Invoke(this, false));
    }

    // ---- haptics ---------------------------------------------------------

    private void StartHaptics(GCController controller)
    {
        try
        {
            var haptics = controller.Haptics;
            if (haptics is null) return;

            _leftEngine = haptics.CreateEngine(GCHapticsLocality.LeftHandle);
            _rightEngine = haptics.CreateEngine(GCHapticsLocality.RightHandle);
            _leftEngine?.StartAndReturnError(out _);
            _rightEngine?.StartAndReturnError(out _);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MauiGamepadProvider] Haptics init failed: {ex}");
        }
    }

    private static void PlayPulse(CHHapticEngine? engine, float intensity, int durationMs)
    {
        if (engine is null || intensity <= 0f) return;

        try
        {
            var intensityP = new CHHapticEventParameter(
                CHHapticEventParameterID.HapticIntensity, Math.Clamp(intensity, 0f, 1f));
            var sharpnessP = new CHHapticEventParameter(
                CHHapticEventParameterID.HapticSharpness, 0.5f);

            var evt = new CHHapticEvent(
                CHHapticEventType.HapticContinuous,
                new[] { intensityP, sharpnessP },
                relativeTime: 0,
                duration: Math.Max(0.05, durationMs / 1000.0));

            var pattern = CHHapticPattern.CreatePattern(
                new[] { evt }, Array.Empty<CHHapticDynamicParameter>(), out _);
            if (pattern is null) return;

            var player = engine.CreatePlayer(pattern, out _);
            player?.Start(0, out _);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MauiGamepadProvider] Haptic pulse failed: {ex}");
        }
    }

    // ---- button wiring ---------------------------------------------------

    private void WireButtons(GCController controller)
    {
        var gp = controller.ExtendedGamepad;
        if (gp is null)
        {
            Debug.WriteLine("[MauiGamepadProvider] Controller has no extended gamepad profile — buttons will not report.");
            return;
        }

        gp.ButtonA.PressedChangedHandler = (_, _, pressed) => Raise(pressed, "A");
        gp.ButtonB.PressedChangedHandler = (_, _, pressed) => Raise(pressed, "B");
        gp.ButtonX.PressedChangedHandler = (_, _, pressed) => Raise(pressed, "X");
        gp.ButtonY.PressedChangedHandler = (_, _, pressed) => Raise(pressed, "Y");

        gp.LeftShoulder.PressedChangedHandler = (_, _, pressed) => Raise(pressed, "LeftShoulder");
        gp.RightShoulder.PressedChangedHandler = (_, _, pressed) => Raise(pressed, "RightShoulder");

        gp.LeftTrigger.PressedChangedHandler = (_, _, pressed) => Raise(pressed, "LeftTrigger");
        gp.RightTrigger.PressedChangedHandler = (_, _, pressed) => Raise(pressed, "RightTrigger");

        gp.DPad.Up.PressedChangedHandler = (_, _, pressed) => Raise(pressed, "DPadUp");
        gp.DPad.Down.PressedChangedHandler = (_, _, pressed) => Raise(pressed, "DPadDown");
        gp.DPad.Left.PressedChangedHandler = (_, _, pressed) => Raise(pressed, "DPadLeft");
        gp.DPad.Right.PressedChangedHandler = (_, _, pressed) => Raise(pressed, "DPadRight");

        if (gp.ButtonMenu is { } menu)
            menu.PressedChangedHandler = (_, _, pressed) => Raise(pressed, "Menu");
        if (gp.ButtonOptions is { } options)
            options.PressedChangedHandler = (_, _, pressed) => Raise(pressed, "Options");

        if (gp.LeftThumbstickButton is { } lsb)
            lsb.PressedChangedHandler = (_, _, pressed) => Raise(pressed, "LeftThumbstickButton");
        if (gp.RightThumbstickButton is { } rsb)
            rsb.PressedChangedHandler = (_, _, pressed) => Raise(pressed, "RightThumbstickButton");
    }

    private void Raise(bool pressed, string key)
    {
        if (!pressed) return;
        MainThread.BeginInvokeOnMainThread(() =>
            KeyPressed?.Invoke(this, key));
    }
}
