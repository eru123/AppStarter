@echo off
setlocal enabledelayedexpansion

:: AppStarter Build & Automation Script
:: This script increments the version, builds x64/x86 single files, and prepares installers.

echo ========================================
echo   AppStarter Build Automation
echo ========================================

:: 1. Auto-increment Version in .csproj
echo Incrementing version...
for /f "usebackq tokens=*" %%i in (`powershell -NoProfile -Command "$xml = [xml](Get-Content AppStarter.csproj); $v = [version]$xml.Project.PropertyGroup.Version; $newV = [version]::new($v.Major, $v.Minor, $v.Build + 1); $xml.Project.PropertyGroup.Version = $newV.ToString(); $xml.Save('AppStarter.csproj'); $newV.ToString()"`) do set APP_VERSION=v%%i

echo Target Version: %APP_VERSION%

:: 2. Build x64 Single File
echo Building x64 Single File...
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "./dist/%APP_VERSION%/x64"
if exist "./dist/%APP_VERSION%/x64/AppStarter.exe" (
    echo x64 Build Success
) else (
    ren "./dist/%APP_VERSION%/x64/*.exe" "AppStarter.exe"
)

:: 3. Build x86 Single File
echo Building x86 Single File...
dotnet publish -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "./dist/%APP_VERSION%/x86"
if exist "./dist/%APP_VERSION%/x86/AppStarter.exe" (
    echo x86 Build Success
) else (
    ren "./dist/%APP_VERSION%/x86/*.exe" "AppStarter.exe"
)

echo ========================================
echo   Build Complete
echo ========================================
echo Files are located in "./dist/%APP_VERSION%" folder.
pause
