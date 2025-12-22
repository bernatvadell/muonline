# MuAndroid

Android platform head for the MuOnline client.

## Requirements

- .NET 10 SDK
- Android SDK with API-36 platform installed
- Android SDK path set (typically `C:\Users\<username>\AppData\Local\Android\Sdk` on Windows)

## Building

### Install Android Dependencies

First-time setup requires installing the Android SDK API-36 platform:

```bash
dotnet build -t:InstallAndroidDependencies -f net10.0-android "-p:AndroidSdkDirectory=C:\Users\<username>\AppData\Local\Android\Sdk"
```

### Build Release

```bash
dotnet build MuAndroid.csproj -c Release -f net10.0-android "-p:AndroidSdkDirectory=C:\Users\<username>\AppData\Local\Android\Sdk" -p:AcceptAndroidSdkLicenses=true
```

### Publish APK

```bash
dotnet publish MuAndroid.csproj -c Release -f net10.0-android "-p:AndroidSdkDirectory=C:\Users\<username>\AppData\Local\Android\Sdk" -p:AcceptAndroidSdkLicenses=true
```

## Configuration

- **Target Framework**: `net10.0-android`
- **TargetPlatformVersion**: `36.0` (compile-time SDK level)
- **targetSdkVersion**: `35` (runtime target in AndroidManifest.xml)
- **SupportedOSPlatformVersion**: `23` (minimum Android 6.0)

## CI/CD

The project builds in GitHub Actions with Android SDK API-36 platform auto-installed.

See `.github/workflows/build.yml` for the complete CI configuration.
