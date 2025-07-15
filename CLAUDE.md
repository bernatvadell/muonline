# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a MuOnline clone built with .NET 9.0 and MonoGame framework. It supports multiple platforms: Windows, Android, iOS, and Linux.

## Project Structure

The solution contains these main projects:
- **Client.Data**: Data readers for game files (BMD, ATT, MAP, OZB, etc.)
- **Client.Main**: Core game logic, rendering, UI, networking, and game objects
- **Client.Editor**: Editor tool for game assets
- **MuWin**: Windows platform executable
- **MuAndroid**: Android platform executable  
- **MuIos**: iOS platform executable
- **MuLinux**: Linux platform executable

## Build Commands

### Development (Debug builds)
```bash
# Windows
dotnet run --project ./MuWin/MuWin.csproj -f net9.0-windows -c Debug

# Linux
dotnet run --project ./MuLinux/MuLinux.csproj -f net9.0 -c Debug

# iOS (macOS only)
dotnet run --project ./MuIos/MuIos.csproj -f net9.0-ios -c Debug
```

### Production (Release builds)
```bash
# Windows
dotnet publish ./MuWin/MuWin.csproj -f net9.0-windows -c Release

# Android
dotnet publish ./MuAndroid/MuAndroid.csproj -f net9.0-android -c Release

# Linux
dotnet publish ./MuLinux/MuLinux.csproj -f net9.0 -c Release -r linux-x64

# iOS (macOS only)
dotnet publish ./MuIos/MuIos.csproj -f net9.0-ios -c Release
```

### Tool Management
```bash
# Restore .NET tools (required after clone)
dotnet tool restore
```

## Data Path Configuration

The game requires original MuOnline data files. Configure the data path in `Client.Main/Constants.cs:25`:
```csharp
public static string DataPath = @"C:\Games\MU_Red_1_20_61_Full\Data";
```

## Architecture

### Client.Main Structure
- **Scenes**: Game scene management (Login, Game, Load, etc.)
- **Objects**: Game entities (Players, Monsters, NPCs, Effects)
- **Controllers**: System controllers (Graphics, Animation, Sound, Effects)
- **Controls**: UI controls and terrain rendering
- **Networking**: Packet handling and server communication
- **Worlds**: World-specific implementations (Lorencia, Devias, etc.)

### Key Systems
- **Terrain System**: Heightmap-based terrain with LOD and culling
- **Object System**: 3D model rendering with BMD format support
- **Networking**: Custom packet-based protocol
- **UI System**: Custom UI controls with sprite support
- **Animation**: BMD-based skeletal animation system

### File Format Support
- **BMD**: 3D models and animations
- **ATT**: Terrain attributes
- **MAP**: Terrain height data
- **OZB/OZG**: Compressed texture formats
- **CWS**: Camera walk scripts
- **OBJS**: Object placement data

## Development Notes

- The game uses MonoGame for cross-platform graphics
- All game data is loaded from original MuOnline files
- Debug builds use hardcoded data paths; release builds use relative paths
- The project supports both Debug and Release configurations with x64 variants