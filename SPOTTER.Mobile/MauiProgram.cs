using Microsoft.Maui.Devices.Sensors;
using SPOTTER.Abstractions;
using SPOTTER.Mobile.Services;

namespace SPOTTER.Mobile;

/// <summary>
/// MAUI entry point. Builds the application host, registers services,
/// and starts the shell.
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

        // MAUI Essentials sensors.
        builder.Services.AddSingleton(Geolocation.Default);

        // Platform-neutral GPS contract. iOS uses CoreLocation via
        // MAUI's IGeolocation; a WinForms host would register the
        // SerialPort-backed GPSController instead.
        builder.Services.AddSingleton<IGpsProvider, MauiGpsProvider>();

        // Pages. Registering them in DI lets the shell resolve
        // IGpsProvider through constructor injection.
        builder.Services.AddSingleton<MainPage>();

        return builder.Build();
    }
}
