dotnet build -t:InstallAndroidDependencies -f net9.0-android -p:AndroidSdkDirectory=C:\Users\linuxer\AppData\Local\Android\Sdk -p:JavaSdkDirectory=D:\mu\microsoft-jdk-11.0.26-windows-x64\jdk-11.0.26+4 -p:AcceptAndroidSdkLicenses=True


dotnet publish -c Release -t:InstallAndroidDependencies -f net9.0-android -p:AndroidSdkDirectory=C:\Users\linuxer\AppData\Local\Android\Sdk -p:JavaSdkDirectory=D:\mu\microsoft-jdk-11.0.26-windows-x64\jdk-11.0.26+4 -p:AcceptAndroidSdkLicenses=True


dotnet publish -c Release -f net9.0-windows10.0.19041.0 