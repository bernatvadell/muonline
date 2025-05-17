# MuOnline Clone (MonoGame)

This project is a clone of Mu Online built using .NET 9.0 and MonoGame. It aims to support Windows, Android, and iOS platforms.

[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/bernatvadell/muonline)

## Prerequisites

Before you begin, ensure you have the following installed:

*   **.NET 9.0 SDK:** [Download here](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
*   **(For Android Development):**
    *   Android SDK: Required for building the Android application.
    *   Java Development Kit (JDK): Version 11 or later recommended (as specified in the example command).
*   **(For iOS Development):**
    *   macOS with Xcode: Required for building and running the iOS application.
*   **Git:** For cloning the repository.

## Setup

1.  **Clone the Repository:**
    ```bash
    git clone <your-repository-url>
    cd <repository-directory>
    ```

2.  **Download Game Data:**
    This project requires original Mu Online game data. Download it from the official source:
    [Download MU Red 1.20.61 Full Data](https://full-wkr.mu.webzen.co.kr/muweb/full/MU_Red_1_20_61_Full.zip)

3.  **Configure Data Path:**
    *   Extract the downloaded `Data.zip` file.
    *   Open the `Client.Main/Constants.cs` file in the project.
    *   Locate the `DataPath` variable:
        ```csharp
        // Example:
        public static string DataPath = @"C:\Games\MU_Red_1_20_61_Full\Data";
        ```
    *   **Crucially, update this path** to the exact location where you extracted the `Data` folder from the downloaded zip file.

4.  **Restore .NET Tools:**
    The project uses .NET tools (like the MonoGame Content Builder). Restore them by running:
    ```bash
    dotnet tool restore
    ```

## Building the Project

Use the following commands to build the project for each platform. Builds will typically be placed in the `bin/Release/net9.0-<platform>/publish/` directory within the respective platform project folder (e.g., `MuWin/bin/Release/...`).

### Windows

```bash
dotnet publish ./MuWin/MuWin.csproj -f net9.0-windows -c Release
```
This creates a self-contained executable in the publish directory.

### Android

```bash
# Replace paths with your actual SDK/JDK locations!
dotnet publish ./MuAndroid/MuAndroid.csproj -f net9.0-android -c Release -p:AndroidSdkDirectory="C:\path\to\your\Android\Sdk" -p:JavaSdkDirectory="C:\path\to\your\jdk-11" -p:AcceptAndroidSdkLicenses=True
```
*   This command builds the release APK.
*   You **must** replace `"C:\path\to\your\Android\Sdk"` and `"C:\path\to\your\jdk-11"` with the correct paths on your system.
*   `-p:AcceptAndroidSdkLicenses=True` attempts to accept licenses automatically; you might need to accept them manually via Android Studio or SDK manager tools if this fails.
*   The output APK will be suitable for deployment to an Android device or emulator.

### iOS

```bash
dotnet publish ./MuIos/MuIos.csproj -f net9.0-ios -c Release
```
*   Requires a macOS machine with Xcode installed and properly configured signing certificates.

## Running the Project (Development)

Use these commands to run the project directly for development and testing.

### Windows

```bash
dotnet run --project ./MuWin/MuWin.csproj -f net9.0-windows -c Debug
```

### Android

Running directly via `dotnet run` on Android is less common for MonoGame projects. The typical workflow is:
1.  **Build the APK** using the `dotnet publish` command (see Building section).
2.  **Deploy the APK** to an Android emulator or a physical device using `adb` (Android Debug Bridge) or through your IDE (like Visual Studio).
    *   Example using adb: `adb install path/to/your/app.apk`
3.  **Launch the app** from the emulator/device.

### iOS

```bash
# Ensure you are on a macOS machine with Xcode
dotnet run --project ./MuIos/MuIos.csproj -f net9.0-ios -c Debug
```
*   This will typically launch the app on a connected device or simulator if configured correctly in Xcode.


---

Feel free to open an issue if you encounter any problems during setup or building.