# SteamOverlayOSC

SteamOverlayOSC is a companion app that links the SteamVR overlay status with avatar parameters for assets like DXOverlay.

The app attempts to handle late starts and disconnects, and can be started before or after SteamVR or VRChat. It will automatically detect SteamVR closing and reconnect when it becomes available again.

Parameters `isOverlayOpen` as well as `isSteamOverlay` and `isKeyboardOpen` are set to false when exiting or when SteamVR connection is lost to avoid avatar assets getting stuck open.

* `isOverlayOpen`: Set to true when SteamVR overlay open is detected
* `isSteamOverlay`: Always set to true so that assets can perform SteamVR specific behavior
* `isKeyboardOpen`: Currently always set to true due to outdated OpenVR API SDK limitations

## Usage

Run the app alongside SteamVR and VRChat.

## Options

These shouldn't need to be adjusted, but may be useful in specific scenarios

* `--poll-ms` Query interval; default: 250
* `--reconnect-ms` SteamVR retry interval; default: 2000
* `--osc-host` VRChat OSC destination; default: 127.0.0.1
* `--osc-port` VRChat OSC port; default: 9000

## Building From Source

```
dotnet publish SteamOverlayOSC.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=embedded -o ./release
```

## License

MIT
