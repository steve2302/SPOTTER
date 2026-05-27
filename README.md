# SPOTTER

**Survey Program for Observing, Tracking, Tagging and Event Recording**

A Windows desktop application for recording wildlife observations during aerial surveys. Developed for the NSW Department of Primary Industries and Regional Development.

## Overview

SPOTTER runs on a ruggedised tablet in the aircraft and lets observers log sightings of kangaroos, feral goats, and other wildlife using an Xbox gamepad controller. Each observation is automatically stamped with GPS coordinates, bearing, altitude, HDOP, and a precise UTC timestamp derived from the GPS signal.

## Features

- **GPS tracking** — reads raw NMEA sentences from any serial COM port; auto-scans all available ports or connects directly to a configured port
- **Gamepad input** — Xbox controller (XInput) for hands-free observation recording while watching out the window
- **Controller vibration alert** — gamepad rumbles when GPS signal is lost during a session
- **Session management** — AM/PM session, observer name, seating position (left/right front/rear), and weather data
- **Track log** — continuous position log written throughout the session
- **Map view** — live aircraft position on a map
- **Satellite display** — satellites in view and satellites used
- **GPS diagnostics** — debug form showing raw NMEA stream and port scan results
- **Practice mode** — for training observers without recording to a file

## Output files

Each session produces two files named `{timestamp}_{AM|PM}_{observer}_{position}`:

| Extension | Contents |
|---|---|
| `.dat` | Observation records — timestamp, GPS fix, bearing, altitude, HDOP/VDOP, weather, and the observation string |
| `.log` | Continuous track log |

Files are written to a configurable data directory.

## Requirements

- Windows 10 or 11 (x64)
- .NET Framework 4.7.2
- A serial GPS receiver (USB or built-in) outputting NMEA 0183
- Xbox-compatible gamepad (optional but recommended)

## Building

Open `SPOTTER.sln` in Visual Studio 2022 and build the `Debug|x64` or `Release|x64` configuration, or from the command line:

```
MSBuild SPOTTER\SPOTTER.csproj /p:Configuration=Release /p:Platform=x64
```

NuGet packages restore automatically on first build.

## GPS configuration

By default SPOTTER scans all COM ports at startup and connects to the first one that produces valid NMEA sentences. To skip the scan and connect directly to a known port, edit `SPOTTER.exe.config` in the output folder:

```xml
<add key="GpsPreferredPort" value="COM5" />
```

Leave the value empty to re-enable auto-scan.

### Panasonic FZ-G1 ToughPad

The FZ-G1's built-in u-blox GPS appears in Device Manager as both a **u-blox Virtual COM Port** (usually COM5) and a **u-blox GNSS Location Sensor**. The Windows sensor driver holds the COM port open, preventing direct access.

**Recommended setup:**

1. Install [GpsGate Splitter](https://gpsgate.com/gpsgate-splitter) and configure it to read from COM5
2. Point GpsGate's virtual output port at SPOTTER via `GpsPreferredPort` in the config file

Alternatively, disable the *u-blox GNSS Location Sensor* in Device Manager to release COM5, then set `GpsPreferredPort=COM5` directly.

## Observer list

The observer drop-down is populated from `Resources\observers.txt` in the application directory. Edit this file to add or remove names — one comma-separated list per line.

## Gamepad mapping

Observation recording and navigation are driven by the Xbox controller. Button assignments are configurable via `GamepadAudioConfig.csv` in the application directory and can be reviewed in the Gamepad Config Viewer (accessible from the menu).
