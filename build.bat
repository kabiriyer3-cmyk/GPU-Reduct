@echo off
cd /d "%~dp0"
where dotnet >nul 2>nul || (echo .NET 8 SDK not found. Install it from https://dotnet.microsoft.com/download/dotnet/8.0 & pause & exit /b 1)
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
if errorlevel 1 (echo Build failed. & pause & exit /b 1)
echo.
echo Done: %~dp0publish\GpuReduct.exe
pause
