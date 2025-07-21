# MuOnline Clone (MonoGame)

A cross-platform MuOnline client implementation built with .NET 9.0 and MonoGame framework. Supports Windows, Android, iOS, Linux, and macOS platforms.

[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/bernatvadell/muonline)

## Features

- Cross-platform support (Windows, Android, iOS, Linux, macOS)
- Full 3D rendering with MonoGame
- Original MuOnline data file compatibility
- Multiplayer networking support
- Custom UI system with game controls
- Terrain rendering with heightmaps
- 3D model loading and animation (BMD format)
- Real-time lighting and effects

## Prerequisites

- **.NET 9.0 SDK** - [Download here](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)
- **Git** - For cloning the repository
- **Original MuOnline Data** - MU Red 1.20.61 Full data files required

### Platform-Specific Requirements

**Android Development:**
- Android SDK
- Java Development Kit (JDK) 11 or later

**iOS Development:**
- macOS with Xcode
- Valid Apple Developer account for device deployment

**Linux Development:**
- Compatible with most x64 distributions

## Project Structure

The solution contains these main projects:

- **Client.Data** - Data readers for game files (BMD, ATT, MAP, OZB, etc.)
- **Client.Main** - Core game logic, rendering, UI, networking, and game objects  
- **Client.Editor** - Editor tool for game assets
- **MuWin** - Windows platform executable
- **MuAndroid** - Android platform executable
- **MuIos** - iOS platform executable
- **MuLinux** - Linux platform executable
- **MuMac** - macOS platform executable

## Quick Start

1. **Clone the Repository:**
   ```bash
   git clone <your-repository-url>
   cd muonline
   ```

2. **Download Game Data:**
   This client is designed for **Season 6 (S6) protocol compatibility** but requires **Season 20 (1.20.61) client data files**. 
   Download the original MuOnline data files:
   [MU Red 1.20.61 Full Data](https://full-wkr.mu.webzen.co.kr/muweb/full/MU_Red_1_20_61_Full.zip)
   
   **Note:** The MonoGame client uses S20 data files for assets (models, textures, maps) but communicates using S6 network protocol.

3. **Configure Data Path:**
   - Extract the downloaded Data.zip file
   - Open `Client.Main/Constants.cs`
   - Update the `DataPath` variable to point to your extracted Data folder:
     ```csharp
     public static string DataPath = @"C:\Games\MU_Red_1_20_61_Full\Data";
     ```

4. **Configure Server Settings:**
   - Open `Client.Main/appsettings.json`
   - Configure the MuOnline server connection settings:
     ```json
     {
       "MuOnlineSettings": {
         "ConnectServerHost": "your.server.host",
         "ConnectServerPort": 44405,
         "ProtocolVersion": "Season6",
         "ClientVersion": "1.04d",
         "ClientSerial": "0123456789ABCDEF"
       }
     }
     ```

### Recommended Server: OpenMU

This client is designed to work with **[OpenMU](https://github.com/MUnique/OpenMU)**, an open-source MuOnline server implementation. You can easily run OpenMU using Docker:

```bash
# Download and run OpenMU server
curl -o docker-compose.yml https://raw.githubusercontent.com/MUnique/OpenMU/master/deploy/all-in-one/docker-compose.yml
docker-compose up -d
```

The server will be available at `localhost:44405` (Connect Server) which matches the default client configuration.

5. **Restore Tools:**
   ```bash
   dotnet tool restore
   ```

6. **Build and Run:**
   ```bash
   # Windows
   dotnet run --project ./MuWin/MuWin.csproj -f net9.0-windows -c Debug
   
   # macOS
   dotnet run --project ./MuMac/MuMac.csproj -f net9.0 -c Debug
   
   # Linux
   dotnet run --project ./MuLinux/MuLinux.csproj -f net9.0 -c Debug
   ```

## Building the Project

### Development Builds
```bash
# Build entire solution
dotnet build

# Build and run specific platforms
dotnet run --project ./MuWin/MuWin.csproj -f net9.0-windows -c Debug     # Windows
dotnet run --project ./MuLinux/MuLinux.csproj -f net9.0 -c Debug         # Linux
dotnet run --project ./MuMac/MuMac.csproj -f net9.0 -c Debug             # macOS
dotnet run --project ./MuIos/MuIos.csproj -f net9.0-ios -c Debug         # iOS (macOS only)
```

### Production Builds
Build outputs are placed in `bin/Release/net9.0-<platform>/publish/` directories.

```bash
# Windows
dotnet publish ./MuWin/MuWin.csproj -f net9.0-windows -c Release

# Android (replace paths with your actual SDK/JDK locations)
dotnet publish ./MuAndroid/MuAndroid.csproj -f net9.0-android -c Release \
  -p:AndroidSdkDirectory="C:\path\to\your\Android\Sdk" \
  -p:JavaSdkDirectory="C:\path\to\your\jdk-11" \
  -p:AcceptAndroidSdkLicenses=True

# Linux  
dotnet publish ./MuLinux/MuLinux.csproj -f net9.0 -c Release -r linux-x64

# macOS
dotnet publish ./MuMac/MuMac.csproj -f net9.0 -c Release

# iOS (macOS only, requires Xcode and signing certificates)
dotnet publish ./MuIos/MuIos.csproj -f net9.0-ios -c Release
```

## Architecture Overview

This project follows a well-structured game architecture:

- **Scene Management** - Finite state machine pattern with BaseScene handling Login, Game, Load scenes
- **Networking** - Service-oriented architecture with PacketRouter and attribute-based handler registration
- **Game Objects** - Hierarchical system with WorldObject base class extending to PlayerObject, MonsterObject, NPCObject
- **Rendering** - Multi-pass rendering pipeline with MonoGame, supporting 3D models, terrain, and UI
- **World System** - Strategy pattern for different game worlds (Lorencia, Devias, etc.)

## File Format Support

- **BMD** - 3D models and animations
- **ATT** - Terrain attributes  
- **MAP** - Terrain height data
- **OZB/OZG** - Compressed texture formats
- **CWS** - Camera walk scripts
- **OBJS** - Object placement data

## Contributing

This project uses the original MuOnline data format and is intended for educational purposes. Feel free to open an issue if you encounter any problems during setup or building.
