using System.Diagnostics;
using Foundation;
using GameController;
using SPOTTER.Abstractions;

namespace SPOTTER.Mobile.Services;

/// <summary>
/// iOS implementation of <see cref="IGamepadProvider"/> backed by Apple's
/// GameController framework. Detects a paired Xbox One S / Series or
/// DualShock / DualSense controller and raises <see cref="KeyPressed"/>
/// for every button edge.
///
/// Pairing happens outside the app: iPad Settings → Bluetooth puts the
/// controller in the OS list. When the app launches, any already-paired
/// controller is picked up immediately. Connect / disconnect events are
/// handled live via GCController notifications.
///
/// SetVibration currently triggers the iPad's own taptic engine via MAUI's
/// cross-platform <see cref="Vibration"/> API. True on-controller rumble
/// through Core Haptics will land in a follow-up once we lock in the
/// Microsoft.iOS binding names for CHHapticEngine / CHHapticEvent on this
/// workload release. For an aerial survey where the operator holds the
/// iPad, device haptics are still a useful "GPS lost" alert.
///
/// ObservationRecorded is not raised yet — the GamepadAudioConfig.csv
/// mapping that resolves raw button names to observation strings will be
/// wired in alongside session management in a later step.
/// </summary>
public sealed class MauiGamepadProvider : IGamepadProvider
{
    private GCController? _controller;
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
        // The iPad has a single taptic engine — merge the two motor
        // intensities and ignore any that are zero.
        var intensity = Math.Max(leftMotor, rightMotor);
        if (intensity <= 0f) return;

        try
        {
            Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(durationMs));
        }
        catch (FeatureNotSupportedException)
        {
            Debug.WriteLine("[MauiGamepadProvider] Vibration not supported on this device.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MauiGamepadProvider] Vibration failed: {ex}");
        }
    }

    public void StopVibration()
    {
        try
        {
            Vibration.Default.Cancel();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MauiGamepadProvider] Vibration cancel failed: {ex}");
        }
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

        WireButtons(controller);

        MainThread.BeginInvokeOnMainThread(() =>
            ControllerConnectionChanged?.Invoke(this, true));
    }

    private void Detach()
    {
        _controller = null;

        MainThread.BeginInvokeOnMainThread(() =>
            ControllerConnectionChanged?.Invoke(this, false));
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
