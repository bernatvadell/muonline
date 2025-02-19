## build commands

### Windows

```cmd
dotnet publish ./MuWin/MuWin.csproj -f net8.0-windows -c Release
```

### Android

```cmd
dotnet publish ./MuAndroid/MuAndroid.csproj -f net8.0-android -c Release
```

### iOS

```cmd
dotnet publish ./MuIos/MuIos.csproj -f net8.0-ios -c Release
```

## run commands

### Windows

```cmd
 dotnet publish ./MuAndroid/MuAndroid.csproj -f net8.0-android -c Release -p:AndroidSdkDirectory=C:\Users\linuxer\AppData\Local\Android\Sdk -p:JavaSdkDirectory=D:\mu\microsoft-jdk-11.0.26-windows-x64\jdk-11.0.26+4 -p:AcceptAndroidSdkLicenses=True
```

### Android

```cmd
dotnet run --project ./MuAndroid/MuAndroid.csproj -f net8.0-android -c Release
```

### iOS

```cmd
dotnet run --project ./MuIos/MuIos.csproj -f net8.0-ios -c Release
```