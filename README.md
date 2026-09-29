# GPU Reduct

Mem Reduct-style VRAM monitor & cleaner for Windows (C# / WPF / .NET 8), Linear-style dark UI.

## Requirements
- Windows 10 1709+ / Windows 11 with a WDDM 2.x GPU driver
- .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
- Admin rights (the app asks for elevation, like Mem Reduct)

## Build the .exe (easiest)
Double-click `build.bat`. The exe is written to `publish\GpuReduct.exe`.
It is self-contained, so it runs on PCs without .NET installed.

## Build manually
From an elevated terminal in this folder:

    dotnet run
    dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish

Or open `GpuReduct.csproj` in Visual Studio 2022 / Rider (run the IDE as administrator for debugging).

## Actions
- Clean memory (Ctrl+Enter): trims the working sets of all processes using the GPU
- Reset driver: Win+Ctrl+Shift+B, restarts the graphics driver (the real VRAM flush)
- End process: hover a row in "Top consumers" and click the X
- Auto clean: runs Clean above a VRAM threshold (at most once a minute)
- Closing the window hides it to the tray; use the tray menu > Exit to quit

## Known limitations
- Windows does not let one app force another to free dedicated VRAM; the driver's memory manager decides.
  Clean mostly frees RAM and shared GPU memory. Reset driver or closing apps frees real VRAM.
- On non-English Windows the GPU counter names may be translated. If the app says counters are
  unavailable, GpuMonitor needs to switch to PDH with PdhAddEnglishCounter.
