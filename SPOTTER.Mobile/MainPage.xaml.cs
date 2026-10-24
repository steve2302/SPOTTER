using SPOTTER.Models;

namespace SPOTTER.Mobile;

/// <summary>
/// Scaffold landing page. Instantiates two types from SPOTTER.Core to prove
/// the project reference compiles and resolves at runtime. Will be replaced
/// by the real session-setup / logger pages in a later step.
/// </summary>
public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();

        var settings = new ControllerSettings();
        CoreStatusLabel.Text =
            $"SPOTTER.Core reference OK — ControllerSettings defaults: " +
            $"ThumbstickTolerance={settings.ThumbstickTolerance}, " +
            $"TriggerTolerance={settings.TriggerTolerance}";

        var session = new SessionInfo
        {
            Session = SessionInfo.TimeOfDay.AM,
            ObserverName = "scaffold",
            Position = SessionInfo.ObserverPosition.LeftFront
        };
        session.CreateFileNames();
        SessionStatusLabel.Text =
            $"Shared SessionInfo OK — sample filename: {session.DataFileName}";
    }
}
