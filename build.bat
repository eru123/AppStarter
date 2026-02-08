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

:: 4. Build Installers (Inno Setup & WiX)
echo Looking for installer tools...

:: Detect Inno Setup
set "ISCC="
for %%i in (iscc.exe) do set "ISCC=%%~$PATH:i"
if not defined ISCC if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" set "ISCC=C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if not defined ISCC if exist "C:\Program Files\Inno Setup 6\ISCC.exe" set "ISCC=C:\Program Files\Inno Setup 6\ISCC.exe"

:: Detect WiX (Modern v4/v5/v6)
set "WIXEXE="
for %%i in (wix.exe) do set "WIXEXE=%%~$PATH:i"

:: Detect WiX (Legacy v3 fallback)
set "CANDLE="
set "LIGHT="
for %%i in (candle.exe) do set "CANDLE=%%~$PATH:i"
for %%i in (light.exe) do set "LIGHT=%%~$PATH:i"
if not defined CANDLE if exist "C:\Program Files (x86)\WiX Toolset v3.11\bin\candle.exe" set "CANDLE=C:\Program Files (x86)\WiX Toolset v3.11\bin\candle.exe"
if not defined LIGHT if exist "C:\Program Files (x86)\WiX Toolset v3.11\bin\light.exe" set "LIGHT=C:\Program Files (x86)\WiX Toolset v3.11\bin\light.exe"

:: Building Inno Setup EXE installers
if defined ISCC (
    echo Building Inno Setup EXE (x64)...
    "!ISCC!" /DAppVersion=%APP_VERSION% /DAppArch=x64 /DArchitecturesAllowed=x64 /DArchitecturesInstallIn64BitMode=x64 installer.iss
    
    echo Building Inno Setup EXE (x86)...
    "!ISCC!" /DAppVersion=%APP_VERSION% /DAppArch=x86 /DArchitecturesAllowed=x86 installer.iss
) else (
    echo Inno Setup (ISCC.exe) not found. Skipping EXE installers.
)

:: Building WiX MSI installers
if defined WIXEXE (
    echo Building Modern WiX MSI (x64)...
    "!WIXEXE!" build installer.wxs -d Version=%APP_VERSION:~1% -d Platform=x64 -d ProgramFilesId=ProgramFiles64Folder -d SourcePath=dist\%APP_VERSION%\x64 -o dist\%APP_VERSION%\AppStarter_x64.msi
    
    echo Building Modern WiX MSI (x86)...
    "!WIXEXE!" build installer.wxs -d Version=%APP_VERSION:~1% -d Platform=x86 -d ProgramFilesId=ProgramFilesFolder -d SourcePath=dist\%APP_VERSION%\x86 -o dist\%APP_VERSION%\AppStarter_x86.msi
) else if defined CANDLE if defined LIGHT (
    echo Building WiX v3 MSI (x64)...
    "!CANDLE!" -dVersion=%APP_VERSION:~1% -dPlatform=x64 -dProgramFilesId=ProgramFiles64Folder -dSourcePath=dist\%APP_VERSION%\x64 installer.wxs -o dist\%APP_VERSION%\installer_x64.wixobj
    "!LIGHT!" -ext WixUIExtension dist\%APP_VERSION%\installer_x64.wixobj -o dist\%APP_VERSION%\AppStarter_x64.msi
    
    echo Building WiX v3 MSI (x86)...
    "!CANDLE!" -dVersion=%APP_VERSION:~1% -dPlatform=x86 -dProgramFilesId=ProgramFilesFolder -dSourcePath=dist\%APP_VERSION%\x86 installer.wxs -o dist\%APP_VERSION%\installer_x86.wixobj
    "!LIGHT!" -ext WixUIExtension dist\%APP_VERSION%\installer_x86.wixobj -o dist\%APP_VERSION%\AppStarter_x86.msi
) else (
    echo WiX Toolset not found. Skipping MSI installers.
)

echo ========================================
echo   Build Complete
echo ========================================
echo Files are located in "./dist/%APP_VERSION%" folder.
pause
