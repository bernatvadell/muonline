# MuOnline

This project is built using .NET 8.0 and utilizes MonoGame for game development. Follow the instructions below to build and run the project.

## Prerequisites

Make sure you have the following installed on your system:

- [.NET 8.0 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)

## Setup and Configuration

### Step 1: Download the Game Data

Before running the project, you need to download the game data from the official source:

[Download the game data](http://full-wkr.mu.webzen.co.kr/muweb/full/MU_Red_1_20_61_Full.ex)

### Step 2: Modify the `Constants.cs` File

Once the game data is downloaded, you need to configure the correct path in the code.

1. Go to the file `src\Client.Main\Constants.cs`.
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

Once the game data path is set, and tools are restored, build the project using the .NET CLI:

```bash
dotnet build
```

### Step 5: Run the Project

To run the project, execute the following command:

```bash
dotnet run --project src/Client.Main/Client.Main.csproj
```

## Additional Information

- Dependencies are automatically restored when you build the project.
- The project targets Windows (`WinExe`), so ensure you are on a Windows machine for proper execution.

Feel free to open an issue if you encounter any problems.