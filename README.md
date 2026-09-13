# SteamVRDashOSC

SteamVRDashOSC is a small companion app that polls the SteamVR dashboard status and sends it over OSC using the `isOverlayOpen` avatar parameter.

This enables the use of avatar overlay indicator assets with the SteamVR overlay.

## Usage

Run the app with VRChat

## Options

* `--poll-ms` Query interval; default: 250
* `--reconnect-ms` SteamVR retry interval; default: 2000
* `--osc-host` VRChat OSC destination; default: 127.0.0.1
* `--osc-port` VRChat OSC port; default: 9000

## Building From Source

```
dotnet publish SteamVRDashOSC.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded
```

## License

MIT
