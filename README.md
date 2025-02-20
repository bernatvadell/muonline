# MuOnline

This project is built using .NET 9.0 and utilizes MonoGame for game development. Follow the instructions below to build and run the project.

## Prerequisites

Make sure you have the following installed on your system:

- [.NET 9.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)

## Setup and Configuration

### Step 1: Download the Game Data

Before running the project, you need to download the game data from the official source:

[Download the game data](https://full-wkr.mu.webzen.co.kr/muweb/full/MU_Red_1_20_61_Full.zip)

### Step 2: Modify the `Constants.cs` File

Once the game data is downloaded, you need to configure the correct path in the code.

1. Go to the file `Client.Main\Constants.cs`.
2. Open the file and find the following line:

    ```csharp
    public static string DataPath = @"C:\Games\MU_Red_1_20_61_Full\Data";
    ```

3. Change the `DataPath` to point to the directory where you extracted the downloaded game files.

### Step 3: Restore .NET Tools

To restore the necessary tools for building the project, run the following command in the terminal:

```bash
dotnet tool restore
```

### Step 4: Build the Project


## Build commands

### Windows

```cmd
dotnet publish ./MuWin/MuWin.csproj -f net9.0-windows -c Release
```

### Android

```cmd
dotnet publish ./MuAndroid/MuAndroid.csproj -f net9.0-android -c Release
```

### iOS

```cmd
dotnet publish ./MuIos/MuIos.csproj -f net9.0-ios -c Release
```

## Run commands

### Windows

```cmd
dotnet run --project ./MuWin/MuWin.csproj -f net9.0-windows -c Debug
```

### Android

```cmd

 dotnet publish ./MuAndroid/MuAndroid.csproj -f net9.0-android -c Release -p:AndroidSdkDirectory=C:\Users\linuxer\AppData\Local\Android\Sdk -p:JavaSdkDirectory=D:\mu\microsoft-jdk-11.0.26-windows-x64\jdk-11.0.26+4 -p:AcceptAndroidSdkLicenses=True
```

### iOS

```cmd
dotnet run --project ./MuIos/MuIos.csproj -f net9.0-ios -c Release
```

### Step 5: Run the Project

To run the project, execute the following command:

```bash
dotnet run --project Client.Main/Client.Main.csproj
```

## Additional Information

- Dependencies are automatically restored when you build the project.

Feel free to open an issue if you encounter any problems.