## build commands

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

## run commands

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