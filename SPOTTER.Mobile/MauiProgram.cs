using Microsoft.Extensions.Logging;

namespace SPOTTER.Mobile;

/// <summary>
/// MAUI entry point. Builds the application host, registers services
/// (currently none — iOS implementations of IGpsProvider / IGamepadProvider
/// will be registered here in a later step), and starts the shell.
/// </summary>
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                // Default system fonts are fine for the scaffold. Custom fonts
                // can be added under Resources/Fonts and registered here.
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
